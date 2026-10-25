// <copyright file="IItemOptionPresetRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AccountManager;

using System.Threading;

/// <summary>
/// A repository for the <see cref="ItemOptionPreset"/>s of the account manager.
/// </summary>
public interface IItemOptionPresetRepository
{
    /// <summary>
    /// Ensures that the underlying storage exists and is up to date.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    ///   <c>true</c>, if the storage is available; otherwise, <c>false</c>, e.g. when no database server is reachable.
    /// </returns>
    ValueTask<bool> EnsureStorageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all presets in the order they were created.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>All presets.</returns>
    ValueTask<IList<ItemOptionPreset>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the preset, or replaces the stored preset with the same identifier.
    /// </summary>
    /// <param name="preset">The preset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask SaveAsync(ItemOptionPreset preset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the preset with the specified identifier.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
