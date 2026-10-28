// <copyright file="UnavailableWebMiniGamePlayRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using System.Threading;
using MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// The <see cref="IWebMiniGamePlayRepository"/> without a database, e.g. in demo mode: the games can't be played.
/// </summary>
public class UnavailableWebMiniGamePlayRepository : IWebMiniGamePlayRepository
{
    private const string NotAvailableMessage = "The mini game storage is not available.";

    /// <inheritdoc />
    public ValueTask<long> GetBetSumAsync(string loginName, DateTime since, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    /// <inheritdoc />
    public ValueTask AddAsync(WebMiniGamePlay play, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    /// <inheritdoc />
    public ValueTask UpdateResultAsync(Guid id, int payout, string result, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);
}
