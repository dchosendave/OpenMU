// <copyright file="PackageConverter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.Web.AdminPanel.Properties;

/// <summary>
/// Converts between the items of packages and items of the game, and decides which items of a package a class gets.
/// </summary>
public sealed class PackageConverter
{
    private readonly GameConfiguration _configuration;
    private Dictionary<Guid, IncreasableItemOption>? _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageConverter"/> class.
    /// </summary>
    /// <param name="configuration">The game configuration.</param>
    public PackageConverter(GameConfiguration configuration)
    {
        this._configuration = configuration;
    }

    /// <summary>
    /// Gets the first classes of each class line (Dark Knight, Dark Wizard, ...), in the order of the game.
    /// </summary>
    public IReadOnlyList<CharacterClass> BaseClasses => this._configuration.CharacterClasses
        .Where(c => !this._configuration.CharacterClasses.Any(other => other.NextGenerationClass == c))
        .OrderBy(c => c.Number)
        .ToList();

    private Dictionary<Guid, IncreasableItemOption> Options => this._options ??= this._configuration.ItemOptions.SelectMany(o => o.PossibleOptions)
        .Concat(this._configuration.Items.SelectMany(d => d.PossibleItemOptions).SelectMany(o => o.PossibleOptions))
        .Concat(this._configuration.ItemSetGroups.Where(g => g.Options is not null).SelectMany(g => g.Options!.PossibleOptions))
        .Concat(this._configuration.ItemSetGroups.SelectMany(g => g.Items).Select(i => i.BonusOption).OfType<IncreasableItemOption>())
        .DistinctBy(o => o.GetId())
        .ToDictionary(o => o.GetId());

    /// <summary>
    /// Creates the package item of an item, with all its options.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The package item.</returns>
    public static ItemPackageItem FromItem(Item item) => new()
    {
        Group = item.Definition?.Group ?? 0,
        Number = item.Definition?.Number ?? 0,
        Name = item.Definition?.Name.ToString() ?? string.Empty,
        Level = item.Level,
        Durability = item.Durability,
        HasSkill = item.HasSkill,
        SocketCount = item.SocketCount,
        Options = item.ItemOptions
            .Where(o => o.ItemOption is not null)
            .Select(o => new ItemPackageItemOption { OptionId = o.ItemOption!.GetId(), Level = o.Level, Index = o.Index })
            .ToList(),
        SetItemIds = item.ItemSetGroups.Select(s => s.GetId()).ToList(),
    };

    /// <summary>
    /// Describes the package item, e.g. "Dragon Armor +13".
    /// </summary>
    /// <param name="item">The package item.</param>
    /// <returns>The description.</returns>
    public static string Describe(ItemPackageItem item)
    {
        var name = string.IsNullOrEmpty(item.Name) ? string.Format(Resources.AccountManagerUnknownItemName, item.Group, item.Number) : item.Name;
        return item.Level > 0 ? $"{name} +{item.Level}" : name;
    }

    /// <summary>
    /// Gets the item definition of the package item.
    /// </summary>
    /// <param name="item">The package item.</param>
    /// <returns>The item definition, if the server knows it.</returns>
    public ItemDefinition? DefinitionOf(ItemPackageItem item)
        => this._configuration.Items.FirstOrDefault(d => d.Group == item.Group && d.Number == item.Number);

