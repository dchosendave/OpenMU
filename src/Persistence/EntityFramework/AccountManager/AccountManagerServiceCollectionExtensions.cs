// <copyright file="AccountManagerServiceCollectionExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AccountManager;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// Extensions to register the persistence of the account manager of the admin panel.
/// </summary>
public static class AccountManagerServiceCollectionExtensions
{
    /// <summary>
    /// Adds the database backed <see cref="IItemPackageRepository"/> and <see cref="IItemOptionPresetRepository"/>
    /// to the service collection. They need the <see cref="AdminAuth.AdminAuthServiceCollectionExtensions.AddAdminUserRepository"/>,
    /// because they share its storage.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same instance, to allow chaining of further calls.</returns>
    public static IServiceCollection AddAccountManagerRepositories(this IServiceCollection services)
    {
        services.TryAddSingleton<IItemPackageRepository, ItemPackageRepository>();
        services.TryAddSingleton<IItemOptionPresetRepository, ItemOptionPresetRepository>();
        services.TryAddSingleton<IWebMiniGamePlayRepository, WebMiniGamePlayRepository>();
        services.TryAddSingleton<IPlayerSiteRepository, PlayerSiteRepository>();
        services.TryAddSingleton<ICastleSiegeRepository, CastleSiegeRepository>();
        return services;
    }
}
