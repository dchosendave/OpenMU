// <copyright file="AccountManagerState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.Web.AdminPanel.Properties;
using MUnique.OpenMU.Web.Shared;
using MUnique.OpenMU.Web.Shared.Components.Modal;
using MUnique.OpenMU.Web.Shared.Components.Toast;

/// <summary>
/// The state of the account manager of one user session: the account list, the open account, the open
/// item package and the selection. The components render from it and call its methods. Every method
/// reports problems as toasts instead of throwing.
/// </summary>
public sealed class AccountManagerState : IDisposable
{
    /// <summary>
    /// The maximum number of accounts which one search shows. The database isn't read beyond that.
    /// </summary>
    public const int AccountSearchLimit = 50;

    private readonly IPersistenceContextProvider _persistenceContextProvider;
    private readonly IAccountOnlineStatus _onlineStatus;
    private readonly IToastService _toastService;
    private readonly IModalService _modalService;
    private readonly ILogger<AccountManagerState> _logger;
    private IContext? _configurationContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountManagerState"/> class.
    /// </summary>
    /// <param name="persistenceContextProvider">The persistence context provider.</param>
    /// <param name="onlineStatus">The online status of accounts.</param>
    /// <param name="packages">The item packages.</param>
    /// <param name="presets">The item option presets.</param>
    /// <param name="toastService">The toast service.</param>
    /// <param name="modalService">The modal service.</param>
    /// <param name="logger">The logger.</param>
    public AccountManagerState(
        IPersistenceContextProvider persistenceContextProvider,
        IAccountOnlineStatus onlineStatus,
        ItemPackageStore packages,
        ItemOptionPresetStore presets,
        IToastService toastService,
        IModalService modalService,
        ILogger<AccountManagerState> logger)
    {
        this._persistenceContextProvider = persistenceContextProvider;
        this._onlineStatus = onlineStatus;
        this.Packages = packages;
        this.Presets = presets;
        this._toastService = toastService;
        this._modalService = modalService;
        this._logger = logger;
    }

    /// <summary>
    /// Occurs when the state changed.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Gets the item packages (freebies, rewards, ...).
    /// </summary>
    public ItemPackageStore Packages { get; }

    /// <summary>
    /// Gets the item option presets which items get when they're added.
    /// </summary>
    public ItemOptionPresetStore Presets { get; }

