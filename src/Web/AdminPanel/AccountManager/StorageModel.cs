// <copyright file="StorageModel.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Web.AdminPanel.Properties;
using MUnique.OpenMU.Web.ItemEditor;

/// <summary>
/// The item storage of a character (inventory) or an account (vault), with the placement rules of the game.
/// </summary>
public sealed class StorageModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageModel"/> class.
    /// </summary>
    /// <param name="storage">The storage.</param>
    /// <param name="storageType">The type of the storage.</param>
    /// <param name="extensions">The number of inventory extensions, or vault extensions.</param>
    /// <param name="title">The title.</param>
    public StorageModel(ItemStorage storage, StorageType storageType, byte extensions, string title)
    {
        this.Storage = storage;
        this.StorageType = storageType;
        this.Title = title;
        if (storageType == StorageType.Inventory)
        {
            var areas = new List<StorageArea>
            {
                new(Resources.AccountManagerAreaInventory, InventoryConstants.EquippableSlotsCount, InventoryConstants.InventoryRows, StorageAreaKind.Inventory),
            };
            var expansionSize = InventoryConstants.RowsOfOneExtension * InventoryConstants.RowSize;
            for (var i = 0; i < Math.Min(extensions, InventoryConstants.MaximumNumberOfExtensions); i++)
            {
                areas.Add(new(string.Format(Resources.AccountManagerAreaExpansion, i + 1), InventoryConstants.FirstExtensionItemSlotIndex + (i * expansionSize), InventoryConstants.RowsOfOneExtension, StorageAreaKind.Expansion));
            }

            areas.Add(new(Resources.AccountManagerAreaPersonalStore, InventoryConstants.FirstStoreItemSlotIndex, InventoryConstants.StoreRows, StorageAreaKind.PersonalStore));
            this.Areas = areas;
            this.Equipment = EquipmentNames.Select((name, i) => new EquipmentSlot((byte)i, name)).ToList();
        }
        else
        {
            var areas = new List<StorageArea> { new(Resources.AccountManagerAreaVault, 0, InventoryConstants.WarehouseRows, StorageAreaKind.Vault) };
            if (extensions > 0)
            {
                areas.Add(new(Resources.AccountManagerAreaVaultPage2, InventoryConstants.WarehouseSize, InventoryConstants.WarehouseRows, StorageAreaKind.Vault));
            }

            this.Areas = areas;
            this.Equipment = Array.Empty<EquipmentSlot>();
        }
    }

    /// <summary>
    /// Gets the names of the equipment slots in the current UI culture, by slot number.
    /// </summary>
    public static IReadOnlyList<string> EquipmentNames => new[]
    {
        Resources.AccountManagerSlotLeftHand,
        Resources.AccountManagerSlotRightHand,
        Resources.AccountManagerSlotHelm,
        Resources.AccountManagerSlotArmor,
        Resources.AccountManagerSlotPants,
        Resources.AccountManagerSlotGloves,
        Resources.AccountManagerSlotBoots,
        Resources.AccountManagerSlotWings,
        Resources.AccountManagerSlotPet,
        Resources.AccountManagerSlotPendant,
        Resources.AccountManagerSlotRing1,
        Resources.AccountManagerSlotRing2,
    };

    /// <summary>
    /// Gets the storage.
    /// </summary>
    public ItemStorage Storage { get; }

    /// <summary>
    /// Gets the type of the storage.
    /// </summary>
    public StorageType StorageType { get; }

    /// <summary>
    /// Gets the title which is shown, e.g. "Inventory of Warrior" or "Vault".
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the areas.
    /// </summary>
    public IReadOnlyList<StorageArea> Areas { get; }

    /// <summary>
    /// Gets the equipment slots. A vault has none.
    /// </summary>
    public IReadOnlyList<EquipmentSlot> Equipment { get; }

    /// <summary>
    /// Gets the items.
    /// </summary>
    public IEnumerable<Item> Items => this.Storage.Items;

    /// <summary>
    /// Gets or sets the money.
    /// </summary>
    public int Money
    {
        get => this.Storage.Money;
        set => this.Storage.Money = Math.Max(0, value);
    }

    /// <summary>
    /// Gets the size of the item in cells.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The width and height.</returns>
    public static (int Width, int Height) SizeOf(Item item)
        => (Math.Max((byte)1, item.Definition?.Width ?? 1), Math.Max((byte)1, item.Definition?.Height ?? 1));

    /// <summary>
    /// Determines whether the slot is an equipment slot.
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <returns><c>true</c>, if the slot is an equipment slot; otherwise, <c>false</c>.</returns>
    public bool IsEquipmentSlot(int slot) => this.Equipment.Any(e => e.Slot == slot);

    /// <summary>
    /// Gets the item which is placed at the slot (with its top left cell, or in an equipment slot).
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <returns>The item, if there is one.</returns>
    public Item? ItemAt(int slot) => this.Storage.Items.FirstOrDefault(i => i.ItemSlot == slot);

    /// <summary>
    /// Gets the item which covers the cell of a grid, or which is in the equipment slot.
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <returns>The item, if there is one.</returns>
    public Item? ItemCovering(int slot) => this.IsEquipmentSlot(slot)
        ? this.ItemAt(slot)
        : this.Storage.Items.FirstOrDefault(i => this.CellsOf(i, i.ItemSlot).Contains(slot));

    /// <summary>
    /// Finds the items which lie on cells of another item (or share an equipment slot). The game never
    /// creates such a storage, but imported or hand-made data can, so the account manager shows them.
    /// </summary>
    /// <returns>Every item which lies on top of an item which was placed before it.</returns>
    public IReadOnlyList<Item> FindOverlaps()
    {
        var used = new HashSet<int>();
        var overlapping = new List<Item>();
        foreach (var item in this.Storage.Items.OrderBy(i => i.ItemSlot))
        {
            var cells = this.CellsOf(item, item.ItemSlot).ToList();
            if (cells.Any(used.Contains))
            {
                overlapping.Add(item);
            }

            used.UnionWith(cells);
        }

        return overlapping;
    }

    /// <summary>
    /// Finds the first place for the item: the inventory, then the extensions in their order (or the vault
    /// pages), never across two of them, never in the personal store.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The slot, if there is a free place.</returns>
    public byte? FindFreeSlot(Item item)
    {
        foreach (var area in this.Areas.Where(a => a.AutoPlace))
        {
            for (var slot = area.FirstSlot; slot < area.FirstSlot + area.SlotCount; slot++)
            {
                if (this.CanPlace(item, slot, out _))
                {
                    return (byte)slot;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Finds free places for several new items at once, like <see cref="FindFreeSlot"/> does for one.
    /// The largest items are placed first, so they still find room. Nothing is changed.
    /// </summary>
    /// <param name="definitions">The definitions of the new items.</param>
    /// <returns>The slot of each definition (in the given order), or <c>null</c> when not all of them fit.</returns>
    public IReadOnlyList<byte>? PlanFreeSlots(IReadOnlyList<ItemDefinition> definitions)
    {
        var used = this.Storage.Items.Where(i => !this.IsEquipmentSlot(i.ItemSlot)).SelectMany(i => this.CellsOf(i, i.ItemSlot)).ToHashSet();
        var slots = new byte[definitions.Count];
        var order = Enumerable.Range(0, definitions.Count)
            .OrderByDescending(i => Math.Max((byte)1, definitions[i].Width) * Math.Max((byte)1, definitions[i].Height))
            .ThenByDescending(i => definitions[i].Height);
        foreach (var index in order)
        {
            var width = Math.Max((byte)1, definitions[index].Width);
            var height = Math.Max((byte)1, definitions[index].Height);
            var found = this.Areas.Where(a => a.AutoPlace)
                .SelectMany(area => Enumerable.Range(area.FirstSlot, area.SlotCount)
                    .Where(slot => area.ColumnOf(slot) + width <= StorageArea.Columns && area.RowOf(slot) + height <= area.Rows)
                    .Select(slot => (int?)slot))
                .FirstOrDefault(slot => Cells(slot!.Value, width, height).All(c => !used.Contains(c)));
            if (found is not { } place)
            {
                return null;
            }

            used.UnionWith(Cells(place, width, height));
            slots[index] = (byte)place;
        }

        return slots;
    }

    /// <summary>
    /// Gets the areas of a kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The areas of the kind.</returns>
    public IEnumerable<StorageArea> AreasOf(StorageAreaKind kind) => this.Areas.Where(a => a.Kind == kind);

    /// <summary>
    /// Determines whether the item can be put at the slot: an equipment slot which it's made for,
    /// or grid cells within one area which no other item covers.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="reason">The reason why it's not possible.</param>
    /// <returns><c>true</c>, if the item can be put at the slot; otherwise, <c>false</c>.</returns>
    public bool CanPlace(Item item, int slot, out string reason)
    {
        var others = this.Storage.Items.Where(i => i != item).ToList();
        if (this.IsEquipmentSlot(slot))
        {
            if (item.Definition?.ItemSlot is null)
            {
                reason = string.Format(Resources.AccountManagerNotEquippable, item.Definition?.Name);
                return false;
            }

            if (!item.Definition.ItemSlot.ItemSlots.Contains(slot))
            {
                reason = string.Format(Resources.AccountManagerCannotBeEquippedAs, item.Definition.Name, EquipmentNames[slot]);
                return false;
            }

            if (others.Any(o => o.ItemSlot == slot))
            {
                reason = string.Format(Resources.AccountManagerSlotInUse, EquipmentNames[slot]);
                return false;
            }

            reason = string.Empty;
            return true;
        }

        var area = this.Areas.FirstOrDefault(a => a.Contains(slot));
        if (area is null)
        {
            reason = string.Format(Resources.AccountManagerSlotOutside, slot);
            return false;
        }

        var (width, height) = SizeOf(item);
        if (area.ColumnOf(slot) + width > StorageArea.Columns || area.RowOf(slot) + height > area.Rows)
        {
            reason = Resources.AccountManagerItemDoesNotFit;
            return false;
        }

        var cells = this.CellsOf(item, slot).ToHashSet();
        if (others.Where(o => area.Contains(o.ItemSlot)).SelectMany(o => this.CellsOf(o, o.ItemSlot)).Any(cells.Contains))
        {
            reason = Resources.AccountManagerItemInTheWay;
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static IEnumerable<int> Cells(int slot, int width, int height)
    {
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                yield return slot + (row * StorageArea.Columns) + column;
            }
        }
    }

    private IEnumerable<int> CellsOf(Item item, int slot)
    {
        if (this.IsEquipmentSlot(slot))
        {
            return new[] { slot };
        }

        var (width, height) = SizeOf(item);
        return Cells(slot, width, height);
    }
}
