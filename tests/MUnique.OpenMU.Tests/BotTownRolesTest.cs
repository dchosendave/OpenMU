// <copyright file="BotTownRolesTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic.Bots;
using NUnit.Framework;

/// <summary>
/// Tests for <see cref="BotTownRoles"/>: town bots, AFK bots and companions for hire.
/// </summary>
[TestFixture]
public class BotTownRolesTest
{
    private static readonly string[] Names = Enumerable.Range(0, 2000).Select(i => $"Bot{i}").ToArray();

    /// <summary>
    /// Tests that the companions for hire are about the configured share, and come before the town roles.
    /// </summary>
    [Test]
    public void CompanionsForHire_AboutTheConfiguredShare()
    {
        var configuration = new BotConfiguration { CompanionsForHirePercent = 5, TownIdlerPercent = 0 };

        var forHire = Names.Count(n => BotTownRoles.GetRole(n, configuration) == BotTownRole.ForHire);

        Assert.That(forHire, Is.InRange(60, 140));
        Assert.That(BotTownRoles.GetRole("Anyone", new BotConfiguration { CompanionsForHirePercent = 100 }), Is.EqualTo(BotTownRole.ForHire));
        Assert.That(BotTownRoles.GetRole("Anyone", new BotConfiguration { CompanionsForHirePercent = 0, TownIdlerPercent = 0 }), Is.EqualTo(BotTownRole.None));
    }

    /// <summary>
    /// Tests that every companion gets a place in the line from 0 to 9, always the same one.
    /// </summary>
    [Test]
    public void LinePlace_FromZeroToNine_Stable()
    {
        Assert.That(Names.Select(BotTownRoles.LinePlace).All(p => p is >= 0 and <= 9), Is.True);
        Assert.That(BotTownRoles.LinePlace("Valhalla"), Is.EqualTo(BotTownRoles.LinePlace("valhalla")));
    }
}
