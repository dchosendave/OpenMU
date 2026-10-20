// <copyright file="SaveOutcome.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

/// <summary>
/// The result of <see cref="AccountSession.SaveAsync"/>.
/// </summary>
public enum SaveOutcome
{
    /// <summary>
    /// The changes are in the database.
    /// </summary>
    Saved,

    /// <summary>
    /// Nothing was saved, because the account is logged in. The game server would overwrite the changes when the player logs out.
    /// </summary>
    BlockedOnline,
}
