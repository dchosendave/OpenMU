// <copyright file="ZenStoreBotPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Bots;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlayerActions.PlayerStore;
using MUnique.OpenMU.GameLogic.PlayerShopCurrencies;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Now and then a hunting bot opens a personal store where everything costs 1 zen. The map is announced
/// a minute before, but not the spot, so it's a race; the store closes when it's sold out or after a
/// while. It sells only farmable things, like jewel packs and seed spheres (game-design.md, "Using the bots").
/// </summary>
[Guid("9C4E2B78-5D13-4A6F-8B90-E1D7F3A2C658")]
[PlugIn]
[Display(Name = "1-zen bot store", Description = "Now and then a hunting bot opens a store where everything costs 1 zen; the map is announced a minute before.")]
public class ZenStoreBotPlugIn : IPeriodicTaskPlugIn, ISupportCustomConfiguration<ZenStoreBotConfiguration>, ISupportDefaultCustomConfiguration
{
    private const string StoreName = "1 Zen Sale!";

    private readonly OpenStoreAction _openStoreAction = new();
    private readonly CloseStoreAction _closeStoreAction = new();

    private DateTime _nextStoreUtc = DateTime.UtcNow.AddMinutes(10);
    private Sale? _sale;

    /// <inheritdoc />
    public ZenStoreBotConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new ZenStoreBotConfiguration();

    /// <inheritdoc />
    public void ForceStart() => this._nextStoreUtc = DateTime.UtcNow;

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var configuration = this.Configuration ?? new ZenStoreBotConfiguration();
        var utcNow = DateTime.UtcNow;
        if (this._sale is { } sale)
        {
            // One store at a time across the game servers; the server of its bot runs it.
            if (ReferenceEquals(sale.Bot.GameContext, gameContext))
            {
                await this.ContinueAsync(gameContext, sale, configuration, utcNow).ConfigureAwait(false);
            }

            return;
        }

        if (utcNow < this._nextStoreUtc)
        {
            return;
        }

        this._nextStoreUtc = utcNow.AddMinutes(Math.Max(1, configuration.IntervalMinutes));
        var bot = (await gameContext.GetPlayersAsync().ConfigureAwait(false))
            .OfType<BotPlayer>()
            .Where(b => b.IsAlive && !b.IsIdlingInTown && !b.IsRunningStore && b.CurrentMap is not null
                        && b.SelectedCharacter is not null && b.CurrentMiniGame is null && b.ShopStorage is not null)
            .SelectRandom();
        if (bot is null)
        {
            return;
        }

