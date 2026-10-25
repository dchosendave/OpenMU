// <copyright file="ItemPackageItem.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// An item of an <see cref="ItemPackage"/>: the kind of item with all its options.
/// </summary>
public class ItemPackageItem
{
    /// <summary>
    /// The maximum length of the <see cref="Name"/>.
    /// </summary>
    public const int NameMaximumLength = 100;

    /// <summary>
    /// Gets or sets the identifier of this item.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the identifier of the package which contains this item.
    /// </summary>
    public Guid ItemPackageId { get; set; }

    /// <summary>
    /// Gets or sets the position of the item within its package.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the group of the item definition.
    /// </summary>
    public byte Group { get; set; }

    /// <summary>
    /// Gets or sets the number of the item definition within its <see cref="Group"/>.
    /// </summary>
    public short Number { get; set; }

    /// <summary>
    /// Gets or sets the name of the item when the package was saved.
    /// It's only used to describe an item which the server doesn't know (anymore).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item level.
    /// </summary>
    public byte Level { get; set; }

    /// <summary>
    /// Gets or sets the durability, or the stack size of stackable items.
    /// </summary>
    public double Durability { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item has its skill.
    /// </summary>
    public bool HasSkill { get; set; }

    /// <summary>
    /// Gets or sets the number of sockets.
    /// </summary>
    public int SocketCount { get; set; }

    /// <summary>
    /// Gets or sets the options of the item (excellent, luck, normal option, harmony, sockets, ...).
    /// </summary>
    public List<ItemPackageItemOption> Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the identifiers of the <c>ItemOfItemSet</c>s, which define the membership in an ancient set.
    /// </summary>
    public List<Guid> SetItemIds { get; set; } = new();

    /// <summary>
    /// Creates an independent copy of this item, with new identifiers.
    /// </summary>
    /// <returns>The copy.</returns>
    public ItemPackageItem Clone() => new()
    {
        Position = this.Position,
        Group = this.Group,
        Number = this.Number,
        Name = this.Name,
        Level = this.Level,
        Durability = this.Durability,
        HasSkill = this.HasSkill,
        SocketCount = this.SocketCount,
        Options = this.Options.Select(option => option.Clone()).ToList(),
        SetItemIds = this.SetItemIds.ToList(),
    };

    /// <inheritdoc />
    public override string ToString() => this.Level > 0 ? $"{this.Name} +{this.Level}" : this.Name;
}
