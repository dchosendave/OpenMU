// <copyright file="WebMiniGamePlayRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AccountManager;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.Persistence.AdminAuth;
using MUnique.OpenMU.Persistence.EntityFramework.AdminAuth;

/// <summary>
/// The plays of the mini games of the player website, in the schema of the admin panel.
/// </summary>
public class WebMiniGamePlayRepository : IWebMiniGamePlayRepository
{
    private readonly IAdminUserRepository _adminUserRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebMiniGamePlayRepository"/> class.
    /// </summary>
    /// <param name="adminUserRepository">The admin user repository, which creates and migrates the schema.</param>
    public WebMiniGamePlayRepository(IAdminUserRepository adminUserRepository)
    {
        this._adminUserRepository = adminUserRepository;
    }

    /// <inheritdoc />
    public async ValueTask<long> GetBetSumAsync(string loginName, DateTime since, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);
        await using var context = new AdminPanelContext();
        return await context.WebMiniGamePlays
            .Where(p => p.LoginName == loginName && p.PlayedAt >= since)
            .SumAsync(p => (long)p.Bet, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask AddAsync(WebMiniGamePlay play, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);
        await using var context = new AdminPanelContext();
        context.WebMiniGamePlays.Add(play);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask UpdateResultAsync(Guid id, int payout, string result, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);
        await using var context = new AdminPanelContext();
        await context.WebMiniGamePlays
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Payout, payout).SetProperty(p => p.Result, result), cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask EnsureAvailableStorageAsync(CancellationToken cancellationToken)
    {
        if (!await this._adminUserRepository.EnsureStorageAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The mini game storage is not available. Please check the database connection.");
        }
    }
}
