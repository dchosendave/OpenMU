// <copyright file="PlayerSiteFeaturesService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.PlayerSite;

using System.Threading;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.CashShop;
using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.GameLogic.PlayerSite;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Web.AdminPanel.AccountManager;
using MUnique.OpenMU.Web.AdminPanel.Services;

/// <summary>
/// The features of the player website besides its mini games: daily check-in, character tools, news and
/// the player market. Changes of accounts go through <see cref="PlayerSiteService"/>, so they share its
/// rules (offline only, one change per account at a time).
/// </summary>
public sealed class PlayerSiteFeaturesService
{
    private static readonly IReadOnlyDictionary<string, AttributeDefinition> StatsByName = new Dictionary<string, AttributeDefinition>(StringComparer.OrdinalIgnoreCase)
    {
        ["strength"] = Stats.BaseStrength,
        ["agility"] = Stats.BaseAgility,
        ["vitality"] = Stats.BaseVitality,
        ["energy"] = Stats.BaseEnergy,
        ["command"] = Stats.BaseLeadership,
    };

    /// <summary>
    /// The account whose vault holds the listed items. It's banned, so nobody can log in to it.
    /// </summary>
    public const string EscrowLogin = "_market_";

    /// <summary>
    /// The e-mail address which marks <see cref="EscrowLogin"/> as the market's own account.
    /// </summary>
    private const string EscrowMarker = "escrow@market.local";

    private readonly PlayerSiteService _playerSite;
    private readonly IDataSource<GameConfiguration> _configurationSource;
    private readonly IPlayerSiteRepository _repository;
    private readonly CashShopCoinService _coins;
    private readonly ILogger<PlayerSiteFeaturesService> _logger;
    private readonly SemaphoreSlim _marketLock = new(1, 1);
    private bool _escrowReady;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerSiteFeaturesService"/> class.
    /// </summary>
    /// <param name="playerSite">The player site service, which changes accounts.</param>
    /// <param name="configurationSource">The game configuration.</param>
    /// <param name="persistenceContextProvider">The persistence context provider, for coin grants.</param>
    /// <param name="repository">The data of the player website.</param>
    /// <param name="logger">The logger.</param>
    public PlayerSiteFeaturesService(PlayerSiteService playerSite, IDataSource<GameConfiguration> configurationSource, IPersistenceContextProvider persistenceContextProvider, IPlayerSiteRepository repository, ILogger<PlayerSiteFeaturesService> logger)
    {
        this._playerSite = playerSite;
        this._configurationSource = configurationSource;
        this._repository = repository;
        this._coins = new CashShopCoinService(persistenceContextProvider, configurationSource);
        this._logger = logger;
    }

