// <copyright file="ItemPackageItemOption.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// An option of an <see cref="ItemPackageItem"/>, which references an <c>IncreasableItemOption</c> of the game configuration.
/// </summary>
public class ItemPackageItemOption
{
    /// <summary>
    /// Gets or sets the identifier of this option.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the identifier of the item which has this option.
    /// </summary>
    public Guid ItemPackageItemId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the <c>IncreasableItemOption</c>.
    /// </summary>
    public Guid OptionId { get; set; }

    /// <summary>
    /// Gets or sets the level of the option.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Gets or sets the index of the option, e.g. the socket of a socket option.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Creates an independent copy of this option, with a new identifier.
    /// </summary>
    /// <returns>The copy.</returns>
    public ItemPackageItemOption Clone() => new()
    {
        OptionId = this.OptionId,
        Level = this.Level,
        Index = this.Index,
    };
}
