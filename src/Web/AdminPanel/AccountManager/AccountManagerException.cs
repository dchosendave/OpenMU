// <copyright file="AccountManagerException.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

/// <summary>
/// An edit of the account manager which isn't possible. The message is meant for the user.
/// </summary>
public class AccountManagerException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AccountManagerException"/> class.
    /// </summary>
    /// <param name="message">The message for the user.</param>
    public AccountManagerException(string message)
        : base(message)
    {
    }
}
