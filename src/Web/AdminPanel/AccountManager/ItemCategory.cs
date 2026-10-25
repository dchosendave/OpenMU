// <copyright file="ItemCategory.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

/// <summary>
/// A heading of the item catalog, with its filters.
/// </summary>
/// <param name="Name">The name.</param>
/// <param name="Filters">The filters.</param>
/// <param name="HasArmorSets">If set to <c>true</c>, the category also offers the armor sets.</param>
public sealed record ItemCategory(string Name, IReadOnlyList<ItemFilter> Filters, bool HasArmorSets = false);
