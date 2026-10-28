// <copyright file="WebMiniGameEngine.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.WebMiniGames;

using System.Globalization;
using System.Security.Cryptography;

/// <summary>
/// The outcome of one play of a web mini game.
/// </summary>
/// <param name="Bet">The coins the play cost.</param>
/// <param name="Payout">The coins the play pays, the bet included; 0 when it lost.</param>
/// <param name="Result">What happened, for the animation of the website, e.g. the rolled dice.</param>
public sealed record WebMiniGameOutcome(int Bet, int Payout, object Result);

/// <summary>
/// A playing card.
/// </summary>
/// <param name="Rank">The rank, 1 (ace) to 13 (king).</param>
/// <param name="Suit">The suit, 0 to 3 (spades, hearts, diamonds, clubs).</param>
public sealed record Card(int Rank, int Suit);

/// <summary>
/// Plays the web mini games: draws the random outcome and computes the payout. Everything random is
/// decided here, on the server; the website only animates the result.
/// </summary>
public sealed class WebMiniGameEngine
{
    /// <summary>The names of the games, as used in the API.</summary>
    public static readonly IReadOnlyList<string> Games = ["color", "wheel", "dropball", "scratch", "oddeven", "coin", "lucky9", "hilo", "sabong"];

    /// <summary>The number of colors of the color game.</summary>
    public const int ColorCount = 6;

    private readonly Func<int, int> _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebMiniGameEngine"/> class.
    /// </summary>
    /// <param name="random">Returns a uniformly distributed number from 0 to the argument, exclusive. Defaults to a cryptographic generator.</param>
    public WebMiniGameEngine(Func<int, int>? random = null)
    {
        this._random = random ?? RandomNumberGenerator.GetInt32;
    }

