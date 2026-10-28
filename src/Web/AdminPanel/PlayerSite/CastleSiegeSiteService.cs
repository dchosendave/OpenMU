// <copyright file="CastleSiegeSiteService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.PlayerSite;

using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.CastleSiege.PointMode;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The Castle Siege point mode on the player website: keeps its settings (the configuration of the
/// <see cref="CastleSiegePointModePlugIn"/>) and puts the item rewards into the vaults of the guild
/// masters while their accounts are offline.
/// </summary>
public sealed class CastleSiegeSiteService : BackgroundService
{
    private static readonly Guid PlugInId = typeof(CastleSiegePointModePlugIn).GUID;

    /// <summary>How often the pending vault rewards are tried.</summary>
    private static readonly TimeSpan DeliveryInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceProvider _services;
    private readonly ILogger<CastleSiegeSiteService> _logger;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeSiteService"/> class.
    /// </summary>
    /// <param name="services">The services. They're resolved on use: this service starts with the host,
    /// before the game configuration is loaded.</param>
    /// <param name="logger">The logger.</param>
    public CastleSiegeSiteService(IServiceProvider services, ILogger<CastleSiegeSiteService> logger)
    {
        this._services = services;
        this._logger = logger;
    }

    /// <summary>
    /// Saves the settings.
    /// </summary>
    /// <param name="active">Whether the point mode is on; off means the classic rules.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The result; refused with the problems if the configuration is invalid.</returns>
    public async Task<PlayerSiteResult> SaveSettingsAsync(bool active, CastleSiegePointModeConfiguration configuration)
    {
        if (configuration.Validate() is { Count: > 0 } problems)
        {
            return new PlayerSiteResult(PlayerSiteResult.Invalid, string.Join(" ", problems));
        }

        await this._saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var gameConfiguration = await this.ConfigurationSource.GetOwnerAsync().ConfigureAwait(false);
            var unknown = AllItems(configuration).FirstOrDefault(i => !gameConfiguration.Items.Any(d => d.Group == i.Group && d.Number == i.Number));
            if (unknown is not null)
            {
                return new PlayerSiteResult(PlayerSiteResult.Invalid, $"There's no item {unknown.Group}:{unknown.Number}.");
            }

            var context = await this.ConfigurationSource.GetContextAsync().ConfigureAwait(false);
            var plugIn = gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == PlugInId);
            if (plugIn is null)
            {
                plugIn = context.CreateNew<PlugInConfiguration>();
                plugIn.TypeId = PlugInId;
                gameConfiguration.PlugInConfigurations.Add(plugIn);
            }

            plugIn.IsActive = active;
            plugIn.SetConfiguration(configuration, new ByDataSourceReferenceHandler(this.ConfigurationSource));
            await context.SaveChangesAsync().ConfigureAwait(false);
            return new PlayerSiteResult(PlayerSiteResult.Ok);
        }
        finally
        {
            this._saveLock.Release();
        }
    }

    /// <summary>
    /// Puts the pending vault rewards of offline accounts into their vaults. An account which is online,
    /// or whose vault is full, is tried again later.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of delivered rewards.</returns>
    public async Task<int> DeliverPendingAsync(CancellationToken cancellationToken = default)
    {
        if (this._services.GetService<ICastleSiegeRepository>() is not { } repository)
        {
            return 0;
        }

        var delivered = 0;
        var pending = await repository.GetPendingVaultRewardsAsync(200, cancellationToken).ConfigureAwait(false);
        foreach (var rewards in pending.GroupBy(r => r.LoginName, StringComparer.OrdinalIgnoreCase))
        {
            var list = rewards.ToList();
            var ids = list.Select(r => r.Id).ToList();
            var result = await this.PlayerSite.ChangeAccountAsync(
                rewards.Key,
                session =>
                {
                    foreach (var reward in list)
                    {
                        var definition = session.Configuration.Items.FirstOrDefault(d => d.Group == reward.ItemGroup && d.Number == reward.ItemNumber)
                            ?? throw PlayerSiteService.Refuse(PlayerSiteResult.Invalid, $"There's no item {reward.ItemGroup}:{reward.ItemNumber}.");
                        session.AddItems(
                            session.Vault,
                            Enumerable.Repeat(definition, reward.Count).ToList(),
                            $"Castle Siege reward ({reward.Placement}, {reward.GuildName})",
                            reward.ItemLevel);
                    }

                    return Task.FromResult<object?>(null);
                }).ConfigureAwait(false);

            if (result.Succeeded)
            {
                await repository.UpdateVaultRewardsAsync(ids, DateTime.UtcNow, null, cancellationToken).ConfigureAwait(false);
                delivered += list.Count;
                this._logger.LogInformation("Castle Siege: put {Count} rewards into the vault of {Login}.", list.Count, rewards.Key);
            }
            else if (result.Status != PlayerSiteResult.Online)
            {
                // Kept pending, e.g. until the guild master makes room in the vault; the reason shows on the website.
                await repository.UpdateVaultRewardsAsync(ids, null, result.Message, cancellationToken).ConfigureAwait(false);
            }
        }

        return delivered;
    }

    private PlayerSiteService PlayerSite => this._services.GetRequiredService<PlayerSiteService>();

    private IDataSource<GameConfiguration> ConfigurationSource => this._services.GetRequiredService<IDataSource<GameConfiguration>>();

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(DeliveryInterval);

        // The first round waits too, until the server has started up.
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await this.DeliverPendingAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                this._logger.LogWarning(ex, "Castle Siege: delivering the vault rewards failed; trying again later.");
            }
        }
    }

    private static IEnumerable<CastleSiegeRewardItem> AllItems(CastleSiegePointModeConfiguration configuration)
        => new[] { configuration.Winner, configuration.Consolation, configuration.Participant }
            .SelectMany(tier => CastleSiegeRewardItem.TryParseList(tier.Items, out var items, out _) ? items : []);
}
