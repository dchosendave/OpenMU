// <copyright file="UnavailablePlayerSiteRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using System.Threading;
using MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// The <see cref="IPlayerSiteRepository"/> without a database, e.g. in demo mode.
/// </summary>
public class UnavailablePlayerSiteRepository : IPlayerSiteRepository
{
    private const string NotAvailableMessage = "The player website storage is not available.";

    /// <inheritdoc />
    public ValueTask<WebCheckIn?> GetLastCheckInAsync(string loginName, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    /// <inheritdoc />
    public ValueTask<bool> TryAddCheckInAsync(WebCheckIn checkIn, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    /// <inheritdoc />
    public ValueTask<bool> SaveNewsPostAsync(WebNewsPost post, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    /// <inheritdoc />
    public ValueTask DeleteNewsPostAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    /// <inheritdoc />
    public ValueTask<WebMarketListing?> GetListingAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    /// <inheritdoc />
    public ValueTask<int> CountActiveListingsAsync(string sellerLogin, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    /// <inheritdoc />
    public ValueTask AddListingAsync(WebMarketListing listing, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    /// <inheritdoc />
    public ValueTask<bool> TryChangeListingStateAsync(Guid id, string expectedState, string newState, string? buyerLogin, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    public ValueTask SaveFlashSaleAsync(WebFlashSale sale, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    public ValueTask DeleteFlashSaleAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    public ValueTask<WebFlashSale?> GetFlashSaleAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    public ValueTask<bool> TryTakeFlashSaleStockAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    public ValueTask ReturnFlashSaleStockAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    public ValueTask<bool> TryTakeShopStockAsync(Guid itemPackageId, DateTime weekStart, int weeklyStock, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    public ValueTask ReturnShopStockAsync(Guid itemPackageId, DateTime weekStart, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    public ValueTask<bool> TryAddMilestoneClaimAsync(WebMilestoneClaim claim, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    public ValueTask<WebMilestoneClaim?> GetMilestoneClaimAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    public ValueTask<IReadOnlyList<WebMilestoneClaim>> GetMilestoneClaimsAsync(string loginName, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    public ValueTask<bool> TryMarkMilestoneClaimedAsync(Guid id, string claimedItem, DateTime utcNow, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    public ValueTask ReopenMilestoneClaimAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);
}
