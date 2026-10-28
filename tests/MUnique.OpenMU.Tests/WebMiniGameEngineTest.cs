// <copyright file="WebMiniGameEngineTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Text.Json;
using MUnique.OpenMU.GameLogic.WebMiniGames;
using NUnit.Framework;

/// <summary>
/// Tests for the <see cref="WebMiniGameEngine"/>.
/// </summary>
[TestFixture]
public class WebMiniGameEngineTest
{
    /// <summary>
    /// Tests that the default configuration is valid.
    /// </summary>
    [Test]
    public void Validate_DefaultConfiguration_HasNoProblems()
    {
        Assert.That(WebMiniGameEngine.Validate(new WebMiniGamesConfiguration()), Is.Empty);
    }

    /// <summary>
    /// Tests that every game keeps about 8 % of the bets with the default configuration, like a fair.
    /// </summary>
    [Test]
    public void ReturnToPlayer_DefaultConfiguration_IsAboutNinetyTwoPercent()
    {
        var returns = WebMiniGameEngine.ReturnToPlayer(new WebMiniGamesConfiguration());

        Assert.That(returns.Keys, Is.EquivalentTo(WebMiniGameEngine.Games.Append("oddeven-exact")));
        Assert.That(new[] { returns["color"], returns["wheel"], returns["dropball"], returns["scratch"] }, Has.All.InRange(0.90, 0.94));
        Assert.That(returns["color"], Is.EqualTo(199.0 / 216).Within(1e-9));
    }

    /// <summary>
    /// Tests that the color game pays by the number of dice which show the color.
    /// </summary>
    /// <param name="die1">The first die.</param>
    /// <param name="die2">The second die.</param>
    /// <param name="die3">The third die.</param>
    /// <param name="expectedPayout">The expected payout for a bet of 100 on color 2.</param>
    [TestCase(0, 1, 3, 0)]
    [TestCase(2, 1, 3, 200)]
    [TestCase(2, 2, 3, 300)]
    [TestCase(2, 2, 2, 400)]
    public void PlayColorGame_PaysByMatches(int die1, int die2, int die3, int expectedPayout)
    {
        var rolls = new Queue<int>([die1, die2, die3]);
        var engine = new WebMiniGameEngine(_ => rolls.Dequeue());

        var outcome = engine.PlayColorGame(new ColorGameConfiguration(), 100, 2);

        Assert.That(outcome.Bet, Is.EqualTo(100));
        Assert.That(outcome.Payout, Is.EqualTo(expectedPayout));
    }

    /// <summary>
    /// Tests that the wheel stops on the slice which the weighted roll falls into.
    /// </summary>
    [Test]
    public void SpinPrizeWheel_PicksTheWeightedSlice()
    {
        var configuration = new PrizeWheelConfiguration
        {
            Price = 10,
            Slices = [new() { Payout = 0, Weight = 3 }, new() { Payout = 50, Weight = 1 }],
        };

        Assert.That(new WebMiniGameEngine(_ => 2).SpinPrizeWheel(configuration).Payout, Is.EqualTo(0));
        Assert.That(new WebMiniGameEngine(_ => 3).SpinPrizeWheel(configuration).Payout, Is.EqualTo(50));
    }

    /// <summary>
    /// Tests that the ball lands in the slot given by the number of right bounces.
    /// </summary>
    [Test]
    public void DropBall_SlotIsTheNumberOfRightBounces()
    {
        var bounces = new Queue<int>([1, 1, 0, 1, 0, 0, 0, 1]);
        var engine = new WebMiniGameEngine(_ => bounces.Dequeue());

        var outcome = engine.DropBall(new DropBallConfiguration(), 100);

        var result = JsonSerializer.SerializeToElement(outcome.Result);
        Assert.That(result.GetProperty("slot").GetInt32(), Is.EqualTo(4));
        Assert.That(outcome.Payout, Is.EqualTo(30));
    }

    /// <summary>
    /// Tests that a losing scratch card never shows three matching symbols, whatever the random numbers are.
    /// </summary>
    [Test]
    public void ScratchCard_Losing_NeverShowsThreeOfAKind()
    {
        var configuration = new ScratchCardConfiguration();
        var random = new Random(1);
        var engine = new WebMiniGameEngine(max => max == 1000 ? 999 : random.Next(max));
        for (var i = 0; i < 1000; i++)
        {
            var outcome = engine.ScratchCard(configuration);
            var symbols = JsonSerializer.SerializeToElement(outcome.Result).GetProperty("symbols").EnumerateArray().Select(s => s.GetString()).ToList();

            Assert.That(outcome.Payout, Is.EqualTo(0));
            Assert.That(symbols.Distinct().Count(), Is.GreaterThan(1));
        }
    }

