// <copyright file="UnavailableItemPackageRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using System.Threading;
using MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// An <see cref="IItemPackageRepository"/> which is used when the server runs without a database for the
/// admin panel, e.g. in the demo mode. It has no packages and can't store any.
/// </summary>
public class UnavailableItemPackageRepository : IItemPackageRepository
{
    private const string NotAvailableMessage = "The item package storage is not available.";

    /// <inheritdoc />
    public ValueTask<bool> EnsureStorageAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(false);

    /// <inheritdoc />
    public ValueTask<IList<ItemPackage>> GetAllAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IList<ItemPackage>>(new List<ItemPackage>());

    /// <inheritdoc />
    public ValueTask SaveAsync(ItemPackage package, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    /// <inheritdoc />
    public ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);
}
