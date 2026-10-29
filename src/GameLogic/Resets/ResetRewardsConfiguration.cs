// <copyright file="ResetRewardsConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

using System.ComponentModel.DataAnnotations;
using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// The configuration of the <see cref="ResetRewardsPlugIn"/>.
/// </summary>
public class ResetRewardsConfiguration
{
    /// <summary>Gets or sets the coin type of the rewards.</summary>
    [Display(Name = "Reward coin")]
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>Gets or sets the coins for every reset.</summary>
    [Display(Name = "Coins per reset")]
    public int CoinsPerReset { get; set; } = 15;

    /// <summary>Gets or sets the extra coins for every 10th reset.</summary>
    [Display(Name = "Extra coins every 10th reset")]
    public int CoinsPerTenthReset { get; set; } = 100;

    /// <summary>Gets or sets after how many resets a grand reset is reached.</summary>
    [Display(Name = "Resets per grand reset", Description = "Every this many resets count as one grand reset (GR).")]
    [Range(1, 10000)]
    public int ResetsPerGrandReset { get; set; } = 100;

    /// <summary>Gets or sets the coins for a grand reset.</summary>
    [Display(Name = "Coins per grand reset")]
    public int CoinsPerGrandReset { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the items of a grand reset: comma separated <c>group:number:level:count</c>, where
    /// the number may be a range (e.g. <c>12:112-117:0:10</c> for ten random level 3 seed spheres).
    /// </summary>
    [Display(Name = "Grand reset items", Description = "Comma separated group:number:level:count; the number may be a range like 112-117 for a random one each time.")]
    public string GrandResetItems { get; set; } =
        "12:30:2:1, 12:31:2:1, 12:140:2:1, 12:137:2:1, 12:138:2:1, 12:136:2:3, 12:141:2:2, 12:112-117:0:10";

    /// <summary>
    /// Gets the items of a grand reset.
    /// </summary>
    /// <returns>The items as group, first and last number, level and count.</returns>
    /// <summary>
    /// Gets or sets the grand reset milestones, see <see cref="GrandResetMilestone.Parse"/>.
    /// </summary>
    [Display(Name = "Grand reset milestones", Description = "At these grand resets the player picks one ancient part on the website, e.g. \"5 = +9 Warrior, Hyperion; 50 = +13 excellent *\". \"+13\" is the item level, \"excellent\" also allows an excellent part with 2 options, \"*\" means every set of the other milestones.")]
    public string Milestones { get; set; } =
        "5 = +9 Warrior, Anonymous, Hyperion, Mist, Apollo, Barnake, Ceto, Drake; "
        + "10 = +9 Berserker, Eplete, Garuda, Cloud, Evis, Sylion, Gaia, Fase; "
        + "25 = +9 Kantata, Rave, Heras, Minet, Odin, Elvian, Argo, Karis; "
        + "50 = +13 excellent *";

    /// <summary>
    /// Parses <see cref="Milestones"/>.
    /// </summary>
    /// <returns>The milestones.</returns>
    public IReadOnlyList<GrandResetMilestone> ParseMilestones() => GrandResetMilestone.Parse(this.Milestones);

    public IReadOnlyList<(byte Group, short FirstNumber, short LastNumber, byte Level, int Count)> ParseGrandResetItems()
        => PlayerRewards.ParseItems(this.GrandResetItems);
}
