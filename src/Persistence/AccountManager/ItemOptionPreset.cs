// <copyright file="ItemOptionPreset.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// A set of item options which items get when the account manager of the admin panel adds them,
/// e.g. "+15, luck, skill, +16", or which is applied to several items of a package at once.
/// Options which an item can't have are left out.
/// </summary>
public class ItemOptionPreset
{
    /// <summary>
    /// The highest normal option level of any item (+28). Items with a lower maximum get theirs.
    /// </summary>
    public const int MaximumOptionLevel = 7;

    /// <summary>
    /// The maximum length of the <see cref="Name"/>.
    /// </summary>
    public const int NameMaximumLength = 100;

    /// <summary>
    /// Gets or sets the identifier of this preset.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item level, or <c>null</c> to keep the level which was chosen when adding the item (or the level of the item).
    /// </summary>
    public byte? Level { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether items get their skill.
    /// </summary>
    public bool Skill { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether items get luck.
    /// </summary>
    public bool Luck { get; set; }

    /// <summary>
    /// Gets or sets the level of the normal option: 0 is none, 1 is +4 up to 7 which is +28.
    /// It's capped at what the item allows.
    /// </summary>
    public int OptionLevel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether items get every excellent option (wing options on wings).
    /// </summary>
    public bool Excellent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether items which can have it get the 380 (guardian) option.
    /// </summary>
    public bool Guardian { get; set; }

    /// <summary>
    /// Gets or sets the date and time when this preset has been created. Presets are shown in this order.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates an independent copy of this preset, with the same identifier.
    /// </summary>
    /// <returns>The copy.</returns>
    public ItemOptionPreset Clone() => (ItemOptionPreset)this.MemberwiseClone();

    /// <inheritdoc />
    public override string ToString() => this.Name;
}
