// <copyright file="CastleSiegeScoreboardTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic.CastleSiege.PointMode;
using NUnit.Framework;

/// <summary>
/// Tests for the <see cref="CastleSiegeScoreboard"/> of the Castle Siege point mode and its reward items.
/// </summary>
[TestFixture]
public class CastleSiegeScoreboardTest
{
    private static readonly DateTime Start = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Defender = Guid.NewGuid();
    private static readonly Guid AttackerA = Guid.NewGuid();
    private static readonly Guid AttackerB = Guid.NewGuid();

    /// <summary>
    /// Tests that the guild with the most seals wins, even though another guild sealed last.
    /// </summary>
    [Test]
    public void DetermineResult_MostPoints_WinsOverLastSeal()
    {
        var scoreboard = new CastleSiegeScoreboard(Guid.NewGuid(), Start);
        scoreboard.AddSeal(AttackerA, "A", 10, Start.AddMinutes(10));
        scoreboard.AddSeal(AttackerA, "A", 10, Start.AddMinutes(20));
        scoreboard.AddSeal(AttackerB, "B", 10, Start.AddMinutes(59));

        var result = scoreboard.DetermineResult();

        Assert.That(result.WinnerGuildId, Is.EqualTo(AttackerA));
        Assert.That(result.LastHolderGuildId, Is.EqualTo(AttackerB));
        Assert.That(result.PlacementOf(AttackerA), Is.EqualTo(CastleSiegeRewardPlacement.Winner));
        Assert.That(result.PlacementOf(AttackerB), Is.EqualTo(CastleSiegeRewardPlacement.Consolation));
    }

    /// <summary>
    /// Tests that a tie goes to the guild holding the castle at the end.
    /// </summary>
    [Test]
    public void DetermineResult_Tie_GoesToHolder()
    {
        var scoreboard = new CastleSiegeScoreboard(Guid.NewGuid(), Start);
        scoreboard.AddSeal(AttackerA, "A", 10, Start.AddMinutes(10));
        scoreboard.AddSeal(AttackerB, "B", 10, Start.AddMinutes(30));

        var result = scoreboard.DetermineResult();

        Assert.That(result.WinnerGuildId, Is.EqualTo(AttackerB));
        Assert.That(result.PlacementOf(AttackerA), Is.EqualTo(CastleSiegeRewardPlacement.Participant));
    }

    /// <summary>
    /// Tests that the holder scores one tick per full interval, and that a seal restarts the clock.
    /// </summary>
    [Test]
    public void PayDefenseTicks_FullIntervals_RestartWithSeal()
    {
        var scoreboard = new CastleSiegeScoreboard(Guid.NewGuid(), Start);
        scoreboard.SetInitialHolder(Defender, "D", Start);
        var interval = TimeSpan.FromMinutes(5);

        Assert.That(scoreboard.PayDefenseTicks(interval, 3, Start.AddMinutes(4)), Is.EqualTo(0));
        Assert.That(scoreboard.PayDefenseTicks(interval, 3, Start.AddMinutes(11)), Is.EqualTo(2));

        // A seal at 12:14 takes the castle; the defender's 1 minute towards the next tick is lost.
        scoreboard.AddSeal(AttackerA, "A", 10, Start.AddMinutes(14));
        Assert.That(scoreboard.PayDefenseTicks(interval, 3, Start.AddMinutes(18)), Is.EqualTo(0));
        Assert.That(scoreboard.PayDefenseTicks(interval, 3, Start.AddMinutes(19)), Is.EqualTo(1));

        var standings = scoreboard.GetStandings();
        Assert.That(standings.Single(s => s.GuildId == Defender).Points, Is.EqualTo(6));
        Assert.That(standings.Single(s => s.GuildId == AttackerA).Points, Is.EqualTo(13));
    }

    /// <summary>
    /// Tests that every guild keeps its own crown progress, and that a seal uses up only the sealer's.
    /// </summary>
    [Test]
    public void SwitchCrownBank_KeepsProgressPerGuild()
    {
        var scoreboard = new CastleSiegeScoreboard(Guid.NewGuid(), Start);

        Assert.That(scoreboard.SwitchCrownBank(AttackerA, TimeSpan.Zero), Is.EqualTo(TimeSpan.Zero));
        Assert.That(scoreboard.SwitchCrownBank(AttackerB, TimeSpan.FromSeconds(29)), Is.EqualTo(TimeSpan.Zero));
        Assert.That(scoreboard.SwitchCrownBank(AttackerA, TimeSpan.FromSeconds(12)), Is.EqualTo(TimeSpan.FromSeconds(29)));

        scoreboard.AddSeal(AttackerA, "A", 10, Start);
        Assert.That(scoreboard.SwitchCrownBank(AttackerB, TimeSpan.Zero), Is.EqualTo(TimeSpan.FromSeconds(12)));
        Assert.That(scoreboard.SwitchCrownBank(AttackerA, TimeSpan.FromSeconds(12)), Is.EqualTo(TimeSpan.Zero));
    }

    /// <summary>
    /// Tests that nobody wins and nobody is rewarded when nobody scored.
    /// </summary>
    [Test]
    public void DetermineResult_NoPoints_NoWinner()
    {
        var scoreboard = new CastleSiegeScoreboard(Guid.NewGuid(), Start);
        scoreboard.SetInitialHolder(Defender, "D", Start);
        scoreboard.Register(AttackerA, "A");

        var result = scoreboard.DetermineResult();

        Assert.That(result.WinnerGuildId, Is.Null);
        Assert.That(result.PlacementOf(Defender), Is.Null);
    }

    /// <summary>
    /// Tests the reward item list format.
    /// </summary>
    [Test]
    public void TryParseList_ValidAndInvalid()
    {
        Assert.That(CastleSiegeRewardItem.TryParseList("14:13:0:10, 12:15:0:1", out var items, out _), Is.True);
        Assert.That(items, Is.EqualTo(new[] { new CastleSiegeRewardItem(14, 13, 0, 10), new CastleSiegeRewardItem(12, 15, 0, 1) }));
        Assert.That(CastleSiegeRewardItem.TryParseList(string.Empty, out items, out _), Is.True);
        Assert.That(items, Is.Empty);
        Assert.That(CastleSiegeRewardItem.TryParseList("14:13:0", out _, out _), Is.False);
        Assert.That(CastleSiegeRewardItem.TryParseList("14:13:16:1", out _, out _), Is.False);
        Assert.That(CastleSiegeRewardItem.TryParseList("14:13:0:0", out _, out _), Is.False);
        Assert.That(new CastleSiegePointModeConfiguration().Validate(), Is.Empty);
    }
}
