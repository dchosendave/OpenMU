// <copyright file="ItemDisplay.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using System.Globalization;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Web.AdminPanel.Properties;

/// <summary>
/// How the account manager shows items: caption, kind (tile color), group names and catalog categories.
/// </summary>
public static class ItemDisplay
{
    /// <summary>
    /// The look of wearable items by level. The game client has models for +0, +3, +5, ... +15.
    /// </summary>
    private static readonly int[] LevelLooks = { 0, 0, 0, 3, 3, 5, 5, 7, 7, 9, 9, 11, 11, 13, 13, 15 };

    /// <summary>
    /// Gets the catalog categories: the item groups, where the mixed groups 12 and 13 are split by the
    /// equipment slot which the items go into.
    /// </summary>
    public static IReadOnlyList<ItemCategory> Categories => new ItemCategory[]
    {
        new(Resources.AccountManagerCategoryWeapons, new[] { Group(0), Group(1), Group(2), Group(3), Group(4), Group(5) }),
        new(Resources.AccountManagerCategoryArmor, new[] { Group(6), Group(7), Group(8), Group(9), Group(10), Group(11) }, HasArmorSets: true),
        new(Resources.AccountManagerGroupWings, new ItemFilter[]
        {
            new(Resources.AccountManagerFilterWings, d => d.Group == 12 && FitsSlot(d, InventoryConstants.WingsSlot)),
            new(Resources.AccountManagerFilterOrbsAndOther, d => d.Group == 12 && !FitsSlot(d, InventoryConstants.WingsSlot)),
        }),
        new(Resources.AccountManagerGroupPets, new ItemFilter[]
        {
            new(Resources.AccountManagerFilterPets, d => d.Group == 13 && FitsSlot(d, InventoryConstants.PetSlot)),
            new(Resources.AccountManagerFilterRings, d => d.Group == 13 && FitsSlot(d, InventoryConstants.Ring1Slot)),
            new(Resources.AccountManagerFilterPendants, d => d.Group == 13 && FitsSlot(d, InventoryConstants.PendantSlot)),
            new(Resources.AccountManagerFilterOther, d => d.Group == 13 && d.ItemSlot is null),
        }),
        new(Resources.AccountManagerCategoryMisc, new[] { Group(14), Group(15) }),
    };

    /// <summary>
    /// Gets the name of an item group (<see cref="ItemDefinition.Group"/>).
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The name.</returns>
    public static string GroupName(byte group) => group switch
    {
        0 => Resources.AccountManagerGroupSwords,
        1 => Resources.AccountManagerGroupAxes,
        2 => Resources.AccountManagerGroupScepters,
        3 => Resources.AccountManagerGroupSpears,
        4 => Resources.AccountManagerGroupBows,
        5 => Resources.AccountManagerGroupStaffs,
        6 => Resources.AccountManagerGroupShields,
        7 => Resources.AccountManagerGroupHelms,
        8 => Resources.AccountManagerGroupArmors,
        9 => Resources.AccountManagerGroupPants,
        10 => Resources.AccountManagerGroupGloves,
        11 => Resources.AccountManagerGroupBoots,
        12 => Resources.AccountManagerGroupWings,
        13 => Resources.AccountManagerGroupPets,
        14 => Resources.AccountManagerGroupPotions,
        15 => Resources.AccountManagerGroupScrolls,
        _ => string.Format(Resources.AccountManagerGroupNumber, group),
    };

    /// <summary>
    /// Gets the caption of an item, e.g. "Dragon Armor +13".
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The caption.</returns>
    public static string Caption(Item item) => item.Definition is { } d
        ? $"{d.Name}{(item.Level > 0 ? $" +{item.Level}" : string.Empty)}"
        : Resources.AccountManagerUnknownItemCaption;

    /// <summary>
    /// Gets the kind of item which is used for the tile color: excellent, ancient, socket or normal.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The kind, as CSS class.</returns>
    public static string Kind(Item item) => item.ItemSetGroups.Count > 0 ? "ancient"
        : item.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent) ? "excellent"
        : item.SocketCount > 0 ? "socket"
        : "normal";

    /// <summary>
    /// Gets the icon of the item, rendered from the game client like the item editor's: wearable items
    /// change their look at +3, +5, ... +15, excellent and ancient items have their own glow.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The path of the icon.</returns>
    public static string Icon(Item item)
    {
        if (item.Definition is not { } definition)
        {
            return string.Empty;
        }

        var level = item.IsTrainablePet() || item.IsWing() ? 0
            : item.IsWearable() ? (item.Level < LevelLooks.Length ? LevelLooks[item.Level] : 0)
            : item.Level;
        var suffix = item.ItemSetGroups.Any(s => s.AncientSetDiscriminator > 0) ? "_a"
            : definition.Group < 12 && item.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent) ? "_e"
            : string.Empty;
        return IconPath(definition, level, suffix);
    }

    /// <summary>
    /// Gets the plain icon of an item definition, which is also the fallback when a level or glow has no own icon.
    /// </summary>
    /// <param name="definition">The item definition.</param>
    /// <returns>The path of the icon.</returns>
    public static string Icon(ItemDefinition definition) => IconPath(definition, 0, string.Empty);

    /// <summary>
    /// Gets the tooltip of an item: its caption, skill, options, durability and slot.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The tooltip.</returns>
    public static string Tooltip(Item item)
    {
        var lines = new List<string> { Caption(item) };
        if (item.HasSkill)
        {
            lines.Add("+ " + Resources.AccountManagerSkill);
        }

        lines.AddRange(item.ItemOptions
            .Where(o => o.ItemOption is not null)
            .Select(o => o.ItemOption!.OptionType == ItemOptionTypes.Option ? $"{o.ItemOption} +{o.Level * 4}" : $"{o.ItemOption}"));
        lines.Add(string.Format(Resources.AccountManagerDurabilityAndSlot, item.Durability.ToString("0", CultureInfo.CurrentCulture), item.ItemSlot));
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Gets a short summary of the options of an item, e.g. "Skill · Luck · Additional Damage +16".
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The summary.</returns>
    public static string OptionSummary(Item item)
    {
        var parts = new List<string>();
        if (item.HasSkill)
        {
            parts.Add(Resources.AccountManagerSkill);
        }

        foreach (var option in item.ItemOptions.Where(o => o.ItemOption is not null))
        {
            parts.Add(option.ItemOption!.OptionType == ItemOptionTypes.Option ? $"{option.ItemOption} +{option.Level * 4}" : $"{option.ItemOption}");
        }

        if (item.ItemSetGroups.FirstOrDefault(s => s.AncientSetDiscriminator > 0)?.ItemSetGroup is { } set)
        {
            parts.Add(string.Format(Resources.AccountManagerAncientSetOf, set.Name));
        }

        return string.Join(" · ", parts);
    }

    private static string IconPath(ItemDefinition definition, int level, string suffix)
        => $"_content/MUnique.OpenMU.Web.ItemEditor/img/items/item_{definition.Group}_{definition.Number}_{level}{suffix}.png";

    private static ItemFilter Group(byte group) => new(GroupName(group), d => d.Group == group);

    private static bool FitsSlot(ItemDefinition definition, byte slot) => definition.ItemSlot?.ItemSlots.Contains(slot) == true;
}
