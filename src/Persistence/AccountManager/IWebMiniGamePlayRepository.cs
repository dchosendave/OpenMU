// <copyright file="IWebMiniGamePlayRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AccountManager;

using System.Threading;

/// <summary>
/// The stored plays of the mini games of the player website.
/// </summary>
public interface IWebMiniGamePlayRepository
{
    /// <summary>
    /// Gets the sum of the bets of an account since a point in time.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="since">The point in time (UTC).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The sum of the bets.</returns>
    ValueTask<long> GetBetSumAsync(string loginName, DateTime since, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a play.
    /// </summary>
    /// <param name="play">The play.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask AddAsync(WebMiniGamePlay play, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the payout and result of a play, e.g. when a hi-lo round ends.
    /// </summary>
    /// <param name="id">The identifier of the play.</param>
    /// <param name="payout">The payout.</param>
    /// <param name="result">The result as JSON.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask UpdateResultAsync(Guid id, int payout, string result, CancellationToken cancellationToken = default);
}
