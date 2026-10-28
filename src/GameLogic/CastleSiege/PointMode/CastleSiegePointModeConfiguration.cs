// <copyright file="CastleSiegePointModeConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.CastleSiege.PointMode;

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// The configuration of the <see cref="CastleSiegePointModePlugIn"/>.
/// </summary>
public class CastleSiegePointModeConfiguration
{
    /// <summary>Gets or sets the points of a successful seal (crown registration).</summary>
    [Display(Name = "Points per seal")]
    [Range(0, 1000)]
    public int SealPoints { get; set; } = 10;

    /// <summary>Gets or sets the points for killing a switch holder of the opposing side.</summary>
    [Display(Name = "Points per switch holder kill")]
    [Range(0, 1000)]
    public int SwitchHolderKillPoints { get; set; } = 2;

    /// <summary>Gets or sets how long a player must have held the switch for the kill to score.</summary>
    [Display(Name = "Switch held at least (seconds)", Description = "A kill only scores when the victim held the switch at least this long.")]
    [Range(0, 600)]
    public int SwitchHoldMinimumSeconds { get; set; } = 3;

    /// <summary>Gets or sets the minutes the castle must be held for a defense tick.</summary>
    [Display(Name = "Defense tick every (minutes)", Description = "The castle holder scores every full interval; the clock restarts with every seal. 0 turns the ticks off.")]
    [Range(0, 120)]
    public int DefenseTickMinutes { get; set; } = 5;

    /// <summary>Gets or sets the points of a defense tick.</summary>
    [Display(Name = "Points per defense tick")]
    [Range(0, 1000)]
    public int DefenseTickPoints { get; set; } = 3;

    /// <summary>Gets or sets a value indicating whether every guild banks its own crown progress.</summary>
    [Display(Name = "Crown progress per guild", Description = "Each guild keeps its own banked crown time, so nobody can finish a seal on another guild's progress.")]
    public bool CrownProgressPerGuild { get; set; } = true;

    /// <summary>Gets or sets how often the scoreboard is posted in game.</summary>
    [Display(Name = "Scoreboard in chat every (minutes)", Description = "0 turns it off; the score changes are announced anyway.")]
    [Range(0, 60)]
    public int ScoreboardIntervalMinutes { get; set; } = 5;

    /// <summary>Gets or sets the coin type of the rewards.</summary>
    [Display(Name = "Reward coin")]
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>Gets or sets the reward of the guild with the most points.</summary>
    [Display(Name = "Winner")]
    public CastleSiegeRewardTier Winner { get; set; } = new()
    {
        Items = "14:13:0:10, 14:14:0:10",
        GuildMasterCoins = 1000,
        MemberCoins = 100,
    };

    /// <summary>Gets or sets the reward of the guild which held the castle at the end, but lost on points.</summary>
    [Display(Name = "Consolation (held the castle at the end, lost on points)")]
    public CastleSiegeRewardTier Consolation { get; set; } = new()
    {
        Items = "14:13:0:5, 14:14:0:5",
        GuildMasterCoins = 500,
        MemberCoins = 60,
    };

    /// <summary>Gets or sets the reward of every other guild which scored at least one point.</summary>
    [Display(Name = "Participant (scored at least one point)")]
    public CastleSiegeRewardTier Participant { get; set; } = new()
    {
        Items = string.Empty,
        GuildMasterCoins = 0,
        MemberCoins = 30,
    };

    /// <summary>
    /// Gets the problems of the configuration.
    /// </summary>
    /// <returns>The problems; empty, if it's valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        if (this.SealPoints < 0 || this.SwitchHolderKillPoints < 0 || this.DefenseTickPoints < 0)
        {
            problems.Add("Points can't be negative.");
        }

        if (this.DefenseTickMinutes < 0 || this.SwitchHoldMinimumSeconds < 0 || this.ScoreboardIntervalMinutes < 0)
        {
            problems.Add("Times can't be negative.");
        }

        foreach (var (name, tier) in new[] { ("Winner", this.Winner), ("Consolation", this.Consolation), ("Participant", this.Participant) })
        {
            if (tier.GuildMasterCoins < 0 || tier.MemberCoins < 0)
            {
                problems.Add($"{name}: coins can't be negative.");
            }

            if (!CastleSiegeRewardItem.TryParseList(tier.Items, out _, out var error))
            {
                problems.Add($"{name}: {error}");
            }
        }

        return problems;
    }
}

/// <summary>
/// The reward of a placement in a point mode Castle Siege.
/// </summary>
public class CastleSiegeRewardTier
{
    /// <summary>Gets or sets the items for the vault of the guild master's account.</summary>
    [Display(Name = "Items for the guild master's vault", Description = "Comma separated group:number:level:count, e.g. 14:13:0:10 for ten Jewels of Bless.")]
    public string Items { get; set; } = string.Empty;

    /// <summary>Gets or sets the coins for the guild master's account, to share out in the guild.</summary>
    [Display(Name = "Coins for the guild master")]
    public int GuildMasterCoins { get; set; }

    /// <summary>Gets or sets the coins for every member who took part long enough.</summary>
    [Display(Name = "Coins per participating member")]
    public int MemberCoins { get; set; }
}

/// <summary>
/// An item reward: <c>count</c> pieces of the item with the given group, number and level.
/// </summary>
/// <param name="Group">The item group.</param>
/// <param name="Number">The item number within the group.</param>
/// <param name="Level">The item level.</param>
/// <param name="Count">The number of pieces.</param>
public sealed record CastleSiegeRewardItem(byte Group, short Number, byte Level, int Count)
{
    /// <summary>The highest count of one reward entry.</summary>
    public const int MaximumCount = 100;

    /// <summary>
    /// Parses a comma separated list of <c>group:number:level:count</c> entries.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="items">The parsed items.</param>
    /// <param name="error">The problem, if it can't be parsed.</param>
    /// <returns><c>true</c>, if the text is valid.</returns>
    public static bool TryParseList(string? text, out IReadOnlyList<CastleSiegeRewardItem> items, out string? error)
    {
        var result = new List<CastleSiegeRewardItem>();
        items = result;
        error = null;
        foreach (var entry in (text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 4
                || !byte.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var group)
                || !short.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                || !byte.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var level)
                || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            {
                error = $"'{entry}' isn't group:number:level:count.";
                return false;
            }

            if (level > 15 || count is < 1 or > MaximumCount)
            {
                error = $"'{entry}': the level goes up to 15 and the count from 1 to {MaximumCount}.";
                return false;
            }

            result.Add(new CastleSiegeRewardItem(group, number, level, count));
        }

        return true;
    }
}
