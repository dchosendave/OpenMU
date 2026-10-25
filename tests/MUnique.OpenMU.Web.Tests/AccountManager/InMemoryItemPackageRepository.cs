// <copyright file="InMemoryItemPackageRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AccountManager;

using System.Threading;
using MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// An <see cref="IItemPackageRepository"/> which keeps copies of the packages in memory, like a database would.
/// </summary>
public sealed class InMemoryItemPackageRepository : IItemPackageRepository
{
    private readonly List<ItemPackage> _packages = new();

    /// <inheritdoc />
    public ValueTask<bool> EnsureStorageAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(true);

    /// <inheritdoc />
    public ValueTask<IList<ItemPackage>> GetAllAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IList<ItemPackage>>(this._packages.OrderBy(p => p.Name).Select(p => p.Clone()).ToList());

    /// <inheritdoc />
    public ValueTask SaveAsync(ItemPackage package, CancellationToken cancellationToken = default)
    {
        this._packages.RemoveAll(p => p.Id == package.Id);
        this._packages.Add(package.Clone());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        this._packages.RemoveAll(p => p.Id == id);
        return ValueTask.CompletedTask;
    }
}
