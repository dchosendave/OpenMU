// <copyright file="CastleSiegeEntities.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// A point mode Castle Siege battle: its live scoreboard while it runs, and its result afterwards.
/// </summary>
public class CastleSiegeBattle
{
    /// <summary>Gets or sets the id.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the start of the battle (UTC).</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>Gets or sets the end of the battle (UTC); <c>null</c> while it runs.</summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>Gets or sets the name of the guild holding the castle right now, or at the end.</summary>
    public string? HolderGuildName { get; set; }

    /// <summary>Gets or sets the name of the winning guild.</summary>
    public string? WinnerGuildName { get; set; }

    /// <summary>
    /// Gets or sets the scores as JSON: a list of <c>{ guild, points, seals, switchKills, defenseTicks, placement }</c>, best first.
    /// </summary>
    public string ScoresJson { get; set; } = "[]";

    /// <summary>Gets or sets the rewards as JSON, for the history: a list of <c>{ guild, placement, guildMasterCoins, memberCoins, members, items }</c>.</summary>
    public string RewardsJson { get; set; } = "[]";
}

/// <summary>
/// Items of a Castle Siege reward which wait to be put into the vault of a guild master's account. The
/// player website delivers them while the account is offline.
/// </summary>
public class CastleSiegeVaultReward
{
    /// <summary>Gets or sets the id.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the battle.</summary>
    public Guid BattleId { get; set; }

    /// <summary>Gets or sets the login name of the receiving account.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets the name of the rewarded guild.</summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>Gets or sets the placement, e.g. "Winner".</summary>
    public string Placement { get; set; } = string.Empty;

    /// <summary>Gets or sets the item group.</summary>
    public byte ItemGroup { get; set; }

    /// <summary>Gets or sets the item number.</summary>
    public short ItemNumber { get; set; }

    /// <summary>Gets or sets the item level.</summary>
    public byte ItemLevel { get; set; }

    /// <summary>Gets or sets the number of pieces.</summary>
    public int Count { get; set; }

    /// <summary>Gets or sets when the reward was created (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets when the items were put into the vault (UTC); <c>null</c> while pending.</summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>Gets or sets why the last delivery attempt failed, e.g. a full vault.</summary>
    public string? LastProblem { get; set; }
}
