// <copyright file="WebMiniGamesConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.WebMiniGames;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// The currency in which the web mini games take bets and pay out.
/// </summary>
public enum WebMiniGameCurrency
{
    /// <summary>The WCoin (C) balance of the account.</summary>
    WCoinC,

    /// <summary>The WCoin (P) balance of the account.</summary>
    WCoinP,

    /// <summary>The goblin points of the account.</summary>
    GoblinPoints,
}

/// <summary>
/// The configuration of the <see cref="WebMiniGamesPlugIn"/>. Payouts include the bet: a multiplier of 2
/// gives the bet back plus the same amount again. The defaults keep about 8 % of all bets, like the
/// games of a fair.
/// </summary>
public class WebMiniGamesConfiguration
{
    /// <summary>Gets or sets the currency of bets and payouts.</summary>
    [Display(Name = "Currency", Description = "The balance from which bets are taken and to which wins are paid.")]
    public WebMiniGameCurrency Currency { get; set; } = WebMiniGameCurrency.WCoinC;

    /// <summary>Gets or sets the most an account may bet per day (UTC), all games together. 0 means no limit.</summary>
    [Display(Name = "Daily bet limit", Description = "The most an account may bet per day (UTC), all games together. 0 means no limit.")]
    public int DailyBetLimit { get; set; } = 5000;

    /// <summary>Gets or sets the color game.</summary>
    [Display(Name = "Color game")]
    public ColorGameConfiguration ColorGame { get; set; } = new();

    /// <summary>Gets or sets the prize wheel.</summary>
    [Display(Name = "Prize wheel")]
    public PrizeWheelConfiguration PrizeWheel { get; set; } = new();

    /// <summary>Gets or sets the drop ball game.</summary>
    [Display(Name = "Drop ball")]
    public DropBallConfiguration DropBall { get; set; } = new();

    /// <summary>Gets or sets the scratch card.</summary>
    [Display(Name = "Scratch card")]
    public ScratchCardConfiguration ScratchCard { get; set; } = new();

    /// <summary>Gets or sets odd or even.</summary>
    [Display(Name = "Odd or even")]
    public OddOrEvenConfiguration OddOrEven { get; set; } = new();

    /// <summary>Gets or sets kara y krus.</summary>
    [Display(Name = "Kara y krus")]
    public CoinTossConfiguration CoinToss { get; set; } = new();

    /// <summary>Gets or sets lucky 9.</summary>
    [Display(Name = "Lucky 9")]
    public LuckyNineConfiguration LuckyNine { get; set; } = new();

    /// <summary>Gets or sets hi-lo.</summary>
    [Display(Name = "Hi-lo")]
    public HiLoConfiguration HiLo { get; set; } = new();

    /// <summary>Gets or sets sabong.</summary>
    [Display(Name = "Sabong")]
    public SabongConfiguration Sabong { get; set; } = new();
}

/// <summary>
/// The bet limits which every game with a free bet has.
/// </summary>
public abstract class BetGameConfiguration
{
    /// <summary>Gets or sets a value indicating whether the game is open.</summary>
    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the minimum bet.</summary>
    [Display(Name = "Minimum bet")]
    public int MinimumBet { get; set; } = 10;

    /// <summary>Gets or sets the maximum bet.</summary>
    [Display(Name = "Maximum bet")]
    public int MaximumBet { get; set; } = 1000;
}

/// <summary>
/// Odd or even: one die rolls; the player bets on odd, even, or an exact number.
/// </summary>
public class OddOrEvenConfiguration : BetGameConfiguration
{
    /// <summary>Gets or sets the payout multiplier of a right odd or even bet.</summary>
    [Display(Name = "Odd or even pays", Description = "Multiplier of the bet, the bet included. Right half the time.")]
    public double OddEvenMultiplier { get; set; } = 1.84;

    /// <summary>Gets or sets the payout multiplier of a right exact number.</summary>
    [Display(Name = "Exact number pays", Description = "Multiplier of the bet, the bet included. Right one time in six.")]
    public double ExactNumberMultiplier { get; set; } = 5.5;
}

/// <summary>
/// Kara y krus: a coin toss, heads (kara) or tails (krus).
/// </summary>
public class CoinTossConfiguration : BetGameConfiguration
{
    /// <summary>Gets or sets the payout multiplier of a right call.</summary>
    [Display(Name = "Right call pays", Description = "Multiplier of the bet, the bet included. Right half the time.")]
    public double Multiplier { get; set; } = 1.84;
}

