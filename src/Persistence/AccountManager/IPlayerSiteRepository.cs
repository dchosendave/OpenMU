// <copyright file="IPlayerSiteRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AccountManager;

using System.Threading;

/// <summary>
/// The stored data of the player website: check-ins, news and market listings.
/// </summary>
public interface IPlayerSiteRepository
{
    /// <summary>Gets the latest check-in of an account.</summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The check-in, or <c>null</c>.</returns>
    ValueTask<WebCheckIn?> GetLastCheckInAsync(string loginName, CancellationToken cancellationToken = default);

    /// <summary>Adds a check-in; fails if the account already checked in that day.</summary>
    /// <param name="checkIn">The check-in.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> if it was added; <c>false</c> if the account already checked in that day.</returns>
    ValueTask<bool> TryAddCheckInAsync(WebCheckIn checkIn, CancellationToken cancellationToken = default);

    /// <summary>Adds or replaces a news post.</summary>
    /// <param name="post">The post.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>false</c> if another post has the same slug.</returns>
    ValueTask<bool> SaveNewsPostAsync(WebNewsPost post, CancellationToken cancellationToken = default);

    /// <summary>Deletes a news post.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask DeleteNewsPostAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets a market listing.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The listing, or <c>null</c>.</returns>
    ValueTask<WebMarketListing?> GetListingAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Counts the active listings of a seller.</summary>
    /// <param name="sellerLogin">The login name of the seller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count.</returns>
    ValueTask<int> CountActiveListingsAsync(string sellerLogin, CancellationToken cancellationToken = default);

    /// <summary>Adds a listing.</summary>
    /// <param name="listing">The listing.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask AddListingAsync(WebMarketListing listing, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the state of a listing, but only if it's in the expected state; so of two buyers only one
    /// gets it.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="expectedState">The state the listing must have.</param>
    /// <param name="newState">The new state.</param>
    /// <param name="buyerLogin">The buyer; <c>null</c> keeps it, an empty string clears it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> if the state was changed.</returns>
    ValueTask<bool> TryChangeListingStateAsync(Guid id, string expectedState, string newState, string? buyerLogin, CancellationToken cancellationToken = default);

    /// <summary>Adds or updates a flash sale.</summary>
    /// <param name="sale">The sale.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    ValueTask SaveFlashSaleAsync(WebFlashSale sale, CancellationToken cancellationToken = default);

    /// <summary>Deletes a flash sale.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    ValueTask DeleteFlashSaleAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets a flash sale.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The sale, or <c>null</c>.</returns>
    ValueTask<WebFlashSale?> GetFlashSaleAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes one piece of the stock of a flash sale, if the sale is open at <paramref name="utcNow"/> and
    /// has stock left. Concurrent buyers can't take the same last piece.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="utcNow">The current time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c>, if a piece was taken.</returns>
    ValueTask<bool> TryTakeFlashSaleStockAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>Puts a piece back into the stock of a flash sale, after a purchase failed.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    ValueTask ReturnFlashSaleStockAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a grand reset milestone reward, unless the character already has one for this milestone.
    /// </summary>
    /// <param name="claim">The claim.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c>, if it was added.</returns>
    ValueTask<bool> TryAddMilestoneClaimAsync(WebMilestoneClaim claim, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a grand reset milestone reward.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The claim, or <c>null</c>.</returns>
    ValueTask<WebMilestoneClaim?> GetMilestoneClaimAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the grand reset milestone rewards of an account, newest first.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The claims.</returns>
    ValueTask<IReadOnlyList<WebMilestoneClaim>> GetMilestoneClaimsAsync(string loginName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an open milestone reward as picked. Only one caller succeeds.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="claimedItem">The picked item.</param>
    /// <param name="utcNow">The current time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c>, if this call marked it.</returns>
    ValueTask<bool> TryMarkMilestoneClaimedAsync(Guid id, string claimedItem, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a milestone reward again, after its item couldn't be delivered.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    ValueTask ReopenMilestoneClaimAsync(Guid id, CancellationToken cancellationToken = default);
}
