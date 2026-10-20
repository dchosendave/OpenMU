// <copyright file="AccountManagerDatabase.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using System.Text.RegularExpressions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.Properties;

/// <summary>
/// The access of the account manager to the accounts, through the persistence of the server,
/// so accounts, characters and items are loaded and saved exactly like the server does it.
/// </summary>
public partial class AccountManagerDatabase
{
    /// <summary>
    /// The minimum length of login names and passwords, like the registration of the admin panel's account API.
    /// </summary>
    public const int MinimumLength = 3;

    /// <summary>
    /// The maximum length of login names, like the login field of the game client.
    /// </summary>
    public const int LoginNameMaximumLength = 10;

    /// <summary>
    /// The maximum length of passwords, like the password field of the game client.
    /// </summary>
    public const int PasswordMaximumLength = 20;

    private readonly IPersistenceContextProvider _persistenceContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountManagerDatabase"/> class.
    /// </summary>
    /// <param name="persistenceContextProvider">The persistence context provider.</param>
    /// <param name="configuration">The game configuration.</param>
    /// <param name="onlineStatus">The online status of accounts.</param>
    public AccountManagerDatabase(IPersistenceContextProvider persistenceContextProvider, GameConfiguration configuration, IAccountOnlineStatus onlineStatus)
    {
        this._persistenceContextProvider = persistenceContextProvider;
        this.Configuration = configuration;
        this.OnlineStatus = onlineStatus;
        this.ItemDefinitions = configuration.Items.OrderBy(d => d.Group).ThenBy(d => d.Number).ToList();
        this.ArmorSets = ItemSets.Find(this.ItemDefinitions);
        this.CharacterClasses = configuration.CharacterClasses.OrderBy(c => c.Number).ToList();
        this.Maps = configuration.Maps.OrderBy(m => m.Number).ToList();
    }

    /// <summary>
    /// Gets the game configuration (item definitions, options, classes, maps).
    /// </summary>
    public GameConfiguration Configuration { get; }

    /// <summary>
    /// Gets the online status of accounts.
    /// </summary>
    public IAccountOnlineStatus OnlineStatus { get; }

    /// <summary>
    /// Gets the item definitions, ordered by group and number.
    /// </summary>
    public IReadOnlyList<ItemDefinition> ItemDefinitions { get; }

    /// <summary>
    /// Gets the armor sets: the pieces with the same number in the groups of helms to boots.
    /// </summary>
    public IReadOnlyList<ArmorSet> ArmorSets { get; }

    /// <summary>
    /// Gets the character classes, ordered by number.
    /// </summary>
    public IReadOnlyList<CharacterClass> CharacterClasses { get; }

    /// <summary>
    /// Gets the maps, ordered by number.
    /// </summary>
    public IReadOnlyList<GameMapDefinition> Maps { get; }

    /// <summary>
    /// Validates a login name.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <exception cref="AccountManagerException">When the login name is invalid.</exception>
    public static void ValidateLoginName(string loginName)
    {
        if (loginName.Length is < MinimumLength or > LoginNameMaximumLength || !LoginNameRegex().IsMatch(loginName))
        {
            throw new AccountManagerException(string.Format(Resources.AccountManagerInvalidLoginName, MinimumLength, LoginNameMaximumLength));
        }
    }

    /// <summary>
    /// Validates a password.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <exception cref="AccountManagerException">When the password is invalid.</exception>
    public static void ValidatePassword(string password)
    {
        if (password.Length is < MinimumLength or > PasswordMaximumLength)
        {
            throw new AccountManagerException(string.Format(Resources.AccountManagerInvalidPassword, MinimumLength, PasswordMaximumLength));
        }
    }

    /// <summary>
    /// Creates a context for one editing session of an account. Disposing it discards unsaved changes.
    /// </summary>
    /// <returns>The created context.</returns>
    public IPlayerContext CreatePlayerContext() => this._persistenceContextProvider.CreateNewPlayerContext(this.Configuration);

    /// <summary>
    /// Searches the accounts whose login name contains the search term.
    /// </summary>
    /// <param name="search">The search term. An empty term finds nothing.</param>
    /// <param name="maximum">The maximum number of accounts which are read.</param>
    /// <returns>The found accounts, ordered by login name.</returns>
    public async Task<IReadOnlyList<AccountSummary>> GetAccountsAsync(string? search, int maximum)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return Array.Empty<AccountSummary>();
        }

        var term = search.Trim();
        using var context = this.CreatePlayerContext();

        // The search of the persistence also finds character names; only login names are wanted here.
        var accounts = await context.SearchAccountsAsync(term, 0, maximum).ConfigureAwait(false);
        return accounts
            .Where(a => a.LoginName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Select(a => new AccountSummary(a.LoginName, a.State, a.EMail, a.RegistrationDate))
            .OrderBy(a => a.LoginName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Opens an account for editing.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <returns>The editing session.</returns>
    public Task<AccountSession> OpenAccountAsync(string loginName) => AccountSession.OpenAsync(this, loginName);

    /// <summary>
    /// Registers a new account which can log in right away, like the account API of the admin panel does.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="password">The password.</param>
    /// <param name="email">The optional e-mail address.</param>
    public async Task CreateAccountAsync(string loginName, string password, string? email)
    {
        loginName = loginName.Trim();
        ValidateLoginName(loginName);
        ValidatePassword(password);

        using var context = this.CreatePlayerContext();
        if (await context.GetAccountByLoginNameAsync(loginName).ConfigureAwait(false) is not null)
        {
            throw new AccountManagerException(string.Format(Resources.AccountManagerAccountExists, loginName));
        }

        var account = context.CreateNew<Account>();
        account.LoginName = loginName;
        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        account.EMail = email?.Trim() ?? string.Empty;
        account.State = AccountState.Normal;
        account.RegistrationDate = DateTime.UtcNow;
        account.Vault = context.CreateNew<ItemStorage>();
        if (!await context.SaveChangesAsync().ConfigureAwait(false))
        {
            throw new AccountManagerException(string.Format(Resources.AccountManagerAccountNotSaved, loginName));
        }
    }

    /// <summary>
    /// Deletes an account with its characters and items. It's refused while the account is online.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    public async Task DeleteAccountAsync(string loginName)
    {
        if (await this.OnlineStatus.IsOnlineAsync(loginName).ConfigureAwait(false))
        {
            throw new AccountManagerException(string.Format(Resources.AccountManagerAccountIsOnline, loginName));
        }

        using var context = this.CreatePlayerContext();
        var account = await context.GetAccountByLoginNameAsync(loginName).ConfigureAwait(false)
            ?? throw new AccountManagerException(string.Format(Resources.AccountManagerAccountNotFound, loginName));
        await context.DeleteAsync(account).ConfigureAwait(false);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Determines whether a character name is taken on any account.
    /// </summary>
    /// <param name="name">The character name.</param>
    /// <returns><c>true</c>, if the name is taken; otherwise, <c>false</c>.</returns>
    public async Task<bool> CharacterNameExistsAsync(string name)
    {
        using var context = this.CreatePlayerContext();
        return await context.GetAccountIdByCharacterNameAsync(name).ConfigureAwait(false) is not null;
    }

    [GeneratedRegex("^[A-Za-z0-9_]+$")]
    private static partial Regex LoginNameRegex();
}
