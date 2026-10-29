// <copyright file="OfflineLevelingLimitPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Offline;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Limits how many characters of one IP address may level offline at the same time, so players with
/// more computers don't farm many times more (game-design.md, "More decisions").
/// </summary>
[Guid("D7E3A915-4B68-4C21-8F0A-6E2B9C5D1A37")]
[PlugIn]
[Display(Name = "Offline leveling limit", Description = "How many characters of one IP address may level offline at the same time.")]
public class OfflineLevelingLimitPlugIn : IFeaturePlugIn, ISupportCustomConfiguration<OfflineLevelingLimitConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public OfflineLevelingLimitConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new OfflineLevelingLimitConfiguration();
}

/// <summary>
/// The configuration of the <see cref="OfflineLevelingLimitPlugIn"/>.
/// </summary>
public class OfflineLevelingLimitConfiguration
{
    /// <summary>Gets or sets how many characters of one IP address may level offline at the same time.</summary>
    [Display(Name = "Offline characters per IP address")]
    [Range(1, 100)]
    public int MaximumPerIpAddress { get; set; } = 2;
}
