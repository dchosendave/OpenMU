// <copyright file="ResetRewardsTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic.Resets;
using NUnit.Framework;

/// <summary>
/// Tests for the <see cref="ResetRewardsPlugIn"/> amounts and its grand reset box.
/// </summary>
[TestFixture]
public class ResetRewardsTest
{
    /// <summary>
    /// Tests the coins of a normal reset, a 10th reset and a grand reset.
    /// </summary>
    [Test]
    public void CoinsFor_ResetTenthAndGrandReset()
    {
        var configuration = new ResetRewardsConfiguration();

        Assert.That(ResetRewardsPlugIn.CoinsFor(configuration, 7), Is.EqualTo(15));
        Assert.That(ResetRewardsPlugIn.CoinsFor(configuration, 30), Is.EqualTo(115));
        Assert.That(ResetRewardsPlugIn.CoinsFor(configuration, 100), Is.EqualTo(2115));
        Assert.That(ResetRewardsPlugIn.IsGrandReset(configuration, 200), Is.True);
        Assert.That(ResetRewardsPlugIn.IsGrandReset(configuration, 150), Is.False);
    }

    /// <summary>
    /// Tests the default grand reset box: 10 packs of 30 jewels and 10 random level 3 seed spheres.
    /// </summary>
    [Test]
    public void GrandResetItems_DefaultBox()
    {
        var items = new ResetRewardsConfiguration().ParseGrandResetItems();

        var packs = items.Where(i => i.Level == 2).Sum(i => i.Count);
        var spheres = items.Single(i => i.FirstNumber == 112);
        Assert.That(packs, Is.EqualTo(10));
        Assert.That(spheres.LastNumber, Is.EqualTo(117));
        Assert.That(spheres.Count, Is.EqualTo(10));
    }

    /// <summary>
    /// Tests the default milestones: 5, 10 and 25 grand resets with 8 lower ancient sets each at +9,
    /// and 50 with every one of them at +13, also as excellent part.
    /// </summary>
    [Test]
    public void Milestones_DefaultTiers()
    {
        var milestones = new ResetRewardsConfiguration().ParseMilestones();

        Assert.That(milestones.Select(m => m.GrandResets), Is.EqualTo(new[] { 5, 10, 25, 50 }));
        Assert.That(milestones.Take(3).All(m => m.Sets.Count == 8 && m.Level == 9 && !m.AllowsExcellent), Is.True);
        Assert.That(milestones[0].Includes("warrior"), Is.True);
        Assert.That(milestones[0].Includes("Hyon"), Is.False);
        Assert.That(milestones[3].Sets.Count, Is.EqualTo(24));
        Assert.That(milestones[3].Level, Is.EqualTo(13));
        Assert.That(milestones[3].AllowsExcellent, Is.True);
        Assert.That(milestones.Any(m => m.Includes("Vicious")), Is.False);
    }

    /// <summary>
    /// Tests that broken entries are skipped and the level is capped at +15.
    /// </summary>
    [Test]
    public void Milestones_SkipsBrokenEntries()
    {
        var milestones = GrandResetMilestone.Parse("x = +9 Warrior; 3 = +20 Hyperion;; 0 = Mist");

        Assert.That(milestones, Has.Count.EqualTo(1));
        Assert.That(milestones[0].Level, Is.EqualTo(15));
        Assert.That(milestones[0].Sets, Is.EqualTo(new[] { "Hyperion" }));
    }
}
