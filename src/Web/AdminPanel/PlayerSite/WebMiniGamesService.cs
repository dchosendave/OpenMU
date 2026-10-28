// <copyright file="WebMiniGamesService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.PlayerSite;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.WebMiniGames;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Web.AdminPanel.AccountManager;

/// <summary>
/// The coin mini games of the player website: plays them on an offline account and keeps their settings,
/// which are the configuration of the <see cref="WebMiniGamesPlugIn"/>.
/// </summary>
public sealed class WebMiniGamesService : IDisposable
{
    private static readonly Guid PlugInId = typeof(WebMiniGamesPlugIn).GUID;

    /// <summary>How long an untouched hi-lo round stays open; after that its bet is lost.</summary>
    private static readonly TimeSpan HiLoRoundLifetime = TimeSpan.FromHours(1);

    private readonly PlayerSiteService _playerSite;
    private readonly IDataSource<GameConfiguration> _configurationSource;
    private readonly IWebMiniGamePlayRepository _plays;
    private readonly ILogger<WebMiniGamesService> _logger;
    private readonly WebMiniGameEngine _engine = new();
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly ConcurrentDictionary<string, HiLoRound> _hiLoRounds = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="WebMiniGamesService"/> class.
    /// </summary>
    /// <param name="playerSite">The player site service, which changes accounts.</param>
    /// <param name="configurationSource">The game configuration, which holds the settings.</param>
    /// <param name="plays">The stored plays.</param>
    /// <param name="logger">The logger.</param>
    public WebMiniGamesService(PlayerSiteService playerSite, IDataSource<GameConfiguration> configurationSource, IWebMiniGamePlayRepository plays, ILogger<WebMiniGamesService> logger)
    {
        this._playerSite = playerSite;
        this._configurationSource = configurationSource;
        this._plays = plays;
        this._logger = logger;
    }

