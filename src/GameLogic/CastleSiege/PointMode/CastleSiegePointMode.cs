// <copyright file="CastleSiegePointMode.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.CastleSiege.PointMode;

using System.Text.Json;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.GameLogic.Views.CastleSiege;
using MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// The point mode of Castle Siege (see <see cref="CastleSiegePointModePlugIn"/>), hooked into the classic
/// battle flow. Every hook does nothing while the plugin is inactive.
/// </summary>
internal static class CastleSiegePointMode
{
    private const string Prefix = "[Castle Siege] ";

    /// <summary>A holder who just died may be released from the switch a moment before the kill is reported.</summary>
    private static readonly TimeSpan ReleaseGrace = TimeSpan.FromSeconds(3);

    /// <summary>The live scoreboard is stored at most this often.</summary>
    private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(10);

    /// <summary>The game clients count the timers down themselves; this often they get the exact values again.</summary>
    private static readonly TimeSpan ClientSyncInterval = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Starts the scoreboard of a battle. The guild holding the castle at the start holds it from now on.
    /// </summary>
    /// <param name="context">The Castle Siege context.</param>
    /// <param name="utcNow">The current time.</param>
    public static async ValueTask OnBattleStartAsync(CastleSiegeContext context, DateTime utcNow)
    {
        if (GetPlugIn(context) is not { Configuration: { } configuration } plugIn)
        {
            context.Scoreboard = null;
            return;
        }

        var scoreboard = new CastleSiegeScoreboard(Guid.NewGuid(), utcNow);
        foreach (var guild in context.FinalGuildList.Values)
        {
            var main = GetMainGuild(context, guild);
            scoreboard.Register(main.PersistentGuildId, main.GuildName);
        }

        if (context.MiddleOwnerGuildId is { } ownerId
            && context.FinalGuildList.TryGetValue(ownerId, out var owner))
        {
            var main = GetMainGuild(context, owner);
            scoreboard.SetInitialHolder(main.PersistentGuildId, main.GuildName, utcNow);
        }

        scoreboard.NextScoreboardPostUtc = configuration.ScoreboardIntervalMinutes > 0
            ? utcNow.AddMinutes(configuration.ScoreboardIntervalMinutes)
            : DateTime.MaxValue;
        Array.Clear(context.SwitchHeldSinceUtc);
        Array.Clear(context.ReleasedSwitchHolders);
        context.Scoreboard = scoreboard;

        await SendToSiegeAsync(context, Prefix + "Point battle! Seals, switch holder kills and holding the castle score. Most points wins.", MessageType.GoldenCenter).ConfigureAwait(false);
        await BroadcastScoreboardAsync(context, CastleSiegeScoreboardState.Running).ConfigureAwait(false);
        await SaveBattleAsync(plugIn, context, scoreboard, null, null).ConfigureAwait(false);
    }

    /// <summary>
    /// Swaps in the banked crown progress of the guild which works the crown now, when every guild
    /// banks its own progress.
    /// </summary>
    /// <param name="context">The Castle Siege context.</param>
    /// <param name="crownUser">The player on the crown.</param>
    public static void ApplyCrownBank(CastleSiegeContext context, Player crownUser)
    {
        if (context.Scoreboard is not { } scoreboard
            || GetPlugIn(context)?.Configuration is not { CrownProgressPerGuild: true }
            || GetMainGuild(context, crownUser) is not { } main)
        {
            return;
        }

        context.CrownAccumulatedTime = scoreboard.SwitchCrownBank(main.PersistentGuildId, context.CrownAccumulatedTime);
    }

