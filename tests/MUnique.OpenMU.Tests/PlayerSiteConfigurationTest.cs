// <copyright file="PlayerSiteConfigurationTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic.PlayerSite;
using NUnit.Framework;

/// <summary>
/// Tests for the check-in streak and the market fee of the <see cref="PlayerSiteConfiguration"/>.
/// </summary>
[TestFixture]
public class PlayerSiteConfigurationTest
{
    private static readonly DateOnly Today = new(2026, 10, 28);

    /// <summary>
    /// Tests that the first check-in and one after a missed day start the streak over.
    /// </summary>
    [Test]
    public void StreakDayOf_FirstOrMissedDay_StartsOver()
    {
        var checkIn = new CheckInConfiguration();

        Assert.That(checkIn.StreakDayOf(0, null, Today), Is.EqualTo(1));
        Assert.That(checkIn.StreakDayOf(3, Today.AddDays(-2), Today), Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that consecutive days climb the streak and wrap after its last day.
    /// </summary>
    [Test]
    public void StreakDayOf_ConsecutiveDays_ClimbsAndWraps()
    {
        var checkIn = new CheckInConfiguration { Rewards = "10, 20, 30" };

        Assert.That(checkIn.StreakDayOf(1, Today.AddDays(-1), Today), Is.EqualTo(2));
        Assert.That(checkIn.StreakDayOf(3, Today.AddDays(-1), Today), Is.EqualTo(1));
        Assert.That(checkIn.RewardOf(2), Is.EqualTo(20));
        Assert.That(checkIn.RewardOf(4), Is.EqualTo(10));
    }

    /// <summary>
    /// Tests that invalid reward lists are rejected.
    /// </summary>
    [Test]
    public void ParseRewards_Invalid_IsEmpty()
    {
        Assert.That(CheckInConfiguration.ParseRewards("10, x"), Is.Empty);
        Assert.That(CheckInConfiguration.ParseRewards("-5"), Is.Empty);
    }

    /// <summary>
    /// Tests that the market keeps its fee, rounded up, and never pays a negative share.
    /// </summary>
    [Test]
    public void SellerShareOf_KeepsTheFee()
    {
        var market = new MarketConfiguration { FeePercent = 5 };

        Assert.That(market.SellerShareOf(1000), Is.EqualTo(950));
        Assert.That(market.SellerShareOf(10), Is.EqualTo(9));
        Assert.That(new MarketConfiguration { FeePercent = 150 }.SellerShareOf(100), Is.EqualTo(0));
    }
}
