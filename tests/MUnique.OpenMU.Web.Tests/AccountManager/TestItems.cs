// <copyright file="TestItems.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AccountManager;

using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Web.AdminPanel.AccountManager;

/// <summary>
/// Item definitions of the Season 6 data which the tests use.
/// </summary>
public static class TestItems
{
    /// <summary>
    /// Gets the item definition with the name.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="name">The name in the neutral language.</param>
    /// <returns>The item definition.</returns>
    public static ItemDefinition Named(this AccountManagerDatabase database, string name)
        => database.ItemDefinitions.First(d => d.Name.ValueInNeutralLanguage == name);

    /// <summary>
    /// Gets the item definition with the group and number.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="group">The group.</param>
    /// <param name="number">The number.</param>
    /// <returns>The item definition.</returns>
    public static ItemDefinition ByNumber(this AccountManagerDatabase database, byte group, short number)
        => database.ItemDefinitions.First(d => d.Group == group && d.Number == number);

    /// <summary>
    /// Gets the Jewel of Bless: 1x1, not equippable.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <returns>The item definition.</returns>
    public static ItemDefinition Jewel(this AccountManagerDatabase database) => database.ByNumber(14, 13);

    /// <summary>
    /// Gets the Kris: a 1x2 sword with excellent options.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <returns>The item definition.</returns>
    public static ItemDefinition Kris(this AccountManagerDatabase database) => database.ByNumber(0, 0);

    /// <summary>
    /// Gets a helm, which is equippable only in the helm slot (2).
    /// </summary>
    /// <param name="database">The database.</param>
    /// <returns>The item definition.</returns>
    public static ItemDefinition Helm(this AccountManagerDatabase database) => database.ItemDefinitions.First(d => d.Group == 7);

    /// <summary>
    /// Gets the largest item (by cells) which fits in a vault.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <returns>The item definition.</returns>
    public static ItemDefinition Large(this AccountManagerDatabase database)
        => database.ItemDefinitions.Where(d => d.Width <= 8 && d.Height <= 8).OrderByDescending(d => d.Width * d.Height).First();

    /// <summary>
    /// Gets an item which can be part of an ancient set.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <returns>The item definition.</returns>
    public static ItemDefinition AncientItem(this AccountManagerDatabase database)
        => database.ItemDefinitions.First(d => d.PossibleItemSetGroups.SelectMany(g => g.Items).Any(i => i.ItemDefinition == d && i.AncientSetDiscriminator > 0));

    /// <summary>
    /// Gets an item with at least three sockets.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <returns>The item definition.</returns>
    public static ItemDefinition SocketItem(this AccountManagerDatabase database) => database.ItemDefinitions.First(d => d.MaximumSockets >= 3);

    /// <summary>
    /// Gets an equippable item with excellent options.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <returns>The item definition.</returns>
    public static ItemDefinition ExcellentItem(this AccountManagerDatabase database)
        => database.ItemDefinitions.First(d => d.PossibleItemOptions.Any(o => o.PossibleOptions.Any(p => p.OptionType == ItemOptionTypes.Excellent)) && d.ItemSlot is not null);
}
