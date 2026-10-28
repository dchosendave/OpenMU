// <copyright file="PlayerSiteService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.PlayerSite;

using System.Collections.Concurrent;
using System.Threading;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.Web.AdminPanel.AccountManager;

/// <summary>
/// Changes which players request on the player website: password changes, moving items between the vault and
/// their characters, and buying item packages in the web shop.
/// </summary>
/// <remarks>
/// It works on the <see cref="AccountSession"/> of the account manager, so it shares its rules: changes are
/// only saved while the account is offline, because the game server would overwrite them otherwise.
/// Changes of one account are serialized, so two purchases can't spend the same balance.
/// </remarks>
public sealed class PlayerSiteService : IDisposable
{
    private const string OnlineMessage = "Log out of the game first, otherwise the game server would overwrite the change.";

    private readonly IPersistenceContextProvider _persistenceContextProvider;
    private readonly IAccountOnlineStatus _onlineStatus;
    private readonly IItemPackageRepository _packages;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _accountLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private IContext? _configurationContext;
    private AccountManagerDatabase? _database;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerSiteService"/> class.
    /// </summary>
    /// <param name="persistenceContextProvider">The persistence context provider.</param>
    /// <param name="onlineStatus">The online status of accounts.</param>
    /// <param name="packages">The item packages, which are the products of the web shop.</param>
    public PlayerSiteService(IPersistenceContextProvider persistenceContextProvider, IAccountOnlineStatus onlineStatus, IItemPackageRepository packages)
    {
        this._persistenceContextProvider = persistenceContextProvider;
        this._onlineStatus = onlineStatus;
        this._packages = packages;
    }

    /// <summary>
    /// Sets a new password.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <param name="password">The new password.</param>
    /// <returns>The result.</returns>
    public Task<PlayerSiteResult> ChangePasswordAsync(string loginName, string password)
        => this.ChangeAsync(loginName, session =>
        {
            session.SetPassword(password);
            return Task.FromResult<object?>(null);
        });

    /// <summary>
    /// Moves an item between the vault and the inventories of the characters of the account.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <param name="from">The name of the character, or <c>null</c> for the vault.</param>
    /// <param name="slot">The slot of the item in its storage.</param>
    /// <param name="to">The name of the target character, or <c>null</c> for the vault.</param>
    /// <returns>The result, with the new slot of the item.</returns>
    public Task<PlayerSiteResult> MoveItemAsync(string loginName, string? from, byte slot, string? to)
        => this.ChangeAsync(loginName, session =>
        {
            var source = StorageOf(session, from) ?? throw new PlayerSiteException(PlayerSiteResult.NotFound, $"There is no character '{from}' on this account.");
            var target = StorageOf(session, to) ?? throw new PlayerSiteException(PlayerSiteResult.NotFound, $"There is no character '{to}' on this account.");
            if (source == target)
            {
                throw new PlayerSiteException(PlayerSiteResult.Invalid, "The item is already there.");
            }

            var item = source.ItemAt(slot) ?? throw new PlayerSiteException(PlayerSiteResult.Invalid, "There is no item in this slot.");
            if (target.FindFreeSlot(item) is null)
            {
                throw new PlayerSiteException(PlayerSiteResult.NoSpace, $"There is no room for {item.Definition?.Name} in {target.Title}.");
            }

            session.TransferItem(source, item, target);
            return Task.FromResult<object?>(new { slot = item.ItemSlot });
        });

