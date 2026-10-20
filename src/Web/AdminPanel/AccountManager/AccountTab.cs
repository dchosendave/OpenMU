// <copyright file="AccountTab.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

/// <summary>
/// The part of the account which the account manager shows.
/// </summary>
public enum AccountTab
{
    /// <summary>
    /// The items of the selected character and the vault.
    /// </summary>
    Items,

    /// <summary>
    /// The selected character.
    /// </summary>
    Character,

    /// <summary>
    /// The account itself.
    /// </summary>
    Account,
}