    /// <summary>
    /// Creates the item in the context. Options or ancient sets which this server doesn't know are
    /// left out and reported in <paramref name="problems"/>.
    /// </summary>
    /// <param name="packageItem">The package item.</param>
    /// <param name="context">The context.</param>
    /// <param name="problems">The problems.</param>
    /// <returns>The item, or <c>null</c> when the server doesn't know the item definition.</returns>
    public Item? CreateItem(ItemPackageItem packageItem, IContext context, ICollection<string> problems)
    {
        if (this.DefinitionOf(packageItem) is not { } definition)
        {
            problems.Add(string.Format(Resources.AccountManagerUnknownItem, Describe(packageItem)));
            return null;
        }

        var item = context.CreateNew<Item>();
        item.Definition = definition;
        item.Level = Math.Min(packageItem.Level, definition.MaximumItemLevel);
        item.Durability = packageItem.Durability;
        item.HasSkill = packageItem.HasSkill && definition.Skill is not null;
        item.SocketCount = Math.Clamp(packageItem.SocketCount, 0, definition.MaximumSockets);
        foreach (var option in packageItem.Options)
        {
            if (!this.Options.TryGetValue(option.OptionId, out var itemOption))
            {
                problems.Add(string.Format(Resources.AccountManagerUnknownOption, definition.Name));
                continue;
            }

            var link = context.CreateNew<ItemOptionLink>();
            link.ItemOption = itemOption;
            link.Level = option.Level;
            link.Index = option.Index;
            item.ItemOptions.Add(link);
        }

        foreach (var setItemId in packageItem.SetItemIds)
        {
            var setItem = definition.PossibleItemSetGroups.SelectMany(g => g.Items).FirstOrDefault(i => i.GetId() == setItemId);
            if (setItem is null)
            {
                problems.Add(string.Format(Resources.AccountManagerUnknownAncientSet, definition.Name));
                continue;
            }

            item.ItemSetGroups.Add(setItem);
        }

        return item;
    }

    /// <summary>
    /// Determines whether the package item is meant for the class. Items without class requirement are
    /// for everyone. Otherwise, one of the classes of its line (e.g. Dark Knight, Blade Knight, Blade
    /// Master) must be able to wear it, so a new Dark Knight also gets Blade Knight wings.
    /// </summary>
    /// <param name="item">The package item.</param>
    /// <param name="characterClass">The class, or <c>null</c> for every class.</param>
    /// <returns><c>true</c>, if the item is meant for the class; otherwise, <c>false</c>.</returns>
    public bool IsFor(ItemPackageItem item, CharacterClass? characterClass)
        => this.DefinitionOf(item) is not { } definition || this.IsFor(definition, characterClass);

    /// <summary>
    /// Determines whether the item definition is meant for the class line, see <see cref="IsFor(ItemPackageItem, CharacterClass?)"/>.
    /// </summary>
    /// <param name="definition">The item definition.</param>
    /// <param name="characterClass">The class, or <c>null</c> for every class.</param>
    /// <returns><c>true</c>, if the item is meant for the class; otherwise, <c>false</c>.</returns>
    public bool IsFor(ItemDefinition definition, CharacterClass? characterClass)
    {
        if (characterClass is null || definition.QualifiedCharacters.Count == 0)
        {
            return true;
        }

        var line = this.LineOf(characterClass);
        return definition.QualifiedCharacters.Any(line.Contains);
    }

    /// <summary>
    /// Gets the names of the base classes which can use the item, e.g. "Dark Knight, Magic Gladiator".
    /// </summary>
    /// <param name="item">The package item.</param>
    /// <returns>The names of the base classes; empty when every class can use it.</returns>
    public IReadOnlyList<string> ClassesOf(ItemPackageItem item)
    {
        if (this.DefinitionOf(item) is not { } definition || definition.QualifiedCharacters.Count == 0)
        {
            return Array.Empty<string>();
        }

        var roots = definition.QualifiedCharacters.Select(this.RootOf).Distinct().ToList();
        var allRoots = this._configuration.CharacterClasses.Select(this.RootOf).Distinct().Count();
        return roots.Count >= allRoots
            ? Array.Empty<string>()
            : roots.OrderBy(c => c.Number).Select(c => c.Name.ToString() ?? string.Empty).ToList();
    }

    /// <summary>
    /// Gets the class with all classes before and after it (its line of evolution).
    /// </summary>
    private HashSet<CharacterClass> LineOf(CharacterClass characterClass)
    {
        var line = new HashSet<CharacterClass>();
        for (var current = this.RootOf(characterClass); current is not null && line.Add(current); current = current.NextGenerationClass)
        {
            // just collecting the classes of the line
        }

        line.Add(characterClass);
        return line;
    }

    private CharacterClass RootOf(CharacterClass characterClass)
    {
        var current = characterClass;
        var seen = new HashSet<CharacterClass> { current };
        while (this._configuration.CharacterClasses.FirstOrDefault(c => c.NextGenerationClass == current) is { } previous && seen.Add(previous))
        {
            current = previous;
        }

        return current;
    }
}
