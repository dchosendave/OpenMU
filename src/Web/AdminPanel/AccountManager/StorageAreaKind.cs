// <copyright file="StorageAreaKind.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

/// <summary>
/// What an area of a storage is. The game shows each one as its own grid.
/// </summary>
public enum StorageAreaKind
{
    /// <summary>
    /// The 8x8 inventory.
    /// </summary>
    Inventory,

    /// <summary>
    /// One of the up to four inventory extensions (8x4 each).
    /// </summary>
    Expansion,

    /// <summary>
    /// The personal store (8x4). Its items need a price, so items are never placed there automatically.
    /// </summary>
    PersonalStore,

    /// <summary>
    /// A page of the vault (8x15). An extended vault has two.
    /// </summary>
    Vault,
}