    /// <summary>
    /// Parses the slot multipliers of the drop ball game.
    /// </summary>
    /// <param name="text">The comma separated multipliers.</param>
    /// <returns>The multipliers, or <c>null</c> if the text is invalid.</returns>
    public static double[]? ParseSlotMultipliers(string text)
    {
        var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var values = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || values[i] < 0 || values[i] > 10_000)
            {
                return null;
            }
        }

        return values.Length is >= 3 and <= 17 ? values : null;
    }

    /// <summary>
    /// Checks a configuration for values the games can't run with.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The problems; empty if the configuration is valid.</returns>
    public static IReadOnlyList<string> Validate(WebMiniGamesConfiguration configuration)
    {
        var problems = new List<string>();
        if (configuration.DailyBetLimit < 0)
        {
            problems.Add("The daily bet limit can't be negative.");
        }

        void CheckBets(string game, int minimum, int maximum)
        {
            if (minimum < 1 || maximum < minimum)
            {
                problems.Add($"{game}: the minimum bet must be at least 1 and not above the maximum bet.");
            }
        }

        var color = configuration.ColorGame;
        CheckBets("Color game", color.MinimumBet, color.MaximumBet);
        if (new[] { color.OneMatchMultiplier, color.TwoMatchesMultiplier, color.ThreeMatchesMultiplier }.Any(m => m < 0 || m > 1000))
        {
            problems.Add("Color game: the multipliers must be between 0 and 1000.");
        }

        var wheel = configuration.PrizeWheel;
        if (wheel.Price < 1)
        {
            problems.Add("Prize wheel: the price must be at least 1.");
        }

        if (wheel.Slices.Count is < 2 or > 24 || wheel.Slices.Any(s => s.Weight < 0 || s.Payout < 0) || wheel.Slices.Sum(s => (long)s.Weight) <= 0)
        {
            problems.Add("Prize wheel: it needs 2 to 24 slices with payouts and weights of at least 0, and a weight above 0.");
        }

        var dropBall = configuration.DropBall;
        CheckBets("Drop ball", dropBall.MinimumBet, dropBall.MaximumBet);
        if (ParseSlotMultipliers(dropBall.SlotMultipliers) is null)
        {
            problems.Add("Drop ball: the slot multipliers must be 3 to 17 comma separated numbers from 0 to 10000.");
        }

        var scratch = configuration.ScratchCard;
        if (scratch.Price < 1)
        {
            problems.Add("Scratch card: the price must be at least 1.");
        }

        if (scratch.Prizes.Count is < 1 or > 12 || scratch.LosingWeight < 0 || scratch.Prizes.Any(p => p.Weight < 0 || p.Payout < 0 || string.IsNullOrWhiteSpace(p.Symbol))
            || scratch.Prizes.Select(p => p.Symbol.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != scratch.Prizes.Count
            || scratch.Prizes.Count < 2 && scratch.LosingWeight > 0)
        {
            problems.Add("Scratch card: it needs 1 to 12 prizes with different symbols, payouts and weights of at least 0, and at least 2 prizes if cards can lose.");
        }

        CheckBets("Odd or even", configuration.OddOrEven.MinimumBet, configuration.OddOrEven.MaximumBet);
        CheckBets("Kara y krus", configuration.CoinToss.MinimumBet, configuration.CoinToss.MaximumBet);
        CheckBets("Lucky 9", configuration.LuckyNine.MinimumBet, configuration.LuckyNine.MaximumBet);
        CheckBets("Hi-lo", configuration.HiLo.MinimumBet, configuration.HiLo.MaximumBet);
        CheckBets("Sabong", configuration.Sabong.MinimumBet, configuration.Sabong.MaximumBet);
        if (new[]
            {
                configuration.OddOrEven.OddEvenMultiplier, configuration.OddOrEven.ExactNumberMultiplier, configuration.CoinToss.Multiplier,
                configuration.LuckyNine.WinMultiplier, configuration.LuckyNine.NaturalNineMultiplier, configuration.Sabong.WinMultiplier,
            }.Any(m => m < 0 || m > 1000))
        {
            problems.Add("Odd or even, kara y krus, lucky 9, sabong: the multipliers must be between 0 and 1000.");
        }

        if (configuration.HiLo.ReturnPerGuess is < 0.5 or > 1 || configuration.HiLo.MaximumMultiplier is < 1 or > 100_000)
        {
            problems.Add("Hi-lo: the return per guess must be between 0.5 and 1, the maximum multiplier between 1 and 100000.");
        }

        if (configuration.Sabong.DrawChancePercent is < 0 or > 50)
        {
            problems.Add("Sabong: the draw chance must be between 0 and 50 %.");
        }

        return problems;
    }

    /// <summary>
    /// Plays the color game.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="bet">The bet.</param>
    /// <param name="color">The color the player bet on, 0 to 5.</param>
    /// <returns>The outcome, with the colors of the three dice.</returns>
    public WebMiniGameOutcome PlayColorGame(ColorGameConfiguration configuration, int bet, int color)
    {
        var dice = new[] { this._random(ColorCount), this._random(ColorCount), this._random(ColorCount) };
        var matches = dice.Count(d => d == color);
        var multiplier = matches switch
        {
            1 => configuration.OneMatchMultiplier,
            2 => configuration.TwoMatchesMultiplier,
            3 => configuration.ThreeMatchesMultiplier,
            _ => 0,
        };
        return new WebMiniGameOutcome(bet, Pay(bet, multiplier), new { color, dice, matches });
    }

    /// <summary>
    /// Spins the prize wheel.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The outcome, with the index of the slice the wheel stopped on.</returns>
    public WebMiniGameOutcome SpinPrizeWheel(PrizeWheelConfiguration configuration)
    {
        var slices = configuration.Slices.ToList();
        var index = this.PickWeighted(slices.Select(s => s.Weight).ToList());
        return new WebMiniGameOutcome(configuration.Price, slices[index].Payout, new { slice = index });
    }

    /// <summary>
    /// Drops the ball.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="bet">The bet.</param>
    /// <returns>The outcome, with the path of the ball (0 = left, 1 = right at each row) and its slot.</returns>
    public WebMiniGameOutcome DropBall(DropBallConfiguration configuration, int bet)
    {
        var multipliers = ParseSlotMultipliers(configuration.SlotMultipliers) ?? throw new InvalidOperationException("Invalid slot multipliers.");
        var path = Enumerable.Range(0, multipliers.Length - 1).Select(_ => this._random(2)).ToArray();
        var slot = path.Sum();
        return new WebMiniGameOutcome(bet, Pay(bet, multipliers[slot]), new { path, slot, multiplier = multipliers[slot] });
    }

    /// <summary>
    /// Scratches a card. The prize is drawn first, then three symbols are laid out which show it: three of the
    /// prize's symbol for a win, otherwise symbols of which no three match.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The outcome, with the three symbols and the index of the won prize (-1 if none).</returns>
    public WebMiniGameOutcome ScratchCard(ScratchCardConfiguration configuration)
    {
        var prizes = configuration.Prizes.ToList();
        var weights = prizes.Select(p => p.Weight).Append(configuration.LosingWeight).ToList();
        var pick = this.PickWeighted(weights);
        string[] symbols;
        if (pick < prizes.Count)
        {
            symbols = [prizes[pick].Symbol, prizes[pick].Symbol, prizes[pick].Symbol];
        }
        else
        {
            pick = -1;
            symbols = new string[3];
            for (var i = 0; i < 3; i++)
            {
                symbols[i] = prizes[this._random(prizes.Count)].Symbol;
            }

            if (symbols[0] == symbols[1] && symbols[1] == symbols[2])
            {
                // Three of a kind would look like a win; break the triple with another symbol.
                var others = prizes.Where(p => p.Symbol != symbols[0]).ToList();
                symbols[this._random(3)] = others[this._random(others.Count)].Symbol;
            }
        }

        return new WebMiniGameOutcome(configuration.Price, pick >= 0 ? prizes[pick].Payout : 0, new { symbols, prize = pick });
    }

    /// <summary>
    /// Rolls one die for odd or even.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="bet">The bet.</param>
    /// <param name="pick">0 = odd, 1 = even, 2 to 7 = the exact number 1 to 6.</param>
    /// <returns>The outcome, with the rolled number.</returns>
    public WebMiniGameOutcome RollOddOrEven(OddOrEvenConfiguration configuration, int bet, int pick)
    {
        var roll = this._random(6) + 1;
        var won = pick switch
        {
            0 => roll % 2 == 1,
            1 => roll % 2 == 0,
            _ => roll == pick - 1,
        };
        var multiplier = pick <= 1 ? configuration.OddEvenMultiplier : configuration.ExactNumberMultiplier;
        return new WebMiniGameOutcome(bet, won ? Pay(bet, multiplier) : 0, new { pick, roll, won });
    }

    /// <summary>
    /// Tosses the coin of kara y krus.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="bet">The bet.</param>
    /// <param name="pick">0 = kara (heads), 1 = krus (tails).</param>
    /// <returns>The outcome, with the side the coin landed on.</returns>
    public WebMiniGameOutcome TossCoin(CoinTossConfiguration configuration, int bet, int pick)
    {
        var side = this._random(2);
        return new WebMiniGameOutcome(bet, side == pick ? Pay(bet, configuration.Multiplier) : 0, new { pick, side, won = side == pick });
    }

    /// <summary>
    /// Deals a round of lucky 9 against the banker. Both draw a third card on a total of 5 or less, unless
    /// their first two cards make 9 (a natural). The last digit of the total closest to 9 wins.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="bet">The bet.</param>
    /// <returns>The outcome, with the cards of both hands.</returns>
    public WebMiniGameOutcome PlayLuckyNine(LuckyNineConfiguration configuration, int bet)
    {
        var player = this.DealLuckyNineHand();
        var banker = this.DealLuckyNineHand();
        var playerTotal = LuckyNineTotal(player);
        var bankerTotal = LuckyNineTotal(banker);
        var natural = player.Count == 2 && playerTotal == 9;
        var outcome = playerTotal > bankerTotal ? "win" : playerTotal < bankerTotal ? "lose" : "tie";
        var payout = outcome switch
        {
            "win" => Pay(bet, natural ? configuration.NaturalNineMultiplier : configuration.WinMultiplier),
            "tie" => bet,
            _ => 0,
        };
        return new WebMiniGameOutcome(bet, payout, new { player, banker, playerTotal, bankerTotal, natural, outcome });
    }

    /// <summary>
    /// Fights a round of sabong.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="bet">The bet.</param>
    /// <param name="pick">0 = meron, 1 = wala.</param>
    /// <returns>The outcome, with the winner: 0 = meron, 1 = wala, 2 = draw.</returns>
    public WebMiniGameOutcome FightSabong(SabongConfiguration configuration, int bet, int pick)
    {
        var drawRoll = this._random(10_000);
        var winner = drawRoll < configuration.DrawChancePercent * 100 ? 2 : this._random(2);
        var payout = winner == 2 ? bet : winner == pick ? Pay(bet, configuration.WinMultiplier) : 0;
        return new WebMiniGameOutcome(bet, payout, new { pick, winner });
    }

    /// <summary>
    /// Draws a card for hi-lo.
    /// </summary>
    /// <returns>The card.</returns>
    public Card DrawCard() => new(this._random(13) + 1, this._random(4));

    /// <summary>
    /// Gets the multiplier factor of a right hi-lo guess on a card, or 0 if the guess can't be right.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="rank">The rank of the shown card, 1 (ace) to 13 (king).</param>
    /// <param name="higher"><c>true</c> to guess higher, <c>false</c> to guess lower.</param>
    /// <returns>The factor; an equal card loses.</returns>
    public static double HiLoFactor(HiLoConfiguration configuration, int rank, bool higher)
    {
        var chance = (higher ? 13 - rank : rank - 1) / 13.0;
        return chance <= 0 ? 0 : configuration.ReturnPerGuess / chance;
    }

    /// <summary>
    /// Calculates the average share of the bets which a game pays back (the return to player), from 0 to 1.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The return to player of each game.</returns>
    public static IReadOnlyDictionary<string, double> ReturnToPlayer(WebMiniGamesConfiguration configuration)
    {
        var color = configuration.ColorGame;
        var colorReturn = ((75 * color.OneMatchMultiplier) + (15 * color.TwoMatchesMultiplier) + color.ThreeMatchesMultiplier) / 216;

        var wheel = configuration.PrizeWheel;
        var wheelWeight = wheel.Slices.Sum(s => (double)s.Weight);
        var wheelReturn = wheelWeight > 0 && wheel.Price > 0 ? wheel.Slices.Sum(s => s.Weight * (double)s.Payout) / wheelWeight / wheel.Price : 0;

        var multipliers = ParseSlotMultipliers(configuration.DropBall.SlotMultipliers) ?? [];
        var rows = multipliers.Length - 1;
        var dropBallReturn = rows <= 0 ? 0 : multipliers.Select((m, k) => m * Binomial(rows, k)).Sum() / Math.Pow(2, rows);

        var scratch = configuration.ScratchCard;
        var scratchWeight = scratch.Prizes.Sum(p => (double)p.Weight) + scratch.LosingWeight;
        var scratchReturn = scratchWeight > 0 && scratch.Price > 0 ? scratch.Prizes.Sum(p => p.Weight * (double)p.Payout) / scratchWeight / scratch.Price : 0;

        var lucky = configuration.LuckyNine;
        var (win, naturalWin, tie) = LuckyNineChances.Value;
        var sabong = configuration.Sabong;
        var draw = Math.Clamp(sabong.DrawChancePercent / 100, 0, 1);

        return new Dictionary<string, double>
        {
            ["color"] = colorReturn,
            ["wheel"] = wheelReturn,
            ["dropball"] = dropBallReturn,
            ["scratch"] = scratchReturn,
            ["oddeven"] = configuration.OddOrEven.OddEvenMultiplier / 2,
            ["oddeven-exact"] = configuration.OddOrEven.ExactNumberMultiplier / 6,
            ["coin"] = configuration.CoinToss.Multiplier / 2,
            ["lucky9"] = ((win - naturalWin) * lucky.WinMultiplier) + (naturalWin * lucky.NaturalNineMultiplier) + tie,
            ["hilo"] = configuration.HiLo.ReturnPerGuess,
            ["sabong"] = ((1 - draw) / 2 * sabong.WinMultiplier) + draw,
        };
    }

    /// <summary>
    /// The chances of a lucky 9 round: player wins, player wins with a natural 9, tie. Cards are drawn from an
    /// endless deck, so each value has a fixed chance: 0 (ten and pictures) 4 in 13, 1 to 9 1 in 13 each.
    /// </summary>
    private static readonly Lazy<(double Win, double NaturalWin, double Tie)> LuckyNineChances = new(() =>
    {
        var chance = Enumerable.Range(0, 10).Select(v => v == 0 ? 4 / 13.0 : 1 / 13.0).ToArray();
        var hands = new Dictionary<(int Total, bool Natural), double>();
        void Add((int, bool) key, double p) => hands[key] = hands.GetValueOrDefault(key) + p;
        for (var a = 0; a < 10; a++)
        {
            for (var b = 0; b < 10; b++)
            {
                var total = (a + b) % 10;
                if (total > 5)
                {
                    Add((total, total == 9), chance[a] * chance[b]);
                    continue;
                }

                for (var c = 0; c < 10; c++)
                {
                    Add(((total + c) % 10, false), chance[a] * chance[b] * chance[c]);
                }
            }
        }

        double win = 0, naturalWin = 0, tie = 0;
        foreach (var (player, p) in hands)
        {
            foreach (var (banker, q) in hands)
            {
                if (player.Total > banker.Total)
                {
                    win += p * q;
                    naturalWin += player.Natural ? p * q : 0;
                }
                else if (player.Total == banker.Total)
                {
                    tie += p * q;
                }
            }
        }

        return (win, naturalWin, tie);
    });

    private static int LuckyNineValue(Card card) => card.Rank >= 10 ? 0 : card.Rank;

    private static int LuckyNineTotal(IEnumerable<Card> hand) => hand.Sum(LuckyNineValue) % 10;

    private List<Card> DealLuckyNineHand()
    {
        var hand = new List<Card> { this.DrawCard(), this.DrawCard() };
        if (LuckyNineTotal(hand) <= 5)
        {
            hand.Add(this.DrawCard());
        }

        return hand;
    }

    // The epsilon keeps e.g. 100 * 2.3 = 229.99999999999997 from rounding down to 229.
    private static int Pay(int bet, double multiplier) => (int)Math.Min(int.MaxValue, Math.Floor((bet * multiplier) + 1e-9));

    private static double Binomial(int n, int k)
    {
        double result = 1;
        for (var i = 1; i <= k; i++)
        {
            result = result * (n - k + i) / i;
        }

        return result;
    }

    private int PickWeighted(IReadOnlyList<int> weights)
    {
        var total = weights.Sum(w => (long)Math.Max(0, w));
        if (total <= 0 || total > int.MaxValue)
        {
            throw new InvalidOperationException("The weights must add up to more than 0.");
        }

        var roll = this._random((int)total);
        for (var i = 0; i < weights.Count; i++)
        {
            roll -= Math.Max(0, weights[i]);
            if (roll < 0)
            {
                return i;
            }
        }

        return weights.Count - 1;
    }
}
