// <copyright file="IShowCastleSiegeScoreboardPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.CastleSiege;

/// <summary>
/// A view which shows the scoreboard of a point mode Castle Siege battle in the game client.
/// </summary>
public interface IShowCastleSiegeScoreboardPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the scoreboard, or hides it.
    /// </summary>
    /// <param name="scoreboard">The scoreboard.</param>
    /// <returns>A task which represents the asynchronous operation.</returns>
    ValueTask ShowScoreboardAsync(CastleSiegeScoreboardView scoreboard);
}

/// <summary>
/// What the scoreboard of a point mode Castle Siege battle shows.
/// </summary>
/// <param name="State">Whether the battle runs, just ended, or the scoreboard is hidden.</param>
/// <param name="Remaining">The remaining time of the battle.</param>
/// <param name="NextDefenseTick">The time until the castle holder's next defense tick; <c>null</c> if none is coming.</param>
/// <param name="Guilds">The guilds, best first.</param>
public sealed record CastleSiegeScoreboardView(
    CastleSiegeScoreboardState State,
    TimeSpan Remaining,
    TimeSpan? NextDefenseTick,
    IReadOnlyList<CastleSiegeScoreboardLine> Guilds)
{
    /// <summary>
    /// Gets the view which hides the scoreboard.
    /// </summary>
    public static CastleSiegeScoreboardView Hidden { get; } = new(CastleSiegeScoreboardState.Hidden, TimeSpan.Zero, null, []);
}

/// <summary>
/// A guild on the Castle Siege scoreboard.
/// </summary>
/// <param name="GuildName">The name of the main guild.</param>
/// <param name="Points">The points.</param>
/// <param name="Seals">The successful seals.</param>
/// <param name="HoldsCastle">Whether the guild holds the castle right now.</param>
/// <param name="IsOwnGuild">Whether it's the receiving player's (main) guild.</param>
public sealed record CastleSiegeScoreboardLine(string GuildName, int Points, int Seals, bool HoldsCastle, bool IsOwnGuild);

/// <summary>
/// The states of the Castle Siege scoreboard.
/// </summary>
public enum CastleSiegeScoreboardState : byte
{
    /// <summary>The scoreboard is hidden.</summary>
    Hidden = 0,

    /// <summary>The battle runs.</summary>
    Running = 1,

    /// <summary>The battle ended; these are the final scores.</summary>
    Final = 2,
}
