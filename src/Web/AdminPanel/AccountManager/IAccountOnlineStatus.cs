// <copyright file="IAccountOnlineStatus.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

/// <summary>
/// Tells whether an account is in use by a game server. The data of such an account lives in the memory
/// of the game server and is written back when the player logs out, which would overwrite any edit.
/// </summary>
public interface IAccountOnlineStatus
{
    /// <summary>
    /// Determines whether the account is in use by a game server.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <returns><c>true</c>, if the account is in use; otherwise, <c>false</c>.</returns>
    ValueTask<bool> IsOnlineAsync(string loginName);
}
