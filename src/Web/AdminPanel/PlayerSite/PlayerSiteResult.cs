// <copyright file="PlayerSiteResult.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.PlayerSite;

/// <summary>
/// The outcome of a change which a player requested on the player website.
/// </summary>
/// <param name="Status">The status, see the constants of this class.</param>
/// <param name="Message">A message for the player, when the change was refused.</param>
/// <param name="Data">The data which the change returns, when it succeeded.</param>
public sealed record PlayerSiteResult(string Status, string? Message = null, object? Data = null)
{
    /// <summary>The change was saved.</summary>
    public const string Ok = "ok";

    /// <summary>The account or something it refers to doesn't exist.</summary>
    public const string NotFound = "not_found";

    /// <summary>The account is logged in; the game server would overwrite the change.</summary>
    public const string Online = "online";

    /// <summary>The request is invalid, e.g. there is no item in the given slot.</summary>
    public const string Invalid = "invalid";

    /// <summary>The target storage has no room for the items.</summary>
    public const string NoSpace = "no_space";

    /// <summary>The account can't pay the price.</summary>
    public const string InsufficientFunds = "insufficient_funds";

    /// <summary>Gets a value indicating whether the change was saved.</summary>
    public bool Succeeded => this.Status == Ok;
}
