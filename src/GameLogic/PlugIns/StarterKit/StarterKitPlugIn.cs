// <copyright file="StarterKitPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.StarterKit;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Gives every new character the starter kit of its class: the lowest set, weapons, wings and rings,
/// equipped where they fit and in the bag otherwise. Armor, weapons, shields and rings are excellent with
/// their 2 weakest options, so every later drop is an upgrade (game-design.md, "Starter items").
/// </summary>
[Guid("6F2B8D14-3A97-4C50-B1E2-7D9A4C3E5F08")]
[PlugIn]
[Display(Name = "Starter kit", Description = "Every new character starts with its class's starter kit (set, weapons, wings, rings) in the inventory.")]
public class StarterKitPlugIn : ICharacterCreatedPlugIn, ISupportCustomConfiguration<StarterKitConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <summary>The first inventory slot of the bag; the slots before it are the equipment.</summary>
    private const int FirstBagSlot = 12;

    private const int BagColumns = 8;

    private const int BagRows = 8;

    /// <summary>
    /// The 2 weakest excellent options of each kind: defense success rate and reflect on armor, shields
    /// and rings; damage +level/20 and damage +2% on weapons.
    /// </summary>
    private static readonly AttributeDefinition[] WeakestExcellentAttributes =
    [
        Stats.DefenseRatePvm,
        Stats.DamageReflection,
        Stats.PhysicalBaseDmg,
        Stats.PhysicalBaseDmgIncrease,
        Stats.WizardryBaseDmg,
        Stats.WizardryBaseDmgIncrease,
    ];

    /// <inheritdoc />
    public StarterKitConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new StarterKitConfiguration();

    /// <inheritdoc />
    public void CharacterCreated(Account account, Character createdCharacter, IContext persistenceContext, GameConfiguration gameConfiguration, ILogger logger)
    {
        if (createdCharacter.Inventory is not { } inventory
            || createdCharacter.CharacterClass is not { } characterClass)
        {
            return;
        }

        var configuration = this.Configuration ?? new StarterKitConfiguration();
        var occupied = new bool[BagColumns * BagRows];
        foreach (var existing in inventory.Items)
        {
            MarkOccupied(occupied, existing);
        }

        foreach (var (group, number) in configuration.ItemsOf(characterClass.Number))
        {
            if (gameConfiguration.Items.FirstOrDefault(d => d.Group == group && d.Number == number) is not { } definition)
            {
                logger.LogWarning("Starter kit: there's no item {Group}:{Number}.", group, number);
                continue;
            }

            var item = CreateItem(definition, configuration, persistenceContext);
            if (FindSlot(inventory, definition, occupied) is not { } slot)
            {
                logger.LogWarning("Starter kit: no room for {Item} in the inventory of {Character}.", definition.Name, createdCharacter.Name);
                persistenceContext.Detach(item);
                continue;
            }

            item.ItemSlot = slot;
            inventory.Items.Add(item);
            MarkOccupied(occupied, item);
        }
    }

    private static Item CreateItem(ItemDefinition definition, StarterKitConfiguration configuration, IContext persistenceContext)
    {
        var item = persistenceContext.CreateNew<Item>();
        item.Definition = definition;
        var excellentOptions = definition.PossibleItemOptions
            .FirstOrDefault(o => o.PossibleOptions.Any(p => object.Equals(p.OptionType, ItemOptionTypes.Excellent)));

        // Armor, weapons, shields and rings: the ones which can be excellent. Wings and pets stay plain.
        if (excellentOptions is not null)
        {
            item.Level = Math.Min(configuration.ItemLevel, definition.MaximumItemLevel);
            if (configuration.Excellent)
            {
                foreach (var option in excellentOptions.PossibleOptions.Where(IsWeakestOption).Take(2))
                {
                    var link = persistenceContext.CreateNew<ItemOptionLink>();
                    link.ItemOption = option;
                    item.ItemOptions.Add(link);
                }
            }
        }

        item.HasSkill = item.CanHaveSkill();
        item.Durability = item.GetMaximumDurabilityOfOnePiece();
        return item;
    }

    private static bool IsWeakestOption(IncreasableItemOption option)
        => option.PowerUpDefinition?.TargetAttribute is { } attribute
           && WeakestExcellentAttributes.Any(a => a.Id == attribute.Id);

    /// <summary>
    /// Gets a free equipment slot the item fits, or else the first free place in the bag.
    /// </summary>
    private static byte? FindSlot(ItemStorage inventory, ItemDefinition definition, bool[] occupied)
    {
        foreach (var slot in definition.ItemSlot?.ItemSlots ?? [])
        {
            if (slot < FirstBagSlot && inventory.Items.All(i => i.ItemSlot != slot))
            {
                return (byte)slot;
            }
        }

        for (var row = 0; row + definition.Height <= BagRows; row++)
        {
            for (var column = 0; column + definition.Width <= BagColumns; column++)
            {
                if (IsFree(occupied, column, row, definition.Width, definition.Height))
                {
                    return (byte)(FirstBagSlot + (row * BagColumns) + column);
                }
            }
        }

        return null;
    }

    private static bool IsFree(bool[] occupied, int column, int row, int width, int height)
    {
        for (var y = row; y < row + height; y++)
        {
            for (var x = column; x < column + width; x++)
            {
                if (occupied[(y * BagColumns) + x])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void MarkOccupied(bool[] occupied, Item item)
    {
        if (item.ItemSlot < FirstBagSlot || item.Definition is not { } definition)
        {
            return;
        }

        var index = item.ItemSlot - FirstBagSlot;
        var row = index / BagColumns;
        var column = index % BagColumns;
        for (var y = row; y < Math.Min(BagRows, row + definition.Height); y++)
        {
            for (var x = column; x < Math.Min(BagColumns, column + definition.Width); x++)
            {
                occupied[(y * BagColumns) + x] = true;
            }
        }
    }
}
