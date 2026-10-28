// <copyright file="PackageEditor.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// Edits an item package. Its items become items of the game in a context which is never saved,
/// so they're edited with the same <see cref="ItemEditorModel"/> as the items of an account.
/// </summary>
public sealed class PackageEditor : IDisposable
{
    private readonly IContext _context;
    private readonly List<Item> _items = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageEditor"/> class.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="package">The package, which is copied.</param>
    public PackageEditor(AccountManagerDatabase database, ItemPackage package)
    {
        this._context = database.CreatePlayerContext();
        this.Converter = new PackageConverter(database.Configuration);
        this.Package = package.Clone();
        var problems = new List<string>();
        foreach (var packageItem in this.Package.Items)
        {
            if (this.Converter.CreateItem(packageItem, this._context, problems) is { } item)
            {
                this._items.Add(item);
            }
        }

        this.Problems = problems.Distinct().ToList();
    }

    /// <summary>
    /// Occurs after every change.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Gets the package which is edited. It's a copy; the store keeps the saved one.
    /// </summary>
    public ItemPackage Package { get; }

    /// <summary>
    /// Gets the items.
    /// </summary>
    public IReadOnlyList<Item> Items => this._items;

    /// <summary>
    /// Gets what couldn't be loaded (unknown item definitions or options). Saving drops those.
    /// </summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>
    /// Gets a value indicating whether the package has unsaved changes.
    /// </summary>
    public bool HasChanges { get; private set; }

    /// <summary>
    /// Gets the converter.
    /// </summary>
    public PackageConverter Converter { get; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name
    {
        get => this.Package.Name;
        set => this.Update(() => this.Package.Name = value);
    }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description
    {
        get => this.Package.Description;
        set => this.Update(() => this.Package.Description = value);
    }

    /// <summary>
    /// Gets or sets the zen.
    /// </summary>
    public int Zen
    {
        get => this.Package.Zen;
        set => this.Update(() => this.Package.Zen = Math.Max(0, value));
    }

    /// <summary>
    /// Gets or sets a value indicating whether the web shop of the player website sells the package.
    /// </summary>
    public bool IsInWebShop
    {
        get => this.Package.IsInWebShop;
        set => this.Update(() => this.Package.IsInWebShop = value);
    }

    /// <summary>
    /// Gets or sets the web shop price.
    /// </summary>
    public int WebShopPrice
    {
        get => this.Package.WebShopPrice;
        set => this.Update(() => this.Package.WebShopPrice = Math.Max(0, value));
    }

    /// <summary>
    /// Gets or sets the web shop currency.
    /// </summary>
    public WebShopCurrency WebShopCurrency
    {
        get => this.Package.WebShopCurrency;
        set => this.Update(() => this.Package.WebShopCurrency = value);
    }

    /// <summary>
    /// Adds a new item.
    /// </summary>
    /// <param name="definition">The item definition.</param>
    /// <param name="level">The level.</param>
    /// <param name="preset">The options which the item gets. Its level overrides <paramref name="level"/>.</param>
    /// <returns>The added item.</returns>
    public Item AddItem(ItemDefinition definition, byte level = 0, ItemOptionPreset? preset = null)
    {
        var item = this.CreateItem(definition, level, preset);
        this.MarkChanged();
        return item;
    }

    /// <summary>
    /// Adds several new items, e.g. the pieces of an armor set.
    /// </summary>
    /// <param name="definitions">The item definitions.</param>
    /// <param name="level">The level.</param>
    /// <param name="preset">The options which the items get.</param>
    /// <returns>The added items.</returns>
    public IReadOnlyList<Item> AddItems(IEnumerable<ItemDefinition> definitions, byte level = 0, ItemOptionPreset? preset = null)
    {
        var items = definitions.Select(d => this.CreateItem(d, level, preset)).ToList();
        this.MarkChanged();
        return items;
    }

    /// <summary>
    /// Gives items of the package a level and/or the options of a preset.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="preset">The preset, or <c>null</c> to keep the options.</param>
    /// <param name="level">The level, or <c>null</c> to keep the level.</param>
    public void Apply(IEnumerable<Item> items, ItemOptionPreset? preset, byte? level)
    {
        foreach (var item in items.Where(this._items.Contains))
        {
            var editor = new ItemEditorModel(item, this._context, () => { });
            if (preset is not null)
            {
                editor.ApplyPreset(preset);
            }

            if (level is { } newLevel)
            {
                editor.Level = newLevel;
                if (!editor.IsStackable)
                {
                    editor.Durability = editor.MaximumDurability;
                }
            }
        }

        this.MarkChanged();
    }

    /// <summary>
    /// Adds a copy of an item with all its options.
    /// </summary>
    /// <param name="original">The original item.</param>
    /// <returns>The copy.</returns>
    public Item Duplicate(Item original)
    {
        var copy = this._context.CreateNew<Item>();
        copy.AssignValues(original);
        this._items.Insert(this._items.IndexOf(original) + 1, copy);
        this.MarkChanged();
        return copy;
    }

    /// <summary>
    /// Removes an item.
    /// </summary>
    /// <param name="item">The item.</param>
    public void Remove(Item item)
    {
        if (this._items.Remove(item))
        {
            this.MarkChanged();
        }
    }

    /// <summary>
    /// Opens an editor for an item of the package.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The editor.</returns>
    public ItemEditorModel Edit(Item item) => new(item, this._context, this.MarkChanged);

    /// <summary>
    /// Gets the package with the current items, as it would be saved.
    /// </summary>
    /// <returns>The package.</returns>
    public ItemPackage ToPackage()
    {
        var package = this.Package.Clone();
        package.Items = this._items.Select(PackageConverter.FromItem).ToList();
        return package;
    }

    /// <summary>
    /// Saves the package to the store.
    /// </summary>
    /// <param name="store">The store.</param>
    public async Task SaveAsync(ItemPackageStore store)
    {
        var package = this.ToPackage();
        await store.SaveAsync(package).ConfigureAwait(true);
        this.Package.Name = package.Name;
        this.HasChanges = false;
        this.Changed?.Invoke();
    }

    /// <inheritdoc />
    public void Dispose() => this._context.Dispose();

    private Item CreateItem(ItemDefinition definition, byte level, ItemOptionPreset? preset)
    {
        var item = this._context.CreateNew<Item>();
        item.Definition = definition;
        item.Level = Math.Min(level, definition.MaximumItemLevel);
        item.Durability = item.IsStackable() ? 1 : item.GetMaximumDurabilityOfOnePiece();
        if (preset is not null)
        {
            new ItemEditorModel(item, this._context, () => { }).ApplyPreset(preset);
        }

        this._items.Add(item);
        return item;
    }

    private void Update(Action change)
    {
        change();
        this.MarkChanged();
    }

    private void MarkChanged()
    {
        this.HasChanges = true;
        this.Changed?.Invoke();
    }
}
