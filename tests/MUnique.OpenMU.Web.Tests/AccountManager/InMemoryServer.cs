// <copyright file="InMemoryServer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AccountManager;

using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Web.AdminPanel.AccountManager;
using Nito.AsyncEx;

/// <summary>
/// A database with the same content as a new server (Season 6 data and test accounts), in memory, for the tests of the account manager.
/// </summary>
/// <remarks>
/// The initialization takes a while, so all tests share one instance. They create their own accounts, so they don't affect each other.
/// In-memory contexts share their objects, so saving or discarding makes no difference here.
/// </remarks>
public sealed class InMemoryServer
{
    private static readonly AsyncLazy<InMemoryServer> Instance = new(CreateAsync);

    private static int accountCounter;

    private InMemoryServer(InMemoryPersistenceContextProvider provider, GameConfiguration configuration)
    {
        this.Provider = provider;
        this.Database = new AccountManagerDatabase(provider, configuration, this.Online);
    }

    /// <summary>
    /// Gets the persistence context provider.
    /// </summary>
    public InMemoryPersistenceContextProvider Provider { get; }

    /// <summary>
    /// Gets the online status of the accounts, which the tests control.
    /// </summary>
    public FakeAccountOnlineStatus Online { get; } = new();

    /// <summary>
    /// Gets the database of the account manager.
    /// </summary>
    public AccountManagerDatabase Database { get; }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    /// <returns>The shared instance.</returns>
    public static Task<InMemoryServer> GetAsync() => Instance.Task;

    /// <summary>
    /// Returns a unique character name.
    /// </summary>
    /// <returns>The character name.</returns>
    public static string NewCharacterName() => $"Hero{Interlocked.Increment(ref accountCounter)}";

    /// <summary>
    /// Creates a new, empty account with the password "secret".
    /// </summary>
    /// <returns>The login name.</returns>
    public async Task<string> NewAccountAsync()
    {
        var loginName = $"acc{Interlocked.Increment(ref accountCounter)}";
        await this.Database.CreateAccountAsync(loginName, "secret", null).ConfigureAwait(false);
        return loginName;
    }

    private static async Task<InMemoryServer> CreateAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        return new InMemoryServer(provider, configuration);
    }
}
