// <copyright file="WebMiniGamePlay.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// One play of a mini game of the player website: who bet how much and what it paid. It's the history of
/// the games and the base of the daily bet limit.
/// </summary>
public class WebMiniGamePlay
{
    /// <summary>
    /// The maximum length of the <see cref="Result"/>.
    /// </summary>
    public const int ResultMaximumLength = 1000;

    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the login name of the account which played.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets the game, e.g. "color".</summary>
    public string Game { get; set; } = string.Empty;

    /// <summary>Gets or sets the coins the play cost.</summary>
    public int Bet { get; set; }

    /// <summary>Gets or sets the coins the play paid, the bet included.</summary>
    public int Payout { get; set; }

    /// <summary>Gets or sets the currency of bet and payout.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the outcome as JSON, e.g. the rolled dice.</summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>Gets or sets when it was played (UTC).</summary>
    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;
}
