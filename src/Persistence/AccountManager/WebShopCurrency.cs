// <copyright file="WebShopCurrency.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// The currency in which the web shop of the player website sells an <see cref="ItemPackage"/>.
/// </summary>
public enum WebShopCurrency
{
    /// <summary>
    /// The WCoin (C) balance of the account.
    /// </summary>
    WCoinC,

    /// <summary>
    /// The WCoin (P) balance of the account.
    /// </summary>
    WCoinP,

    /// <summary>
    /// The goblin points of the account.
    /// </summary>
    GoblinPoints,

    /// <summary>
    /// The zen in the vault of the account.
    /// </summary>
    Zen,
}
