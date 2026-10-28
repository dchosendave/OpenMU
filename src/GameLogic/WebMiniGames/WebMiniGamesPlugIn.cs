// <copyright file="WebMiniGamesPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.WebMiniGames;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The mini games of the player website (color game, prize wheel, drop ball and scratch cards), in which
/// players bet coins of their account. This plugin only holds their configuration: odds, prizes and limits.
/// The games are played through the player website API of the admin panel, while the account is offline.
/// </summary>
[Guid("5D3F8B21-6C4E-4A97-B1D8-2E7F9A0C4B63")]
[PlugIn]
[Display(Name = "Web mini games", Description = "Coin mini games of the player website (color game, prize wheel, drop ball, scratch cards): odds, prizes and limits. Deactivate it to close all games.")]
public class WebMiniGamesPlugIn : IFeaturePlugIn, ISupportCustomConfiguration<WebMiniGamesConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public WebMiniGamesConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new WebMiniGamesConfiguration();
}
