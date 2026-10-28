// <copyright file="ICastleSiegeRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AccountManager;

using System.Threading;

/// <summary>
/// Stores the point mode Castle Siege battles and the vault rewards which wait for delivery.
/// </summary>
public interface ICastleSiegeRepository
{
    /// <summary>
    /// Adds or updates a battle.
    /// </summary>
    /// <param name="battle">The battle.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    ValueTask SaveBattleAsync(CastleSiegeBattle battle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds vault rewards.
    /// </summary>
    /// <param name="rewards">The rewards.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    ValueTask AddVaultRewardsAsync(IReadOnlyCollection<CastleSiegeVaultReward> rewards, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the vault rewards which aren't delivered yet, oldest first.
    /// </summary>
    /// <param name="count">The maximum number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The pending rewards.</returns>
    ValueTask<IReadOnlyList<CastleSiegeVaultReward>> GetPendingVaultRewardsAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks vault rewards as delivered, or stores why they couldn't be delivered.
    /// </summary>
    /// <param name="ids">The ids of the rewards.</param>
    /// <param name="deliveredAt">The delivery time; <c>null</c>, if the delivery failed.</param>
    /// <param name="problem">The problem, if the delivery failed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    ValueTask UpdateVaultRewardsAsync(IReadOnlyCollection<Guid> ids, DateTime? deliveredAt, string? problem, CancellationToken cancellationToken = default);
}
