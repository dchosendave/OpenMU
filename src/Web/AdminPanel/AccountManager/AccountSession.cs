// <copyright file="AccountSession.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.CashShop;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.Web.AdminPanel.Properties;
using MUnique.OpenMU.Web.ItemEditor;

/// <summary>
/// The editing session of one account: its characters, inventories and vault, in one persistence context.
/// Nothing reaches the database before <see cref="SaveAsync"/>. Disposing discards the changes.
/// </summary>
public sealed class AccountSession : IDisposable
{
    private readonly AccountManagerDatabase _database;
    private readonly IPlayerContext _context;
    private readonly List<CharacterModel> _characters = new();
    private readonly List<string> _pendingChanges = new();

    private AccountSession(AccountManagerDatabase database, IPlayerContext context, Account account)
    {
        this._database = database;
        this._context = context;
        this.Account = account;
        account.Vault ??= context.CreateNew<ItemStorage>();
        this.Vault = new StorageModel(account.Vault, StorageType.Vault, (byte)(account.IsVaultExtended ? 1 : 0), Resources.AccountManagerAreaVault);
        foreach (var character in account.Characters.OrderBy(c => c.CharacterSlot))
        {
            this._characters.Add(this.CreateCharacterModel(character));
        }
    }

    /// <summary>
    /// Occurs after every change, so the user interface can render again.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Gets the account.
    /// </summary>
    public Account Account { get; }

    /// <summary>
    /// Gets the vault.
    /// </summary>
    public StorageModel Vault { get; }

    /// <summary>
    /// Gets the characters.
    /// </summary>
    public IReadOnlyList<CharacterModel> Characters => this._characters;

    /// <summary>
    /// Gets a value indicating whether the account was online when it was loaded.
    /// </summary>
    public bool WasOnline { get; private set; }

    /// <summary>
    /// Gets a value indicating whether there are unsaved changes.
    /// </summary>
    public bool HasChanges => this._pendingChanges.Count > 0;

    /// <summary>
    /// Gets a description of each change since loading or saving.
    /// </summary>
    public IReadOnlyList<string> PendingChanges => this._pendingChanges;

    /// <summary>
    /// Gets the game configuration.
    /// </summary>
    public GameConfiguration Configuration => this._database.Configuration;

    /// <summary>
    /// Gets or sets the state of the account.
    /// </summary>
    public AccountState State
    {
        get => this.Account.State;
        set => this.Update(() => this.Account.State = value, string.Format(Resources.AccountManagerChangeAccountState, value));
    }

    /// <summary>
    /// Gets or sets the e-mail address.
    /// </summary>
    public string EMail
    {
        get => this.Account.EMail;
        set => this.Update(() => this.Account.EMail = value.Trim(), Resources.AccountManagerChangeEMail);
    }

    /// <summary>
    /// Gets or sets the zen of the vault.
    /// </summary>
    public int VaultZen
    {
        get => this.Vault.Money;
        set => this.Update(() => this.Vault.Money = value, Resources.AccountManagerChangeVaultZen);
    }

    /// <summary>
    /// Gets or sets the WCoin (C).
    /// </summary>
    public int WCoinC
    {
        get => (int)this.Account.WCoinC;
        set => this.Update(() => this.Account.WCoinC = Math.Max(0, value), "WCoin (C)");
    }

    /// <summary>
    /// Gets or sets the WCoin (P).
    /// </summary>
    public int WCoinP
    {
        get => (int)this.Account.WCoinP;
        set => this.Update(() => this.Account.WCoinP = Math.Max(0, value), "WCoin (P)");
    }

