// <copyright file="ItemOptionPresetStore.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.Web.AdminPanel.Properties;

/// <summary>
/// The item option presets of the account manager, loaded from the <see cref="IItemOptionPresetRepository"/>.
/// </summary>
public class ItemOptionPresetStore
{
    private readonly IItemOptionPresetRepository _repository;
    private readonly List<ItemOptionPreset> _presets = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemOptionPresetStore"/> class.
    /// </summary>
    /// <param name="repository">The repository.</param>
    public ItemOptionPresetStore(IItemOptionPresetRepository repository)
    {
        this._repository = repository;
    }

    /// <summary>
    /// Gets the presets in the order they were created.
    /// </summary>
    public IReadOnlyList<ItemOptionPreset> Presets => this._presets.OrderBy(p => p.CreatedAt).ToList();

    /// <summary>
    /// Loads the presets from the database.
    /// </summary>
    public async Task LoadAsync()
    {
        this._presets.Clear();
        this._presets.AddRange(await this._repository.GetAllAsync().ConfigureAwait(true));
    }

    /// <summary>
    /// Finds the preset with the identifier.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The preset, if found.</returns>
    public ItemOptionPreset? Find(Guid id) => this._presets.FirstOrDefault(p => p.Id == id);

    /// <summary>
    /// Adds the preset, or saves the changes of the one with the same identifier.
    /// </summary>
    /// <param name="preset">The preset. Its values are corrected, if necessary.</param>
    public async Task SaveAsync(ItemOptionPreset preset)
    {
        preset.Name = preset.Name.Trim();
        if (preset.Name.Length == 0)
        {
            throw new AccountManagerException(Resources.AccountManagerPresetNeedsName);
        }

        if (preset.Name.Length > ItemOptionPreset.NameMaximumLength)
        {
            preset.Name = preset.Name[..ItemOptionPreset.NameMaximumLength];
        }

        preset.OptionLevel = Math.Clamp(preset.OptionLevel, 0, ItemOptionPreset.MaximumOptionLevel);
        preset.Level = preset.Level is { } level ? Math.Min(level, (byte)15) : null;
        await this._repository.SaveAsync(preset).ConfigureAwait(true);
        var index = this._presets.FindIndex(p => p.Id == preset.Id);
        if (index >= 0)
        {
            this._presets[index] = preset.Clone();
        }
        else
        {
            this._presets.Add(preset.Clone());
        }
    }

    /// <summary>
    /// Deletes the preset with the identifier.
    /// </summary>
    /// <param name="id">The identifier.</param>
    public async Task DeleteAsync(Guid id)
    {
        await this._repository.DeleteAsync(id).ConfigureAwait(true);
        this._presets.RemoveAll(p => p.Id == id);
    }

    /// <summary>
    /// Gets a name which no preset has yet, e.g. "New preset (2)".
    /// </summary>
    /// <param name="name">The wanted name.</param>
    /// <returns>The unique name.</returns>
    public string UniqueName(string name)
    {
        name = name.Trim();
        var candidate = name;
        for (var i = 2; this._presets.Any(p => string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase)); i++)
        {
            candidate = $"{name} ({i})";
        }

        return candidate;
    }
}