    /// <summary>
    /// Scores a successful seal.
    /// </summary>
    /// <param name="context">The Castle Siege context.</param>
    /// <param name="winnerGuild">The main guild which holds the castle now.</param>
    /// <param name="sealer">The player who sealed.</param>
    public static async ValueTask OnSealAsync(CastleSiegeContext context, CastleSiegeGuildParticipant winnerGuild, Player sealer)
    {
        if (context.Scoreboard is not { } scoreboard || GetPlugIn(context)?.Configuration is not { } configuration)
        {
            return;
        }

        var utcNow = context.TimeProvider.GetUtcNow().UtcDateTime;
        scoreboard.AddSeal(winnerGuild.PersistentGuildId, winnerGuild.GuildName, configuration.SealPoints, utcNow);
        var points = PointsOf(scoreboard, winnerGuild.PersistentGuildId);
        await SendToSiegeAsync(context, $"{Prefix}{sealer.Name} sealed the crown for {winnerGuild.GuildName}! +{configuration.SealPoints} ({points} points)", MessageType.GoldenCenter).ConfigureAwait(false);
        await context.GameContext.SendGlobalMessageAsync($"{Prefix}{winnerGuild.GuildName} sealed the crown and holds the castle ({points} points).", MessageType.BlueNormal).ConfigureAwait(false);
        await BroadcastScoreboardAsync(context, CastleSiegeScoreboardState.Running).ConfigureAwait(false);
    }

    /// <summary>
    /// Remembers when a switch got a new holder, so a kill can tell how long its victim held it.
    /// </summary>
    /// <param name="context">The Castle Siege context.</param>
    /// <param name="switchIndex">The index of the switch.</param>
    /// <param name="previous">The previous holder.</param>
    /// <param name="next">The new holder.</param>
    public static void RecordSwitchHolderChange(CastleSiegeContext context, int switchIndex, Player? previous, Player? next)
    {
        if (switchIndex is < 0 or > 1)
        {
            return;
        }

        var utcNow = context.TimeProvider.GetUtcNow().UtcDateTime;
        if (previous is not null)
        {
            context.ReleasedSwitchHolders[switchIndex] = (previous, context.SwitchHeldSinceUtc[switchIndex], utcNow);
        }

        context.SwitchHeldSinceUtc[switchIndex] = utcNow;
    }

    /// <summary>
    /// Scores the kill of a switch holder by a player of an opposing side.
    /// </summary>
    /// <param name="context">The Castle Siege context.</param>
    /// <param name="victim">The killed player.</param>
    /// <param name="killer">The killing player.</param>
    public static async ValueTask OnPlayerKilledAsync(CastleSiegeContext context, Player victim, Player killer)
    {
        if (context.Scoreboard is not { } scoreboard
            || !context.IsEventRunning
            || GetPlugIn(context)?.Configuration is not { } configuration
            || configuration.SwitchHolderKillPoints <= 0)
        {
            return;
        }

        var victimSide = context.GetTrackedPlayerJoinSide(victim);
        var killerSide = context.GetTrackedPlayerJoinSide(killer);
        if (victimSide == CastleSiegeJoinSide.None
            || killerSide == CastleSiegeJoinSide.None
            || victimSide == killerSide
            || GetMainGuild(context, killer) is not { } main)
        {
            return;
        }

        var utcNow = context.TimeProvider.GetUtcNow().UtcDateTime;
        if (!HeldSwitch(context, victim, TimeSpan.FromSeconds(configuration.SwitchHoldMinimumSeconds), utcNow))
        {
            return;
        }

        scoreboard.AddSwitchKill(main.PersistentGuildId, main.GuildName, configuration.SwitchHolderKillPoints);
        var points = PointsOf(scoreboard, main.PersistentGuildId);
        await SendToSiegeAsync(context, $"{Prefix}{killer.Name} ({main.GuildName}) took down switch holder {victim.Name}! +{configuration.SwitchHolderKillPoints} ({points} points)", MessageType.BlueNormal).ConfigureAwait(false);
        await BroadcastScoreboardAsync(context, CastleSiegeScoreboardState.Running).ConfigureAwait(false);
    }