/// <summary>
/// Lucky 9: the player and the banker get two cards each and draw a third on 5 or less; the total's last
/// digit closest to 9 wins. A tie gives the bet back.
/// </summary>
public class LuckyNineConfiguration : BetGameConfiguration
{
    /// <summary>Gets or sets the payout multiplier of a win.</summary>
    [Display(Name = "Win pays", Description = "Multiplier of the bet, the bet included. A tie gives the bet back.")]
    public double WinMultiplier { get; set; } = 1.7;

    /// <summary>Gets or sets the payout multiplier of a win with a natural 9 (the first two cards make 9).</summary>
    [Display(Name = "Natural 9 win pays", Description = "Multiplier of the bet when the player's first two cards make 9 and win.")]
    public double NaturalNineMultiplier { get; set; } = 2.3;
}

/// <summary>
/// Hi-lo: a card is shown and the player guesses if the next one is higher or lower; every right guess
/// raises the multiplier, a wrong one (or an equal card) loses the bet. The player can cash out any time.
/// </summary>
public class HiLoConfiguration : BetGameConfiguration
{
    /// <summary>Gets or sets the share of the fair odds which a right guess pays, e.g. 0.95.</summary>
    [Display(Name = "Return per guess", Description = "Share of the fair odds a right guess multiplies by, from 0.5 to 1. 0.95 keeps 5 % per guess.")]
    public double ReturnPerGuess { get; set; } = 0.95;

    /// <summary>Gets or sets the highest multiplier; the round cashes out on its own when it's reached.</summary>
    [Display(Name = "Maximum multiplier")]
    public double MaximumMultiplier { get; set; } = 500;
}

/// <summary>
/// Sabong: the player bets on meron or wala; the winning side pays, a draw gives the bet back.
/// </summary>
public class SabongConfiguration : BetGameConfiguration
{
    /// <summary>Gets or sets the payout multiplier of the winning side.</summary>
    [Display(Name = "Winning side pays", Description = "Multiplier of the bet, the bet included.")]
    public double WinMultiplier { get; set; } = 1.85;

    /// <summary>Gets or sets the chance of a draw in percent, which gives every bet back.</summary>
    [Display(Name = "Draw chance (%)")]
    public double DrawChancePercent { get; set; } = 2;
}

/// <summary>
/// The color game: the player bets on one of six colors, three dice with the six colors roll, and every
/// die showing the color wins.
/// </summary>
public class ColorGameConfiguration
{
    /// <summary>Gets or sets a value indicating whether the game is open.</summary>
    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the minimum bet.</summary>
    [Display(Name = "Minimum bet")]
    public int MinimumBet { get; set; } = 10;

    /// <summary>Gets or sets the maximum bet.</summary>
    [Display(Name = "Maximum bet")]
    public int MaximumBet { get; set; } = 1000;

    /// <summary>Gets or sets the payout multiplier when one die shows the color.</summary>
    [Display(Name = "Payout for 1 match", Description = "Multiplier of the bet, the bet included.")]
    public double OneMatchMultiplier { get; set; } = 2;

    /// <summary>Gets or sets the payout multiplier when two dice show the color.</summary>
    [Display(Name = "Payout for 2 matches", Description = "Multiplier of the bet, the bet included.")]
    public double TwoMatchesMultiplier { get; set; } = 3;

    /// <summary>Gets or sets the payout multiplier when all three dice show the color.</summary>
    [Display(Name = "Payout for 3 matches", Description = "Multiplier of the bet, the bet included.")]
    public double ThreeMatchesMultiplier { get; set; } = 4;
}

/// <summary>
/// The prize wheel: the player pays a fixed price and the wheel stops on one of its slices.
/// </summary>
public class PrizeWheelConfiguration
{
    /// <summary>Gets or sets a value indicating whether the game is open.</summary>
    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the price of one spin.</summary>
    [Display(Name = "Price per spin")]
    public int Price { get; set; } = 100;