    /// <summary>
    /// Tests that a winning scratch card shows the prize's symbol three times.
    /// </summary>
    [Test]
    public void ScratchCard_Winning_ShowsThreeOfThePrizeSymbol()
    {
        var engine = new WebMiniGameEngine(_ => 0);

        var outcome = engine.ScratchCard(new ScratchCardConfiguration());

        var symbols = JsonSerializer.SerializeToElement(outcome.Result).GetProperty("symbols").EnumerateArray().Select(s => s.GetString());
        Assert.That(outcome.Payout, Is.EqualTo(1000));
        Assert.That(symbols, Is.EqualTo(new[] { "Crown", "Crown", "Crown" }));
    }

    /// <summary>
    /// Tests that the default settings of the newer games keep about 5 to 10 % too.
    /// </summary>
    [Test]
    public void ReturnToPlayer_NewerGames_AreBetweenNinetyAndNinetyFivePercent()
    {
        var returns = WebMiniGameEngine.ReturnToPlayer(new WebMiniGamesConfiguration());

        Assert.That(new[] { returns["oddeven"], returns["oddeven-exact"], returns["coin"], returns["lucky9"], returns["hilo"], returns["sabong"] }, Has.All.InRange(0.90, 0.951));
        Assert.That(returns["lucky9"], Is.EqualTo(0.9154).Within(0.001));
    }

    /// <summary>
    /// Tests odd, even and exact number bets.
    /// </summary>
    /// <param name="roll">The rolled number minus one.</param>
    /// <param name="pick">The pick.</param>
    /// <param name="expectedPayout">The expected payout for a bet of 100.</param>
    [TestCase(2, 0, 184)]
    [TestCase(2, 1, 0)]
    [TestCase(3, 1, 184)]
    [TestCase(3, 5, 550)]
    [TestCase(3, 4, 0)]
    public void RollOddOrEven_PaysOddEvenAndExact(int roll, int pick, int expectedPayout)
    {
        var outcome = new WebMiniGameEngine(_ => roll).RollOddOrEven(new OddOrEvenConfiguration(), 100, pick);

        Assert.That(outcome.Payout, Is.EqualTo(expectedPayout));
    }

    /// <summary>
    /// Tests that a lucky 9 natural pays more, and that a tie gives the bet back.
    /// </summary>
    [Test]
    public void PlayLuckyNine_NaturalAndTie()
    {
        // Ranks are drawn as 0-based: 3 = four, 4 = five. Player 4+5 = natural 9, banker 4+4 = 8, no third card.
        var natural = new Queue<int>([3, 0, 4, 0, 3, 0, 3, 0]);
        var win = new WebMiniGameEngine(_ => natural.Dequeue()).PlayLuckyNine(new LuckyNineConfiguration(), 100);
        Assert.That(win.Payout, Is.EqualTo(230));

        // Player 4+4 = 8, banker 3+5 = 8: a tie.
        var tie = new Queue<int>([3, 0, 3, 0, 2, 0, 4, 0]);
        Assert.That(new WebMiniGameEngine(_ => tie.Dequeue()).PlayLuckyNine(new LuckyNineConfiguration(), 100).Payout, Is.EqualTo(100));
    }

    /// <summary>
    /// Tests the hi-lo factors: fair odds times the return per guess, and impossible guesses.
    /// </summary>
    [Test]
    public void HiLoFactor_UsesFairOdds()
    {
        var configuration = new HiLoConfiguration { ReturnPerGuess = 1 };

        Assert.That(WebMiniGameEngine.HiLoFactor(configuration, 7, true), Is.EqualTo(13 / 6.0).Within(1e-9));
        Assert.That(WebMiniGameEngine.HiLoFactor(configuration, 13, true), Is.EqualTo(0));
        Assert.That(WebMiniGameEngine.HiLoFactor(configuration, 1, false), Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that a sabong draw gives the bet back and the right side wins.
    /// </summary>
    [Test]
    public void FightSabong_DrawRefundsAndWinnerPays()
    {
        var draw = new Queue<int>([0]);
        Assert.That(new WebMiniGameEngine(_ => draw.Dequeue()).FightSabong(new SabongConfiguration(), 100, 0).Payout, Is.EqualTo(100));

        var meron = new Queue<int>([9999, 0]);
        Assert.That(new WebMiniGameEngine(_ => meron.Dequeue()).FightSabong(new SabongConfiguration(), 100, 0).Payout, Is.EqualTo(185));
    }

    /// <summary>
    /// Tests that invalid settings are reported.
    /// </summary>
    [Test]
    public void Validate_InvalidSettings_AreReported()
    {
        var configuration = new WebMiniGamesConfiguration
        {
            ColorGame = { MinimumBet = 0 },
            DropBall = { SlotMultipliers = "1, x" },
            PrizeWheel = { Slices = [new() { Weight = 1 }] },
        };

        Assert.That(WebMiniGameEngine.Validate(configuration), Has.Count.EqualTo(3));
    }
}
