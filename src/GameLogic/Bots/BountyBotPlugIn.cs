// <copyright file="BountyBotPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Bots;

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Now and then one hunting bot becomes "wanted": the server names it and its map, and whoever kills it
/// within the time gets a reward. A wanted bot counts as a player killer, so the hunter isn't punished
/// for the kill (game-design.md, "Using the bots").
/// </summary>
[Guid("E83B5C27-1F96-4D0A-B7E4-9A2C6D8F3B15")]
[PlugIn]
[Display(Name = "Bounty bot", Description = "Now and then a hunting bot becomes wanted; whoever kills it in time gets a reward.")]
public class BountyBotPlugIn : IPeriodicTaskPlugIn, ISupportCustomConfiguration<BountyBotConfiguration>, ISupportDefaultCustomConfiguration
{
    private static readonly ConcurrentDictionary<IGameContext, Bounty> Bounties = new();

    private DateTime _nextBountyUtc = DateTime.UtcNow.AddMinutes(5);

    /// <inheritdoc />
    public BountyBotConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new BountyBotConfiguration();

    /// <inheritdoc />
    public void ForceStart() => this._nextBountyUtc = DateTime.UtcNow;

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var configuration = this.Configuration ?? new BountyBotConfiguration();
        var utcNow = DateTime.UtcNow;
        if (Bounties.TryGetValue(gameContext, out var bounty))
        {
            if (utcNow >= bounty.EndsAtUtc || !bounty.Bot.IsAlive && bounty.Bot.CurrentMap is null)
            {
                await EndAsync(gameContext, bounty, null).ConfigureAwait(false);
            }

            return;
        }

        if (utcNow < this._nextBountyUtc)
        {
            return;
        }

        this._nextBountyUtc = utcNow.AddMinutes(Math.Max(1, configuration.IntervalMinutes));
        var candidates = (await gameContext.GetPlayersAsync().ConfigureAwait(false))
            .OfType<BotPlayer>()
            .Where(b => b.IsAlive && !b.IsIdlingInTown && b.CurrentMap is not null && b.SelectedCharacter is not null
                        && !b.IsAtSafezone() && b.CurrentMiniGame is null)
            .ToList();
        if (candidates.SelectRandom() is not { } bot)
        {
            return;
        }

        bounty = new Bounty(bot, bot.SelectedCharacter!.State, utcNow.AddMinutes(Math.Max(1, configuration.DurationMinutes)));
        if (!Bounties.TryAdd(gameContext, bounty))
        {
            return;
        }

        bot.SelectedCharacter.State = HeroState.PlayerKiller1stStage;
        var map = bot.CurrentMap!.Definition.Name;
        await gameContext.SendGlobalMessageAsync(
                $"WANTED: {bot.Name} is on {map}! Take it down within {configuration.DurationMinutes} minutes for a reward.",
                MessageType.GoldenCenter)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Pays the bounty when the wanted bot got killed by a player.
    /// </summary>
    /// <param name="killed">The killed object.</param>
    /// <param name="killer">The killer.</param>
    /// <returns>The task.</returns>
    internal static async ValueTask OnKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (killed is not BotPlayer bot
            || !Bounties.TryGetValue(bot.GameContext, out var bounty)
            || !ReferenceEquals(bounty.Bot, bot)
            || (killer as Player ?? (killer as NPC.Monster)?.SummonedBy) is not { } hunter
            || hunter is BotPlayer)
        {
            return;
        }

        await EndAsync(bot.GameContext, bounty, hunter).ConfigureAwait(false);
    }

    private static async ValueTask EndAsync(IGameContext gameContext, Bounty bounty, Player? hunter)
    {
        if (!Bounties.TryRemove(new KeyValuePair<IGameContext, Bounty>(gameContext, bounty)))
        {
            return;
        }

        if (bounty.Bot.SelectedCharacter is { } character)
        {
            character.State = bounty.PreviousState;
        }

        if (hunter is null)
        {
            await gameContext.SendGlobalMessageAsync($"{bounty.Bot.Name} got away. No bounty this time.", MessageType.BlueNormal).ConfigureAwait(false);
            return;
        }

        var configuration = gameContext.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<BountyBotPlugIn>().FirstOrDefault()?.Configuration
            ?? new BountyBotConfiguration();
        if (hunter.Account is { } account)
        {
            PlayerRewards.AddCoins(account, configuration.CoinType, configuration.Coins);
        }

        await PlayerRewards.GiveItemsAsync(hunter, configuration.Items).ConfigureAwait(false);
        await gameContext.SendGlobalMessageAsync($"{hunter.Name} took down the wanted {bounty.Bot.Name} and claims the bounty!", MessageType.GoldenCenter).ConfigureAwait(false);
    }

    private sealed record Bounty(BotPlayer Bot, HeroState PreviousState, DateTime EndsAtUtc);
}

/// <summary>
/// Reports kills to the <see cref="BountyBotPlugIn"/>.
/// </summary>
[Guid("4B91E6D3-2A58-4C7F-9E03-B6D8A1F5C724")]
[PlugIn]
[Display(Name = "Bounty bot kills", Description = "Pays the bounty of the Bounty bot plugin when the wanted bot is killed.")]
public class BountyBotKillPlugIn : IAttackableGotKilledPlugIn
{
    /// <inheritdoc />
    public ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer) => BountyBotPlugIn.OnKilledAsync(killed, killer);
}

/// <summary>
/// The configuration of the <see cref="BountyBotPlugIn"/>.
/// </summary>
public class BountyBotConfiguration
{
    /// <summary>Gets or sets how often a bot becomes wanted.</summary>
    [Display(Name = "A bounty every (minutes)")]
    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>Gets or sets how long the bounty lasts.</summary>
    [Display(Name = "Bounty lasts (minutes)")]
    [Range(1, 240)]
    public int DurationMinutes { get; set; } = 15;

    /// <summary>Gets or sets the coin type of the reward.</summary>
    [Display(Name = "Reward coin")]
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>Gets or sets the coins of the reward.</summary>
    [Display(Name = "Reward coins")]
    public int Coins { get; set; } = 200;

    /// <summary>Gets or sets the items of the reward, as comma separated group:number:level:count.</summary>
    [Display(Name = "Reward items", Description = "Comma separated group:number:level:count; e.g. 12:30:0:1 is a pack of 10 Jewels of Bless.")]
    public string Items { get; set; } = "12:30:0:1, 12:31:0:1";
}
