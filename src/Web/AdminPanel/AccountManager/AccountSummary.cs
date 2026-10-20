// <copyright file="AccountSummary.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// An entry of the account list of the account manager.
/// </summary>
/// <param name="LoginName">The login name.</param>
/// <param name="State">The state.</param>
/// <param name="EMail">The e-mail address.</param>
/// <param name="RegistrationDate">The registration date.</param>
public sealed record AccountSummary(string LoginName, AccountState State, string EMail, DateTime RegistrationDate);
