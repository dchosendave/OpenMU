// <copyright file="PlayerSiteRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AccountManager;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.Persistence.AdminAuth;
using MUnique.OpenMU.Persistence.EntityFramework.AdminAuth;

/// <summary>
/// The data of the player website, in the schema of the admin panel.
/// </summary>
public class PlayerSiteRepository : IPlayerSiteRepository
{
    private readonly IAdminUserRepository _adminUserRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerSiteRepository"/> class.
    /// </summary>
    /// <param name="adminUserRepository">The admin user repository, which creates and migrates the schema.</param>
    public PlayerSiteRepository(IAdminUserRepository adminUserRepository)
    {
        this._adminUserRepository = adminUserRepository;
    }

    /// <inheritdoc />
    public async ValueTask<WebCheckIn?> GetLastCheckInAsync(string loginName, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.WebCheckIns.AsNoTracking()
            .Where(c => c.LoginName == loginName)
            .OrderByDescending(c => c.Day)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryAddCheckInAsync(WebCheckIn checkIn, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        context.WebCheckIns.Add(checkIn);
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            // The unique index on login name and day: this account already checked in today.
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> SaveNewsPostAsync(WebNewsPost post, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        if (await context.WebNewsPosts.AnyAsync(p => p.Slug == post.Slug && p.Id != post.Id, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (await context.WebNewsPosts.AnyAsync(p => p.Id == post.Id, cancellationToken).ConfigureAwait(false))
        {
            context.WebNewsPosts.Update(post);
        }
        else
        {
            context.WebNewsPosts.Add(post);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async ValueTask DeleteNewsPostAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        await context.WebNewsPosts.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<WebMarketListing?> GetListingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.WebMarketListings.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<int> CountActiveListingsAsync(string sellerLogin, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.WebMarketListings
            .CountAsync(l => l.SellerLogin == sellerLogin && (l.State == WebMarketListing.Active || l.State == WebMarketListing.Pending), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask AddListingAsync(WebMarketListing listing, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        context.WebMarketListings.Add(listing);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryChangeListingStateAsync(Guid id, string expectedState, string newState, string? buyerLogin, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        var closed = newState is WebMarketListing.Sold or WebMarketListing.Cancelled ? DateTime.UtcNow : (DateTime?)null;
        var changed = await context.WebMarketListings
            .Where(l => l.Id == id && l.State == expectedState)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(l => l.State, newState)
                    .SetProperty(l => l.BuyerLogin, l => buyerLogin == null ? l.BuyerLogin : buyerLogin == string.Empty ? null : buyerLogin)
                    .SetProperty(l => l.ClosedAt, l => closed ?? l.ClosedAt),
                cancellationToken)
            .ConfigureAwait(false);
        return changed == 1;
    }

    private async ValueTask<AdminPanelContext> CreateContextAsync(CancellationToken cancellationToken)
    {
        if (!await this._adminUserRepository.EnsureStorageAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The player website storage is not available. Please check the database connection.");
        }

        return new AdminPanelContext();
    }
}
