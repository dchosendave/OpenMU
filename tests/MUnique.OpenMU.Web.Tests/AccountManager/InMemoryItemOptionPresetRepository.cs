// <copyright file="InMemoryItemOptionPresetRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AccountManager;

using System.Threading;
using MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// An <see cref="IItemOptionPresetRepository"/> which keeps copies of the presets in memory, like a database would.
/// </summary>
public sealed class InMemoryItemOptionPresetRepository : IItemOptionPresetRepository
{
    private readonly List<ItemOptionPreset> _presets = new();

    /// <inheritdoc />
    public ValueTask<bool> EnsureStorageAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(true);

    /// <inheritdoc />
    public ValueTask<IList<ItemOptionPreset>> GetAllAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IList<ItemOptionPreset>>(this._presets.OrderBy(p => p.CreatedAt).Select(p => p.Clone()).ToList());

    /// <inheritdoc />
    public ValueTask SaveAsync(ItemOptionPreset preset, CancellationToken cancellationToken = default)
    {
        this._presets.RemoveAll(p => p.Id == preset.Id);
        this._presets.Add(preset.Clone());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        this._presets.RemoveAll(p => p.Id == id);
        return ValueTask.CompletedTask;
    }
}
