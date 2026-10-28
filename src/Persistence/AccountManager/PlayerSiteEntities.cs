// <copyright file="PlayerSiteEntities.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// A daily check-in of an account on the player website.
/// </summary>
public class WebCheckIn
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the login name.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets the day (UTC); unique per account.</summary>
    public DateOnly Day { get; set; }

    /// <summary>Gets or sets the day of the streak, 1 based.</summary>
    public int StreakDay { get; set; }

    /// <summary>Gets or sets the reward.</summary>
    public int Reward { get; set; }

    /// <summary>Gets or sets the coin type of the reward.</summary>
    public string CoinType { get; set; } = string.Empty;
}

/// <summary>
/// A news post of the player website, written by a game master.
/// </summary>
public class WebNewsPost
{
    /// <summary>The maximum length of the <see cref="Title"/>.</summary>
    public const int TitleMaximumLength = 120;

    /// <summary>The maximum length of the <see cref="Summary"/>.</summary>
    public const int SummaryMaximumLength = 400;

    /// <summary>The maximum length of the <see cref="Body"/>.</summary>
    public const int BodyMaximumLength = 20_000;

    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the slug for the address, unique.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the tag: Update, Event or Notice.</summary>
    public string Tag { get; set; } = "Notice";

    /// <summary>Gets or sets the summary for the news list.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets the text, in Markdown.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether players can see the post.</summary>
    public bool IsPublished { get; set; } = true;

    /// <summary>Gets or sets when the post was published (UTC).</summary>
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the login name of the game master who wrote or last changed the post.</summary>
    public string Author { get; set; } = string.Empty;
}

/// <summary>
/// An item which a player offers on the player market. While it's listed, the item lies in the vault of the
/// market's escrow account (<see cref="EscrowLogin"/>), which nobody can log in to.
/// </summary>
public class WebMarketListing
{
    /// <summary>The state of a listing which can be bought.</summary>
    public const string Active = "active";

    /// <summary>The state of a listing which is being bought right now.</summary>
    public const string Pending = "pending";

    /// <summary>The state of a sold listing.</summary>
    public const string Sold = "sold";

    /// <summary>The state of a listing which the seller took back.</summary>
    public const string Cancelled = "cancelled";

    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the login name of the seller.</summary>
    public string SellerLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets the login name of the escrow account which holds the item while it's listed.</summary>
    public string EscrowLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets the identifier of the item.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the name of the item, for searching.</summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>Gets or sets the price.</summary>
    public int Price { get; set; }

    /// <summary>Gets or sets the coin type of the price.</summary>
    public string CoinType { get; set; } = string.Empty;

    /// <summary>Gets or sets the state, see the constants of this class.</summary>
    public string State { get; set; } = Active;

    /// <summary>Gets or sets the login name of the buyer.</summary>
    public string? BuyerLogin { get; set; }

    /// <summary>Gets or sets when the item was listed (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets when the item was sold or taken back (UTC).</summary>
    public DateTime? ClosedAt { get; set; }
}
