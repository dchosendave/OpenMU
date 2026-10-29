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

    /// <inheritdoc />
    public async ValueTask SaveFlashSaleAsync(WebFlashSale sale, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        if (await context.WebFlashSales.AnyAsync(s => s.Id == sale.Id, cancellationToken).ConfigureAwait(false))
        {
            // The stock sold so far stays as it is.
            await context.WebFlashSales.Where(s => s.Id == sale.Id).ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.Title, sale.Title)
                    .SetProperty(s => s.ItemPackageId, sale.ItemPackageId)
                    .SetProperty(s => s.PickOneRandomItem, sale.PickOneRandomItem)
                    .SetProperty(s => s.Price, sale.Price)
                    .SetProperty(s => s.Currency, sale.Currency)
                    .SetProperty(s => s.Stock, sale.Stock)
                    .SetProperty(s => s.StartsAt, sale.StartsAt)
                    .SetProperty(s => s.DurationSeconds, sale.DurationSeconds),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        context.WebFlashSales.Add(sale);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DeleteFlashSaleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        await context.WebFlashSales.Where(s => s.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<WebFlashSale?> GetFlashSaleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.WebFlashSales.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryTakeFlashSaleStockAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        var changed = await context.WebFlashSales
            .Where(s => s.Id == id && s.Sold < s.Stock && s.StartsAt <= utcNow && s.StartsAt.AddSeconds(s.DurationSeconds) > utcNow)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Sold, s => s.Sold + 1), cancellationToken)
            .ConfigureAwait(false);
        return changed == 1;
    }

    /// <inheritdoc />
    public async ValueTask ReturnFlashSaleStockAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        await context.WebFlashSales.Where(s => s.Id == id && s.Sold > 0)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Sold, s => s.Sold - 1), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryTakeShopStockAsync(Guid itemPackageId, DateTime weekStart, int weeklyStock, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        if (!await context.WebShopStocks.AnyAsync(s => s.ItemPackageId == itemPackageId && s.WeekStart == weekStart, cancellationToken).ConfigureAwait(false))
        {
            context.WebShopStocks.Add(new WebShopStock { ItemPackageId = itemPackageId, WeekStart = weekStart });
            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException)
            {
                // Another buyer added the week at the same time; its row is used below.
            }
        }

        var changed = await context.WebShopStocks
            .Where(s => s.ItemPackageId == itemPackageId && s.WeekStart == weekStart && s.Sold < weeklyStock)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Sold, s => s.Sold + 1), cancellationToken)
            .ConfigureAwait(false);
        return changed == 1;
    }

    /// <inheritdoc />
    public async ValueTask ReturnShopStockAsync(Guid itemPackageId, DateTime weekStart, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        await context.WebShopStocks.Where(s => s.ItemPackageId == itemPackageId && s.WeekStart == weekStart && s.Sold > 0)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Sold, s => s.Sold - 1), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryAddMilestoneClaimAsync(WebMilestoneClaim claim, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        context.WebMilestoneClaims.Add(claim);
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            // The unique index on character and milestone: it was reached before, e.g. after a reset was undone.
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask<WebMilestoneClaim?> GetMilestoneClaimAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.WebMilestoneClaims.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<WebMilestoneClaim>> GetMilestoneClaimsAsync(string loginName, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.WebMilestoneClaims.AsNoTracking()
            .Where(c => c.LoginName == loginName)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryMarkMilestoneClaimedAsync(Guid id, string claimedItem, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        var changed = await context.WebMilestoneClaims
            .Where(c => c.Id == id && c.ClaimedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.ClaimedAt, utcNow).SetProperty(c => c.ClaimedItem, claimedItem), cancellationToken)
            .ConfigureAwait(false);
        return changed == 1;
    }

    /// <inheritdoc />
    public async ValueTask ReopenMilestoneClaimAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        await context.WebMilestoneClaims.Where(c => c.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.ClaimedAt, (DateTime?)null).SetProperty(c => c.ClaimedItem, (string?)null), cancellationToken)
            .ConfigureAwait(false);
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
