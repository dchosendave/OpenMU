// <copyright file="StorageArea.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using MUnique.OpenMU.DataModel;

/// <summary>
/// One area of an item storage: a grid of 8 columns. Like in the game, items never span two areas.
/// </summary>
/// <param name="Title">The title.</param>
/// <param name="FirstSlot">The first slot.</param>
/// <param name="Rows">The number of rows.</param>
/// <param name="Kind">The kind of area.</param>
public sealed record StorageArea(string Title, int FirstSlot, int Rows, StorageAreaKind Kind)
{
    /// <summary>
    /// The number of columns of every area.
    /// </summary>
    public static readonly int Columns = InventoryConstants.RowSize;

    /// <summary>
    /// Gets the number of slots.
    /// </summary>
    public int SlotCount => this.Rows * Columns;

    /// <summary>
    /// Gets a value indicating whether new items may be put here when no slot is given.
    /// </summary>
    public bool AutoPlace => this.Kind != StorageAreaKind.PersonalStore;

    /// <summary>
    /// Determines whether the slot belongs to this area.
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <returns><c>true</c>, if the slot belongs to this area; otherwise, <c>false</c>.</returns>
    public bool Contains(int slot) => slot >= this.FirstSlot && slot < this.FirstSlot + this.SlotCount;

    /// <summary>
    /// Gets the row of the slot within this area.
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <returns>The row.</returns>
    public int RowOf(int slot) => (slot - this.FirstSlot) / Columns;

    /// <summary>
    /// Gets the column of the slot within this area.
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <returns>The column.</returns>
    public int ColumnOf(int slot) => (slot - this.FirstSlot) % Columns;
}
