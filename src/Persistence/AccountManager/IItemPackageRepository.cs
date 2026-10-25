// <copyright file="IItemPackageRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AccountManager;

using System.Threading;

/// <summary>
/// A repository for the <see cref="ItemPackage"/>s of the account manager.
/// </summary>
public interface IItemPackageRepository
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
    /// Gets all packages with their items, ordered by their name.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>All packages.</returns>
    ValueTask<IList<ItemPackage>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the package, or replaces the stored package with the same identifier including all its items.
    /// </summary>
    /// <param name="package">The package.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask SaveAsync(ItemPackage package, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the package with the specified identifier.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