    /// <summary>Gets or sets the slices of the wheel, clockwise.</summary>
    [Display(Name = "Slices", Description = "Clockwise. The weight is how likely the wheel stops on the slice, relative to the others.")]
    public ICollection<PrizeWheelSlice> Slices { get; set; } = new List<PrizeWheelSlice>
    {
        new() { Label = "1000", Payout = 1000, Weight = 1 },
        new() { Label = "Try again", Payout = 0, Weight = 11 },
        new() { Label = "100", Payout = 100, Weight = 10 },
        new() { Label = "50", Payout = 50, Weight = 13 },
        new() { Label = "500", Payout = 500, Weight = 4 },
        new() { Label = "Try again", Payout = 0, Weight = 11 },
        new() { Label = "150", Payout = 150, Weight = 10 },
        new() { Label = "50", Payout = 50, Weight = 12 },
        new() { Label = "200", Payout = 200, Weight = 7 },
        new() { Label = "Try again", Payout = 0, Weight = 11 },
        new() { Label = "100", Payout = 100, Weight = 10 },
    };
}

/// <summary>
/// A slice of the <see cref="PrizeWheelConfiguration"/>.
/// </summary>
public class PrizeWheelSlice
{
    /// <summary>Gets or sets the text on the slice.</summary>
    [Display(Name = "Label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the coins the slice pays.</summary>
    [Display(Name = "Payout")]
    public int Payout { get; set; }

    /// <summary>Gets or sets how likely the wheel stops on the slice, relative to the others.</summary>
    [Display(Name = "Weight")]
    public int Weight { get; set; } = 1;

    /// <inheritdoc />
    public override string ToString() => $"{this.Label} ({this.Payout}, weight {this.Weight})";
}

/// <summary>
/// The drop ball game: a ball falls through rows of pegs, bouncing left or right at each, into one of the
/// slots at the bottom, which multiply the bet.
/// </summary>
public class DropBallConfiguration
{
    /// <summary>Gets or sets a value indicating whether the game is open.</summary>
    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the minimum bet.</summary>
    [Display(Name = "Minimum bet")]
    public int MinimumBet { get; set; } = 10;

    /// <summary>Gets or sets the maximum bet.</summary>
    [Display(Name = "Maximum bet")]
    public int MaximumBet { get; set; } = 1000;

    /// <summary>Gets or sets the multipliers of the slots, from left to right; one more than there are rows of pegs.</summary>
    [Display(Name = "Slot multipliers", Description = "Comma separated, left to right, the bet included. Their count minus one is the number of peg rows (2 to 16).")]
    public string SlotMultipliers { get; set; } = "8, 3, 1.3, 0.7, 0.3, 0.7, 1.3, 3, 8";
}

/// <summary>
/// The scratch card: the player buys a card with three hidden symbols; three matching symbols win that
/// symbol's prize.
/// </summary>
public class ScratchCardConfiguration
{
    /// <summary>Gets or sets a value indicating whether the game is open.</summary>
    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the price of one card.</summary>
    [Display(Name = "Price per card")]
    public int Price { get; set; } = 50;

    /// <summary>Gets or sets how likely a card wins nothing, relative to the prize weights.</summary>
    [Display(Name = "Losing weight", Description = "How likely a card wins nothing, relative to the prize weights.")]
    public int LosingWeight { get; set; } = 348;

    /// <summary>Gets or sets the prizes.</summary>
    [Display(Name = "Prizes")]
    public ICollection<ScratchCardPrize> Prizes { get; set; } = new List<ScratchCardPrize>
    {
        new() { Symbol = "Crown", Payout = 1000, Weight = 2 },
        new() { Symbol = "Wings", Payout = 250, Weight = 20 },
        new() { Symbol = "Jewel", Payout = 100, Weight = 150 },
        new() { Symbol = "Zen", Payout = 50, Weight = 480 },
    };
}

/// <summary>
/// A prize of the <see cref="ScratchCardConfiguration"/>.
/// </summary>
public class ScratchCardPrize
{
    /// <summary>Gets or sets the symbol; three of it on a card win the prize.</summary>
    [Display(Name = "Symbol")]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the coins the prize pays.</summary>
    [Display(Name = "Payout")]
    public int Payout { get; set; }

    /// <summary>Gets or sets how likely a card wins this prize, relative to the others.</summary>
    [Display(Name = "Weight")]
    public int Weight { get; set; } = 1;

    /// <inheritdoc />
    public override string ToString() => $"{this.Symbol} ({this.Payout}, weight {this.Weight})";
}
