// <copyright file="PlayerSitePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerSite;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The features of the player website besides its mini games: the daily check-in, the character tools and
/// the player market. This plugin holds their settings; the website uses them through the admin panel's API.
/// </summary>
[Guid("A3E6C9B2-4D71-4F08-9E25-6B8D1C3F7A90")]
[PlugIn]
[Display(Name = "Player website", Description = "Daily check-in rewards, web character tools (reset, unstuck, stats, PK clear) and the player market of the player website.")]
public class PlayerSitePlugIn : IFeaturePlugIn, ISupportCustomConfiguration<PlayerSiteConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public PlayerSiteConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new PlayerSiteConfiguration();
}
