// <copyright file="AccountManagerServiceCollectionExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// Extensions to register the services of the account manager.
/// </summary>
public static class AccountManagerServiceCollectionExtensions
{
    /// <summary>
    /// Adds the services of the account manager. When no database backed repositories for the item packages
    /// and presets are registered before (e.g. in the demo mode), the account manager works without them.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same instance, to allow chaining of further calls.</returns>
    public static IServiceCollection AddAccountManager(this IServiceCollection services)
    {
        services.TryAddSingleton<IItemPackageRepository, UnavailableItemPackageRepository>();
        services.TryAddSingleton<IItemOptionPresetRepository, UnavailableItemOptionPresetRepository>();
        services.AddSingleton<IAccountOnlineStatus, AccountOnlineStatus>();
        services.AddScoped<ItemPackageStore>();
        services.AddScoped<ItemOptionPresetStore>();
        services.AddScoped<AccountManagerState>();
        services.AddSingleton<MUnique.OpenMU.Web.AdminPanel.PlayerSite.PlayerSiteService>();
        services.AddSingleton<MUnique.OpenMU.Web.AdminPanel.PlayerSite.WebMiniGamesService>();
        services.AddSingleton<MUnique.OpenMU.Web.AdminPanel.PlayerSite.PlayerSiteFeaturesService>();
        services.AddSingleton<MUnique.OpenMU.Web.AdminPanel.PlayerSite.CastleSiegeSiteService>();
        services.AddSingleton<MUnique.OpenMU.Web.AdminPanel.PlayerSite.MilestoneRewardsService>();
        services.AddHostedService(s => s.GetRequiredService<MUnique.OpenMU.Web.AdminPanel.PlayerSite.CastleSiegeSiteService>());
        services.TryAddSingleton<IWebMiniGamePlayRepository, UnavailableWebMiniGamePlayRepository>();
        services.TryAddSingleton<IPlayerSiteRepository, UnavailablePlayerSiteRepository>();
        return services;
    }
}
