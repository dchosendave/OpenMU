// <copyright file="CastleSiegeScoreboard.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.CastleSiege.PointMode;

/// <summary>
/// The points of the guilds in a point mode Castle Siege battle. Points belong to the main guild of an
/// alliance (the alliance master), identified by its persistent id.
/// </summary>
public sealed class CastleSiegeScoreboard
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, CastleSiegeScore> _scores = new();
    private readonly Dictionary<Guid, TimeSpan> _crownBanks = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeScoreboard"/> class.
    /// </summary>
    /// <param name="battleId">The id of the battle.</param>
    /// <param name="startedAtUtc">The start of the battle.</param>
    public CastleSiegeScoreboard(Guid battleId, DateTime startedAtUtc)
    {
        this.BattleId = battleId;
        this.StartedAtUtc = startedAtUtc;
    }

    /// <summary>Gets the id of the battle.</summary>
    public Guid BattleId { get; }

    /// <summary>Gets the start of the battle.</summary>
    public DateTime StartedAtUtc { get; }

    /// <summary>Gets the main guild which holds the castle right now, if any.</summary>
    public Guid? HolderGuildId { get; private set; }

    /// <summary>Gets the name of the guild which holds the castle right now.</summary>
    public string? HolderGuildName { get; private set; }

    /// <summary>Gets the time from which the current holder's next defense tick is counted.</summary>
    public DateTime HoldClockUtc { get; private set; }

    /// <summary>Gets the main guild whose banked crown progress is loaded right now.</summary>
    public Guid? CrownBankOwner { get; private set; }

    /// <summary>Gets or sets when the scoreboard is posted in game next.</summary>
    public DateTime NextScoreboardPostUtc { get; set; } = DateTime.MaxValue;

    /// <summary>Gets or sets a value indicating whether the scores changed since they were last stored.</summary>
    public bool IsDirty { get; set; } = true;

    /// <summary>Gets or sets when the live scoreboard may be stored next.</summary>
    public DateTime NextSaveUtc { get; set; }

    /// <summary>Gets or sets when the scoreboard is sent to the game clients again, to keep their timers right.</summary>
    public DateTime NextClientSyncUtc { get; set; }

    /// <summary>
    /// Gets the scores, best first: most points, then most seals.
    /// </summary>
    /// <returns>A snapshot of the scores.</returns>
    public IReadOnlyList<CastleSiegeScore> GetStandings()
    {
        lock (this._sync)
        {
            return this._scores.Values
                .OrderByDescending(s => s.Points)
                .ThenByDescending(s => s.Seals)
                .ThenBy(s => s.GuildName, StringComparer.OrdinalIgnoreCase)
                .Select(s => s with { })
                .ToList();
        }
    }

    /// <summary>
    /// Makes sure a guild is on the scoreboard, with zero points.
    /// </summary>
    /// <param name="guildId">The persistent id of the main guild.</param>
    /// <param name="guildName">The name of the main guild.</param>
    public void Register(Guid guildId, string guildName)
    {
        lock (this._sync)
        {
            this.GetOrAdd(guildId, guildName);
        }
    }

    /// <summary>
    /// Sets the castle holder at the start of the battle, without points.
    /// </summary>
    /// <param name="guildId">The persistent id of the main guild.</param>
    /// <param name="guildName">The name.</param>
    /// <param name="utcNow">The current time.</param>
    public void SetInitialHolder(Guid guildId, string guildName, DateTime utcNow)
    {
        lock (this._sync)
        {
            this.GetOrAdd(guildId, guildName);
            this.HolderGuildId = guildId;
            this.HolderGuildName = guildName;
            this.HoldClockUtc = utcNow;
        }
    }

    /// <summary>
    /// Scores a successful seal: the guild gets the points and holds the castle from now on.
    /// </summary>
    /// <param name="guildId">The persistent id of the sealing main guild.</param>
    /// <param name="guildName">The name.</param>
    /// <param name="points">The points of a seal.</param>
    /// <param name="utcNow">The current time.</param>
    public void AddSeal(Guid guildId, string guildName, int points, DateTime utcNow)
    {
        lock (this._sync)
        {
            var score = this.GetOrAdd(guildId, guildName);
            score.Points += points;
            score.Seals++;
            this.HolderGuildId = guildId;
            this.HolderGuildName = guildName;
            this.HoldClockUtc = utcNow;
            this._crownBanks[guildId] = TimeSpan.Zero;
            this.IsDirty = true;
        }
    }

    /// <summary>
    /// Scores a kill of a switch holder.
    /// </summary>
    /// <param name="guildId">The persistent id of the killer's main guild.</param>
    /// <param name="guildName">The name.</param>
    /// <param name="points">The points of such a kill.</param>
    public void AddSwitchKill(Guid guildId, string guildName, int points)
    {
        lock (this._sync)
        {
            var score = this.GetOrAdd(guildId, guildName);
            score.Points += points;
            score.SwitchKills++;
            this.IsDirty = true;
        }
    }

    /// <summary>
    /// Pays the defense ticks which are due: one per full interval since the hold clock.
    /// </summary>
    /// <param name="interval">The interval; zero or less turns the ticks off.</param>
    /// <param name="points">The points of a tick.</param>
    /// <param name="utcNow">The current time.</param>
    /// <returns>The number of ticks paid.</returns>
    public int PayDefenseTicks(TimeSpan interval, int points, DateTime utcNow)
    {
        if (interval <= TimeSpan.Zero)
        {
            return 0;
        }

        lock (this._sync)
        {
            if (this.HolderGuildId is not { } holder || !this._scores.TryGetValue(holder, out var score))
            {
                return 0;
            }

            var ticks = 0;
            while (utcNow - this.HoldClockUtc >= interval)
            {
                this.HoldClockUtc += interval;
                score.Points += points;
                score.DefenseTicks++;
                ticks++;
            }

            if (ticks > 0)
            {
                this.IsDirty = true;
            }

            return ticks;
        }
    }

    /// <summary>
    /// Switches the banked crown progress to another guild: stores the progress of the guild which had it,
    /// and returns the progress of the given guild.
    /// </summary>
    /// <param name="guildId">The persistent id of the main guild which works the crown now.</param>
    /// <param name="currentProgress">The crown progress which is loaded right now.</param>
    /// <returns>The progress to continue with.</returns>
    public TimeSpan SwitchCrownBank(Guid guildId, TimeSpan currentProgress)
    {
        lock (this._sync)
        {
            if (this.CrownBankOwner == guildId)
            {
                return currentProgress;
            }

            if (this.CrownBankOwner is { } previous)
            {
                this._crownBanks[previous] = currentProgress;
            }

            this.CrownBankOwner = guildId;
            return this._crownBanks.GetValueOrDefault(guildId);
        }
    }

    /// <summary>
    /// Determines the result: the guild with the most points wins; on a tie, the guild holding the
    /// castle at the end wins, if it's among the tied guilds, else the one with more seals.
    /// </summary>
    /// <returns>The result.</returns>
    public CastleSiegePointResult DetermineResult()
    {
        var standings = this.GetStandings();
        var best = standings.FirstOrDefault();
        if (best is null || best.Points <= 0)
        {
            return new CastleSiegePointResult(null, this.HolderGuildId, standings);
        }

        var tied = standings.Where(s => s.Points == best.Points).ToList();
        var winner = tied.FirstOrDefault(s => s.GuildId == this.HolderGuildId) ?? tied[0];
        return new CastleSiegePointResult(winner.GuildId, this.HolderGuildId, standings);
    }

    private CastleSiegeScore GetOrAdd(Guid guildId, string guildName)
    {
        if (!this._scores.TryGetValue(guildId, out var score))
        {
            score = new CastleSiegeScore { GuildId = guildId, GuildName = guildName };
            this._scores.Add(guildId, score);
            this.IsDirty = true;
        }

        return score;
    }
}

