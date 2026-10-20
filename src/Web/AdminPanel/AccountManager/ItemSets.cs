// <copyright file="ItemSets.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Finds the armor sets of the game. Like in the game client, the pieces of a set have the same item
/// number in the groups of helms (7), armors (8), pants (9), gloves (10) and boots (11). Some sets have no
/// helm (Magic Gladiator, Rage Fighter) or no gloves.
/// </summary>
public static class ItemSets
{
    /// <summary>
    /// The group of helms, the first group of armor pieces.
    /// </summary>
    public const byte FirstGroup = 7;

    /// <summary>
    /// The group of armors.
    /// </summary>
    public const byte ArmorGroup = 8;

    /// <summary>
    /// The group of boots, the last group of armor pieces.
    /// </summary>
    public const byte LastGroup = 11;

    /// <summary>
    /// Finds the armor sets among the item definitions.
    /// </summary>
    /// <param name="definitions">The item definitions.</param>
    /// <returns>The armor sets, ordered by item number.</returns>
    public static IReadOnlyList<ArmorSet> Find(IEnumerable<ItemDefinition> definitions) => definitions
        .Where(d => d.Group is >= FirstGroup and <= LastGroup)
        .GroupBy(d => d.Number)
        .Where(g => g.Count() >= 2)
        .Select(g =>
        {
            var pieces = g.OrderBy(d => d.Group).ToList();
            return new ArmorSet(NameOf(pieces), g.Key, pieces);
        })
        .OrderBy(s => s.Number)
        .ToList();

    /// <summary>
    /// Gets the name of a set from the names of its pieces without the piece ("Dragon Helm" is "Dragon").
    /// The most common one wins, which skips typos of the game data like "Red Sprit Armor".
    /// </summary>
    private static string NameOf(IReadOnlyList<ItemDefinition> pieces) => pieces
        .Select(p => p.Name.ToString() ?? string.Empty)
        .Select(n => n.LastIndexOf(' ') is var space and > 0 ? n[..space] : n)
        .GroupBy(n => n)
        .OrderByDescending(g => g.Count())
        .ThenBy(g => g.Key.Length)
        .First().Key;
}
