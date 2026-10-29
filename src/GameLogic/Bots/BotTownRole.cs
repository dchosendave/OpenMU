// <copyright file="BotTownRole.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Bots;

/// <summary>
/// What a bot does with its time: hunt like usual, or hang around Lorencia town.
/// </summary>
internal enum BotTownRole
{
    /// <summary>The bot hunts, the normal routine.</summary>
    None,

    /// <summary>The bot strolls around Lorencia town, pausing now and then.</summary>
    Stroller,

    /// <summary>The bot stands somewhere in Lorencia town for minutes at a time, like a player who is away.</summary>
    Afk,

    /// <summary>The bot stands in the line of companions for hire in Lorencia, until a player hires it.</summary>
    ForHire,
}

/// <summary>
/// Picks the <see cref="BotTownRole"/> of a bot character (see <see cref="BotConfiguration.TownIdlerPercent"/>).
/// </summary>
internal static class BotTownRoles
{
    /// <summary>The number of the map the town bots hang around on.</summary>
    public const short LorenciaMapNumber = 0;

    /// <summary>
    /// Gets the role of the character. It depends on the name only, so a bot keeps its role over
    /// restarts and presence rotation, and changing the percentages only moves the bots at the edge.
    /// </summary>
    /// <param name="characterName">The name of the bot character.</param>
    /// <param name="configuration">The bot configuration.</param>
    /// <returns>The role.</returns>
    public static BotTownRole GetRole(string? characterName, BotConfiguration? configuration)
    {
        if (characterName is null || configuration is null)
        {
            return BotTownRole.None;
        }

        var hash = StableHash(characterName);
        if ((hash / 10000) % 100 < Math.Clamp(configuration.CompanionsForHirePercent, 0, 100))
        {
            return BotTownRole.ForHire;
        }

        if (hash % 100 >= Math.Clamp(configuration.TownIdlerPercent, 0, 100))
        {
            return BotTownRole.None;
        }

        return (hash / 100) % 100 < Math.Clamp(configuration.TownAfkPercent, 0, 100)
            ? BotTownRole.Afk
            : BotTownRole.Stroller;
    }

    /// <summary>
    /// Gets the place of a companion for hire in its line, from 0 to 9.
    /// </summary>
    /// <param name="characterName">The name of the bot character.</param>
    /// <returns>The place in the line.</returns>
    public static int LinePlace(string characterName) => (int)((StableHash(characterName) / 1000000) % 10);

    /// <summary>
    /// FNV-1a over the lower-cased name: string.GetHashCode differs between processes.
    /// </summary>
    private static uint StableHash(string value)
    {
        var hash = 2166136261u;
        foreach (var c in value.ToLowerInvariant())
        {
            hash = (hash ^ c) * 16777619u;
        }

        return hash;
    }
}
