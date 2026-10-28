// <copyright file="CastleSiegeRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AccountManager;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.Persistence.AdminAuth;
using MUnique.OpenMU.Persistence.EntityFramework.AdminAuth;

/// <summary>
/// The point mode Castle Siege battles and their vault rewards, in the schema of the admin panel.
/// </summary>
public class CastleSiegeRepository : ICastleSiegeRepository
{
    private readonly IAdminUserRepository _adminUserRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeRepository"/> class.
    /// </summary>
    /// <param name="adminUserRepository">The admin user repository, which creates and migrates the schema.</param>
    public CastleSiegeRepository(IAdminUserRepository adminUserRepository)
    {
        this._adminUserRepository = adminUserRepository;
    }

    /// <inheritdoc />
    public async ValueTask SaveBattleAsync(CastleSiegeBattle battle, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        if (await context.CastleSiegeBattles.AnyAsync(b => b.Id == battle.Id, cancellationToken).ConfigureAwait(false))
        {
            context.CastleSiegeBattles.Update(battle);
        }
        else
        {
            context.CastleSiegeBattles.Add(battle);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask AddVaultRewardsAsync(IReadOnlyCollection<CastleSiegeVaultReward> rewards, CancellationToken cancellationToken = default)
    {
        if (rewards.Count == 0)
        {
            return;
        }

        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        context.CastleSiegeVaultRewards.AddRange(rewards);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<CastleSiegeVaultReward>> GetPendingVaultRewardsAsync(int count, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.CastleSiegeVaultRewards.AsNoTracking()
            .Where(r => r.DeliveredAt == null)
            .OrderBy(r => r.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask UpdateVaultRewardsAsync(IReadOnlyCollection<Guid> ids, DateTime? deliveredAt, string? problem, CancellationToken cancellationToken = default)
    {
        await using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        await context.CastleSiegeVaultRewards
            .Where(r => ids.Contains(r.Id))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.DeliveredAt, deliveredAt)
                    .SetProperty(r => r.LastProblem, problem),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<AdminPanelContext> CreateContextAsync(CancellationToken cancellationToken)
    {
        if (!await this._adminUserRepository.EnsureStorageAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The Castle Siege storage is not available. Please check the database connection.");
        }

        return new AdminPanelContext();
    }
}