    /// <summary>
    /// Buys an item package of the web shop: takes the price from the account and puts the items and zen of
    /// the package into its vault.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <param name="packageId">The identifier of the package.</param>
    /// <returns>The result, with the remaining balance of the currency.</returns>
    public Task<PlayerSiteResult> BuyPackageAsync(string loginName, Guid packageId)
        => this.ChangeAsync(loginName, async session =>
        {
            var package = (await this._packages.GetAllAsync().ConfigureAwait(false)).FirstOrDefault(p => p.Id == packageId && p.IsInWebShop)
                ?? throw new PlayerSiteException(PlayerSiteResult.NotFound, "This product isn't for sale.");
            var balance = BalanceOf(session, package.WebShopCurrency);
            if (balance < package.WebShopPrice)
            {
                throw new PlayerSiteException(PlayerSiteResult.InsufficientFunds, $"This costs {package.WebShopPrice:N0}, but you have {balance:N0}.");
            }

            // Pay first: zen is paid from the vault, which the package may add zen to.
            SetBalance(session, package.WebShopCurrency, balance - package.WebShopPrice);
            PackageResult result;
            try
            {
                result = session.ApplyPackage(session.Vault, package, null);
            }
            catch (AccountManagerException ex)
            {
                throw new PlayerSiteException(PlayerSiteResult.NoSpace, ex.Message);
            }

            if (result.Problems.Count > 0)
            {
                // Saving would take the price without delivering everything; the session is discarded instead.
                throw new PlayerSiteException(PlayerSiteResult.Invalid, "This product can't be delivered right now. Nothing was charged.");
            }

            return new
            {
                package = package.Name,
                items = result.Added,
                zen = result.Zen,
                currency = package.WebShopCurrency.ToString(),
                balance = BalanceOf(session, package.WebShopCurrency),
            };
        });

    /// <inheritdoc />
    public void Dispose()
    {
        this._configurationContext?.Dispose();
        this._initializationLock.Dispose();
        foreach (var accountLock in this._accountLocks.Values)
        {
            accountLock.Dispose();
        }
    }

    private static StorageModel? StorageOf(AccountSession session, string? characterName)
        => string.IsNullOrEmpty(characterName)
            ? session.Vault
            : session.Characters.FirstOrDefault(c => string.Equals(c.Name, characterName, StringComparison.OrdinalIgnoreCase))?.Inventory;

    private static int BalanceOf(AccountSession session, WebShopCurrency currency) => currency switch
    {
        WebShopCurrency.WCoinC => session.WCoinC,
        WebShopCurrency.WCoinP => session.WCoinP,
        WebShopCurrency.GoblinPoints => session.GoblinPoints,
        _ => session.VaultZen,
    };

    private static void SetBalance(AccountSession session, WebShopCurrency currency, int value)
    {
        switch (currency)
        {
            case WebShopCurrency.WCoinC:
                session.WCoinC = value;
                break;
            case WebShopCurrency.WCoinP:
                session.WCoinP = value;
                break;
            case WebShopCurrency.GoblinPoints:
                session.GoblinPoints = value;
                break;
            default:
                session.VaultZen = value;
                break;
        }
    }

    /// <summary>
    /// Changes an account under the rules of this service: only while it's offline, one change per account at a time.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <param name="change">Changes the account and returns the data of the result. It may throw an
    /// <see cref="AccountManagerException"/> or call <see cref="Refuse"/> to refuse the change.</param>
    /// <param name="afterSave">Runs after the change was saved, still holding the lock of the account.</param>
    /// <returns>The result.</returns>
    internal Task<PlayerSiteResult> ChangeAccountAsync(string loginName, Func<AccountSession, Task<object?>> change, Func<object?, Task>? afterSave = null)
        => this.ChangeAsync(loginName, change, afterSave);

    /// <summary>
    /// Creates the exception which refuses a change in <see cref="ChangeAccountAsync"/>.
    /// </summary>
    /// <param name="status">The status, see <see cref="PlayerSiteResult"/>.</param>
    /// <param name="message">The message for the player.</param>
    /// <returns>The exception to throw.</returns>
    internal static Exception Refuse(string status, string message) => new PlayerSiteException(status, message);

