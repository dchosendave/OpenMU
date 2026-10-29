// <copyright file="GrandResetMilestone.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

/// <summary>
/// A grand reset milestone, e.g. 5 grand resets: the player picks one part of the listed ancient sets
/// on the website, at the given item level. Some milestones also allow an excellent part instead.
/// </summary>
/// <param name="GrandResets">The number of grand resets.</param>
/// <param name="Level">The item level of the reward.</param>
/// <param name="AllowsExcellent">If set to <c>true</c>, the player may take the part as excellent item with 2 options instead.</param>
/// <param name="Sets">The names of the ancient sets, e.g. "Warrior".</param>
public sealed record GrandResetMilestone(int GrandResets, byte Level, bool AllowsExcellent, IReadOnlyList<string> Sets)
{
    /// <summary>
    /// Parses the milestones, e.g. "5 = +9 Warrior, Hyperion; 50 = +13 excellent *". Each milestone is
    /// the number of grand resets, then optionally the level and "excellent", then the set names; "*"
    /// stands for every set of the other milestones.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The milestones, by number of grand resets.</returns>
    public static IReadOnlyList<GrandResetMilestone> Parse(string? text)
    {
        var parsed = new List<(int GrandResets, byte Level, bool Excellent, List<string> Sets, bool All)>();
        foreach (var entry in (text ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], out var grandResets) || grandResets < 1)
            {
                continue;
            }

            byte level = 0;
            var excellent = false;
            var all = false;
            var sets = new List<string>();
            foreach (var word in parts[1].Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (word.StartsWith('+') && byte.TryParse(word[1..], out var wordLevel))
                {
                    level = Math.Min(wordLevel, (byte)15);
                }
                else if (word.Equals("excellent", StringComparison.OrdinalIgnoreCase))
                {
                    excellent = true;
                }
                else if (word == "*")
                {
                    all = true;
                }
                else
                {
                    sets.Add(word);
                }
            }

            parsed.Add((grandResets, level, excellent, sets, all));
        }

        var allSets = parsed.Where(p => !p.All).SelectMany(p => p.Sets).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return parsed
            .GroupBy(p => p.GrandResets)
            .Select(g => g.Last())
            .OrderBy(p => p.GrandResets)
            .Select(p => new GrandResetMilestone(p.GrandResets, p.Level, p.Excellent, p.All ? allSets.Union(p.Sets, StringComparer.OrdinalIgnoreCase).ToList() : p.Sets))
            .ToList();
    }

    /// <summary>
    /// Determines whether the ancient set belongs to this milestone.
    /// </summary>
    /// <param name="setName">The name of the set.</param>
    /// <returns><c>true</c>, if it does.</returns>
    public bool Includes(string setName) => this.Sets.Contains(setName, StringComparer.OrdinalIgnoreCase);
}