    /// <summary>
    /// Checks the account in for today and grants the reward of its streak day.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <returns>The result, with the streak day and reward.</returns>
    public async Task<PlayerSiteResult> CheckInAsync(string loginName)
    {
        var settings = (await this.GetSettingsAsync().ConfigureAwait(false)).CheckIn;
        if (!settings.Enabled || CheckInConfiguration.ParseRewards(settings.Rewards).Count == 0)
        {
            return new PlayerSiteResult(PlayerSiteResult.NotFound, "The daily check-in is closed right now.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var last = await this._repository.GetLastCheckInAsync(loginName).ConfigureAwait(false);
        if (last?.Day == today)
        {
            return new PlayerSiteResult(PlayerSiteResult.Invalid, "You already checked in today. Come back tomorrow!");
        }

        var streakDay = settings.StreakDayOf(last?.StreakDay ?? 0, last?.Day, today);
        var reward = settings.RewardOf(streakDay);
        var checkIn = new WebCheckIn { LoginName = loginName, Day = today, StreakDay = streakDay, Reward = reward, CoinType = settings.CoinType.ToString() };
        if (!await this._repository.TryAddCheckInAsync(checkIn).ConfigureAwait(false))
        {
            return new PlayerSiteResult(PlayerSiteResult.Invalid, "You already checked in today. Come back tomorrow!");
        }

        if (reward > 0)
        {
            var (status, _) = await this._coins.GrantAsync(loginName, settings.CoinType, reward, $"Daily check-in, day {streakDay}", $"checkin:{loginName}:{today:yyyy-MM-dd}", "player website").ConfigureAwait(false);
            if (status is CashShopCoinGrantStatus.AccountNotFound or CashShopCoinGrantStatus.ReferenceUsedByOtherAccount)
            {
                return new PlayerSiteResult(PlayerSiteResult.NotFound, "The account doesn't exist.");
            }

            // While the account is offline, the reward goes onto the balance right away; otherwise the
            // game adds it when the player opens the cash shop.
            await this._playerSite.ChangeAccountAsync(loginName, _ => Task.FromResult<object?>(null)).ConfigureAwait(false);
        }

        return new PlayerSiteResult(PlayerSiteResult.Ok, Data: new { streakDay, reward, coinType = settings.CoinType.ToString(), tomorrow = settings.RewardOf(settings.StreakDayOf(streakDay, today, today.AddDays(1))) });
    }

    /// <summary>
    /// Resets a character with the rules of the reset feature.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="characterName">The character name.</param>
    /// <returns>The result, with the new reset count.</returns>
    public async Task<PlayerSiteResult> ResetAsync(string loginName, string characterName)
    {
        if (!(await this.GetSettingsAsync().ConfigureAwait(false)).CharacterTools.ResetEnabled)
        {
            return new PlayerSiteResult(PlayerSiteResult.NotFound, "Resetting on the website is turned off.");
        }

        var gameConfiguration = await this._configurationSource.GetOwnerAsync().ConfigureAwait(false);
        var plugIn = gameConfiguration.PlugInConfigurations.FirstOrDefault(p => p.TypeId == typeof(ResetFeaturePlugIn).GUID);
        if (plugIn is not { IsActive: true } || plugIn.GetConfiguration<ResetConfiguration>(new ByDataSourceReferenceHandler(this._configurationSource)) is not { } configuration)
        {
            return new PlayerSiteResult(PlayerSiteResult.NotFound, "Resets are turned off on this server.");
        }

        return await this._playerSite.ChangeAccountAsync(loginName, async session =>
        {
            var model = CharacterOf(session, characterName);
            var character = model.Character;
            var level = Attribute(character, Stats.Level) ?? throw Refuse("This character has no level.");
            var resets = Attribute(character, Stats.Resets) ?? throw Refuse("This character can't reset.");
            var pointsOverride = (int)(character.CharacterClass?.BaseAttributeValues.FirstOrDefault(v => v.Definition == Stats.PointsPerReset)?.Value ?? 0);
            var progression = ResetProgressionCalculator.Calculate((int)resets.Value, pointsOverride, configuration);
            if (level.Value < configuration.RequiredLevel)
            {
                throw Refuse($"Reach level {configuration.RequiredLevel} first.");
            }

            if (configuration.ResetLimit > 0 && progression.NextResetCount > configuration.ResetLimit)
            {
                throw Refuse($"This character has reached the limit of {configuration.ResetLimit} resets.");
            }

            if (model.Zen < progression.RequiredZen)
            {
                throw Refuse($"A reset costs {progression.RequiredZen:N0} zen in {character.Name}'s inventory; there's {model.Zen:N0}.");
            }

            var requiredItems = new List<Item>();
            if (progression.RequiredItemAmount > 0 && configuration.RequiredResetItem is { } required)
            {
                requiredItems = model.Inventory.Items.Where(i => i.Definition?.Group == required.Group && i.Definition.Number == required.Number).Take(progression.RequiredItemAmount).ToList();
                if (requiredItems.Count < progression.RequiredItemAmount)
                {
                    throw Refuse($"A reset needs {progression.RequiredItemAmount} {required.Name} in {character.Name}'s inventory.");
                }
            }

            model.Zen -= progression.RequiredZen;
            foreach (var item in requiredItems)
            {
                await session.DeleteItemAsync(model.Inventory, item).ConfigureAwait(false);
            }

            resets.Value = progression.NextResetCount;
            level.Value = configuration.LevelAfterReset;
            character.Experience = 0;
            if (configuration.ResetStats && character.CharacterClass is { } characterClass)
            {
                foreach (var stat in characterClass.StatAttributes.Where(s => s.IncreasableByPlayer))
                {
                    if (stat.Attribute is { } definition && Attribute(character, definition) is { } attribute)
                    {
                        attribute.Value = stat.BaseValue;
                    }
                }
            }

            character.LevelUpPoints = configuration.ReplacePointsPerReset
                ? progression.TotalPointsAfterReset
                : (int)Math.Min(int.MaxValue, (long)character.LevelUpPoints + progression.PointsForReset);
            if (configuration.MoveHome)
            {
                MoveHome(character);
            }

            session.MarkChanged($"Reset {character.Name} on the player website");
            return new { resets = progression.NextResetCount, level = configuration.LevelAfterReset, points = character.LevelUpPoints, zen = progression.RequiredZen };
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves a character back to the safe zone of its home town.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="characterName">The character name.</param>
    /// <returns>The result.</returns>
    public async Task<PlayerSiteResult> UnstuckAsync(string loginName, string characterName)
    {
        if (!(await this.GetSettingsAsync().ConfigureAwait(false)).CharacterTools.UnstuckEnabled)
        {
            return new PlayerSiteResult(PlayerSiteResult.NotFound, "This tool is turned off.");
        }

        return await this._playerSite.ChangeAccountAsync(loginName, session =>
        {
            var character = CharacterOf(session, characterName).Character;
            if (!MoveHome(character))
            {
                throw Refuse("This character has no home town to go to.");
            }

            session.MarkChanged($"Moved {character.Name} home on the player website");
            return Task.FromResult<object?>(new { map = character.CurrentMap?.Name.ToString() });
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Spends free stat points.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="characterName">The character name.</param>
    /// <param name="points">The points per stat: strength, agility, vitality, energy, command.</param>
    /// <returns>The result.</returns>
    public async Task<PlayerSiteResult> AddStatsAsync(string loginName, string characterName, IReadOnlyDictionary<string, int> points)
    {
        if (!(await this.GetSettingsAsync().ConfigureAwait(false)).CharacterTools.StatsEnabled)
        {
            return new PlayerSiteResult(PlayerSiteResult.NotFound, "This tool is turned off.");
        }

        if (points.Any(p => !StatsByName.ContainsKey(p.Key) || p.Value < 0) || points.Values.Sum(v => (long)v) <= 0)
        {
            return new PlayerSiteResult(PlayerSiteResult.Invalid, "Pick how many points to add.");
        }

        return await this._playerSite.ChangeAccountAsync(loginName, session =>
        {
            var character = CharacterOf(session, characterName).Character;
            var total = points.Values.Sum(v => (long)v);
            if (total > character.LevelUpPoints)
            {
                throw Refuse($"{character.Name} has {character.LevelUpPoints:N0} free points.");
            }

            foreach (var (name, amount) in points.Where(p => p.Value > 0))
            {
                var attribute = Attribute(character, StatsByName[name]) ?? throw Refuse($"{character.Name}'s class has no {name}.");
                var maximum = attribute.Definition?.MaximumValue ?? float.MaxValue;
                if (attribute.Value + amount > maximum)
                {
                    throw Refuse($"{name} can go up to {maximum:N0}; it's at {attribute.Value:N0}.");
                }

                attribute.Value += amount;
            }

            character.LevelUpPoints -= (int)total;
            session.MarkChanged($"Added stat points to {character.Name} on the player website");
            return Task.FromResult<object?>(new { points = character.LevelUpPoints });
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears the player kills of a character for zen from its inventory.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="characterName">The character name.</param>
    /// <returns>The result, with the paid zen.</returns>
    public async Task<PlayerSiteResult> ClearPlayerKillsAsync(string loginName, string characterName)
    {
        var tools = (await this.GetSettingsAsync().ConfigureAwait(false)).CharacterTools;
        if (!tools.PkClearEnabled)
        {
            return new PlayerSiteResult(PlayerSiteResult.NotFound, "This tool is turned off.");
        }

        return await this._playerSite.ChangeAccountAsync(loginName, session =>
        {
            var model = CharacterOf(session, characterName);
            var character = model.Character;
            if (character.PlayerKillCount <= 0 && character.State <= HeroState.Normal)
            {
                throw Refuse($"{character.Name} has no player kills to clear.");
            }

            var cost = (long)Math.Max(1, character.PlayerKillCount) * Math.Max(0, tools.PkClearZenPerKill);
            if (model.Zen < cost)
            {
                throw Refuse($"Clearing costs {cost:N0} zen in {character.Name}'s inventory; there's {model.Zen:N0}.");
            }

            model.Zen -= (int)cost;
            character.PlayerKillCount = 0;
            character.State = HeroState.Normal;
            character.StateRemainingSeconds = 0;
            session.MarkChanged($"Cleared the player kills of {character.Name} on the player website");
            return Task.FromResult<object?>(new { zen = cost });
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves a news post.
    /// </summary>
    /// <param name="post">The post.</param>
    /// <returns>The result.</returns>
    public async Task<PlayerSiteResult> SaveNewsAsync(WebNewsPost post)
    {
        post.Slug = post.Slug.Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(post.Slug, "^[a-z0-9]+(-[a-z0-9]+)*$") || post.Slug.Length > 80
            || string.IsNullOrWhiteSpace(post.Title) || post.Title.Length > WebNewsPost.TitleMaximumLength
            || post.Summary.Length > WebNewsPost.SummaryMaximumLength || post.Body.Length > WebNewsPost.BodyMaximumLength
            || post.Tag is not ("Update" or "Event" or "Notice"))
        {
            return new PlayerSiteResult(PlayerSiteResult.Invalid, "Check the post: a title, a slug of lowercase letters, digits and dashes, a tag, and not too long a text.");
        }

        return await this._repository.SaveNewsPostAsync(post).ConfigureAwait(false)
            ? new PlayerSiteResult(PlayerSiteResult.Ok, Data: new { post.Id, post.Slug })
            : new PlayerSiteResult(PlayerSiteResult.Invalid, "Another post already has this address (slug).");
    }

    /// <summary>
    /// Deletes a news post.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The result.</returns>
    public async Task<PlayerSiteResult> DeleteNewsAsync(Guid id)
    {
        await this._repository.DeleteNewsPostAsync(id).ConfigureAwait(false);
        return new PlayerSiteResult(PlayerSiteResult.Ok);
    }

    /// <summary>
    /// Lists an item of the vault on the market; it waits in the vault of the escrow account until it's sold
    /// or taken back.
    /// </summary>
    /// <param name="loginName">The login name of the seller.</param>
    /// <param name="slot">The slot of the item in the vault.</param>
    /// <param name="price">The price.</param>
    /// <returns>The result, with the listing.</returns>
    public async Task<PlayerSiteResult> ListItemAsync(string loginName, byte slot, int price)
    {
        var market = (await this.GetSettingsAsync().ConfigureAwait(false)).Market;
        if (!market.Enabled)
        {
            return new PlayerSiteResult(PlayerSiteResult.NotFound, "The market is closed right now.");
        }

        if (price < market.MinimumPrice || price > market.MaximumPrice)
        {
            return new PlayerSiteResult(PlayerSiteResult.Invalid, $"Set a price between {market.MinimumPrice:N0} and {market.MaximumPrice:N0}.");
        }

        return await this.WithMarketAsync(async () =>
        {
            if (await this._repository.CountActiveListingsAsync(loginName).ConfigureAwait(false) >= market.MaximumListingsPerAccount)
            {
                return new PlayerSiteResult(PlayerSiteResult.Invalid, $"You can have up to {market.MaximumListingsPerAccount} items on the market at once.");
            }

            WebMarketListing? listing = null;
            return await this._playerSite.ChangeAccountAsync(
                loginName,
                async session =>
                {
                    var item = session.Vault.ItemAt(slot) ?? throw Refuse("There is no item in this vault slot.");
                    if (item.Definition is not { } definition || !definition.IsTradable)
                    {
                        throw Refuse("This item can't be traded.");
                    }

                    var escrow = await OpenEscrowAsync(session).ConfigureAwait(false);
                    if (escrow.FindFreeSlot(item) is null)
                    {
                        throw PlayerSiteService.Refuse(PlayerSiteResult.NoSpace, "The market is full right now. Try again later.");
                    }

                    session.TransferItem(session.Vault, item, escrow);
                    listing = new WebMarketListing
                    {
                        SellerLogin = session.Account.LoginName,
                        EscrowLogin = EscrowLogin,
                        ItemId = item.GetId(),
                        ItemName = $"{definition.Name}{(item.Level > 0 ? $" +{item.Level}" : string.Empty)}",
                        Price = price,
                        CoinType = market.CoinType.ToString(),
                    };
                    return new { listing.Id, listing.ItemName, price, coinType = listing.CoinType };
                },
                async _ =>
                {
                    try
                    {
                        await this._repository.AddListingAsync(listing!).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // The item is in the escrow vault now; without the listing only an admin can return it.
                        this._logger.LogCritical(ex, "Couldn't store the market listing of {Seller}: item {ItemId} lies in the vault of {Escrow}.", listing!.SellerLogin, listing.ItemId, EscrowLogin);
                    }
                }).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes a listed item back into the seller's vault.
    /// </summary>
    /// <param name="loginName">The login name of the seller.</param>
    /// <param name="listingId">The listing.</param>
    /// <returns>The result.</returns>
    public async Task<PlayerSiteResult> CancelListingAsync(string loginName, Guid listingId)
    {
        var listing = await this._repository.GetListingAsync(listingId).ConfigureAwait(false);
        if (listing is null || !string.Equals(listing.SellerLogin, loginName, StringComparison.OrdinalIgnoreCase))
        {
            return new PlayerSiteResult(PlayerSiteResult.NotFound, "You have no such listing.");
        }

        return await this.WithMarketAsync(async () =>
        {
            if (!await this._repository.TryChangeListingStateAsync(listingId, WebMarketListing.Active, WebMarketListing.Pending, null).ConfigureAwait(false))
            {
                return new PlayerSiteResult(PlayerSiteResult.Invalid, "This item was just sold or taken back.");
            }

            var result = await this._playerSite.ChangeAccountAsync(
                loginName,
                async session =>
                {
                    await TakeFromEscrowAsync(session, listing).ConfigureAwait(false);
                    return new { listing.Id };
                },
                _ => this.SetStateAsync(listingId, WebMarketListing.Cancelled)).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                await this._repository.TryChangeListingStateAsync(listingId, WebMarketListing.Pending, WebMarketListing.Active, null).ConfigureAwait(false);
            }

            return result;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Buys a listed item: the price leaves the buyer's balance, the item goes into the buyer's vault, and the
    /// seller gets the price minus the fee as a coin grant.
    /// </summary>
    /// <param name="loginName">The login name of the buyer.</param>
    /// <param name="listingId">The listing.</param>
    /// <returns>The result, with the new balance.</returns>
    public async Task<PlayerSiteResult> BuyListingAsync(string loginName, Guid listingId)
    {
        var market = (await this.GetSettingsAsync().ConfigureAwait(false)).Market;
        var listing = await this._repository.GetListingAsync(listingId).ConfigureAwait(false);
        if (!market.Enabled || listing is null || !Enum.TryParse<CashShopCoinType>(listing.CoinType, out var coinType))
        {
            return new PlayerSiteResult(PlayerSiteResult.NotFound, "This item isn't for sale.");
        }

        if (string.Equals(listing.SellerLogin, loginName, StringComparison.OrdinalIgnoreCase))
        {
            return new PlayerSiteResult(PlayerSiteResult.Invalid, "That's your own item. Take it back from your listings instead.");
        }

        return await this.WithMarketAsync(async () =>
        {
            // Reserve the listing first, so of two buyers only one gets it.
            if (!await this._repository.TryChangeListingStateAsync(listingId, WebMarketListing.Active, WebMarketListing.Pending, loginName).ConfigureAwait(false))
            {
                return new PlayerSiteResult(PlayerSiteResult.Invalid, "Someone else was faster: this item is gone.");
            }

            var result = await this._playerSite.ChangeAccountAsync(
                loginName,
                async session =>
                {
                    var balance = session.Account.GetCashShopCoins(coinType);
                    if (balance < listing.Price)
                    {
                        throw Refuse($"This costs {listing.Price:N0}, but you have {balance:N0}.");
                    }

                    await TakeFromEscrowAsync(session, listing).ConfigureAwait(false);
                    session.Account.SetCashShopCoins(coinType, balance - listing.Price);
                    session.MarkChanged($"Bought {listing.ItemName} on the player market");
                    return new { listing.Id, listing.ItemName, balance = balance - listing.Price, coinType = coinType.ToString() };
                },
                async _ =>
                {
                    await this.SetStateAsync(listingId, WebMarketListing.Sold).ConfigureAwait(false);
                    var share = market.SellerShareOf(listing.Price);
                    if (share > 0)
                    {
                        try
                        {
                            await this._coins.GrantAsync(listing.SellerLogin, coinType, share, $"Sold {listing.ItemName} on the player market", $"market:{listing.Id}", "player website").ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            this._logger.LogCritical(ex, "The market sale {ListingId} is done, but the seller {Seller} couldn't be paid {Share} {CoinType}.", listing.Id, listing.SellerLogin, share, coinType);
                        }
                    }
                }).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                await this._repository.TryChangeListingStateAsync(listingId, WebMarketListing.Pending, WebMarketListing.Active, string.Empty).ConfigureAwait(false);
            }

            return result;
        }).ConfigureAwait(false);
    }

    private static Exception Refuse(string message) => PlayerSiteService.Refuse(PlayerSiteResult.Invalid, message);

    private static CharacterModel CharacterOf(AccountSession session, string name)
        => session.Characters.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
           ?? throw PlayerSiteService.Refuse(PlayerSiteResult.NotFound, $"There is no character '{name}' on this account.");

    private static StatAttribute? Attribute(Character character, AttributeDefinition definition)
        => character.Attributes.FirstOrDefault(a => a.Definition?.Id == definition.Id);

    private static bool MoveHome(Character character)
    {
        if (character.CharacterClass?.HomeMap is not { } home || home.GetSafezoneGate() is not { } gate)
        {
            return false;
        }

        character.CurrentMap = gate.Map ?? home;
        character.PositionX = (byte)Rand.NextInt(gate.X1, gate.X2 + 1);
        character.PositionY = (byte)Rand.NextInt(gate.Y1, gate.Y2 + 1);
        return true;
    }

    private static async Task<StorageModel> OpenEscrowAsync(AccountSession session)
        => (await session.OpenVaultOfAsync(EscrowLogin).ConfigureAwait(false))?.Vault
           ?? throw PlayerSiteService.Refuse(PlayerSiteResult.NotFound, "The market isn't ready. Please contact the team.");

    private static async Task TakeFromEscrowAsync(AccountSession session, WebMarketListing listing)
    {
        var escrow = await OpenEscrowAsync(session).ConfigureAwait(false);
        var item = escrow.Items.FirstOrDefault(i => i.GetId() == listing.ItemId)
            ?? throw PlayerSiteService.Refuse(PlayerSiteResult.NotFound, "The item of this listing can't be found. Please contact the team.");
        try
        {
            session.TransferItem(escrow, item, session.Vault);
        }
        catch (AccountManagerException)
        {
            throw PlayerSiteService.Refuse(PlayerSiteResult.NoSpace, $"There is no room for {item.Definition?.Name} in your vault.");
        }
    }

    /// <summary>
    /// Runs a market operation: one at a time, so they don't pick the same slot of the escrow vault, and only
    /// with an escrow account which belongs to the market.
    /// </summary>
    private async Task<PlayerSiteResult> WithMarketAsync(Func<Task<PlayerSiteResult>> operation)
    {
        await this._marketLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!this._escrowReady)
            {
                this._escrowReady = await this._playerSite.EnsureSystemAccountAsync(EscrowLogin, EscrowMarker).ConfigureAwait(false);
                if (!this._escrowReady)
                {
                    this._logger.LogCritical("The account {Escrow} exists but isn't the market's escrow account; the market stays closed.", EscrowLogin);
                    return new PlayerSiteResult(PlayerSiteResult.NotFound, "The market isn't available right now.");
                }
            }

            return await operation().ConfigureAwait(false);
        }
        finally
        {
            this._marketLock.Release();
        }
    }

    private async Task SetStateAsync(Guid listingId, string state)
    {
        if (!await this._repository.TryChangeListingStateAsync(listingId, WebMarketListing.Pending, state, null).ConfigureAwait(false))
        {
            this._logger.LogError("The market listing {ListingId} wasn't pending when it should become {State}.", listingId, state);
        }
    }

    private async Task<PlayerSiteConfiguration> GetSettingsAsync()
    {
        var gameConfiguration = await this._configurationSource.GetOwnerAsync().ConfigureAwait(false);
        var plugIn = gameConfiguration.PlugInConfigurations.FirstOrDefault(p => p.TypeId == typeof(PlayerSitePlugIn).GUID);
        if (plugIn is { IsActive: false })
        {
            // The plugin switched off closes every feature.
            return new PlayerSiteConfiguration
            {
                CheckIn = { Enabled = false },
                CharacterTools = { ResetEnabled = false, UnstuckEnabled = false, StatsEnabled = false, PkClearEnabled = false },
                Market = { Enabled = false },
            };
        }

        return plugIn?.GetConfiguration<PlayerSiteConfiguration>(new ByDataSourceReferenceHandler(this._configurationSource)) ?? new PlayerSiteConfiguration();
    }
}
