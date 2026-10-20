// <copyright file="UnavailableItemOptionPresetRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using System.Threading;
using MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// An <see cref="IItemOptionPresetRepository"/> which is used when the server runs without a database for the
/// admin panel, e.g. in the demo mode. It has no presets and can't store any.
/// </summary>
public class UnavailableItemOptionPresetRepository : IItemOptionPresetRepository
{
    private const string NotAvailableMessage = "The item option preset storage is not available.";

    /// <inheritdoc />
    public ValueTask<bool> EnsureStorageAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(false);

    /// <inheritdoc />
    public ValueTask<IList<ItemOptionPreset>> GetAllAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IList<ItemOptionPreset>>(new List<ItemOptionPreset>());

    /// <inheritdoc />
    public ValueTask SaveAsync(ItemOptionPreset preset, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);

    /// <inheritdoc />
    public ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException(NotAvailableMessage);
}
