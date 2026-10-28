// <copyright file="PlayerSiteConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerSite;

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// The configuration of the <see cref="PlayerSitePlugIn"/>.
/// </summary>
public class PlayerSiteConfiguration
{
    /// <summary>Gets or sets the daily check-in.</summary>
    [Display(Name = "Daily check-in")]
    public CheckInConfiguration CheckIn { get; set; } = new();

    /// <summary>Gets or sets the character tools.</summary>
    [Display(Name = "Character tools")]
    public CharacterToolsConfiguration CharacterTools { get; set; } = new();

    /// <summary>Gets or sets the player market.</summary>
    [Display(Name = "Player market")]
    public MarketConfiguration Market { get; set; } = new();

    /// <summary>
    /// Gets or sets the login names of the accounts which may change settings on the website, comma separated.
    /// </summary>
    [Display(Name = "Website administrators", Description = "Comma separated login names which may change the Perya and Castle Siege settings on the website. Game masters can't, unless they're listed here.")]
    public string SiteAdministrators { get; set; } = string.Empty;
}

/// <summary>
/// The daily check-in: one reward per account and day (UTC); checking in on consecutive days climbs a
/// streak of rewards, which starts over after its last day or a missed day.
/// </summary>
public class CheckInConfiguration
{
    /// <summary>Gets or sets a value indicating whether the check-in is open.</summary>
    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the coin type of the rewards.</summary>
    [Display(Name = "Reward coin")]
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>Gets or sets the rewards of the streak days, comma separated.</summary>
    [Display(Name = "Rewards per streak day", Description = "Comma separated, day 1 first. After the last day the streak starts over.")]
    public string Rewards { get; set; } = "10, 10, 15, 15, 20, 20, 50";

    /// <summary>
    /// Gets the reward of a streak day.
    /// </summary>
    /// <param name="streakDay">The day of the streak, 1 based.</param>
    /// <returns>The reward.</returns>
    public int RewardOf(int streakDay)
    {
        var rewards = ParseRewards(this.Rewards);
        return rewards.Count == 0 ? 0 : rewards[(streakDay - 1) % rewards.Count];
    }

    /// <summary>
    /// Parses the comma separated rewards.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The rewards; empty if the text is invalid.</returns>
    public static IReadOnlyList<int> ParseRewards(string text)
    {
        var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var rewards = new List<int>();
        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var reward) || reward < 0 || reward > 1_000_000)
            {
                return [];
            }

            rewards.Add(reward);
        }

        return rewards.Count <= 31 ? rewards : [];
    }

    /// <summary>
    /// Gets the streak day of a check-in.
    /// </summary>
    /// <param name="lastStreakDay">The streak day of the account's last check-in, or 0 if there was none.</param>
    /// <param name="lastDay">The day of the last check-in.</param>
    /// <param name="today">Today.</param>
    /// <returns>The streak day of today's check-in, 1 based.</returns>
    public int StreakDayOf(int lastStreakDay, DateOnly? lastDay, DateOnly today)
    {
        var length = Math.Max(1, ParseRewards(this.Rewards).Count);
        return lastDay is { } last && last.AddDays(1) == today && lastStreakDay > 0 ? (lastStreakDay % length) + 1 : 1;
    }
}

/// <summary>
/// The character tools of the player website. They only work while the account is offline.
/// </summary>
public class CharacterToolsConfiguration
{
    /// <summary>Gets or sets a value indicating whether characters can reset on the website, with the in-game reset rules.</summary>
    [Display(Name = "Reset on the website", Description = "Uses the rules of the reset feature; the zen is taken from the character's inventory.")]
    public bool ResetEnabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether characters can be moved back to their home town.</summary>
    [Display(Name = "Unstuck (move home)")]
    public bool UnstuckEnabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether free stat points can be spent on the website.</summary>
    [Display(Name = "Spend stat points")]
    public bool StatsEnabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the player kill count can be cleared on the website.</summary>
    [Display(Name = "Clear player kills")]
    public bool PkClearEnabled { get; set; } = true;

    /// <summary>Gets or sets the zen per player kill which clearing them costs, taken from the character's inventory.</summary>
    [Display(Name = "PK clear zen per kill")]
    public int PkClearZenPerKill { get; set; } = 500_000;
}

/// <summary>
/// The player market: players list items of their vault for coins; buyers get the item into their vault and
/// the seller gets the price minus a fee.
/// </summary>
public class MarketConfiguration
{
    /// <summary>Gets or sets a value indicating whether the market is open.</summary>
    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the coin type of the prices.</summary>
    [Display(Name = "Currency")]
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>Gets or sets the share of the price which the market keeps, in percent.</summary>
    [Display(Name = "Fee (%)", Description = "Kept from the price; the seller gets the rest.")]
    public int FeePercent { get; set; } = 5;

    /// <summary>Gets or sets the most items an account may have listed at the same time.</summary>
    [Display(Name = "Listings per account")]
    public int MaximumListingsPerAccount { get; set; } = 10;

    /// <summary>Gets or sets the lowest price.</summary>
    [Display(Name = "Minimum price")]
    public int MinimumPrice { get; set; } = 1;

    /// <summary>Gets or sets the highest price.</summary>
    [Display(Name = "Maximum price")]
    public int MaximumPrice { get; set; } = 1_000_000;

    /// <summary>
    /// Gets what the seller gets for a price.
    /// </summary>
    /// <param name="price">The price.</param>
    /// <returns>The price minus the fee, rounded down; at least 0.</returns>
    public int SellerShareOf(int price) => (int)Math.Max(0, price - Math.Ceiling(price * Math.Clamp(this.FeePercent, 0, 100) / 100.0));
}