    /// <summary>
    /// Gets the database, after <see cref="InitializeAsync"/>.
    /// </summary>
    public AccountManagerDatabase? Database { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the state is initialized.
    /// </summary>
    public bool IsInitialized => this.Database is not null;

    /// <summary>
    /// Gets the error which occurred during the initialization.
    /// </summary>
    public string? InitializationError { get; private set; }

    /// <summary>
    /// Gets a value indicating whether an operation is running.
    /// </summary>
    public bool IsBusy { get; private set; }

    /// <summary>
    /// Gets the preset which items, which are added from the catalog, get. <c>null</c> adds plain items.
    /// </summary>
    public ItemOptionPreset? CatalogPreset { get; private set; }

    /// <summary>
    /// Gets or sets the account search term.
    /// </summary>
    public string AccountSearch { get; set; } = string.Empty;

    /// <summary>
    /// Gets the accounts which the last search found. It's empty until the user searches.
    /// </summary>
    public IReadOnlyList<AccountSummary> Accounts { get; private set; } = Array.Empty<AccountSummary>();

    /// <summary>
    /// Gets the term of the last search, or <c>null</c> before the first one.
    /// </summary>
    public string? SearchedFor { get; private set; }

    /// <summary>
    /// Gets the open account.
    /// </summary>
    public AccountSession? Session { get; private set; }

    /// <summary>
    /// Gets or sets the shown tab of the account.
    /// </summary>
    public AccountTab Tab { get; set; } = AccountTab.Items;

    /// <summary>
    /// Gets the selected character.
    /// </summary>
    public CharacterModel? SelectedCharacter { get; private set; }

    /// <summary>
    /// Gets the selected storage.
    /// </summary>
    public StorageModel? SelectedStorage { get; private set; }

    /// <summary>
    /// Gets the selected item.
    /// </summary>
    public Item? SelectedItem { get; private set; }

    /// <summary>
    /// Gets the editor of the selected item.
    /// </summary>
    public ItemEditorModel? ItemEditor { get; private set; }

    /// <summary>
    /// Gets the storage which "Add" puts new items in: the selected one, else the inventory of the character.
    /// </summary>
    public StorageModel? TargetStorage => this.SelectedStorage ?? this.SelectedCharacter?.Inventory ?? this.Session?.Vault;

    /// <summary>
    /// Gets or sets a value indicating whether the side panel shows the item catalog instead of the item editor.
    /// </summary>
    public bool ShowCatalog { get; set; } = true;

    /// <summary>
    /// Gets or sets the item which is dragged, with the storage it comes from.
    /// </summary>
    public (StorageModel Storage, Item Item)? Dragged { get; set; }

    /// <summary>
    /// Gets the item definitions.
    /// </summary>
    public IReadOnlyList<ItemDefinition> Definitions => this.Database?.ItemDefinitions ?? Array.Empty<ItemDefinition>();

    /// <summary>
    /// Gets the package which is open in the package manager.
    /// </summary>
    public PackageEditor? PackageEditor { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether the side panel of the package manager shows the catalog instead of the item editor.
    /// </summary>
    public bool PackageShowCatalog { get; set; } = true;

    /// <summary>
    /// Gets the items of the open package which are ticked to change them together.
    /// </summary>
    public HashSet<Item> PackageSelection { get; } = new();

    /// <summary>
    /// Gets the selected item of the open package.
    /// </summary>
    public Item? SelectedPackageItem { get; private set; }

    /// <summary>
    /// Gets the editor of the selected item of the open package.
    /// </summary>
    public ItemEditorModel? PackageItemEditor { get; private set; }

    /// <summary>
    /// Loads the game configuration, the packages and the presets, once.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (this.IsInitialized)
        {
            return;
        }

        await this.RunAsync(async () =>
        {
            try
            {
                // The accounts reference the objects of the cached configuration of the persistence,
                // so the configuration is taken from there and not from the editable configuration of the admin panel.
                this._configurationContext = this._persistenceContextProvider.CreateNewConfigurationContext();
                var configuration = (await this._configurationContext.GetAsync<GameConfiguration>().ConfigureAwait(true)).FirstOrDefault()
                    ?? throw new AccountManagerException(Resources.AccountManagerNoGameConfiguration);
                this.Database = new AccountManagerDatabase(this._persistenceContextProvider, configuration, this._onlineStatus);
                await this.Packages.LoadAsync().ConfigureAwait(true);
                await this.Presets.LoadAsync().ConfigureAwait(true);
                this.InitializationError = null;
            }
            catch (Exception ex)
            {
                this.InitializationError = ex.Message;
                throw;
            }
        }).ConfigureAwait(true);
    }

    /// <summary>
    /// Searches the accounts by login name.
    /// </summary>
    public Task SearchAccountsAsync() => this.RunAsync(this.LoadAccountsAsync);

    /// <summary>
    /// Opens an account. It asks first when the open account has unsaved changes.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    public async Task OpenAccountAsync(string loginName)
    {
        if (this.Database is not { } database || string.Equals(this.Session?.Account.LoginName, loginName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!await this.ConfirmDiscardAsync().ConfigureAwait(true))
        {
            return;
        }

        await this.RunAsync(async () =>
        {
            this.CloseSession();
            this.Session = await database.OpenAccountAsync(loginName).ConfigureAwait(true);
            this.Session.Changed += this.RaiseChanged;
            this.SelectCharacter(this.Session.Characters.FirstOrDefault());
            if (this.Session.WasOnline)
            {
                this._toastService.ShowWarning(string.Format(Resources.AccountManagerOpenedOnlineAccount, loginName));
            }
        }).ConfigureAwait(true);
    }

    /// <summary>
    /// Loads the open account again from the database. Unsaved changes are dropped.
    /// </summary>
    public async Task ReloadAsync()
    {
        if (this.Session is not { } session || this.Database is not { } database)
        {
            return;
        }

        if (session.HasChanges
            && !await this.ConfirmAsync(Resources.AccountManagerDiscardChanges, string.Format(Resources.AccountManagerDiscardChangesOfAccount, session.PendingChanges.Count, session.Account.LoginName)).ConfigureAwait(true))
        {
            return;
        }

        var loginName = session.Account.LoginName;
        var characterName = this.SelectedCharacter?.Name;
        await this.RunAsync(async () =>
        {
            this.CloseSession();
            this.Session = await database.OpenAccountAsync(loginName).ConfigureAwait(true);
            this.Session.Changed += this.RaiseChanged;
            this.SelectCharacter(this.Session.Characters.FirstOrDefault(c => c.Name == characterName) ?? this.Session.Characters.FirstOrDefault());
        }).ConfigureAwait(true);
    }

    /// <summary>
    /// Saves the open account.
    /// </summary>
    public async Task SaveAsync()
    {
        if (this.Session is not { } session)
        {
            return;
        }

        await this.RunAsync(async () =>
        {
            switch (await session.SaveAsync().ConfigureAwait(true))
            {
                case SaveOutcome.Saved:
                    this._logger.LogInformation("The account manager saved the account {LoginName}.", session.Account.LoginName);
                    this._toastService.ShowSuccess(string.Format(Resources.AccountManagerSaved, session.Account.LoginName));
                    await this.RefreshAccountsAsync().ConfigureAwait(true);
                    break;
                case SaveOutcome.BlockedOnline:
                    this._toastService.ShowError(string.Format(Resources.AccountManagerSaveBlockedOnline, session.Account.LoginName));
                    break;
                default:
                    throw new NotImplementedException();
            }
        }).ConfigureAwait(true);
    }

    /// <summary>
    /// Selects a character.
    /// </summary>
    /// <param name="character">The character.</param>
    public void SelectCharacter(CharacterModel? character)
    {
        this.SelectedCharacter = character;
        this.SelectItem(null, null);
    }

    /// <summary>
    /// Selects an item, or only a storage when the item is <c>null</c>.
    /// </summary>
    /// <param name="storage">The storage.</param>
    /// <param name="item">The item.</param>
    public void SelectItem(StorageModel? storage, Item? item)
    {
        this.SelectedStorage = storage;
        this.SelectedItem = item;
        this.ItemEditor = item is not null && this.Session is { } session ? session.EditItem(item) : null;
        if (item is not null)
        {
            this.ShowCatalog = false;
        }

        this.RaiseChanged();
    }

    /// <summary>
    /// Adds a new item to a storage, with the options of the <see cref="CatalogPreset"/>.
    /// </summary>
    /// <param name="definition">The item definition.</param>
    /// <param name="level">The level.</param>
    public void AddItem(ItemDefinition definition, byte level)
    {
        if (this.Session is not { } session || this.TargetStorage is not { } target)
        {
            return;
        }

        this.Run(() =>
        {
            var item = session.AddItem(target, definition, level, preset: this.CatalogPreset);
            this.SelectItem(target, item);
            this._toastService.ShowSuccess(string.Format(Resources.AccountManagerAddedItem, definition.Name, target.Title));
        });
    }

    /// <summary>
    /// Duplicates the selected item.
    /// </summary>
    public void DuplicateSelected()
    {
        if (this.Session is { } session && this.SelectedStorage is { } storage && this.SelectedItem is { } item)
        {
            this.Run(() => this.SelectItem(storage, session.DuplicateItem(storage, item)));
        }
    }

    /// <summary>
    /// Deletes the selected item.
    /// </summary>
    public async Task DeleteSelectedAsync()
    {
        if (this.Session is { } session && this.SelectedStorage is { } storage && this.SelectedItem is { } item)
        {
            await this.RunAsync(async () =>
            {
                await session.DeleteItemAsync(storage, item).ConfigureAwait(true);
                this.SelectItem(storage, null);
            }).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Deletes all items of a storage, after asking.
    /// </summary>
    /// <param name="storage">The storage.</param>
    public async Task ClearStorageAsync(StorageModel storage)
    {
        if (this.Session is not { } session
            || !await this.ConfirmAsync(string.Format(Resources.AccountManagerClearStorageTitle, storage.Title), string.Format(Resources.AccountManagerClearStorageQuestion, storage.Items.Count())).ConfigureAwait(true))
        {
            return;
        }

        await this.RunAsync(async () =>
        {
            await session.ClearStorageAsync(storage).ConfigureAwait(true);
            this.SelectItem(storage, null);
        }).ConfigureAwait(true);
    }

    /// <summary>
    /// Moves the items which lie on top of other items to free places.
    /// </summary>
    /// <param name="storage">The storage.</param>
    public void ResolveOverlaps(StorageModel storage)
    {
        if (this.Session is { } session)
        {
            this.Run(() =>
            {
                var moved = session.ResolveOverlaps(storage);
                var left = storage.FindOverlaps().Count;
                if (left == 0)
                {
                    this._toastService.ShowSuccess(string.Format(Resources.AccountManagerMovedOverlapping, moved));
                }
                else
                {
                    this._toastService.ShowWarning(string.Format(Resources.AccountManagerMovedOverlappingPartially, moved, left));
                }
            });
        }
    }

    /// <summary>
    /// Drops the dragged item on a slot of a storage (the same one or another one of the account).
    /// </summary>
    /// <param name="target">The target storage.</param>
    /// <param name="slot">The slot.</param>
    public void Drop(StorageModel target, int slot)
    {
        if (this.Session is not { } session || this.Dragged is not { } dragged)
        {
            return;
        }

        this.Dragged = null;
        this.Run(() =>
        {
            session.TransferItem(dragged.Storage, dragged.Item, target, slot);
            this.SelectItem(target, dragged.Item);
        });
    }

    /// <summary>
    /// Moves the selected item to the first free place of another storage.
    /// </summary>
    /// <param name="target">The target storage.</param>
    public void SendSelectedTo(StorageModel target)
    {
        if (this.Session is { } session && this.SelectedStorage is { } source && this.SelectedItem is { } item)
        {
            this.Run(() =>
            {
                session.TransferItem(source, item, target);
                this.SelectItem(target, item);
                this._toastService.ShowSuccess(string.Format(Resources.AccountManagerMovedItem, item.Definition?.Name, target.Title));
            });
        }
    }

    /// <summary>
    /// Creates a new account and opens it.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="password">The password.</param>
    /// <param name="email">The optional e-mail address.</param>
    /// <returns><c>true</c>, if the account was created; otherwise, <c>false</c>.</returns>
    public async Task<bool> CreateAccountAsync(string loginName, string password, string? email)
    {
        if (this.Database is not { } database)
        {
            return false;
        }

        var created = false;
        await this.RunAsync(async () =>
        {
            await database.CreateAccountAsync(loginName, password, email).ConfigureAwait(true);
            created = true;
            this._logger.LogInformation("The account manager created the account {LoginName}.", loginName.Trim());
            this._toastService.ShowSuccess(string.Format(Resources.AccountManagerCreatedAccount, loginName.Trim()));
            this.AccountSearch = loginName.Trim();
            await this.LoadAccountsAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);

        if (created)
        {
            await this.OpenAccountAsync(loginName.Trim()).ConfigureAwait(true);
        }

        return created;
    }

    /// <summary>
    /// Deletes the open account, after asking.
    /// </summary>
    public async Task DeleteAccountAsync()
    {
        if (this.Database is not { } database || this.Session is not { } session)
        {
            return;
        }

        var loginName = session.Account.LoginName;
        if (!await this.ConfirmAsync(string.Format(Resources.AccountManagerDeleteTitle, loginName), string.Format(Resources.AccountManagerDeleteAccountQuestion, session.Characters.Count)).ConfigureAwait(true))
        {
            return;
        }

        await this.RunAsync(async () =>
        {
            this.CloseSession();
            await database.DeleteAccountAsync(loginName).ConfigureAwait(true);
            this._logger.LogInformation("The account manager deleted the account {LoginName}.", loginName);
            this._toastService.ShowSuccess(string.Format(Resources.AccountManagerDeletedAccount, loginName));
            await this.RefreshAccountsAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    /// <summary>
    /// Creates a character on the open account.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="characterClass">The class.</param>
    /// <returns><c>true</c>, if the character was created; otherwise, <c>false</c>.</returns>
    public async Task<bool> CreateCharacterAsync(string name, CharacterClass characterClass)
    {
        if (this.Session is not { } session)
        {
            return false;
        }

        var created = false;
        await this.RunAsync(async () =>
        {
            this.SelectCharacter(await session.CreateCharacterAsync(name, characterClass).ConfigureAwait(true));
            created = true;
            this._toastService.ShowSuccess(string.Format(Resources.AccountManagerCreatedCharacter, name.Trim()));
        }).ConfigureAwait(true);
        return created;
    }

    /// <summary>
    /// Deletes a character, after asking.
    /// </summary>
    /// <param name="character">The character.</param>
    public async Task DeleteCharacterAsync(CharacterModel character)
    {
        if (this.Session is not { } session
            || !await this.ConfirmAsync(string.Format(Resources.AccountManagerDeleteTitle, character.Name), Resources.AccountManagerDeleteCharacterQuestion).ConfigureAwait(true))
        {
            return;
        }

        await this.RunAsync(async () =>
        {
            await session.DeleteCharacterAsync(character).ConfigureAwait(true);
            this.SelectCharacter(session.Characters.FirstOrDefault());
        }).ConfigureAwait(true);
    }

    /// <summary>
    /// Sets the password of the open account.
    /// </summary>
    /// <param name="password">The password.</param>
    public void SetPassword(string password)
    {
        if (this.Session is { } session)
        {
            this.Run(() =>
            {
                session.SetPassword(password);
                this._toastService.ShowInfo(Resources.AccountManagerPasswordSet);
            });
        }
    }

    /// <summary>
    /// Adds a package to the vault of the open account (packages never go to an inventory):
    /// the items for the class of the selected character, and the zen.
    /// </summary>
    /// <param name="package">The package.</param>
    public void ApplyPackage(ItemPackage package)
    {
        if (this.Session is not { } session)
        {
            return;
        }

        var target = session.Vault;
        this.Run(() =>
        {
            var characterClass = this.SelectedCharacter?.CharacterClass;
            var result = session.ApplyPackage(target, package, characterClass);
            this.SelectItem(target, null);
            this.ShowCatalog = true;
            var message = string.Format(Resources.AccountManagerAddedPackage, package.Name, target.Title, result.Added);
            if (result.Zen > 0)
            {
                message += " " + string.Format(Resources.AccountManagerAddedPackageZen, result.Zen);
            }

            if (result.SkippedForClass > 0)
            {
                message += " " + string.Format(Resources.AccountManagerAddedPackageSkipped, result.SkippedForClass, characterClass?.Name);
            }

            this._toastService.ShowSuccess(message);
            foreach (var problem in result.Problems)
            {
                this._toastService.ShowWarning(problem);
            }

            if (session.WasOnline)
            {
                this._toastService.ShowWarning(string.Format(Resources.AccountManagerOnlineSaveBlocked, session.Account.LoginName));
            }
        });
    }

    /// <summary>
    /// Adds the pieces of an armor set, to the open package when <paramref name="toPackage"/> is set, else to the target storage.
    /// </summary>
    /// <param name="set">The armor set.</param>
    /// <param name="level">The level.</param>
    /// <param name="toPackage">If set to <c>true</c>, the pieces are added to the open package.</param>
    public void AddSet(ArmorSet set, byte level, bool toPackage)
    {
        var description = string.Format(Resources.AccountManagerSetDescription, set.Name);
        if (toPackage)
        {
            if (this.PackageEditor is { } editor)
            {
                this.Run(() =>
                {
                    editor.AddItems(set.Pieces, level, this.CatalogPreset);
                    this._toastService.ShowSuccess(string.Format(Resources.AccountManagerAddedSet, description, set.Pieces.Count, editor.Name));
                });
            }

            return;
        }

        if (this.Session is { } session && this.TargetStorage is { } target)
        {
            this.Run(() =>
            {
                session.AddItems(target, set.Pieces, description, level, this.CatalogPreset);
                this.SelectItem(target, null);
                this.ShowCatalog = true;
                this._toastService.ShowSuccess(string.Format(Resources.AccountManagerAddedSet, description, set.Pieces.Count, target.Title));
            });
        }
    }

    /// <summary>
    /// Sets the preset which items, which are added from the catalog, get.
    /// </summary>
    /// <param name="id">The identifier of the preset, or <c>null</c> for plain items.</param>
    public void SetCatalogPreset(Guid? id)
    {
        this.CatalogPreset = id is { } presetId ? this.Presets.Find(presetId) : null;
        this.RaiseChanged();
    }

    /// <summary>
    /// Adds a preset or saves the changes of one.
    /// </summary>
    /// <param name="preset">The preset.</param>
    public async Task SavePresetAsync(ItemOptionPreset preset)
    {
        await this.RunAsync(async () =>
        {
            await this.Presets.SaveAsync(preset).ConfigureAwait(true);
            if (this.CatalogPreset?.Id == preset.Id)
            {
                this.CatalogPreset = this.Presets.Find(preset.Id);
            }
        }).ConfigureAwait(true);
    }

    /// <summary>
    /// Creates a new preset.
    /// </summary>
    public Task NewPresetAsync()
    {
        var preset = new ItemOptionPreset { Name = this.Presets.UniqueName(Resources.AccountManagerNewPresetName), Skill = true, Luck = true };
        return this.SavePresetAsync(preset);
    }

    /// <summary>
    /// Deletes a preset.
    /// </summary>
    /// <param name="preset">The preset.</param>
    public async Task DeletePresetAsync(ItemOptionPreset preset)
    {
        await this.RunAsync(async () =>
        {
            await this.Presets.DeleteAsync(preset.Id).ConfigureAwait(true);
            if (this.CatalogPreset?.Id == preset.Id)
            {
                this.CatalogPreset = null;
            }
        }).ConfigureAwait(true);
    }

    /// <summary>
    /// Opens a package in the package manager. It asks first when the open package has unsaved changes.
    /// </summary>
    /// <param name="package">The package.</param>
    public async Task OpenPackageAsync(ItemPackage package)
    {
        if (this.Database is null || this.PackageEditor?.Package.Id == package.Id || !await this.ConfirmDiscardPackageAsync().ConfigureAwait(true))
        {
            return;
        }

        this.Run(() =>
        {
            this.ForceOpenPackage(package);
            foreach (var problem in this.PackageEditor!.Problems)
            {
                this._toastService.ShowWarning(problem);
            }
        });
    }

    /// <summary>
    /// Creates a new, empty package and opens it.
    /// </summary>
    public async Task NewPackageAsync()
    {
        if (this.Database is null || !await this.ConfirmDiscardPackageAsync().ConfigureAwait(true))
        {
            return;
        }

        await this.RunAsync(async () =>
        {
            var package = new ItemPackage { Name = this.Packages.UniqueName(Resources.AccountManagerNewPackageName) };
            await this.Packages.SaveAsync(package).ConfigureAwait(true);
            this.ForceOpenPackage(package);
        }).ConfigureAwait(true);
    }

    /// <summary>
    /// Saves the open package.
    /// </summary>
    public async Task SavePackageAsync()
    {
        if (this.PackageEditor is { } editor)
        {
            await this.RunAsync(async () =>
            {
                await editor.SaveAsync(this.Packages).ConfigureAwait(true);
                this._toastService.ShowSuccess(string.Format(Resources.AccountManagerSavedPackage, editor.Name));
            }).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Loads the open package again from the store. Unsaved changes are dropped.
    /// </summary>
    public async Task RevertPackageAsync()
    {
        if (this.PackageEditor is { HasChanges: true } editor
            && this.Packages.Find(editor.Package.Id) is { } saved
            && await this.ConfirmAsync(Resources.AccountManagerDiscardChanges, string.Format(Resources.AccountManagerDiscardChangesOfPackage, editor.Name)).ConfigureAwait(true))
        {
            this.Run(() => this.ForceOpenPackage(saved));
        }
    }

    /// <summary>
    /// Saves a copy of the open package (as it's saved) and opens it.
    /// </summary>
    public async Task DuplicatePackageAsync()
    {
        if (this.PackageEditor is not { } editor || !await this.ConfirmDiscardPackageAsync().ConfigureAwait(true))
        {
            return;
        }

        await this.RunAsync(async () =>
        {
            var copy = (this.Packages.Find(editor.Package.Id) ?? editor.Package).Clone(newId: true);
            copy.Name = this.Packages.UniqueName(copy.Name);
            await this.Packages.SaveAsync(copy).ConfigureAwait(true);
            this.ForceOpenPackage(copy);
            this._toastService.ShowSuccess(string.Format(Resources.AccountManagerCreatedPackage, copy.Name));
        }).ConfigureAwait(true);
    }

    /// <summary>
    /// Deletes the open package, after asking.
    /// </summary>
    public async Task DeletePackageAsync()
    {
        if (this.PackageEditor is not { } editor
            || !await this.ConfirmAsync(string.Format(Resources.AccountManagerDeleteTitle, editor.Name), Resources.AccountManagerDeletePackageQuestion).ConfigureAwait(true))
        {
            return;
        }

        await this.RunAsync(async () =>
        {
            await this.Packages.DeleteAsync(editor.Package.Id).ConfigureAwait(true);
            this.ClosePackage();
            this._toastService.ShowSuccess(string.Format(Resources.AccountManagerDeletedPackage, editor.Name));
        }).ConfigureAwait(true);
    }

    /// <summary>
    /// Selects an item of the open package.
    /// </summary>
    /// <param name="item">The item.</param>
    public void SelectPackageItem(Item? item)
    {
        this.SelectedPackageItem = item;
        this.PackageItemEditor = item is not null ? this.PackageEditor?.Edit(item) : null;
        if (item is not null)
        {
            // Like on the account: clicking an item shows its editor.
            this.PackageShowCatalog = false;
        }

        this.RaiseChanged();
    }

    /// <summary>
    /// Adds an item to the open package, with the options of the <see cref="CatalogPreset"/>.
    /// </summary>
    /// <param name="definition">The item definition.</param>
    /// <param name="level">The level.</param>
    public void AddPackageItem(ItemDefinition definition, byte level)
    {
        if (this.PackageEditor is { } editor)
        {
            this.Run(() => this.SelectPackageItem(editor.AddItem(definition, level, this.CatalogPreset)));
        }
    }

    /// <summary>
    /// Ticks or unticks items of the open package.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="selected">If set to <c>true</c>, the items get ticked.</param>
    public void SetPackageSelection(IEnumerable<Item> items, bool selected)
    {
        foreach (var item in items)
        {
            if (selected)
            {
                this.PackageSelection.Add(item);
            }
            else
            {
                this.PackageSelection.Remove(item);
            }
        }

        this.RaiseChanged();
    }

    /// <summary>
    /// Gives the ticked items of the package a level and/or the options of a preset.
    /// </summary>
    /// <param name="preset">The preset, or <c>null</c> to keep the options.</param>
    /// <param name="level">The level, or <c>null</c> to keep the level.</param>
    public void ApplyToPackageSelection(ItemOptionPreset? preset, byte? level)
    {
        if (this.PackageEditor is not { } editor || this.PackageSelection.Count == 0 || (preset is null && level is null))
        {
            return;
        }

        this.Run(() =>
        {
            editor.Apply(this.PackageSelection.ToList(), preset, level);
            if (this.SelectedPackageItem is { } selected)
            {
                // The item editor shows the new values.
                this.PackageItemEditor = editor.Edit(selected);
            }

            var what = string.Join(", ", new[] { level is { } l ? $"+{l}" : null, preset?.Name }.OfType<string>());
            this._toastService.ShowSuccess(string.Format(Resources.AccountManagerAppliedToItems, what, this.PackageSelection.Count));
        });
    }

    /// <summary>
    /// Removes the ticked items from the open package.
    /// </summary>
    public void RemovePackageSelection()
    {
        if (this.PackageEditor is { } editor)
        {
            this.Run(() =>
            {
                foreach (var item in this.PackageSelection.ToList())
                {
                    editor.Remove(item);
                }

                if (this.SelectedPackageItem is { } selected && this.PackageSelection.Contains(selected))
                {
                    this.SelectPackageItem(null);
                }

                this.PackageSelection.Clear();
            });
        }
    }

    /// <summary>
    /// Duplicates an item of the open package.
    /// </summary>
    /// <param name="item">The item.</param>
    public void DuplicatePackageItem(Item item)
    {
        if (this.PackageEditor is { } editor)
        {
            this.Run(() => this.SelectPackageItem(editor.Duplicate(item)));
        }
    }

    /// <summary>
    /// Removes an item from the open package.
    /// </summary>
    /// <param name="item">The item.</param>
    public void RemovePackageItem(Item item)
    {
        if (this.PackageEditor is { } editor)
        {
            this.Run(() =>
            {
                editor.Remove(item);
                this.PackageSelection.Remove(item);
                if (this.SelectedPackageItem == item)
                {
                    this.SelectPackageItem(null);
                }
            });
        }
    }

    /// <summary>
    /// Saves copies of items (e.g. of an inventory) as a new package, or adds them to an existing one, right away.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="name">The name of a new package.</param>
    /// <param name="addTo">The package to which the items are added, or <c>null</c> for a new package.</param>
    /// <returns><c>true</c>, if the package was saved; otherwise, <c>false</c>.</returns>
    public async Task<bool> SaveItemsAsPackageAsync(IReadOnlyCollection<Item> items, string name, ItemPackage? addTo)
    {
        var saved = false;
        await this.RunAsync(async () =>
        {
            if (items.Count == 0)
            {
                throw new AccountManagerException(Resources.AccountManagerPickAnItem);
            }

            var package = addTo?.Clone() ?? new ItemPackage { Name = name };
            package.Items.AddRange(items.Select(PackageConverter.FromItem));
            await this.Packages.SaveAsync(package).ConfigureAwait(true);
            saved = true;
            this._toastService.ShowSuccess(addTo is null
                ? string.Format(Resources.AccountManagerCreatedPackageWithItems, package.Name, items.Count)
                : string.Format(Resources.AccountManagerAddedItemsToPackage, items.Count, package.Name));
            if (this.PackageEditor?.Package.Id == package.Id && !this.PackageEditor.HasChanges)
            {
                this.ForceOpenPackage(package);
            }
        }).ConfigureAwait(true);
        return saved;
    }

    /// <summary>
    /// Asks a question in a modal dialog.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="question">The question.</param>
    /// <returns><c>true</c>, if the user confirmed; otherwise, <c>false</c>.</returns>
    public Task<bool> ConfirmAsync(string title, string question) => this._modalService.ShowQuestionAsync(title, question);

    /// <summary>
    /// Raises the <see cref="Changed"/> event.
    /// </summary>
    public void RaiseChanged() => this.Changed?.Invoke();

    /// <inheritdoc />
    public void Dispose()
    {
        this.CloseSession();
        this.ClosePackage();
        this._configurationContext?.Dispose();
        this._configurationContext = null;
    }

    private async Task<bool> ConfirmDiscardAsync()
    {
        return this.Session is not { HasChanges: true } session
            || await this.ConfirmAsync(Resources.AccountManagerDiscardChanges, string.Format(Resources.AccountManagerDiscardChangesOfAccount, session.PendingChanges.Count, session.Account.LoginName)).ConfigureAwait(true);
    }

    private async Task<bool> ConfirmDiscardPackageAsync()
    {
        return this.PackageEditor is not { HasChanges: true } editor
            || await this.ConfirmAsync(Resources.AccountManagerDiscardChanges, string.Format(Resources.AccountManagerDiscardChangesOfPackage, editor.Name)).ConfigureAwait(true);
    }

    /// <summary>
    /// Runs the search again (after a save, create or delete). Before the first search, it does nothing.
    /// </summary>
    private async Task RefreshAccountsAsync()
    {
        if (this.SearchedFor is not null)
        {
            this.AccountSearch = this.SearchedFor;
            await this.LoadAccountsAsync().ConfigureAwait(true);
        }
    }

    private async Task LoadAccountsAsync()
    {
        if (this.Database is { } database)
        {
            var term = this.AccountSearch.Trim();
            this.Accounts = await database.GetAccountsAsync(term, AccountSearchLimit).ConfigureAwait(true);
            this.SearchedFor = term.Length == 0 ? null : term;
        }
    }

    private void ForceOpenPackage(ItemPackage package)
    {
        this.ClosePackage();
        this.PackageEditor = new PackageEditor(this.Database!, package);
        this.PackageEditor.Changed += this.RaiseChanged;
    }

    private void ClosePackage()
    {
        if (this.PackageEditor is { } editor)
        {
            editor.Changed -= this.RaiseChanged;
            editor.Dispose();
        }

        this.PackageEditor = null;
        this.PackageSelection.Clear();
        this.SelectedPackageItem = null;
        this.PackageItemEditor = null;
    }

    private void CloseSession()
    {
        if (this.Session is { } session)
        {
            session.Changed -= this.RaiseChanged;
            session.Dispose();
        }

        this.Session = null;
        this.SelectedCharacter = null;
        this.SelectedStorage = null;
        this.SelectedItem = null;
        this.ItemEditor = null;
    }

    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            this.Report(ex);
        }

        this.RaiseChanged();
    }

    private async Task RunAsync(Func<Task> action)
    {
        this.IsBusy = true;
        this.RaiseChanged();
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.Report(ex);
        }
        finally
        {
            this.IsBusy = false;
            this.RaiseChanged();
        }
    }

    private void Report(Exception exception)
    {
        if (exception is AccountManagerException)
        {
            this._toastService.ShowWarning(exception.Message);
        }
        else
        {
            this._logger.LogError(exception, "Unexpected error in the account manager: {Message}", exception.Message);
            this._toastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, exception.Message));
        }
    }
}