    /// <summary>
    /// Pays the defense ticks, posts the scoreboard and stores it, when due.
    /// </summary>
    /// <param name="context">The Castle Siege context.</param>
    /// <param name="utcNow">The current time.</param>
    public static async ValueTask OnTickAsync(CastleSiegeContext context, DateTime utcNow)
    {
        if (context.Scoreboard is not { } scoreboard || GetPlugIn(context) is not { Configuration: { } configuration } plugIn)
        {
            return;
        }

        var ticks = scoreboard.PayDefenseTicks(TimeSpan.FromMinutes(configuration.DefenseTickMinutes), configuration.DefenseTickPoints, utcNow);
        if (ticks > 0 && scoreboard.HolderGuildId is { } holder)
        {
            await SendToSiegeAsync(context, $"{Prefix}{scoreboard.HolderGuildName} defended the castle for {configuration.DefenseTickMinutes} minutes! +{ticks * configuration.DefenseTickPoints} ({PointsOf(scoreboard, holder)} points)", MessageType.BlueNormal).ConfigureAwait(false);
            await BroadcastScoreboardAsync(context, CastleSiegeScoreboardState.Running).ConfigureAwait(false);
        }
        else if (utcNow >= scoreboard.NextClientSyncUtc)
        {
            await BroadcastScoreboardAsync(context, CastleSiegeScoreboardState.Running).ConfigureAwait(false);
        }

        if (utcNow >= scoreboard.NextScoreboardPostUtc)
        {
            scoreboard.NextScoreboardPostUtc = utcNow.AddMinutes(Math.Max(1, configuration.ScoreboardIntervalMinutes));
            await PostScoreboardAsync(context, scoreboard).ConfigureAwait(false);
        }

        if (scoreboard.IsDirty && utcNow >= scoreboard.NextSaveUtc)
        {
            scoreboard.NextSaveUtc = utcNow + SaveInterval;
            await SaveBattleAsync(plugIn, context, scoreboard, null, null).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Decides the battle by points: the winner becomes the castle owner of the classic result check.
    /// </summary>
    /// <param name="context">The Castle Siege context.</param>
    /// <returns>The result; <c>null</c> with the classic rules.</returns>
    public static async ValueTask<CastleSiegePointResult?> DecideAsync(CastleSiegeContext context)
    {
        if (context.Scoreboard is not { } scoreboard || GetPlugIn(context) is null)
        {
            return null;
        }

        var result = scoreboard.DetermineResult();
        if (result.WinnerGuildId is { } winnerId
            && context.FinalGuildList.Values
                .Where(g => g.PersistentGuildId == winnerId)
                .OrderByDescending(g => g.IsAllianceMaster)
                .FirstOrDefault() is { } winner)
        {
            context.MiddleOwnerGuildId = winner.GuildId;
        }

        await PostScoreboardAsync(context, scoreboard, final: true).ConfigureAwait(false);
        await BroadcastScoreboardAsync(context, CastleSiegeScoreboardState.Final).ConfigureAwait(false);
        var winnerName = result.Standings.FirstOrDefault(s => s.GuildId == result.WinnerGuildId)?.GuildName;
        var message = winnerName is null
            ? Prefix + "The battle is over. Nobody scored, the castle stays with its owner."
            : $"{Prefix}{winnerName} wins the battle with {result.Standings.First(s => s.GuildId == result.WinnerGuildId).Points} points and takes the castle!";
        await context.GameContext.SendGlobalMessageAsync(message, MessageType.GoldenCenter).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Gives out the rewards of the placements and stores the result. Runs once, when the battle ends.
    /// </summary>
    /// <param name="context">The Castle Siege context.</param>
    /// <param name="result">The result of <see cref="DecideAsync"/>.</param>
    public static async ValueTask RewardAsync(CastleSiegeContext context, CastleSiegePointResult result)
    {
        if (context.Scoreboard is not { } scoreboard || GetPlugIn(context) is not { Configuration: { } configuration } plugIn)
        {
            return;
        }

        var logger = context.GameContext.LoggerFactory.CreateLogger(typeof(CastleSiegePointMode));
        var rewards = new List<object>();
        try
        {
            var placements = result.Standings
                .Select(s => (Score: s, Placement: result.PlacementOf(s.GuildId)))
                .Where(p => p.Placement is not null)
                .ToList();
            if (placements.Count == 0)
            {
                return;
            }

            using var playerContext = context.GameContext.PersistenceContextProvider.CreateNewPlayerContext(context.GameContext.Configuration);
            using var guildContext = context.GameContext.PersistenceContextProvider.CreateNewGuildContext();
            var guildMasters = await guildContext.GetGuildMasterNamesAsync().ConfigureAwait(false);
            var memberAccounts = await GetMemberAccountsAsync(context, playerContext).ConfigureAwait(false);
            var vaultRewards = new List<CastleSiegeVaultReward>();
            var paidMembers = new HashSet<Guid>();
            var utcNow = context.TimeProvider.GetUtcNow().UtcDateTime;
            foreach (var (score, placement) in placements)
            {
                var tier = placement switch
                {
                    CastleSiegeRewardPlacement.Winner => configuration.Winner,
                    CastleSiegeRewardPlacement.Consolation => configuration.Consolation,
                    _ => configuration.Participant,
                };
                CastleSiegeRewardItem.TryParseList(tier.Items, out var items, out _);
                string? masterLogin = null;
                if (guildMasters.TryGetValue(score.GuildId, out var masterName)
                    && await playerContext.GetAccountByCharacterNameAsync(masterName).ConfigureAwait(false) is { } masterAccount)
                {
                    masterLogin = masterAccount.LoginName;
                    if (tier.GuildMasterCoins > 0)
                    {
                        AddGrant(playerContext, masterAccount.GetId(), configuration.CoinType, tier.GuildMasterCoins, $"Castle Siege {placement}: {score.GuildName}, for the guild", $"castlesiege:{scoreboard.BattleId}:gm:{score.GuildId}");
                    }

                    vaultRewards.AddRange(items.Select(item => new CastleSiegeVaultReward
                    {
                        BattleId = scoreboard.BattleId,
                        LoginName = masterAccount.LoginName,
                        GuildName = score.GuildName,
                        Placement = placement.ToString()!,
                        ItemGroup = item.Group,
                        ItemNumber = item.Number,
                        ItemLevel = item.Level,
                        Count = item.Count,
                        CreatedAt = utcNow,
                    }));
                }
                else
                {
                    logger.LogWarning("Castle Siege: the guild master of {Guild} wasn't found; its guild master reward is skipped.", score.GuildName);
                }

                // An account is paid once, even if several of its characters fought.
                var members = memberAccounts.Where(m => m.GuildId == score.GuildId && paidMembers.Add(m.AccountId)).Select(m => m.AccountId).ToList();
                if (tier.MemberCoins > 0)
                {
                    foreach (var accountId in members)
                    {
                        AddGrant(playerContext, accountId, configuration.CoinType, tier.MemberCoins, $"Castle Siege {placement}: {score.GuildName}", $"castlesiege:{scoreboard.BattleId}:member:{accountId}");
                    }
                }

                rewards.Add(new
                {
                    guild = score.GuildName,
                    placement = placement.ToString(),
                    guildMasterCoins = masterLogin is null ? 0 : tier.GuildMasterCoins,
                    memberCoins = tier.MemberCoins,
                    members = members.Count,
                    items = masterLogin is null ? string.Empty : tier.Items,
                });
            }

            await playerContext.SaveChangesAsync().ConfigureAwait(false);
            if (plugIn.Repository is { } repository)
            {
                await repository.AddVaultRewardsAsync(vaultRewards).ConfigureAwait(false);
            }
            else if (vaultRewards.Count > 0)
            {
                logger.LogWarning("Castle Siege: the player website storage isn't available, so the vault rewards aren't given out.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Castle Siege: giving out the point mode rewards failed.");
        }
        finally
        {
            await SaveBattleAsync(plugIn, context, scoreboard, result, rewards).ConfigureAwait(false);
            context.Scoreboard = null;
        }
    }

    private static async ValueTask<List<(Guid GuildId, Guid AccountId)>> GetMemberAccountsAsync(CastleSiegeContext context, IPlayerContext playerContext)
    {
        // Members count when they were in the battle long enough; their points belong to their main guild.
        var eligible = context.ParticipantTracking.Values
            .Where(p => p.ParticipationTime.TotalSeconds >= context.Configuration.ParticipantRewardMinSeconds)
            .Select(p => (Participant: p, Main: context.FinalGuildList.TryGetValue(p.GuildId, out var guild) ? GetMainGuild(context, guild) : null))
            .Where(p => p.Main is not null)
            .ToList();
        if (eligible.Count == 0)
        {
            return [];
        }

        var accounts = await playerContext.GetAccountIdsByCharacterNamesAsync(eligible.Select(p => p.Participant.CharacterName).ToList()).ConfigureAwait(false);
        return eligible
            .Where(p => accounts.ContainsKey(p.Participant.CharacterName))
            .Select(p => (p.Main!.PersistentGuildId, accounts[p.Participant.CharacterName]))
            .ToList();
    }

    private static void AddGrant(IPlayerContext playerContext, Guid accountId, CashShopCoinType coinType, int amount, string reason, string reference)
    {
        var grant = playerContext.CreateNew<CashShopCoinGrant>();
        grant.AccountId = accountId;
        grant.CoinType = coinType;
        grant.Amount = amount;
        grant.Reason = reason;
        grant.Reference = reference;
        grant.GrantedBy = "Castle Siege";
        grant.CreatedAt = DateTime.UtcNow;
    }

    private static async ValueTask SaveBattleAsync(CastleSiegePointModePlugIn plugIn, CastleSiegeContext context, CastleSiegeScoreboard scoreboard, CastleSiegePointResult? result, IReadOnlyList<object>? rewards)
    {
        if (plugIn.Repository is not { } repository)
        {
            return;
        }

        try
        {
            var standings = result?.Standings ?? scoreboard.GetStandings();
            var battle = new CastleSiegeBattle
            {
                Id = scoreboard.BattleId,
                StartedAt = scoreboard.StartedAtUtc,
                EndedAt = result is null ? null : context.TimeProvider.GetUtcNow().UtcDateTime,
                HolderGuildName = scoreboard.HolderGuildName,
                WinnerGuildName = result?.WinnerGuildId is { } winnerId ? standings.FirstOrDefault(s => s.GuildId == winnerId)?.GuildName : null,
                ScoresJson = JsonSerializer.Serialize(
                    standings.Select(s => new
                    {
                        guild = s.GuildName,
                        points = s.Points,
                        seals = s.Seals,
                        switchKills = s.SwitchKills,
                        defenseTicks = s.DefenseTicks,
                        holds = s.GuildId == scoreboard.HolderGuildId,
                        placement = result?.PlacementOf(s.GuildId)?.ToString(),
                    }),
                    JsonOptions),
                RewardsJson = JsonSerializer.Serialize(rewards ?? [], JsonOptions),
            };
            scoreboard.IsDirty = false;
            await repository.SaveBattleAsync(battle).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            context.GameContext.LoggerFactory.CreateLogger(typeof(CastleSiegePointMode)).LogWarning(ex, "Castle Siege: storing the scoreboard failed.");
        }
    }

    private static async ValueTask PostScoreboardAsync(CastleSiegeContext context, CastleSiegeScoreboard scoreboard, bool final = false)
    {
        var standings = scoreboard.GetStandings().Take(5).ToList();
        if (standings.Count == 0)
        {
            return;
        }

        var remaining = context.RemainingTime;
        await SendToSiegeAsync(context, final ? Prefix + "Final scores:" : $"{Prefix}Scores, {(int)remaining.TotalMinutes} minutes left:", MessageType.BlueNormal).ConfigureAwait(false);
        var place = 1;
        foreach (var score in standings)
        {
            var holds = score.GuildId == scoreboard.HolderGuildId ? ", holds the castle" : string.Empty;
            await SendToSiegeAsync(context, $"{place++}. {score.GuildName}: {score.Points} points ({score.Seals} seals, {score.SwitchKills} switch kills{holds})", MessageType.BlueNormal).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends the running scoreboard to a player who just entered the siege map.
    /// </summary>
    /// <param name="context">The Castle Siege context.</param>
    /// <param name="player">The player.</param>
    public static async ValueTask SendScoreboardToAsync(CastleSiegeContext context, Player player)
    {
        if (context.Scoreboard is not { } scoreboard || !context.IsEventRunning)
        {
            return;
        }

        await SendScoreboardAsync(context, scoreboard, player, CastleSiegeScoreboardState.Running, context.TimeProvider.GetUtcNow().UtcDateTime).ConfigureAwait(false);
    }

    private static async ValueTask BroadcastScoreboardAsync(CastleSiegeContext context, CastleSiegeScoreboardState state)
    {
        if (context.Scoreboard is not { } scoreboard)
        {
            return;
        }

        var utcNow = context.TimeProvider.GetUtcNow().UtcDateTime;
        scoreboard.NextClientSyncUtc = utcNow + ClientSyncInterval;
        await context.ForEachSiegePlayerAsync(player => SendScoreboardAsync(context, scoreboard, player, state, utcNow).AsTask()).ConfigureAwait(false);
    }

    private static ValueTask SendScoreboardAsync(CastleSiegeContext context, CastleSiegeScoreboard scoreboard, Player player, CastleSiegeScoreboardState state, DateTime utcNow)
    {
        var ownGuild = GetMainGuild(context, player)?.PersistentGuildId;
        var lines = scoreboard.GetStandings()
            .Select(s => new CastleSiegeScoreboardLine(s.GuildName, s.Points, s.Seals, s.GuildId == scoreboard.HolderGuildId, s.GuildId == ownGuild))
            .ToList();
        TimeSpan? nextTick = null;
        if (state == CastleSiegeScoreboardState.Running
            && scoreboard.HolderGuildId is not null
            && GetPlugIn(context)?.Configuration is { DefenseTickMinutes: > 0 } configuration)
        {
            nextTick = scoreboard.HoldClockUtc.AddMinutes(configuration.DefenseTickMinutes) - utcNow;
        }

        var remaining = state == CastleSiegeScoreboardState.Running ? context.RemainingTime : TimeSpan.Zero;
        var view = new CastleSiegeScoreboardView(state, remaining, nextTick, lines);
        return player.InvokeViewPlugInAsync<IShowCastleSiegeScoreboardPlugIn>(p => p.ShowScoreboardAsync(view));
    }

    private static bool HeldSwitch(CastleSiegeContext context, Player victim, TimeSpan minimum, DateTime utcNow)
    {
        for (var i = 0; i < context.SwitchUsers.Length; i++)
        {
            if (ReferenceEquals(context.SwitchUsers[i], victim) && utcNow - context.SwitchHeldSinceUtc[i] >= minimum)
            {
                return true;
            }

            var released = context.ReleasedSwitchHolders[i];
            if (ReferenceEquals(released.Player, victim)
                && utcNow - released.ReleasedAtUtc <= ReleaseGrace
                && released.ReleasedAtUtc - released.HeldSinceUtc >= minimum)
            {
                return true;
            }
        }

        return false;
    }

    private static int PointsOf(CastleSiegeScoreboard scoreboard, Guid guildId)
        => scoreboard.GetStandings().FirstOrDefault(s => s.GuildId == guildId)?.Points ?? 0;

    private static CastleSiegePointModePlugIn? GetPlugIn(CastleSiegeContext context)
        => context.GameContext.FeaturePlugIns?.GetPlugIn<CastleSiegePointModePlugIn>();

    /// <summary>
    /// Gets the main guild of a guild: the alliance master on its side, which the points belong to.
    /// </summary>
    private static CastleSiegeGuildParticipant GetMainGuild(CastleSiegeContext context, CastleSiegeGuildParticipant guild)
        => context.FinalGuildList.Values.FirstOrDefault(g => g.Side == guild.Side && g.IsAllianceMaster) ?? guild;

    private static CastleSiegeGuildParticipant? GetMainGuild(CastleSiegeContext context, Player player)
        => player.GuildStatus is { } status && context.FinalGuildList.TryGetValue(status.GuildId, out var guild)
            ? GetMainGuild(context, guild)
            : null;

    private static ValueTask SendToSiegeAsync(CastleSiegeContext context, string message, MessageType messageType)
        => context.ForEachSiegePlayerAsync(player => player.InvokeViewPlugInAsync<Views.IShowMessagePlugIn>(p => p.ShowMessageAsync(message, messageType)).AsTask());
}