    /// <summary>
    /// Makes sure an account exists which nobody can log in to, e.g. the escrow account of the player market:
    /// banned, with an extended vault and the given marker as e-mail address.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="marker">The e-mail address which marks the account as the system's own.</param>
    /// <returns><c>true</c> if the account is ready; <c>false</c> if a player owns an account with this name.</returns>
    internal async Task<bool> EnsureSystemAccountAsync(string loginName, string marker)
    {
        var database = await this.GetDatabaseAsync().ConfigureAwait(false);
        AccountSession session;
        try
        {
            session = await database.OpenAccountAsync(loginName).ConfigureAwait(false);
        }
        catch (AccountManagerException)
        {
            var password = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(12));
            await database.CreateAccountAsync(loginName, password, marker).ConfigureAwait(false);
            session = await database.OpenAccountAsync(loginName).ConfigureAwait(false);
        }

        using (session)
        {
            if (session.EMail != marker || session.Characters.Count > 0)
            {
                return false;
            }

            if (session.State != DataModel.Entities.AccountState.Banned || !session.IsVaultExtended)
            {
                session.State = DataModel.Entities.AccountState.Banned;
                session.IsVaultExtended = true;
                await session.SaveAsync().ConfigureAwait(false);
            }

            return true;
        }
    }

    private async Task<PlayerSiteResult> ChangeAsync(string loginName, Func<AccountSession, Task<object?>> change, Func<object?, Task>? afterSave = null)
    {
        var accountLock = this._accountLocks.GetOrAdd(loginName, _ => new SemaphoreSlim(1, 1));
        await accountLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (await this._onlineStatus.IsOnlineAsync(loginName).ConfigureAwait(false))
            {
                return new PlayerSiteResult(PlayerSiteResult.Online, OnlineMessage);
            }

            var database = await this.GetDatabaseAsync().ConfigureAwait(false);
            AccountSession session;
            try
            {
                session = await database.OpenAccountAsync(loginName).ConfigureAwait(false);
            }
            catch (AccountManagerException)
            {
                return new PlayerSiteResult(PlayerSiteResult.NotFound, "The account doesn't exist.");
            }

            using (session)
            {
                // Coins granted while the account was away (check-in rewards, market sales) count right away.
                await session.ApplyPendingCoinGrantsAsync().ConfigureAwait(false);
                object? data;
                try
                {
                    data = await change(session).ConfigureAwait(false);
                }
                catch (PlayerSiteException ex)
                {
                    return new PlayerSiteResult(ex.Status, ex.Message);
                }
                catch (AccountManagerException ex)
                {
                    return new PlayerSiteResult(PlayerSiteResult.Invalid, ex.Message);
                }

                if (await session.SaveAsync().ConfigureAwait(false) != SaveOutcome.Saved)
                {
                    return new PlayerSiteResult(PlayerSiteResult.Online, OnlineMessage);
                }

                if (afterSave is not null)
                {
                    await afterSave(data).ConfigureAwait(false);
                }

                return new PlayerSiteResult(PlayerSiteResult.Ok, Data: data);
            }
        }
        finally
        {
            accountLock.Release();
        }
    }

    private async Task<AccountManagerDatabase> GetDatabaseAsync()
    {
        if (this._database is { } database)
        {
            return database;
        }

        await this._initializationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (this._database is null)
            {
                // Like the account manager: accounts reference the objects of the cached configuration.
                this._configurationContext = this._persistenceContextProvider.CreateNewConfigurationContext();
                var configuration = (await this._configurationContext.GetAsync<GameConfiguration>().ConfigureAwait(false)).FirstOrDefault()
                    ?? throw new InvalidOperationException("There is no game configuration.");
                this._database = new AccountManagerDatabase(this._persistenceContextProvider, configuration, this._onlineStatus);
            }

            return this._database;
        }
        finally
        {
            this._initializationLock.Release();
        }
    }

    private sealed class PlayerSiteException(string status, string message) : Exception(message)
    {
        public string Status { get; } = status;
    }
}
