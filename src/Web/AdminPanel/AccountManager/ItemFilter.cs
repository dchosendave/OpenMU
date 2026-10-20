// <copyright file="ItemFilter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// One entry of the item catalog: an item group, or the part of one which fits an equipment slot.
/// </summary>
/// <param name="Name">The name.</param>
/// <param name="Matches">The function which determines whether an item definition belongs to the entry.</param>
public sealed record ItemFilter(string Name, Func<ItemDefinition, bool> Matches);
