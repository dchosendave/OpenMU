// <copyright file="WebFlashSale.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// A flash sale of the web shop: an item package for a short time and in a small stock, first come,
/// first served. Website administrators set them up.
/// </summary>
public class WebFlashSale
{
    /// <summary>The longest title.</summary>
    public const int TitleMaximumLength = 60;

    /// <summary>Gets or sets the id.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the title shown on the website, e.g. "Random excellent part".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the item package which is sold.</summary>
    public Guid ItemPackageId { get; set; }

    /// <summary>Gets or sets a value indicating whether a buyer gets one random item of the package instead of all of it.</summary>
    public bool PickOneRandomItem { get; set; }

    /// <summary>Gets or sets the price.</summary>
    public int Price { get; set; }

    /// <summary>Gets or sets the currency of the price.</summary>
    public WebShopCurrency Currency { get; set; } = WebShopCurrency.WCoinC;

    /// <summary>Gets or sets how many can be bought in total.</summary>
    public int Stock { get; set; }

    /// <summary>Gets or sets how many were bought so far.</summary>
    public int Sold { get; set; }

    /// <summary>Gets or sets when the sale opens (UTC).</summary>
    public DateTime StartsAt { get; set; }

    /// <summary>Gets or sets how long the sale stays open, in seconds.</summary>
    public int DurationSeconds { get; set; }

    /// <summary>Gets or sets the login name of the administrator who set it up.</summary>
    public string CreatedBy { get; set; } = string.Empty;
}
