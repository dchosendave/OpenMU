// <copyright file="ItemPackage.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// A named set of items and zen which the account manager of the admin panel adds to an account in one go,
/// e.g. a freebie set, a donation reward or an event prize.
/// </summary>
/// <remarks>
/// Packages are a tool of the admin panel, not game data, so they live in the schema of the admin panel.
/// Their items reference the game configuration only by the ids of item definitions, options and ancient sets,
/// which are the same on every server which was initialized with the same data.
/// </remarks>
public class ItemPackage
{
    /// <summary>
    /// The maximum length of the <see cref="Name"/>.
    /// </summary>
    public const int NameMaximumLength = 100;

    /// <summary>
    /// The maximum length of the <see cref="Description"/>.
    /// </summary>
    public const int DescriptionMaximumLength = 1000;

    /// <summary>
    /// Gets or sets the identifier of this package.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the name, which is unique.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description, e.g. what a donation tier costs.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the zen which applying the package adds to the storage.
    /// </summary>
    public int Zen { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether players can buy this package in the web shop of the player website.
    /// </summary>
    public bool IsInWebShop { get; set; }

    /// <summary>
    /// Gets or sets the price in the web shop, in units of the <see cref="WebShopCurrency"/>.
    /// </summary>
    public int WebShopPrice { get; set; }

    /// <summary>
    /// Gets or sets the currency in which the web shop sells this package.
    /// </summary>
    public WebShopCurrency WebShopCurrency { get; set; }

    /// <summary>
    /// Gets or sets the date and time when this package has been created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the items of the package, in the order of their <see cref="ItemPackageItem.Position"/>.
    /// </summary>
    public List<ItemPackageItem> Items { get; set; } = new();

    /// <summary>
    /// Creates an independent copy of this package.
    /// </summary>
    /// <param name="newId">If set to <c>true</c>, the copy gets a new identifier and creation date.</param>
    /// <returns>The copy.</returns>
    public ItemPackage Clone(bool newId = false)
    {
        return new ItemPackage
        {
            Id = newId ? Guid.NewGuid() : this.Id,
            Name = this.Name,
            Description = this.Description,
            Zen = this.Zen,
            IsInWebShop = this.IsInWebShop,
            WebShopPrice = this.WebShopPrice,
            WebShopCurrency = this.WebShopCurrency,
            CreatedAt = newId ? DateTime.UtcNow : this.CreatedAt,
            Items = this.Items.Select(item => item.Clone()).ToList(),
        };
    }

    /// <inheritdoc />
    public override string ToString() => this.Name;
}
