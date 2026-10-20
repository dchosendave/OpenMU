// <copyright file="ItemOptionPresetRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AccountManager;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.Persistence.AdminAuth;
using MUnique.OpenMU.Persistence.EntityFramework.AdminAuth;

/// <summary>
/// Implementation of the <see cref="IItemOptionPresetRepository"/> which stores the presets
/// in the <c>admin</c> schema of the configured PostgreSQL database.
/// </summary>
public class ItemOptionPresetRepository : IItemOptionPresetRepository
{
    private readonly IAdminUserRepository _adminUserRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemOptionPresetRepository"/> class.
    /// </summary>
    /// <param name="adminUserRepository">The admin user repository, which manages the storage of the <see cref="AdminPanelContext"/>.</param>
    public ItemOptionPresetRepository(IAdminUserRepository adminUserRepository)
    {
        this._adminUserRepository = adminUserRepository;
    }

    /// <inheritdoc />
    public ValueTask<bool> EnsureStorageAsync(CancellationToken cancellationToken = default)
        => this._adminUserRepository.EnsureStorageAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask<IList<ItemOptionPreset>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!await this.EnsureStorageAsync(cancellationToken).ConfigureAwait(false))
        {
            return new List<ItemOptionPreset>();
        }

        await using var context = new AdminPanelContext();
        return await context.ItemOptionPresets
            .AsNoTracking()
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(ItemOptionPreset preset, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        await using var context = new AdminPanelContext();
        var copy = preset.Clone();
        if (await context.ItemOptionPresets.AnyAsync(p => p.Id == copy.Id, cancellationToken).ConfigureAwait(false))
        {
            context.ItemOptionPresets.Update(copy);
        }
        else
        {
            context.ItemOptionPresets.Add(copy);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        await using var context = new AdminPanelContext();
        await context.ItemOptionPresets.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EnsureAvailableStorageAsync(CancellationToken cancellationToken)
    {
        if (!await this.EnsureStorageAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The item option preset storage is not available. Please check the database connection.");
        }
    }
}
