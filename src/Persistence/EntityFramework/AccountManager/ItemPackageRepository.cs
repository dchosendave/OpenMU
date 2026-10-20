// <copyright file="ItemPackageRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AccountManager;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.Persistence.AdminAuth;
using MUnique.OpenMU.Persistence.EntityFramework.AdminAuth;

/// <summary>
/// Implementation of the <see cref="IItemPackageRepository"/> which stores the packages
/// in the <c>admin</c> schema of the configured PostgreSQL database.
/// </summary>
public class ItemPackageRepository : IItemPackageRepository
{
    private readonly IAdminUserRepository _adminUserRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemPackageRepository"/> class.
    /// </summary>
    /// <param name="adminUserRepository">The admin user repository, which manages the storage of the <see cref="AdminPanelContext"/>.</param>
    public ItemPackageRepository(IAdminUserRepository adminUserRepository)
    {
        this._adminUserRepository = adminUserRepository;
    }

    /// <inheritdoc />
    public ValueTask<bool> EnsureStorageAsync(CancellationToken cancellationToken = default)
        => this._adminUserRepository.EnsureStorageAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask<IList<ItemPackage>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!await this.EnsureStorageAsync(cancellationToken).ConfigureAwait(false))
        {
            return new List<ItemPackage>();
        }

        await using var context = new AdminPanelContext();
        var packages = await context.ItemPackages
            .AsNoTracking()
            .Include(p => p.Items)
            .ThenInclude(i => i.Options)
            .AsSplitQuery()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var package in packages)
        {
            package.Items = package.Items.OrderBy(i => i.Position).ToList();
        }

        return packages;
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(ItemPackage package, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        // The items of a package are replaced as a whole, so the stored copy gets new identifiers.
        var copy = package.Clone();
        for (var i = 0; i < copy.Items.Count; i++)
        {
            copy.Items[i].Position = i;
        }

        await using var context = new AdminPanelContext();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await context.ItemPackages.Where(p => p.Id == copy.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        context.ItemPackages.Add(copy);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        await using var context = new AdminPanelContext();
        await context.ItemPackages.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EnsureAvailableStorageAsync(CancellationToken cancellationToken)
    {
        if (!await this.EnsureStorageAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The item package storage is not available. Please check the database connection.");
        }
    }
}
