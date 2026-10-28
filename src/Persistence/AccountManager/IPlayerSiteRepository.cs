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
}
