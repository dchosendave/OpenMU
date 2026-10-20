// <copyright file="AccountOnlineStatus.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// Asks the login server and the game servers of this process whether an account is in use.
/// </summary>
/// <remarks>
/// The login server knows the accounts of logged in players, but not the accounts of offline leveling
/// sessions, which are logged off from it while their character still lives on a game server.
/// So both are asked.
/// </remarks>
public class AccountOnlineStatus : IAccountOnlineStatus
{
    private readonly ILoginServer _loginServer;
    private readonly IServerProvider _serverProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountOnlineStatus"/> class.
    /// </summary>
    /// <param name="loginServer">The login server.</param>
    /// <param name="serverProvider">The server provider.</param>
    public AccountOnlineStatus(ILoginServer loginServer, IServerProvider serverProvider)
    {
        this._loginServer = loginServer;
        this._serverProvider = serverProvider;
    }

    /// <inheritdoc />
    public async ValueTask<bool> IsOnlineAsync(string loginName)
    {
        var loggedIn = await this._loginServer.GetSnapshotAsync().ConfigureAwait(false);
        if (loggedIn.Keys.Any(name => string.Equals(name, loginName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        foreach (var context in this._serverProvider.Servers.OfType<IGameServerContextProvider>().Select(s => s.Context))
        {
            var players = await context.GetPlayersAsync().ConfigureAwait(false);
            if (players.Any(p => string.Equals(p.Account?.LoginName, loginName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
