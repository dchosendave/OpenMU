// <copyright file="CastleSiegePointModePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.CastleSiege.PointMode;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Turns Castle Siege into a point battle: seals, switch holder kills and held time score points, the
/// guild with the most points takes the castle, and the placements are rewarded with items for the guild
/// master's vault and coins. Deactivate it for the classic rules.
/// </summary>
[Guid("C7B2E4A9-3F18-4D6C-8A51-9E0D2B7F4C36")]
[PlugIn]
[Display(Name = "Castle Siege: point mode", Description = "Seals, switch holder kills and held time score points; the guild with the most points wins the castle. Rewards go to the guild master's vault and as coins.")]
public class CastleSiegePointModePlugIn : IFeaturePlugIn, ISupportCustomConfiguration<CastleSiegePointModeConfiguration>, ISupportDefaultCustomConfiguration
{
    private readonly Lazy<ICastleSiegeRepository?>? _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegePointModePlugIn"/> class.
    /// </summary>
    public CastleSiegePointModePlugIn()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegePointModePlugIn"/> class.
    /// </summary>
    /// <param name="services">The services, which may offer the storage of the battles.</param>
    [ActivatorUtilitiesConstructor]
    public CastleSiegePointModePlugIn(IServiceProvider services)
    {
        // Resolved on first use: the plugins are created while the services are still being built.
        this._repository = new Lazy<ICastleSiegeRepository?>(services.GetService<ICastleSiegeRepository>);
    }

    /// <summary>
    /// Gets the storage of the battles and vault rewards; <c>null</c>, when the server runs without the
    /// admin panel's storage. Then the battles aren't stored and no items are given out, only coins.
    /// </summary>
    public ICastleSiegeRepository? Repository => this._repository?.Value;

    /// <inheritdoc />
    public CastleSiegePointModeConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new CastleSiegePointModeConfiguration();
}
