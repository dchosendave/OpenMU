// <copyright file="FakeAccountOnlineStatus.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AccountManager;

using System.Collections.Concurrent;
using MUnique.OpenMU.Web.AdminPanel.AccountManager;

/// <summary>
/// An online status which the tests control. Accounts are offline, unless set otherwise.
/// </summary>
public sealed class FakeAccountOnlineStatus : IAccountOnlineStatus
{
    private readonly ConcurrentDictionary<string, bool> _states = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Sets whether an account is online.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="online">If set to <c>true</c>, the account is online.</param>
    public void Set(string loginName, bool online) => this._states[loginName] = online;

    /// <inheritdoc />
    public ValueTask<bool> IsOnlineAsync(string loginName) => ValueTask.FromResult(this._states.TryGetValue(loginName, out var online) && online);
}
