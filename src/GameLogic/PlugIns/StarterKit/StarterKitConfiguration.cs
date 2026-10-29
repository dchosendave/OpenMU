// <copyright file="StarterKitConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.StarterKit;

using System.ComponentModel.DataAnnotations;
using System.Globalization;

/// <summary>
/// The configuration of the <see cref="StarterKitPlugIn"/>: the items every new character of a class
/// starts with. Items are comma separated as <c>group:number</c>; armor, weapons, shields and rings get
/// <see cref="ItemLevel"/> and their 2 weakest excellent options, wings and pets stay plain.
/// </summary>
public class StarterKitConfiguration
{
    /// <summary>Gets or sets the level of the armor, weapons, shields and rings (capped by each item's maximum).</summary>
    [Display(Name = "Item level")]
    [Range(0, 15)]
    public byte ItemLevel { get; set; } = 15;

    /// <summary>Gets or sets a value indicating whether the armor, weapons, shields and rings are excellent.</summary>
    [Display(Name = "Excellent (2 weakest options)")]
    public bool Excellent { get; set; } = true;

    /// <summary>Gets or sets the items every class gets, e.g. the Pet Panda and the Panda Ring.</summary>
    [Display(Name = "Every class", Description = "Comma separated group:number, e.g. 13:80 for the Pet Panda.")]
    public string AllClasses { get; set; } = "13:80, 13:76";

    /// <summary>Gets or sets the kit of the Dark Wizard.</summary>
    [Display(Name = "Dark Wizard")]
    public string DarkWizard { get; set; } = "7:2, 8:2, 9:2, 10:2, 11:2, 5:0, 12:1, 13:24, 13:24";

    /// <summary>Gets or sets the kit of the Dark Knight.</summary>
    [Display(Name = "Dark Knight")]
    public string DarkKnight { get; set; } = "7:5, 8:5, 9:5, 10:5, 11:5, 0:1, 0:1, 12:2, 13:23, 13:23";

    /// <summary>Gets or sets the kit of the Fairy Elf.</summary>
    [Display(Name = "Fairy Elf")]
    public string FairyElf { get; set; } = "7:10, 8:10, 9:10, 10:10, 11:10, 4:0, 4:15, 12:0, 13:24, 13:24";

    /// <summary>Gets or sets the kit of the Magic Gladiator.</summary>
    [Display(Name = "Magic Gladiator")]
    public string MagicGladiator { get; set; } = "8:2, 9:2, 10:2, 11:2, 0:1, 6:0, 12:1, 13:23, 13:23";

    /// <summary>Gets or sets the kit of the Dark Lord.</summary>
    [Display(Name = "Dark Lord")]
    public string DarkLord { get; set; } = "7:5, 8:5, 9:5, 10:5, 11:5, 2:0, 6:0, 13:30, 13:23, 13:23";

    /// <summary>Gets or sets the kit of the Summoner.</summary>
    [Display(Name = "Summoner")]
    public string Summoner { get; set; } = "7:39, 8:39, 9:39, 10:39, 11:39, 5:14, 5:21, 12:41, 13:24, 13:24";

    /// <summary>Gets or sets the kit of the Rage Fighter.</summary>
    [Display(Name = "Rage Fighter")]
    public string RageFighter { get; set; } = "7:5, 8:5, 9:5, 11:5, 0:32, 0:32, 12:49, 13:23, 13:23";

    /// <summary>
    /// Gets the items of the kit for a character class: the class's own list and the one for every class.
    /// </summary>
    /// <param name="characterClassNumber">The number of the character class, e.g. 4 for the Dark Knight.</param>
    /// <returns>The items as (group, number).</returns>
    public IReadOnlyList<(byte Group, short Number)> ItemsOf(byte characterClassNumber)
    {
        var classKit = characterClassNumber switch
        {
            0 => this.DarkWizard,
            4 => this.DarkKnight,
            8 => this.FairyElf,
            12 => this.MagicGladiator,
            16 => this.DarkLord,
            20 => this.Summoner,
            24 => this.RageFighter,
            _ => string.Empty,
        };

        return Parse(classKit).Concat(Parse(this.AllClasses)).ToList();
    }

    private static IEnumerable<(byte Group, short Number)> Parse(string text)
    {
        foreach (var entry in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length == 2
                && byte.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var group)
                && short.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                yield return (group, number);
            }
        }
    }
}