/// <summary>
/// The points of a main guild.
/// </summary>
public sealed record CastleSiegeScore
{
    /// <summary>Gets the persistent id of the main guild.</summary>
    public Guid GuildId { get; init; }

    /// <summary>Gets the name of the main guild.</summary>
    public string GuildName { get; init; } = string.Empty;

    /// <summary>Gets or sets the points.</summary>
    public int Points { get; set; }

    /// <summary>Gets or sets the successful seals.</summary>
    public int Seals { get; set; }

    /// <summary>Gets or sets the scored switch holder kills.</summary>
    public int SwitchKills { get; set; }

    /// <summary>Gets or sets the paid defense ticks.</summary>
    public int DefenseTicks { get; set; }
}

/// <summary>
/// The result of a point mode battle.
/// </summary>
/// <param name="WinnerGuildId">The main guild with the most points; <c>null</c>, if nobody scored.</param>
/// <param name="LastHolderGuildId">The main guild which held the castle at the end.</param>
/// <param name="Standings">The final scores, best first.</param>
public sealed record CastleSiegePointResult(Guid? WinnerGuildId, Guid? LastHolderGuildId, IReadOnlyList<CastleSiegeScore> Standings)
{
    /// <summary>
    /// Gets the reward tier of a main guild.
    /// </summary>
    /// <param name="guildId">The persistent id of the main guild.</param>
    /// <returns>The tier; <c>null</c>, if it gets nothing.</returns>
    public CastleSiegeRewardPlacement? PlacementOf(Guid guildId)
    {
        if (this.WinnerGuildId is null)
        {
            return null;
        }

        if (guildId == this.WinnerGuildId)
        {
            return CastleSiegeRewardPlacement.Winner;
        }

        if (guildId == this.LastHolderGuildId)
        {
            return CastleSiegeRewardPlacement.Consolation;
        }

        return this.Standings.Any(s => s.GuildId == guildId && s.Points > 0)
            ? CastleSiegeRewardPlacement.Participant
            : null;
    }
}

/// <summary>
/// The reward tiers of a point mode battle.
/// </summary>
public enum CastleSiegeRewardPlacement
{
    /// <summary>The guild with the most points.</summary>
    Winner,

    /// <summary>The guild which held the castle at the end, but lost on points.</summary>
    Consolation,

    /// <summary>Any other guild which scored.</summary>
    Participant,
}
