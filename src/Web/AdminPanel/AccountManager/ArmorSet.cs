// <copyright file="ArmorSet.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// An armor set: the helm, armor, pants, gloves and boots with the same item number.
/// </summary>
/// <param name="Name">The name of the set, e.g. "Dragon".</param>
/// <param name="Number">The item number of the pieces.</param>
/// <param name="Pieces">The pieces.</param>
public sealed record ArmorSet(string Name, short Number, IReadOnlyList<ItemDefinition> Pieces)
{
    /// <summary>
    /// Gets the piece which stands for the set: its armor, which decides the classes.
    /// </summary>
    public ItemDefinition Main => this.Pieces.FirstOrDefault(p => p.Group == ItemSets.ArmorGroup) ?? this.Pieces[0];
}