    /// <summary>
    /// Gets the settings: whether the games are open at all, and their configuration.
    /// </summary>
    /// <returns>The settings.</returns>
    public async Task<(bool Active, WebMiniGamesConfiguration Configuration)> GetSettingsAsync()
    {
        var gameConfiguration = await this._configurationSource.GetOwnerAsync().ConfigureAwait(false);
        var plugIn = gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == PlugInId);
        var configuration = plugIn?.GetConfiguration<WebMiniGamesConfiguration>(new ByDataSourceReferenceHandler(this._configurationSource));
        return (plugIn?.IsActive ?? true, configuration ?? new WebMiniGamesConfiguration());
    }

    /// <summary>
    /// Saves the settings.
    /// </summary>
    /// <param name="active">Whether the games are open.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The result; refused with the problems if the configuration is invalid.</returns>
    public async Task<PlayerSiteResult> SaveSettingsAsync(bool active, WebMiniGamesConfiguration configuration)
    {
        if (WebMiniGameEngine.Validate(configuration) is { Count: > 0 } problems)
        {
            return new PlayerSiteResult(PlayerSiteResult.Invalid, string.Join(" ", problems));
        }

        await this._saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var gameConfiguration = await this._configurationSource.GetOwnerAsync().ConfigureAwait(false);
            var context = await this._configurationSource.GetContextAsync().ConfigureAwait(false);
            var plugIn = gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == PlugInId);
            if (plugIn is null)
            {
                plugIn = context.CreateNew<PlugInConfiguration>();
                plugIn.TypeId = PlugInId;
                gameConfiguration.PlugInConfigurations.Add(plugIn);
            }

            plugIn.IsActive = active;
            plugIn.SetConfiguration(configuration, new ByDataSourceReferenceHandler(this._configurationSource));
            await context.SaveChangesAsync().ConfigureAwait(false);
            return new PlayerSiteResult(PlayerSiteResult.Ok, Data: new { returnToPlayer = WebMiniGameEngine.ReturnToPlayer(configuration) });
        }
        finally
        {
            this._saveLock.Release();
        }
    }

    /// <summary>
    /// Plays a game: takes the bet from the account, draws the outcome and pays the win, in one change.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <param name="game">The game, see <see cref="WebMiniGameEngine.Games"/>.</param>
    /// <param name="bet">The bet, for the games with a free bet.</param>
    /// <param name="choice">The choice of the player, e.g. the color of the color game.</param>
    /// <returns>The result, with the outcome and the new balance.</returns>
    public async Task<PlayerSiteResult> PlayAsync(string loginName, string game, int bet, int choice)
    {
        var (active, configuration) = await this.GetSettingsAsync().ConfigureAwait(false);
        if (!active)
        {
            return new PlayerSiteResult(PlayerSiteResult.Invalid, "The games are closed right now.");
        }

        if (game == "hilo")
        {
            return await this.PlayHiLoAsync(loginName, configuration, bet, choice).ConfigureAwait(false);
        }

        return await this.BookAsync(loginName, configuration, game, () => this.Draw(configuration, game, bet, choice)).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes the bet of a play from the account and pays its win in one change, and stores the play.
    /// </summary>
    private async Task<PlayerSiteResult> BookAsync(string loginName, WebMiniGamesConfiguration configuration, string game, Func<WebMiniGameOutcome> draw, Action<WebMiniGamePlay>? stored = null)
    {
        WebMiniGamePlay? play = null;
        return await this._playerSite.ChangeAccountAsync(
            loginName,
            async session =>
            {
                var outcome = draw();
                if (configuration.DailyBetLimit > 0)
                {
                    var today = await this._plays.GetBetSumAsync(session.Account.LoginName, DateTime.UtcNow.Date).ConfigureAwait(false);
                    if (today + outcome.Bet > configuration.DailyBetLimit)
                    {
                        throw PlayerSiteService.Refuse(PlayerSiteResult.Invalid, $"That's over your daily limit of {configuration.DailyBetLimit:N0}: you can still bet {Math.Max(0, configuration.DailyBetLimit - today):N0} today.");
                    }
                }

                var balance = BalanceOf(session, configuration.Currency);
                if (balance < outcome.Bet)
                {
                    throw PlayerSiteService.Refuse(PlayerSiteResult.InsufficientFunds, $"This costs {outcome.Bet:N0}, but you have {balance:N0}.");
                }

                var newBalance = (int)Math.Min(int.MaxValue, (long)balance - outcome.Bet + outcome.Payout);
                SetBalance(session, configuration.Currency, newBalance);
                play = new WebMiniGamePlay
                {
                    LoginName = session.Account.LoginName,
                    Game = game,
                    Bet = outcome.Bet,
                    Payout = outcome.Payout,
                    Currency = configuration.Currency.ToString(),
                    Result = JsonSerializer.Serialize(outcome.Result),
                };
                return new { game, bet = outcome.Bet, payout = outcome.Payout, result = outcome.Result, balance = (int?)newBalance, currency = configuration.Currency.ToString() };
            },
            async _ =>
            {
                try
                {
                    await this._plays.AddAsync(play!).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // The coins are already booked; a missing history entry must not hide that from the player.
                    this._logger.LogError(ex, "Couldn't store the mini game play of {LoginName}: {Game}, bet {Bet}, payout {Payout}.", play!.LoginName, play.Game, play.Bet, play.Payout);
                }

                stored?.Invoke(play!);
            }).ConfigureAwait(false);
    }

    /// <summary>
    /// Plays hi-lo, which spans several calls: 0 starts a round (takes the bet and shows a card), 1 guesses
    /// higher, 2 guesses lower, 3 cashes out, 4 only returns the open round (e.g. after a page reload).
    /// </summary>
    private async Task<PlayerSiteResult> PlayHiLoAsync(string loginName, WebMiniGamesConfiguration configuration, int bet, int choice)
    {
        var settings = configuration.HiLo;
        if (!settings.Enabled)
        {
            return new PlayerSiteResult(PlayerSiteResult.NotFound, "This game is closed.");
        }

        var key = loginName.ToLowerInvariant();
        var round = this._hiLoRounds.TryGetValue(key, out var open) && open.LastAction > DateTime.UtcNow - HiLoRoundLifetime ? open : null;
        if (round is null && open is not null)
        {
            this._hiLoRounds.TryRemove(key, out _);
        }

        object State(HiLoRound r, string state, Card? previous = null) => new
        {
            state,
            card = r.Card,
            previous,
            multiplier = r.Multiplier,
            steps = r.Steps,
            higher = r.Multiplier * WebMiniGameEngine.HiLoFactor(settings, r.Card.Rank, true),
            lower = r.Multiplier * WebMiniGameEngine.HiLoFactor(settings, r.Card.Rank, false),
            cashOut = (int)Math.Floor(r.Bet * r.Multiplier),
        };

        PlayerSiteResult Ok(HiLoRound r, object result, int payout = 0, int? balance = null)
            => new(PlayerSiteResult.Ok, Data: new { game = "hilo", bet = r.Bet, payout, result, balance, currency = configuration.Currency.ToString() });

        switch (choice)
        {
            case 4:
                return round is null
                    ? new PlayerSiteResult(PlayerSiteResult.Ok, Data: new { game = "hilo", bet = 0, payout = 0, result = new { state = "none" }, balance = (int?)null, currency = configuration.Currency.ToString() })
                    : Ok(round, State(round, "active"));
            case 0:
                if (round is not null)
                {
                    return new PlayerSiteResult(PlayerSiteResult.Invalid, "Finish your open round first.");
                }

                if (bet < settings.MinimumBet || bet > settings.MaximumBet)
                {
                    return new PlayerSiteResult(PlayerSiteResult.Invalid, $"Bet between {settings.MinimumBet:N0} and {settings.MaximumBet:N0}.");
                }

                var start = new HiLoRound(bet, this._engine.DrawCard());
                return await this.BookAsync(
                    loginName,
                    configuration,
                    "hilo",
                    () => this._hiLoRounds.ContainsKey(key)
                        ? throw PlayerSiteService.Refuse(PlayerSiteResult.Invalid, "Finish your open round first.")
                        : new WebMiniGameOutcome(bet, 0, State(start, "active")),
                    play =>
                    {
                        start.PlayId = play.Id;
                        this._hiLoRounds[key] = start;
                    }).ConfigureAwait(false);
            case 1 or 2:
                if (round is null)
                {
                    return new PlayerSiteResult(PlayerSiteResult.Invalid, "Start a round first.");
                }

                lock (round)
                {
                    var factor = WebMiniGameEngine.HiLoFactor(settings, round.Card.Rank, choice == 1);
                    if (factor <= 0)
                    {
                        return new PlayerSiteResult(PlayerSiteResult.Invalid, choice == 1 ? "Nothing is higher than a king." : "Nothing is lower than an ace.");
                    }

                    var previous = round.Card;
                    round.Card = this._engine.DrawCard();
                    round.LastAction = DateTime.UtcNow;
                    var right = choice == 1 ? round.Card.Rank > previous.Rank : round.Card.Rank < previous.Rank;
                    if (!right)
                    {
                        this._hiLoRounds.TryRemove(key, out _);
                        var lost = State(round, "lost", previous);
                        _ = this.UpdatePlayAsync(round.PlayId, 0, lost);
                        return Ok(round, lost);
                    }

                    round.Multiplier = Math.Min(settings.MaximumMultiplier, round.Multiplier * factor);
                    round.Steps++;
                    if (round.Multiplier < settings.MaximumMultiplier)
                    {
                        return Ok(round, State(round, "active", previous));
                    }
                }

                // The maximum is reached: the round cashes out on its own.
                return await this.CashOutHiLoAsync(loginName, configuration, key, round).ConfigureAwait(false);
            case 3:
                if (round is null || round.Steps == 0)
                {
                    return new PlayerSiteResult(PlayerSiteResult.Invalid, round is null ? "Start a round first." : "Guess at least once before you cash out.");
                }

                return await this.CashOutHiLoAsync(loginName, configuration, key, round).ConfigureAwait(false);
            default:
                return new PlayerSiteResult(PlayerSiteResult.Invalid, "That's not a valid play.");
        }
    }

    private async Task<PlayerSiteResult> CashOutHiLoAsync(string loginName, WebMiniGamesConfiguration configuration, string key, HiLoRound round)
    {
        var payout = (int)Math.Min(int.MaxValue, Math.Floor(round.Bet * round.Multiplier));
        var result = new { state = "cashed", card = round.Card, multiplier = round.Multiplier, steps = round.Steps, cashOut = payout };
        return await this._playerSite.ChangeAccountAsync(
            loginName,
            session =>
            {
                // Only one cash-out per round, even if the player clicks twice.
                if (!this._hiLoRounds.TryRemove(new KeyValuePair<string, HiLoRound>(key, round)))
                {
                    throw PlayerSiteService.Refuse(PlayerSiteResult.Invalid, "This round is already over.");
                }

                var balance = (int)Math.Min(int.MaxValue, (long)BalanceOf(session, configuration.Currency) + payout);
                SetBalance(session, configuration.Currency, balance);
                return Task.FromResult<object?>(new { game = "hilo", bet = round.Bet, payout, result, balance = (int?)balance, currency = configuration.Currency.ToString() });
            },
            _ => this.UpdatePlayAsync(round.PlayId, payout, result)).ConfigureAwait(false);
    }

    private async Task UpdatePlayAsync(Guid playId, int payout, object result)
    {
        try
        {
            await this._plays.UpdateResultAsync(playId, payout, JsonSerializer.Serialize(result)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Couldn't update the hi-lo play {PlayId} to payout {Payout}.", playId, payout);
        }
    }

    /// <inheritdoc />
    public void Dispose() => this._saveLock.Dispose();

    private static int BalanceOf(AccountSession session, WebMiniGameCurrency currency) => currency switch
    {
        WebMiniGameCurrency.WCoinP => session.WCoinP,
        WebMiniGameCurrency.GoblinPoints => session.GoblinPoints,
        _ => session.WCoinC,
    };

    private static void SetBalance(AccountSession session, WebMiniGameCurrency currency, int value)
    {
        switch (currency)
        {
            case WebMiniGameCurrency.WCoinP:
                session.WCoinP = value;
                break;
            case WebMiniGameCurrency.GoblinPoints:
                session.GoblinPoints = value;
                break;
            default:
                session.WCoinC = value;
                break;
        }
    }

    private static void CheckBet(int bet, int minimum, int maximum)
    {
        if (bet < minimum || bet > maximum)
        {
            throw PlayerSiteService.Refuse(PlayerSiteResult.Invalid, $"Bet between {minimum:N0} and {maximum:N0}.");
        }
    }

    private static void CheckChoice(int choice, int count, string message)
    {
        if (choice < 0 || choice >= count)
        {
            throw PlayerSiteService.Refuse(PlayerSiteResult.Invalid, message);
        }
    }

    private WebMiniGameOutcome Draw(WebMiniGamesConfiguration configuration, string game, int bet, int choice)
    {
        switch (game)
        {
            case "color" when configuration.ColorGame.Enabled:
                CheckBet(bet, configuration.ColorGame.MinimumBet, configuration.ColorGame.MaximumBet);
                if (choice is < 0 or >= WebMiniGameEngine.ColorCount)
                {
                    throw PlayerSiteService.Refuse(PlayerSiteResult.Invalid, "Pick a color.");
                }

                return this._engine.PlayColorGame(configuration.ColorGame, bet, choice);
            case "wheel" when configuration.PrizeWheel.Enabled:
                return this._engine.SpinPrizeWheel(configuration.PrizeWheel);
            case "dropball" when configuration.DropBall.Enabled:
                CheckBet(bet, configuration.DropBall.MinimumBet, configuration.DropBall.MaximumBet);
                return this._engine.DropBall(configuration.DropBall, bet);
            case "scratch" when configuration.ScratchCard.Enabled:
                return this._engine.ScratchCard(configuration.ScratchCard);
            case "oddeven" when configuration.OddOrEven.Enabled:
                CheckBet(bet, configuration.OddOrEven.MinimumBet, configuration.OddOrEven.MaximumBet);
                CheckChoice(choice, 8, "Pick odd, even or a number.");
                return this._engine.RollOddOrEven(configuration.OddOrEven, bet, choice);
            case "coin" when configuration.CoinToss.Enabled:
                CheckBet(bet, configuration.CoinToss.MinimumBet, configuration.CoinToss.MaximumBet);
                CheckChoice(choice, 2, "Pick kara or krus.");
                return this._engine.TossCoin(configuration.CoinToss, bet, choice);
            case "lucky9" when configuration.LuckyNine.Enabled:
                CheckBet(bet, configuration.LuckyNine.MinimumBet, configuration.LuckyNine.MaximumBet);
                return this._engine.PlayLuckyNine(configuration.LuckyNine, bet);
            case "sabong" when configuration.Sabong.Enabled:
                CheckBet(bet, configuration.Sabong.MinimumBet, configuration.Sabong.MaximumBet);
                CheckChoice(choice, 2, "Pick meron or wala.");
                return this._engine.FightSabong(configuration.Sabong, bet, choice);
            default:
                throw PlayerSiteService.Refuse(PlayerSiteResult.NotFound, "This game is closed.");
        }
    }

    /// <summary>
    /// An open hi-lo round.
    /// </summary>
    private sealed class HiLoRound(int bet, Card card)
    {
        public int Bet { get; } = bet;

        public Card Card { get; set; } = card;

        public double Multiplier { get; set; } = 1;

        public int Steps { get; set; }

        public Guid PlayId { get; set; }

        public DateTime LastAction { get; set; } = DateTime.UtcNow;
    }
}
