// <copyright file="ItemPackageStore.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.Web.AdminPanel.Properties;

/// <summary>
/// The item packages of the account manager, loaded from the <see cref="IItemPackageRepository"/>.
/// </summary>
public class ItemPackageStore
{
    private readonly IItemPackageRepository _repository;
    private readonly List<ItemPackage> _packages = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemPackageStore"/> class.
    /// </summary>
    /// <param name="repository">The repository.</param>
    public ItemPackageStore(IItemPackageRepository repository)
    {
        this._repository = repository;
    }

    /// <summary>
    /// Gets a value indicating whether the packages could be loaded from the database.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Gets the packages, ordered by name.
    /// </summary>
    public IReadOnlyList<ItemPackage> Packages => this._packages.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// Loads the packages from the database.
    /// </summary>
    public async Task LoadAsync()
    {
        this.IsAvailable = await this._repository.EnsureStorageAsync().ConfigureAwait(true);
        this._packages.Clear();
        this._packages.AddRange(await this._repository.GetAllAsync().ConfigureAwait(true));
    }

    /// <summary>
    /// Finds the package with the identifier.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The package, if found.</returns>
    public ItemPackage? Find(Guid id) => this._packages.FirstOrDefault(p => p.Id == id);

    /// <summary>
    /// Adds the package, or replaces the one with the same identifier.
    /// </summary>
    /// <param name="package">The package. Its name and zen are corrected, if necessary.</param>
    public async Task SaveAsync(ItemPackage package)
    {
        var name = package.Name.Trim();
        if (name.Length == 0)
        {
            throw new AccountManagerException(Resources.AccountManagerPackageNeedsName);
        }

        if (name.Length > ItemPackage.NameMaximumLength)
        {
            name = name[..ItemPackage.NameMaximumLength];
        }

        if (this._packages.Any(p => p.Id != package.Id && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AccountManagerException(string.Format(Resources.AccountManagerPackageNameExists, name));
        }

        package.Name = name;
        package.Zen = Math.Max(0, package.Zen);
        if (package.Description.Length > ItemPackage.DescriptionMaximumLength)
        {
            package.Description = package.Description[..ItemPackage.DescriptionMaximumLength];
        }

        foreach (var item in package.Items.Where(i => i.Name.Length > ItemPackageItem.NameMaximumLength))
        {
            item.Name = item.Name[..ItemPackageItem.NameMaximumLength];
        }

        await this._repository.SaveAsync(package).ConfigureAwait(true);
        var copy = package.Clone();
        var index = this._packages.FindIndex(p => p.Id == package.Id);
        if (index >= 0)
        {
            this._packages[index] = copy;
        }
        else
        {
            this._packages.Add(copy);
        }
    }

    /// <summary>
    /// Deletes the package with the identifier.
    /// </summary>
    /// <param name="id">The identifier.</param>
    public async Task DeleteAsync(Guid id)
    {
        await this._repository.DeleteAsync(id).ConfigureAwait(true);
        this._packages.RemoveAll(p => p.Id == id);
    }

    /// <summary>
    /// Gets a name which no package has yet, e.g. "Freebie (2)".
    /// </summary>
    /// <param name="name">The wanted name.</param>
    /// <returns>The unique name.</returns>
    public string UniqueName(string name)
    {
        name = name.Trim();
        var candidate = name;
        for (var i = 2; this._packages.Any(p => string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase)); i++)
        {
            candidate = $"{name} ({i})";
        }

        return candidate;
    }
}
