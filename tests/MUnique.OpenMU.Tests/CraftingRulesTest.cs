// <copyright file="CraftingRulesTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using NUnit.Framework;

/// <summary>
/// Tests for the server's crafting rules: failed +10 to +15 upgrades and the Jewel of Life chances.
/// </summary>
[TestFixture]
public class CraftingRulesTest
{
    /// <summary>
    /// Tests that a failed upgrade keeps or lowers the level by up to 3, as the roll says.
    /// </summary>
    [Test]
    public void UpgradeFailLevel_FollowsTheRoll()
    {
        Assert.That(BaseItemCraftingHandler.UpgradeFailLevel(14, 0), Is.EqualTo(14));
        Assert.That(BaseItemCraftingHandler.UpgradeFailLevel(14, 39), Is.EqualTo(14));
        Assert.That(BaseItemCraftingHandler.UpgradeFailLevel(14, 40), Is.EqualTo(13));
        Assert.That(BaseItemCraftingHandler.UpgradeFailLevel(14, 75), Is.EqualTo(12));
        Assert.That(BaseItemCraftingHandler.UpgradeFailLevel(14, 99), Is.EqualTo(11));
    }

    /// <summary>
    /// Tests that a failed upgrade never takes the item below +9.
    /// </summary>
    [Test]
    public void UpgradeFailLevel_NeverBelowNine()
    {
        Assert.That(BaseItemCraftingHandler.UpgradeFailLevel(10, 99), Is.EqualTo(9));
        Assert.That(BaseItemCraftingHandler.UpgradeFailLevel(9, 99), Is.EqualTo(9));
    }

    /// <summary>
    /// Tests the Jewel of Life chance per option level, and that levels past the list use its last value.
    /// </summary>
    [Test]
    public void LifeJewel_ChancePerLevel()
    {
        var configuration = new LifeJewelConfiguration();

        Assert.That(configuration.ChanceOf(1), Is.EqualTo(0.6).Within(1e-9));
        Assert.That(configuration.ChanceOf(4), Is.EqualTo(0.3).Within(1e-9));
        Assert.That(configuration.ChanceOf(7), Is.EqualTo(0.1).Within(1e-9));
        Assert.That(configuration.ChanceOf(9), Is.EqualTo(0.1).Within(1e-9));
    }
}
