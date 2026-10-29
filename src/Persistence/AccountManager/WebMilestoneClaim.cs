// <copyright file="WebMilestoneClaim.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// A grand reset milestone reward of a character, e.g. for 5 grand resets. The player picks the item on
/// the website, which puts it into the vault of the account.
/// </summary>
public class WebMilestoneClaim
{
    /// <summary>
    /// The maximum length of <see cref="ClaimedItem"/>.
    /// </summary>
    public const int ClaimedItemMaximumLength = 80;

    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the login name of the account.
    /// </summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the character which reached the milestone.
    /// </summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of grand resets of the milestone.
    /// </summary>
    public int GrandResets { get; set; }

    /// <summary>
    /// Gets or sets when the milestone was reached.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when the reward was picked; <c>null</c> while it's open.
    /// </summary>
    public DateTime? ClaimedAt { get; set; }

    /// <summary>
    /// Gets or sets the item the player picked, e.g. "Warrior Leather Armor +9".
    /// </summary>
    public string? ClaimedItem { get; set; }
}