        bot.IsRunningStore = true;
        this._sale = new Sale(bot, utcNow.AddSeconds(Math.Max(0, configuration.AnnounceSecondsBefore)), null);
        await gameContext.SendGlobalMessageAsync(
                $"A merchant opens a 1-zen store on {bot.CurrentMap!.Definition.Name} in {configuration.AnnounceSecondsBefore} seconds! Find it first.",
                MessageType.GoldenCenter)
            .ConfigureAwait(false);
    }

    private async ValueTask ContinueAsync(GameContext gameContext, Sale sale, ZenStoreBotConfiguration configuration, DateTime utcNow)
    {
        var bot = sale.Bot;
        if (!bot.IsAlive || bot.CurrentMap is null || bot.PlayerState.CurrentState != PlayerState.EnteredWorld)
        {
            await this.CloseAsync(sale).ConfigureAwait(false);
            return;
        }

        if (sale.ClosesAtUtc is null)
        {
            if (utcNow >= sale.OpensAtUtc)
            {
                await this.OpenAsync(gameContext, sale, configuration, utcNow).ConfigureAwait(false);
            }

            return;
        }

        if (utcNow >= sale.ClosesAtUtc || bot.ShopStorage?.Items.Any() != true)
        {
            await this.CloseAsync(sale).ConfigureAwait(false);
        }
    }

    private async ValueTask OpenAsync(GameContext gameContext, Sale sale, ZenStoreBotConfiguration configuration, DateTime utcNow)
    {
        var bot = sale.Bot;
        var stock = PlayerRewards.ParseItems(configuration.Items);
        var definitions = gameContext.Configuration.Items;
        for (var i = 0; i < configuration.ItemsPerStore && stock.Count > 0; i++)
        {
            var (group, first, last, level, count) = stock[Rand.NextInt(0, stock.Count)];
            var number = (short)Rand.NextInt(first, last + 1);
            if (definitions.FirstOrDefault(d => d.Group == group && d.Number == number) is not { } definition)
            {
                continue;
            }

            var item = bot.PersistenceContext.CreateNew<Item>();
            item.Definition = definition;
            item.Level = Math.Min(level, definition.MaximumItemLevel);
            item.Durability = item.GetMaximumDurabilityOfOnePiece();
            item.SetStorePrice(new ShopPrice(Math.Max(1, configuration.Price)));
            if (!await bot.ShopStorage!.AddItemAsync(item).ConfigureAwait(false))
            {
                await bot.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
            }
        }

        if (bot.ShopStorage?.Items.Any() != true)
        {
            await this.CloseAsync(sale).ConfigureAwait(false);
            return;
        }

        await this._openStoreAction.OpenStoreAsync(bot, StoreName).ConfigureAwait(false);
        this._sale = sale with { ClosesAtUtc = utcNow.AddMinutes(Math.Max(1, configuration.OpenMinutes)) };
        await gameContext.SendGlobalMessageAsync(
                $"The 1-zen store of {bot.Name} on {bot.CurrentMap!.Definition.Name} is open!",
                MessageType.BlueNormal)
            .ConfigureAwait(false);
    }

    private async ValueTask CloseAsync(Sale sale)
    {
        this._sale = null;
        var bot = sale.Bot;
        try
        {
            if (bot.ShopStorage is { } shop)
            {
                if (shop.StoreOpen)
                {
                    await this._closeStoreAction.CloseStoreAsync(bot).ConfigureAwait(false);
                }

                // What wasn't sold goes away again; the bot only lent its store.
                foreach (var item in shop.Items.ToList())
                {
                    await shop.RemoveItemAsync(item).ConfigureAwait(false);
                    await bot.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            bot.Logger.LogWarning(ex, "Closing the 1-zen store of {Bot} failed.", bot.Name);
        }
        finally
        {
            bot.IsRunningStore = false;
        }
    }

    private sealed record Sale(BotPlayer Bot, DateTime OpensAtUtc, DateTime? ClosesAtUtc);
}

/// <summary>
/// The configuration of the <see cref="ZenStoreBotPlugIn"/>.
/// </summary>
public class ZenStoreBotConfiguration
{
    /// <summary>Gets or sets how often a store opens.</summary>
    [Display(Name = "A store every (minutes)")]
    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 120;

    /// <summary>Gets or sets how long before opening the map is announced.</summary>
    [Display(Name = "Announce before opening (seconds)")]
    [Range(0, 600)]
    public int AnnounceSecondsBefore { get; set; } = 60;

    /// <summary>Gets or sets how long the store stays open at most.</summary>
    [Display(Name = "Open for (minutes)")]
    [Range(1, 120)]
    public int OpenMinutes { get; set; } = 10;

    /// <summary>Gets or sets how many items the store sells.</summary>
    [Display(Name = "Items per store")]
    [Range(1, 32)]
    public int ItemsPerStore { get; set; } = 4;

    /// <summary>Gets or sets the price of every item, in zen.</summary>
    [Display(Name = "Price (zen)")]
    [Range(1, 2000000000)]
    public int Price { get; set; } = 1;

    /// <summary>
    /// Gets or sets what the store may sell: comma separated group:number:level:count (the count is
    /// ignored; each item of the store is picked at random from this list).
    /// </summary>
    [Display(Name = "Possible items", Description = "Comma separated group:number:level:count; the store picks its items at random from these. Only farmable things.")]
    public string Items { get; set; } = "12:30:0:1, 12:31:0:1, 12:141:0:1, 12:136:0:1, 12:100-105:0:1, 12:106-111:0:1, 12:112-117:0:1";
}
