// <copyright file="EquipmentSlot.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

/// <summary>
/// An equipment slot of an inventory.
/// </summary>
/// <param name="Slot">The slot number.</param>
/// <param name="Name">The name.</param>
public sealed record EquipmentSlot(byte Slot, string Name);