    /// <summary>
    /// Gets or sets the Goblin points.
    /// </summary>
    public int GoblinPoints
    {
        get => (int)this.Account.GoblinPoints;
        set => this.Update(() => this.Account.GoblinPoints = Math.Max(0, value), Resources.AccountManagerChangeGoblinPoints);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the vault has its second page. The grid grows after reloading the account.
    /// </summary>
    public bool IsVaultExtended
    {
        get => this.Account.IsVaultExtended;
        set => this.Update(() => this.Account.IsVaultExtended = value, value ? Resources.AccountManagerChangeVaultExtended : Resources.AccountManagerChangeVaultExtensionRemoved);
    }

    /// <summary>
    /// Gets all storages of the account: the vault and the inventory of each character.
    /// </summary>
    public IEnumerable<StorageModel> Storages => this._characters.Select(c => c.Inventory).Prepend(this.Vault);

    /// <summary>
    /// Opens an account for editing.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="loginName">The login name.</param>
    /// <returns>The editing session.</returns>
    public static async Task<AccountSession> OpenAsync(AccountManagerDatabase database, string loginName)
    {
        var context = database.CreatePlayerContext();
        try
        {
            var account = await context.GetAccountByLoginNameAsync(loginName).ConfigureAwait(true)
                ?? throw new AccountManagerException(string.Format(Resources.AccountManagerAccountNotFound, loginName));
            var session = new AccountSession(database, context, account);
            session.WasOnline = await database.OnlineStatus.IsOnlineAsync(loginName).ConfigureAwait(true);
            return session;
        }
        catch
        {
            context.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens an editor for an item of this account.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The editor.</returns>
    public ItemEditorModel EditItem(Item item) => new(item, this._context, () => this.MarkChanged(string.Format(Resources.AccountManagerChangeEditedItem, item.Definition?.Name)));

    /// <summary>
    /// Adds a new item to a storage, at the slot or the first free place.
    /// </summary>
    /// <param name="storage">The storage.</param>
    /// <param name="definition">The item definition.</param>
    /// <param name="level">The level.</param>
    /// <param name="slot">The slot, or <c>null</c> for the first free place.</param>
    /// <param name="preset">The options which the item gets. Its level overrides <paramref name="level"/>.</param>
    /// <returns>The added item.</returns>
    public Item AddItem(StorageModel storage, ItemDefinition definition, byte level = 0, int? slot = null, ItemOptionPreset? preset = null)
    {
        var item = this.CreateItem(definition, level, preset);
        if (!this.TryPlace(storage, item, slot, out var reason))
        {
            this._context.Detach(item);
            throw new AccountManagerException(reason);
        }

        storage.Storage.Items.Add(item);
        this.MarkChanged(string.Format(Resources.AccountManagerChangeAddedItem, definition.Name, storage.Title));
        return item;
    }

    /// <summary>
    /// Adds several new items (e.g. the pieces of an armor set) to free places of a storage.
    /// Either all of them fit, or nothing is added.
    /// </summary>
    /// <param name="storage">The storage.</param>
    /// <param name="definitions">The item definitions.</param>
    /// <param name="description">The description of the items, e.g. "Dragon set".</param>
    /// <param name="level">The level.</param>
    /// <param name="preset">The options which the items get.</param>
    /// <returns>The added items.</returns>
    public IReadOnlyList<Item> AddItems(StorageModel storage, IReadOnlyList<ItemDefinition> definitions, string description, byte level = 0, ItemOptionPreset? preset = null)
    {
        if (storage.PlanFreeSlots(definitions) is not { } slots)
        {
            throw new AccountManagerException(string.Format(Resources.AccountManagerNotEnoughSpace, storage.Title, description, definitions.Count));
        }

        var items = new List<Item>();
        for (var i = 0; i < definitions.Count; i++)
        {
            var item = this.CreateItem(definitions[i], level, preset);
            item.ItemSlot = slots[i];
            storage.Storage.Items.Add(item);
            items.Add(item);
        }

        this.MarkChanged(string.Format(Resources.AccountManagerChangeAddedItem, description, storage.Title));
        return items;
    }

    /// <summary>
    /// Adds a copy of an item, with all its options, to the same storage.
    /// </summary>
    /// <param name="storage">The storage.</param>
    /// <param name="original">The original item.</param>
    /// <returns>The copy.</returns>
    public Item DuplicateItem(StorageModel storage, Item original)
    {
        var copy = this._context.CreateNew<Item>();
        copy.AssignValues(original);
        if (!this.TryPlace(storage, copy, null, out var reason))
        {
            this._context.Detach(copy);
            throw new AccountManagerException(reason);
        }

        storage.Storage.Items.Add(copy);
        this.MarkChanged(string.Format(Resources.AccountManagerChangeDuplicatedItem, original.Definition?.Name, storage.Title));
        return copy;
    }

    /// <summary>
    /// Adds the items of a package (those meant for the class) and its zen to a storage.
    /// Either every item finds a free place, or nothing is added.
    /// </summary>
    /// <param name="storage">The storage.</param>
    /// <param name="package">The package.</param>
    /// <param name="forClass">The class for which the items are. Items for other classes are skipped; <c>null</c> adds all.</param>
    /// <returns>What was added.</returns>
    public PackageResult ApplyPackage(StorageModel storage, ItemPackage package, CharacterClass? forClass)
    {
        var converter = new PackageConverter(this.Configuration);
        var problems = new List<string>();
        var wanted = package.Items.Where(i => converter.IsFor(i, forClass)).ToList();
        var known = wanted.Select(i => (Item: i, Definition: converter.DefinitionOf(i))).ToList();
        problems.AddRange(known.Where(k => k.Definition is null).Select(k => string.Format(Resources.AccountManagerUnknownItem, PackageConverter.Describe(k.Item))));
        known.RemoveAll(k => k.Definition is null);
        if (known.Count == 0 && package.Zen == 0)
        {
            throw new AccountManagerException(wanted.Count == package.Items.Count
                ? string.Format(Resources.AccountManagerPackageIsEmpty, package.Name)
                : string.Format(Resources.AccountManagerPackageHasNothingForClass, package.Name, forClass?.Name));
        }

        if (storage.PlanFreeSlots(known.Select(k => k.Definition!).ToList()) is not { } slots)
        {
            throw new AccountManagerException(string.Format(Resources.AccountManagerNotEnoughSpaceForPackage, storage.Title, known.Count, package.Name));
        }

        var added = 0;
        for (var i = 0; i < known.Count; i++)
        {
            if (converter.CreateItem(known[i].Item, this._context, problems) is { } item)
            {
                item.ItemSlot = slots[i];
                storage.Storage.Items.Add(item);
                added++;
            }
        }

        var zen = (int)Math.Min((long)package.Zen, int.MaxValue - (long)storage.Money);
        storage.Money += zen;
        this.MarkChanged(string.Format(Resources.AccountManagerChangeAddedPackage, package.Name, storage.Title));
        return new PackageResult(added, package.Items.Count - wanted.Count, zen, problems.Distinct().ToList());
    }

    /// <summary>
    /// Deletes an item.
    /// </summary>
    /// <param name="storage">The storage of the item.</param>
    /// <param name="item">The item.</param>
    public async Task DeleteItemAsync(StorageModel storage, Item item)
    {
        if (storage.Storage.Items.Remove(item))
        {
            await this._context.DeleteAsync(item).ConfigureAwait(true);
            this.MarkChanged(string.Format(Resources.AccountManagerChangeDeletedItem, item.Definition?.Name, storage.Title));
        }
    }

    /// <summary>
    /// Deletes every item of a storage. The zen stays.
    /// </summary>
    /// <param name="storage">The storage.</param>
    /// <returns>The number of deleted items.</returns>
    public async Task<int> ClearStorageAsync(StorageModel storage)
    {
        var items = storage.Storage.Items.ToList();
        foreach (var item in items)
        {
            storage.Storage.Items.Remove(item);
            await this._context.DeleteAsync(item).ConfigureAwait(true);
        }

        if (items.Count > 0)
        {
            this.MarkChanged(string.Format(Resources.AccountManagerChangeClearedStorage, items.Count, storage.Title));
        }

        return items.Count;
    }

    /// <summary>
    /// Moves every item which lies on top of another one to a free place of the same storage.
    /// </summary>
    /// <param name="storage">The storage.</param>
    /// <returns>The number of moved items. Items without a free place stay.</returns>
    public int ResolveOverlaps(StorageModel storage)
    {
        var moved = 0;
        foreach (var item in storage.FindOverlaps())
        {
            // Look for a place as if the item wasn't in the storage at all.
            storage.Storage.Items.Remove(item);
            var free = storage.FindFreeSlot(item);
            storage.Storage.Items.Add(item);
            if (free is { } slot)
            {
                item.ItemSlot = slot;
                moved++;
            }
        }

        if (moved > 0)
        {
            this.MarkChanged(string.Format(Resources.AccountManagerChangeMovedOverlapping, moved, storage.Title));
        }

        return moved;
    }

    /// <summary>
    /// Moves an item to another slot of the same storage.
    /// </summary>
    /// <param name="storage">The storage.</param>
    /// <param name="item">The item.</param>
    /// <param name="slot">The slot.</param>
    public void MoveItem(StorageModel storage, Item item, int slot)
    {
        if (item.ItemSlot == slot)
        {
            return;
        }

        if (!storage.CanPlace(item, slot, out var reason))
        {
            throw new AccountManagerException(reason);
        }

        item.ItemSlot = (byte)slot;
        this.MarkChanged(string.Format(Resources.AccountManagerChangeMovedItemToSlot, item.Definition?.Name, slot));
    }

    /// <summary>
    /// Moves an item into another storage of this account, e.g. from an inventory to the vault.
    /// </summary>
    /// <param name="from">The storage of the item.</param>
    /// <param name="item">The item.</param>
    /// <param name="to">The target storage.</param>
    /// <param name="slot">The slot, or <c>null</c> for the first free place.</param>
    public void TransferItem(StorageModel from, Item item, StorageModel to, int? slot = null)
    {
        if (from == to)
        {
            if (slot is { } target)
            {
                this.MoveItem(from, item, target);
            }

            return;
        }

        var previousSlot = item.ItemSlot;
        if (!this.TryPlace(to, item, slot, out var reason))
        {
            item.ItemSlot = previousSlot;
            throw new AccountManagerException(reason);
        }

        from.Storage.Items.Remove(item);
        to.Storage.Items.Add(item);
        this.MarkChanged(string.Format(Resources.AccountManagerChangeMovedItem, item.Definition?.Name, from.Title, to.Title));
    }

    /// <summary>
    /// Creates a character like the character creation of the game: base stats of the class, home map, empty inventory.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="characterClass">The class.</param>
    /// <returns>The created character.</returns>
    public async Task<CharacterModel> CreateCharacterAsync(string name, CharacterClass characterClass)
    {
        name = name.Trim();
        if (string.IsNullOrEmpty(name) || !this.Configuration.IsValidCharacterName(name))
        {
            throw new AccountManagerException(string.Format(Resources.AccountManagerInvalidCharacterName, name, this.Configuration.CharacterNameRegex));
        }

        if (this._characters.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
            || await this._database.CharacterNameExistsAsync(name).ConfigureAwait(true))
        {
            throw new AccountManagerException(string.Format(Resources.AccountManagerCharacterExists, name));
        }

        if (!characterClass.CanGetCreated || characterClass.HomeMap is null)
        {
            throw new AccountManagerException(string.Format(Resources.AccountManagerClassCannotBeCreated, characterClass.Name));
        }

        if (this.Account.GetFreeCharacterSlot(this.Configuration) is not { } slot)
        {
            throw new AccountManagerException(string.Format(Resources.AccountManagerNoFreeCharacterSlot, this.Configuration.MaximumCharactersPerAccount));
        }

        var character = this._context.CreateNew<Character>();
        character.Name = name;
        character.CharacterSlot = slot;
        this._context.InitializeNewCharacter(character, characterClass);
        this.Account.Characters.Add(character);
        var model = this.CreateCharacterModel(character);
        this._characters.Add(model);
        this.MarkChanged(string.Format(Resources.AccountManagerChangeCreatedCharacter, characterClass.Name, name));
        return model;
    }

    /// <summary>
    /// Deletes a character with its inventory.
    /// </summary>
    /// <param name="character">The character.</param>
    public async Task DeleteCharacterAsync(CharacterModel character)
    {
        if (!this._characters.Remove(character))
        {
            return;
        }

        this.Account.Characters.Remove(character.Character);
        await this._context.DeleteAsync(character.Character).ConfigureAwait(true);
        this.MarkChanged(string.Format(Resources.AccountManagerChangeDeletedCharacter, character.Name));
    }

    /// <summary>
    /// Sets the password.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <summary>
    /// Adds the pending coin grants (e.g. check-in rewards, market sales) to the account, like the game does
    /// when the player opens the cash shop.
    /// </summary>
    /// <returns>The number of applied grants.</returns>
    public async Task<int> ApplyPendingCoinGrantsAsync()
    {
        var grants = await this._context.GetPendingCashShopCoinGrantsAsync(this.Account.GetId()).ConfigureAwait(true);
        var applied = 0;
        foreach (var grant in grants.Where(g => Enum.IsDefined(g.CoinType)))
        {
            var balance = (long)this.Account.GetCashShopCoins(grant.CoinType) + grant.Amount;
            this.Account.SetCashShopCoins(grant.CoinType, (int)Math.Clamp(balance, 0, int.MaxValue));
            grant.AppliedAt = DateTime.UtcNow;
            applied++;
        }

        if (applied > 0)
        {
            this.MarkChanged($"Applied {applied} coin grant(s)");
        }

        return applied;
    }

    /// <summary>
    /// Loads the vault of another account into this session, e.g. of the player market's escrow account, so
    /// items can move between the two vaults in one save.
    /// </summary>
    /// <param name="loginName">The login name of the other account.</param>
    /// <returns>The vault, or <c>null</c> if the account doesn't exist.</returns>
    public async Task<(Account Account, StorageModel Vault)?> OpenVaultOfAsync(string loginName)
    {
        if (await this._context.GetAccountByLoginNameAsync(loginName).ConfigureAwait(true) is not { } other)
        {
            return null;
        }

        other.Vault ??= this._context.CreateNew<ItemStorage>();
        return (other, new StorageModel(other.Vault, StorageType.Vault, (byte)(other.IsVaultExtended ? 1 : 0), loginName));
    }

    public void SetPassword(string password)
    {
        AccountManagerDatabase.ValidatePassword(password);
        this.Account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        this.MarkChanged(Resources.AccountManagerChangePassword);
    }

    /// <summary>
    /// Saves all changes, unless the account is online, because the game server would overwrite them when the player logs out.
    /// </summary>
    /// <returns>The outcome.</returns>
    public async Task<SaveOutcome> SaveAsync()
    {
        if (await this._database.OnlineStatus.IsOnlineAsync(this.Account.LoginName).ConfigureAwait(true))
        {
            return SaveOutcome.BlockedOnline;
        }

        if (!await this._context.SaveChangesAsync().ConfigureAwait(true))
        {
            throw new AccountManagerException(Resources.AccountManagerChangesNotAccepted);
        }

        this._pendingChanges.Clear();
        this.Changed?.Invoke();
        return SaveOutcome.Saved;
    }

    /// <summary>
    /// Records a change which was made directly on the entities, e.g. a stat.
    /// </summary>
    /// <param name="description">The description of the change.</param>
    public void MarkChanged(string? description = null)
    {
        description ??= Resources.AccountManagerChangeCharacterData;
        if (this._pendingChanges.Count == 0 || this._pendingChanges[^1] != description)
        {
            this._pendingChanges.Add(description);
        }

        this.Changed?.Invoke();
    }

    /// <inheritdoc />
    public void Dispose() => this._context.Dispose();

    private CharacterModel CreateCharacterModel(Character character) => new(character, this._context, this.Configuration, () => this.MarkChanged());

    private Item CreateItem(ItemDefinition definition, byte level, ItemOptionPreset? preset)
    {
        var item = this._context.CreateNew<Item>();
        item.Definition = definition;
        item.Level = Math.Min(level, definition.MaximumItemLevel);
        item.Durability = item.IsStackable() ? 1 : item.GetMaximumDurabilityOfOnePiece();
        if (preset is not null)
        {
            new ItemEditorModel(item, this._context, () => { }).ApplyPreset(preset);
        }

        return item;
    }

    private bool TryPlace(StorageModel storage, Item item, int? slot, out string reason)
    {
        if (slot is { } wanted)
        {
            if (!storage.CanPlace(item, wanted, out reason))
            {
                return false;
            }

            item.ItemSlot = (byte)wanted;
            return true;
        }

        if (storage.FindFreeSlot(item) is not { } free)
        {
            reason = string.Format(Resources.AccountManagerNoFreeSpace, item.Definition?.Name, storage.Title);
            return false;
        }

        item.ItemSlot = free;
        reason = string.Empty;
        return true;
    }

    private void Update(Action change, string description)
    {
        change();
        this.MarkChanged(description);
    }
}
