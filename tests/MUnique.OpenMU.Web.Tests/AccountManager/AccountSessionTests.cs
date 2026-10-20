// <copyright file="AccountSessionTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AccountManager;

using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Web.AdminPanel.AccountManager;
using MUnique.OpenMU.Web.ItemEditor;

/// <summary>
/// The editing scenarios of the app, on a database with the server's full Season 6 data.
/// </summary>
[TestFixture]
public sealed class AccountSessionTests
{
    private InMemoryServer server = null!;

    private AccountManagerDatabase Db => this.server.Database;

    /// <summary>
    /// Gets the shared in-memory server.
    /// </summary>
    /// <returns>The task.</returns>
    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        this.server = await InMemoryServer.GetAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Tests that new account opens with empty vault and no characters.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewAccountOpensWithEmptyVaultAndNoCharactersAsync()
    {
        var login = await this.server.NewAccountAsync();

        using var session = await this.Db.OpenAccountAsync(login);

        Assert.That(session.Account.LoginName, Is.EqualTo(login));
        Assert.That(session.Characters, Is.Empty);
        Assert.That(session.Vault.Items, Is.Empty);
        Assert.That(session.Vault.StorageType, Is.EqualTo(StorageType.Vault));
        Assert.That(session.Vault.Areas[0].SlotCount, Is.EqualTo(InventoryConstants.WarehouseRows * 8));
        Assert.That(session.HasChanges, Is.False);
        Assert.That(session.WasOnline, Is.False);
    }

    /// <summary>
    /// Tests that new account appears in search.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewAccountAppearsInSearchAsync()
    {
        var login = await this.server.NewAccountAsync();

        var found = await this.Db.GetAccountsAsync(login, AccountManagerState.AccountSearchLimit);

        Assert.That(found.Any(a => a.LoginName == login && a.State == AccountState.Normal), Is.True);
    }

    /// <summary>
    /// Tests that an empty search reads no accounts.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AnEmptySearchReadsNoAccountsAsync()
    {
        await this.server.NewAccountAsync();

        Assert.That(await this.Db.GetAccountsAsync(null, AccountManagerState.AccountSearchLimit), Is.Empty);
        Assert.That(await this.Db.GetAccountsAsync("  ", AccountManagerState.AccountSearchLimit), Is.Empty);
    }

    /// <summary>
    /// Tests that search matches login names only.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SearchMatchesLoginNamesOnlyAsync()
    {
        var login = await this.server.NewAccountAsync();
        var name = InMemoryServer.NewCharacterName();
        using (var session = await this.Db.OpenAccountAsync(login))
        {
            await session.CreateCharacterAsync(name, this.Db.CharacterClasses.First(c => c.CanGetCreated));
            Assert.That(await session.SaveAsync(), Is.EqualTo(SaveOutcome.Saved));
        }

        Assert.That((await this.Db.GetAccountsAsync(name, AccountManagerState.AccountSearchLimit)).Any(a => a.LoginName == login), Is.False);
        Assert.That((await this.Db.GetAccountsAsync(login.ToUpperInvariant(), AccountManagerState.AccountSearchLimit)).Any(a => a.LoginName == login), Is.True);
    }

    /// <summary>
    /// Tests that account names must be unique.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AccountNamesMustBeUniqueAsync()
    {
        var login = await this.server.NewAccountAsync();

        var error = Assert.ThrowsAsync<AccountManagerException>(() => this.Db.CreateAccountAsync(login, "another", null));
        Assert.That(error?.Message, Does.Contain("already exists"));
    }

    /// <summary>
    /// Tests that invalid login names and passwords are rejected.
    /// </summary>
    /// <param name="login">The login.</param>
    /// <param name="password">The password.</param>
    /// <returns>The task.</returns>
    [TestCase("ab", "secret")]
    [TestCase("waytoolongname", "secret")]
    [TestCase("bad name", "secret")]
    [TestCase("goodname", "no")]
    [TestCase("goodname", "a-password-longer-than-20")]
    public async Task InvalidLoginNamesAndPasswordsAreRejectedAsync(string login, string password)
    {
        Assert.ThrowsAsync<AccountManagerException>(() => this.Db.CreateAccountAsync(login, password, null));
    }

    /// <summary>
    /// Tests that created character starts like in the game.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task CreatedCharacterStartsLikeInTheGameAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var darkKnight = this.Db.CharacterClasses.First(c => c.CanGetCreated && c.Name.ValueInNeutralLanguage == "Dark Knight");
        var name = InMemoryServer.NewCharacterName();

        var character = await session.CreateCharacterAsync(name, darkKnight);

        Assert.That(character.Name, Is.EqualTo(name));
        Assert.That(character.CharacterClass, Is.SameAs(darkKnight));
        Assert.That(character.Level, Is.EqualTo(1));
        Assert.That(character.CurrentMap, Is.EqualTo(darkKnight.HomeMap));
        Assert.That(character.Character.CharacterSlot, Is.EqualTo(0));
        Assert.That(character.Attributes.Any(a => a.Definition?.Id == Stats.BaseStrength.Id && a.Value > 0), Is.True);
        Assert.That(character.Inventory.Items, Is.Empty);
        Assert.That(session.Characters, Does.Contain(character));
        Assert.That(session.HasChanges, Is.True);
    }

    /// <summary>
    /// Tests that character slots are used in order up to the maximum.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task CharacterSlotsAreUsedInOrderUpToTheMaximumAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var characterClass = this.Db.CharacterClasses.First(c => c.CanGetCreated);
        var maximum = this.Db.Configuration.MaximumCharactersPerAccount;

        for (var i = 0; i < maximum; i++)
        {
            var character = await session.CreateCharacterAsync(InMemoryServer.NewCharacterName(), characterClass);
            Assert.That(character.Character.CharacterSlot, Is.EqualTo(i));
        }

        var error = Assert.ThrowsAsync<AccountManagerException>(() => session.CreateCharacterAsync(InMemoryServer.NewCharacterName(), characterClass));
        Assert.That(error?.Message, Does.Contain("no free character slot"));
    }

    /// <summary>
    /// Tests that character names must be valid and unique.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task CharacterNamesMustBeValidAndUniqueAsync()
    {
        var characterClass = this.Db.CharacterClasses.First(c => c.CanGetCreated);
        var name = InMemoryServer.NewCharacterName();
        using (var first = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync()))
        {
            await first.CreateCharacterAsync(name, characterClass);
            Assert.That(await first.SaveAsync(), Is.EqualTo(SaveOutcome.Saved));
        }

        using var second = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        Assert.ThrowsAsync<AccountManagerException>(() => second.CreateCharacterAsync(name, characterClass));
        Assert.ThrowsAsync<AccountManagerException>(() => second.CreateCharacterAsync("no spaces", characterClass));
        Assert.ThrowsAsync<AccountManagerException>(() => second.CreateCharacterAsync("ab", characterClass));
        Assert.That(second.Characters, Is.Empty);
    }

    /// <summary>
    /// Tests that second classes cannot be created directly.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SecondClassesCannotBeCreatedDirectlyAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var bladeKnight = this.Db.CharacterClasses.First(c => !c.CanGetCreated);

        Assert.ThrowsAsync<AccountManagerException>(() => session.CreateCharacterAsync(InMemoryServer.NewCharacterName(), bladeKnight));
    }

    /// <summary>
    /// Tests that adding one by one items fills the vault completely.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AddingOneByOneItemsFillsTheVaultCompletelyAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var jewel = this.Db.Jewel();
        var capacity = session.Vault.Areas[0].SlotCount;

        for (var i = 0; i < capacity; i++)
        {
            session.AddItem(session.Vault, jewel);
        }

        Assert.That(session.Vault.Items.Count(), Is.EqualTo(capacity));
        Assert.That(session.Vault.Items.Select(i => i.ItemSlot).Distinct().Count(), Is.EqualTo(capacity));
        var error = Assert.Throws<AccountManagerException>(() => session.AddItem(session.Vault, jewel));
        Assert.That(error?.Message, Does.Contain("no free space"));
        Assert.That(session.Vault.Items.Count(), Is.EqualTo(capacity));
    }

    /// <summary>
    /// Tests that large items never overlap.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task LargeItemsNeverOverlapAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var large = this.Db.Large();
        var kris = this.Db.Kris();

        var added = new List<Item>();
        try
        {
            while (true)
            {
                added.Add(session.AddItem(session.Vault, added.Count % 2 == 0 ? large : kris));
            }
        }
        catch (AccountManagerException)
        {
            // full
        }

        Assert.That(added.Count > 2, Is.True);
        var area = session.Vault.Areas[0];
        var covered = new HashSet<int>();
        foreach (var item in added)
        {
            var (width, height) = StorageModel.SizeOf(item);
            Assert.That(area.ColumnOf(item.ItemSlot) + width <= 8, Is.True, $"{item.Definition!.Name} sticks out on the right");
            Assert.That(area.RowOf(item.ItemSlot) + height <= area.Rows, Is.True, $"{item.Definition!.Name} sticks out at the bottom");
            for (var row = 0; row < height; row++)
            {
                for (var column = 0; column < width; column++)
                {
                    Assert.That(covered.Add(item.ItemSlot + (row * 8) + column), Is.True, $"cell {item.ItemSlot + (row * 8) + column} is used twice");
                }
            }
        }
    }

    /// <summary>
    /// Tests that items move only to free cells within the grid.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ItemsMoveOnlyToFreeCellsWithinTheGridAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var kris = session.AddItem(session.Vault, this.Db.Kris(), slot: 0);   // cells 0 and 8
        var jewel = session.AddItem(session.Vault, this.Db.Jewel(), slot: 1);

        session.MoveItem(session.Vault, kris, 7);   // last column: 1 wide, fits
        Assert.That(kris.ItemSlot, Is.EqualTo(7));
        Assert.Throws<AccountManagerException>(() => session.MoveItem(session.Vault, jewel, 15));  // covered by the kris (7 + 8)
        Assert.Throws<AccountManagerException>(() => session.MoveItem(session.Vault, kris, session.Vault.Areas[0].SlotCount - 1)); // below the last row
        Assert.Throws<AccountManagerException>(() => session.MoveItem(session.Vault, jewel, 500));
        Assert.That(jewel.ItemSlot, Is.EqualTo(1));

        session.MoveItem(session.Vault, jewel, 0);
        Assert.That(jewel.ItemSlot, Is.EqualTo(0));
        Assert.That(session.Vault.ItemCovering(15), Is.SameAs(kris));
        Assert.That(session.PendingChanges.Any(c => c.StartsWith("Moved", StringComparison.Ordinal)), Is.True);
    }

    /// <summary>
    /// Tests that equipment slots only take matching items.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task EquipmentSlotsOnlyTakeMatchingItemsAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var character = await session.CreateCharacterAsync(InMemoryServer.NewCharacterName(), this.Db.CharacterClasses.First(c => c.CanGetCreated));
        var inventory = character.Inventory;
        const int leftHand = 0;
        const int helm = 2;

        var kris = session.AddItem(inventory, this.Db.Kris(), slot: leftHand);
        Assert.That(kris.ItemSlot, Is.EqualTo(leftHand));
        Assert.That(inventory.ItemAt(leftHand), Is.SameAs(kris));

        var helmItem = session.AddItem(inventory, this.Db.Helm());
        Assert.That(helmItem.ItemSlot >= InventoryConstants.EquippableSlotsCount, Is.True, "a new item goes to the grid, not into equipment");
        Assert.Throws<AccountManagerException>(() => session.MoveItem(inventory, helmItem, leftHand)); // wrong slot type and in use
        session.MoveItem(inventory, helmItem, helm);
        Assert.That(helmItem.ItemSlot, Is.EqualTo(helm));

        var jewel = session.AddItem(inventory, this.Db.Jewel());
        var error = Assert.Throws<AccountManagerException>(() => session.MoveItem(inventory, jewel, 3));
        Assert.That(error?.Message, Does.Contain("not equippable"));
    }

    /// <summary>
    /// Tests that items transfer between inventory and vault.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ItemsTransferBetweenInventoryAndVaultAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var character = await session.CreateCharacterAsync(InMemoryServer.NewCharacterName(), this.Db.CharacterClasses.First(c => c.CanGetCreated));
        var kris = session.AddItem(character.Inventory, this.Db.Kris());

        session.TransferItem(character.Inventory, kris, session.Vault, 5);

        Assert.That(character.Inventory.Items, Does.Not.Contain(kris));
        Assert.That(session.Vault.Items, Does.Contain(kris));
        Assert.That(kris.ItemSlot, Is.EqualTo(5));

        session.TransferItem(session.Vault, kris, character.Inventory);
        Assert.That(character.Inventory.Items, Does.Contain(kris));
        Assert.That(session.Vault.Items, Is.Empty);
    }

    /// <summary>
    /// Tests that transfer to a full storage keeps the item where it was.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TransferToAFullStorageKeepsTheItemWhereItWasAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var character = await session.CreateCharacterAsync(InMemoryServer.NewCharacterName(), this.Db.CharacterClasses.First(c => c.CanGetCreated));
        for (var i = 0; i < session.Vault.Areas[0].SlotCount; i++)
        {
            session.AddItem(session.Vault, this.Db.Jewel());
        }

        var kris = session.AddItem(character.Inventory, this.Db.Kris());
        var slot = kris.ItemSlot;

        Assert.Throws<AccountManagerException>(() => session.TransferItem(character.Inventory, kris, session.Vault));

        Assert.That(character.Inventory.Items, Does.Contain(kris));
        Assert.That(kris.ItemSlot, Is.EqualTo(slot));
    }

    /// <summary>
    /// Tests that items move between characters of the account.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ItemsMoveBetweenCharactersOfTheAccountAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var characterClass = this.Db.CharacterClasses.First(c => c.CanGetCreated);
        var first = await session.CreateCharacterAsync(InMemoryServer.NewCharacterName(), characterClass);
        var second = await session.CreateCharacterAsync(InMemoryServer.NewCharacterName(), characterClass);
        var item = session.AddItem(first.Inventory, this.Db.Helm());

        session.TransferItem(first.Inventory, item, second.Inventory, 2);

        Assert.That(first.Inventory.Items, Is.Empty);
        Assert.That(second.Inventory.ItemAt(2), Is.SameAs(item));
        Assert.That(session.Storages.Count(), Is.EqualTo(3));
    }

    /// <summary>
    /// Tests that duplicate copies all options into a new item.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task DuplicateCopiesAllOptionsIntoANewItemAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var original = session.AddItem(session.Vault, this.Db.ExcellentItem(), level: 13);
        var editor = session.EditItem(original);
        editor.MakeFullOption();

        var copy = session.DuplicateItem(session.Vault, original);

        Assert.That(copy, Is.Not.SameAs(original));
        Assert.That(copy.ItemSlot, Is.Not.EqualTo(original.ItemSlot));
        Assert.That(copy.Definition, Is.EqualTo(original.Definition));
        Assert.That(copy.Level, Is.EqualTo(13));
        Assert.That(copy.HasSkill, Is.EqualTo(original.HasSkill));
        Assert.That(copy.ItemOptions.Select(o => o.ItemOption).OrderBy(o => o!.GetHashCode()), Is.EqualTo(original.ItemOptions.Select(o => o.ItemOption).OrderBy(o => o!.GetHashCode())));
        Assert.That(session.Vault.Items.Count(), Is.EqualTo(2));
    }

    /// <summary>
    /// Tests that delete and clear remove items.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task DeleteAndClearRemoveItemsAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var first = session.AddItem(session.Vault, this.Db.Jewel());
        session.AddItem(session.Vault, this.Db.Jewel());
        session.AddItem(session.Vault, this.Db.Kris());

        await session.DeleteItemAsync(session.Vault, first);
        Assert.That(session.Vault.Items.Count(), Is.EqualTo(2));

        Assert.That(await session.ClearStorageAsync(session.Vault), Is.EqualTo(2));
        Assert.That(session.Vault.Items, Is.Empty);
        Assert.That(await session.ClearStorageAsync(session.Vault), Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that new items get level and full durability.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewItemsGetLevelAndFullDurabilityAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var kris = session.AddItem(session.Vault, this.Db.Kris(), level: 20);
        var jewel = session.AddItem(session.Vault, this.Db.Jewel());

        Assert.That(kris.Level, Is.EqualTo(this.Db.Kris().MaximumItemLevel));
        Assert.That(kris.Durability > 0, Is.True);
        Assert.That(jewel.Durability, Is.EqualTo(1)); // a stack of one
    }

    /// <summary>
    /// Tests that editor offers excellent options only for excellent items.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task EditorOffersExcellentOptionsOnlyForExcellentItemsAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var excellent = session.EditItem(session.AddItem(session.Vault, this.Db.ExcellentItem()));
        var jewel = session.EditItem(session.AddItem(session.Vault, this.Db.Jewel()));

        Assert.That(excellent.ExcellentChoices, Is.Not.Empty);
        Assert.That(jewel.ExcellentChoices, Is.Empty);
        Assert.That(jewel.NormalOptionChoices, Is.Empty);
        Assert.That(jewel.CanHaveLuck, Is.False);
        Assert.That(jewel.IsStackable, Is.False);
        var potion = session.EditItem(session.AddItem(session.Vault, this.Db.ItemDefinitions.First(d => d.Group == 14 && d.Durability > 1)));
        Assert.That(potion.IsStackable, Is.True);
        potion.Amount = 3;
        Assert.That(potion.Item.Durability, Is.EqualTo(3));
    }

    /// <summary>
    /// Tests that full option sets every excellent option luck skill and the highest option.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task FullOptionSetsEveryExcellentOptionLuckSkillAndTheHighestOptionAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var item = session.AddItem(session.Vault, this.Db.Kris());
        var editor = session.EditItem(item);

        editor.MakeFullOption();

        foreach (var c in editor.ExcellentChoices)
        {
            Assert.That(c.IsChecked, Is.True, c.Caption);
        }

        Assert.That(item.ItemOptions.Count(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent), Is.EqualTo(editor.ExcellentChoices.Count));
        Assert.That(item.HasSkill, Is.EqualTo(editor.CanHaveSkill));
        Assert.That(editor.HasLuck, Is.True);
        Assert.That(editor.NormalOption, Is.Not.Null);
        Assert.That(editor.NormalOptionLevel, Is.EqualTo(editor.NormalOptionMaximumLevel));
        Assert.That(ItemDisplay.Kind(item), Is.EqualTo("excellent"));
    }

    /// <summary>
    /// Tests that full option repairs the item to its highest durability.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task FullOptionRepairsTheItemToItsHighestDurabilityAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var item = session.AddItem(session.Vault, this.Db.Kris(), level: 13);
        var editor = session.EditItem(item);
        editor.Durability = 3;

        editor.MakeFullOption();

        Assert.That(item.Durability, Is.EqualTo(editor.MaximumDurability));
        Assert.That(item.Durability > 3, Is.True);
    }

    /// <summary>
    /// Tests that full option adds the 380 option when the item can have it.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task FullOptionAddsThe380OptionWhenTheItemCanHaveItAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var editors = this.Db.ItemDefinitions
            .Where(d => d.PossibleItemOptions.Any(o => o.PossibleOptions.Any(p => p.OptionType == ItemOptionTypes.GuardianOption)))
            .Take(1)
            .Select(d => session.EditItem(session.AddItem(session.Vault, d)))
            .ToList();
        Assert.That(editors, Has.Exactly(1).Items);
        var guardian = editors.Single();
        Assert.That(guardian.CanHaveGuardianOption, Is.True);
        var kris = session.EditItem(session.AddItem(session.Vault, this.Db.Kris()));

        guardian.MakeFullOption();
        kris.MakeFullOption();

        Assert.That(guardian.HasGuardianOption, Is.True);
        Assert.That(kris.CanHaveGuardianOption, Is.False);
        Assert.That(kris.HasGuardianOption, Is.False);
    }

    /// <summary>
    /// Tests that harmony options start at their default and go up to what the item level allows.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task HarmonyOptionsStartAtTheirDefaultAndGoUpToWhatTheItemLevelAllowsAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var item = session.AddItem(session.Vault, this.Db.Kris());
        var editor = session.EditItem(item);
        Assert.That(editor.HarmonyOptionChoices, Is.Not.Empty);
        var option = editor.HarmonyOptionChoices.OrderByDescending(o => o.LevelDependentOptions.Count).First();

        editor.Level = 0;
        editor.HarmonyOption = option;
        var lowest = option.LevelDependentOptions.Min(l => l.Level);
        Assert.That(editor.HarmonyOptionDefaultLevel, Is.EqualTo(lowest));
        Assert.That(editor.HarmonyOptionLevel, Is.EqualTo(lowest));

        editor.Level = 15;
        var highest = option.LevelDependentOptions.Where(l => l.RequiredItemLevel <= 15).Max(l => l.Level);
        Assert.That(editor.HarmonyOptionMaximumLevel, Is.EqualTo(highest));
        Assert.That(highest > lowest, Is.True);
        editor.HarmonyOptionLevel = 99;
        Assert.That(editor.HarmonyOptionLevel, Is.EqualTo(highest));

        editor.Level = 5;
        Assert.That(editor.HarmonyOptionMaximumLevel < highest, Is.True, "a +5 item allows fewer harmony levels than a +15 item");
    }

    /// <summary>
    /// Tests that item level stops at the maximum of the item.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ItemLevelStopsAtTheMaximumOfTheItemAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var jewel = session.EditItem(session.AddItem(session.Vault, this.Db.Jewel()));
        var kris = session.EditItem(session.AddItem(session.Vault, this.Db.Kris()));

        jewel.Level = 15;
        kris.Level = 15;

        Assert.That(jewel.Level, Is.EqualTo(jewel.MaximumLevel));
        Assert.That(jewel.MaximumLevel < 15, Is.True);
        Assert.That(kris.Level, Is.EqualTo(15));
    }

    /// <summary>
    /// Tests that excellent options can be switched off again.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ExcellentOptionsCanBeSwitchedOffAgainAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var item = session.AddItem(session.Vault, this.Db.Kris());
        var editor = session.EditItem(item);

        editor.ExcellentChoices[0].IsChecked = true;
        editor.ExcellentChoices[1].IsChecked = true;
        editor.ExcellentChoices[0].IsChecked = false;

        Assert.That(item.ItemOptions.Count(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent), Is.EqualTo(1));
        Assert.That(editor.ExcellentChoices[1].IsChecked, Is.True);
    }

    /// <summary>
    /// Tests that level and option levels are clamped.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task LevelAndOptionLevelsAreClampedAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var editor = session.EditItem(session.AddItem(session.Vault, this.Db.Kris()));

        editor.Level = 99;
        Assert.That(editor.Level, Is.EqualTo(15));

        editor.NormalOption = editor.NormalOptionChoices[0];
        Assert.That(editor.NormalOptionLevel, Is.EqualTo(1));
        editor.NormalOptionLevel = 50;
        Assert.That(editor.NormalOptionLevel, Is.EqualTo(editor.NormalOptionMaximumLevel));
        editor.NormalOption = null;
        Assert.That(editor.Item.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Option), Is.False);
    }

    /// <summary>
    /// Tests that ancient sets are offered for ancient items.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AncientSetsAreOfferedForAncientItemsAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var item = session.AddItem(session.Vault, this.Db.AncientItem());
        var editor = session.EditItem(item);

        Assert.That(editor.AncientSetChoices, Is.Not.Empty);
        editor.AncientSet = editor.AncientSetChoices[0];

        Assert.That(editor.AncientSet, Is.SameAs(editor.AncientSetChoices[0]));
        Assert.That(ItemDisplay.Kind(item), Is.EqualTo("ancient"));

        editor.AncientSet = null;
        Assert.That(editor.AncientSet, Is.Null);
    }

    /// <summary>
    /// Tests that socket items get sockets with seeds.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SocketItemsGetSocketsWithSeedsAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var item = session.AddItem(session.Vault, this.Db.SocketItem());
        var editor = session.EditItem(item);

        editor.SocketCount = 99;
        Assert.That(editor.SocketCount, Is.EqualTo(editor.MaximumSockets));
        Assert.That(editor.Sockets.Count, Is.EqualTo(editor.MaximumSockets));

        var socket = editor.Sockets[0];
        socket.Option = socket.Choices[0];
        socket.Level = 9;
        Assert.That(socket.Level, Is.EqualTo(4));
        Assert.That(socket.Option?.ToString(), Is.EqualTo(socket.Choices[0].ToString()));
        Assert.That(ItemDisplay.Kind(item), Is.EqualTo("socket"));

        editor.SocketCount = 1;
        Assert.That(editor.Sockets, Has.Exactly(1).Items);
    }

    /// <summary>
    /// Tests that changing the item type keeps the item valid.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ChangingTheItemTypeKeepsTheItemValidAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var item = session.AddItem(session.Vault, this.Db.Kris());
        var editor = session.EditItem(item);
        editor.MakeFullOption();
        var otherSword = this.Db.ItemDefinitions.First(d => d.Group == 0 && d != this.Db.Kris());

        editor.Definition = otherSword;

        Assert.That(item.Definition, Is.SameAs(otherSword));
        Assert.That(item.Durability > 0, Is.True);
        Assert.That(session.PendingChanges.Any(c => c.StartsWith("Edited", StringComparison.Ordinal)), Is.True);
    }

    /// <summary>
    /// Tests that character values are clamped and warp uses the spawn gate.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task CharacterValuesAreClampedAndWarpUsesTheSpawnGateAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var character = await session.CreateCharacterAsync(InMemoryServer.NewCharacterName(), this.Db.CharacterClasses.First(c => c.CanGetCreated));
        var devias = this.Db.Maps.First(m => m.Name.ValueInNeutralLanguage == "Devias");

        character.Level = 1000;
        character.Zen = -5;
        character.InventoryExtensions = 9;
        character.LevelUpPoints = -1;
        character.WarpTo(devias);

        Assert.That(character.Level, Is.EqualTo(400));
        Assert.That(character.Zen, Is.EqualTo(0));
        Assert.That(character.InventoryExtensions, Is.EqualTo(InventoryConstants.MaximumNumberOfExtensions));
        Assert.That(character.LevelUpPoints, Is.EqualTo(0));
        Assert.That(character.CurrentMap, Is.SameAs(devias));
        var gate = devias.ExitGates.First(g => g.IsSpawnGate);
        Assert.That(character.PositionX, Is.InRange(gate.X1, gate.X2));
        Assert.That(character.PositionY, Is.InRange(gate.Y1, gate.Y2));
    }

    /// <summary>
    /// Tests that stats are listed strength agility vitality energy first.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task StatsAreListedStrengthAgilityVitalityEnergyFirstAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var darkKnight = await session.CreateCharacterAsync(InMemoryServer.NewCharacterName(), this.Db.CharacterClasses.First(c => c.CanGetCreated && c.Name.ValueInNeutralLanguage == "Dark Knight"));
        var darkLord = await session.CreateCharacterAsync(InMemoryServer.NewCharacterName(), this.Db.CharacterClasses.First(c => c.CanGetCreated && c.Name.ValueInNeutralLanguage == "Dark Lord"));

        Assert.That(darkKnight.Attributes.Take(4).Select(a => a.Definition!.Designation), Is.EqualTo(new[] { "Base Strength", "Base Agility", "Base Vitality", "Base Energy" }));
        Assert.That(darkLord.Attributes.Take(5).Select(a => a.Definition!.Designation), Is.EqualTo(new[] { "Base Strength", "Base Agility", "Base Vitality", "Base Energy", "Base Leadership" }));
        var rest = darkKnight.Attributes.Skip(4).Select(a => a.Definition!.Designation).ToList();
        Assert.That(rest, Is.EqualTo(rest.Order(StringComparer.Ordinal)));
    }

    /// <summary>
    /// Tests that stat attributes can be edited.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task StatAttributesCanBeEditedAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var character = await session.CreateCharacterAsync(InMemoryServer.NewCharacterName(), this.Db.CharacterClasses.First(c => c.CanGetCreated));
        var strength = character.Attributes.First(a => a.Definition?.Id == Stats.BaseStrength.Id);

        character.SetAttribute(strength, 32767);
        character.SetAttribute(character.Attributes.First(a => a.Definition?.Id == Stats.BaseAgility.Id), -10);

        Assert.That(strength.Value, Is.EqualTo(32767));
        Assert.That(character.Attributes.First(a => a.Definition?.Id == Stats.BaseAgility.Id).Value, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that base stats can be maximized.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task BaseStatsCanBeMaximizedAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var character = await session.CreateCharacterAsync(InMemoryServer.NewCharacterName(), this.Db.CharacterClasses.First(c => c.CanGetCreated));
        var strength = character.Attributes.First(a => a.Definition?.Id == Stats.BaseStrength.Id);
        var agility = character.Attributes.First(a => a.Definition?.Id == Stats.BaseAgility.Id);
        Assert.That(character.CappedBaseStats, Is.Empty); // no maximum configured: nothing to maximize

        try
        {
            strength.Definition!.MaximumValue = 32767; // a reset server's cap, on strength only
            Assert.That(character.CanMaximizeBaseStats, Is.True);

            character.MaximizeBaseStats();

            Assert.That(strength.Value, Is.EqualTo(32767));
            Assert.That(agility.Value, Is.Not.EqualTo(32767)); // no maximum, not touched
            Assert.That(character.CanMaximizeBaseStats, Is.False);
            Assert.That(session.HasChanges, Is.True);
        }
        finally
        {
            strength.Definition!.MaximumValue = null;
        }
    }

    /// <summary>
    /// Tests that deleted character leaves the account.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task DeletedCharacterLeavesTheAccountAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var characterClass = this.Db.CharacterClasses.First(c => c.CanGetCreated);
        var keep = await session.CreateCharacterAsync(InMemoryServer.NewCharacterName(), characterClass);
        var delete = await session.CreateCharacterAsync(InMemoryServer.NewCharacterName(), characterClass);

        await session.DeleteCharacterAsync(delete);

        Assert.That(session.Characters, Is.EqualTo(new[] { keep }));
        Assert.That(session.Account.Characters, Does.Not.Contain(delete.Character));
        var again = await session.CreateCharacterAsync(InMemoryServer.NewCharacterName(), characterClass);
        Assert.That(again.Character.CharacterSlot, Is.EqualTo(1)); // the freed slot is used again
    }

    /// <summary>
    /// Tests that account fields are clamped.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AccountFieldsAreClampedAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());

        session.VaultZen = -1;
        session.WCoinC = 500;
        session.GoblinPoints = -3;
        session.State = AccountState.GameMaster;
        session.IsVaultExtended = true;

        Assert.That(session.VaultZen, Is.EqualTo(0));
        Assert.That(session.WCoinC, Is.EqualTo(500));
        Assert.That(session.GoblinPoints, Is.EqualTo(0));
        Assert.That(session.Account.State, Is.EqualTo(AccountState.GameMaster));
        Assert.That(session.Account.IsVaultExtended, Is.True);
        Assert.That(session.PendingChanges.Count, Is.EqualTo(5));
    }

    /// <summary>
    /// Tests that extended vault has two pages.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ExtendedVaultHasTwoPagesAsync()
    {
        var login = await this.server.NewAccountAsync();
        using (var session = await this.Db.OpenAccountAsync(login))
        {
            session.IsVaultExtended = true;
            await session.SaveAsync();
        }

        using var reopened = await this.Db.OpenAccountAsync(login);
        Assert.That(reopened.Vault.Areas.Count, Is.EqualTo(2));
        foreach (var a in reopened.Vault.Areas)
        {
            Assert.That(a.SlotCount, Is.EqualTo(InventoryConstants.WarehouseSize));
        }

        Assert.That(reopened.Vault.Areas[1].FirstSlot, Is.EqualTo(InventoryConstants.WarehouseSize));
    }

    /// <summary>
    /// Tests that items never span the two vault pages.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ItemsNeverSpanTheTwoVaultPagesAsync()
    {
        var login = await this.server.NewAccountAsync();
        using (var session = await this.Db.OpenAccountAsync(login))
        {
            session.IsVaultExtended = true;
            await session.SaveAsync();
        }

        using var reopened = await this.Db.OpenAccountAsync(login);
        var vault = reopened.Vault;
        var kris = reopened.AddItem(vault, this.Db.Kris()); // 1x2
        var lastRowOfPageOne = InventoryConstants.WarehouseSize - 8;

        Assert.Throws<AccountManagerException>(() => reopened.MoveItem(vault, kris, lastRowOfPageOne));
        reopened.MoveItem(vault, kris, InventoryConstants.WarehouseSize); // top of page two
        Assert.That(kris.ItemSlot, Is.EqualTo(InventoryConstants.WarehouseSize));
    }

    /// <summary>
    /// Tests that a character with four expansions has five separate grids.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ACharacterWithFourExpansionsHasFiveSeparateGridsAsync()
    {
        var (session, character) = await this.CharacterWithExpansionsAsync(4);
        using (session)
        {
            var areas = character.Inventory.Areas;

            Assert.That(areas.Select(a => a.Kind), Is.EqualTo(new[] { StorageAreaKind.Inventory, StorageAreaKind.Expansion, StorageAreaKind.Expansion, StorageAreaKind.Expansion, StorageAreaKind.Expansion, StorageAreaKind.PersonalStore }));
            Assert.That(areas[0].SlotCount, Is.EqualTo(64));
            foreach (var a in areas.Where(a => a.Kind == StorageAreaKind.Expansion))
            {
                Assert.That(a.SlotCount, Is.EqualTo(32));
            }

            Assert.That(character.Inventory.AreasOf(StorageAreaKind.Expansion).Select(a => a.Title), Is.EqualTo(new[] { "Expansion 1", "Expansion 2", "Expansion 3", "Expansion 4" }));

            // The grids follow each other without gaps or overlaps, like the server's slot numbers.
            Assert.That(areas[1].FirstSlot, Is.EqualTo(InventoryConstants.FirstExtensionItemSlotIndex));
            for (var i = 1; i < 5; i++)
            {
                Assert.That(areas[i].FirstSlot, Is.EqualTo(areas[i - 1].FirstSlot + areas[i - 1].SlotCount));
            }

            Assert.That(areas[5].FirstSlot, Is.EqualTo(InventoryConstants.FirstStoreItemSlotIndex));
        }
    }

    /// <summary>
    /// Tests that items never span the inventory and an expansion or two expansions.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ItemsNeverSpanTheInventoryAndAnExpansionOrTwoExpansionsAsync()
    {
        var (session, character) = await this.CharacterWithExpansionsAsync(4);
        using (session)
        {
            var inventory = character.Inventory;
            var kris = session.AddItem(inventory, this.Db.Kris()); // 1x2
            var inventoryArea = inventory.Areas[0];
            var expansion1 = inventory.Areas[1];
            var expansion2 = inventory.Areas[2];
            var lastRowOf = (StorageArea a) => a.FirstSlot + a.SlotCount - 8;

            Assert.Throws<AccountManagerException>(() => session.MoveItem(inventory, kris, lastRowOf(inventoryArea)));
            Assert.Throws<AccountManagerException>(() => session.MoveItem(inventory, kris, lastRowOf(expansion1)));
            session.MoveItem(inventory, kris, lastRowOf(expansion1) - 8); // the last two rows of expansion 1
            session.MoveItem(inventory, kris, expansion2.FirstSlot);
            Assert.That(kris.ItemSlot, Is.EqualTo(expansion2.FirstSlot));
        }
    }

    /// <summary>
    /// Tests that new items fill the inventory then the expansions but never the personal store.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewItemsFillTheInventoryThenTheExpansionsButNeverThePersonalStoreAsync()
    {
        var (session, character) = await this.CharacterWithExpansionsAsync(1);
        using (session)
        {
            var inventory = character.Inventory;
            var jewel = this.Db.Jewel();
            for (var i = 0; i < 64; i++)
            {
                Assert.That(inventory.Areas[0].Contains(session.AddItem(inventory, jewel).ItemSlot), Is.True);
            }

            // A 1x2 item does not fit into the last row of the full inventory's neighbour, it goes to the expansion.
            var kris = session.AddItem(inventory, this.Db.Kris());
            Assert.That(kris.ItemSlot, Is.EqualTo(inventory.Areas[1].FirstSlot));

            while (true)
            {
                try
                {
                    session.AddItem(inventory, jewel);
                }
                catch (AccountManagerException)
                {
                    break;
                }
            }

            var store = inventory.AreasOf(StorageAreaKind.PersonalStore).Single();
            Assert.That(inventory.Items.Any(i => store.Contains(i.ItemSlot)), Is.False);
            Assert.That(inventory.Items.Count(), Is.EqualTo(64 + 32 - 1)); // the kris covers two cells
        }
    }

    /// <summary>
    /// Tests that items can be dropped into the personal store on purpose.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ItemsCanBeDroppedIntoThePersonalStoreOnPurposeAsync()
    {
        var (session, character) = await this.CharacterWithExpansionsAsync(0);
        using (session)
        {
            var inventory = character.Inventory;
            var store = inventory.AreasOf(StorageAreaKind.PersonalStore).Single();
            var jewel = session.AddItem(inventory, this.Db.Jewel());

            session.MoveItem(inventory, jewel, store.FirstSlot);

            Assert.That(jewel.ItemSlot, Is.EqualTo(store.FirstSlot));
            Assert.That(inventory.AreasOf(StorageAreaKind.Expansion), Is.Empty);
        }
    }

    private async Task<(AccountSession Session, CharacterModel Character)> CharacterWithExpansionsAsync(int expansions)
    {
        var login = await this.server.NewAccountAsync();
        var name = InMemoryServer.NewCharacterName();
        using (var session = await this.Db.OpenAccountAsync(login))
        {
            var character = await session.CreateCharacterAsync(name, this.Db.CharacterClasses.First(c => c.CanGetCreated));
            character.InventoryExtensions = expansions;
            await session.SaveAsync();
        }

        var reopened = await this.Db.OpenAccountAsync(login);
        return (reopened, reopened.Characters.Single(c => c.Name == name));
    }

    /// <summary>
    /// Tests that new password works for login.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewPasswordWorksForLoginAsync()
    {
        var login = await this.server.NewAccountAsync();
        using (var session = await this.Db.OpenAccountAsync(login))
        {
            session.SetPassword("n3wpass");
            Assert.Throws<AccountManagerException>(() => session.SetPassword("x"));
            Assert.That(await session.SaveAsync(), Is.EqualTo(SaveOutcome.Saved));
        }

        using var context = this.Db.CreatePlayerContext();
        Assert.That(await context.GetAccountByLoginNameAsync(login, "n3wpass"), Is.Not.Null);
        Assert.That(await context.GetAccountByLoginNameAsync(login, "secret"), Is.Null);
    }

    /// <summary>
    /// Tests that saving is blocked while the account is online.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SavingIsBlockedWhileTheAccountIsOnlineAsync()
    {
        var login = await this.server.NewAccountAsync();
        this.server.Online.Set(login, true);
        using var session = await this.Db.OpenAccountAsync(login);
        session.VaultZen = 1000;

        Assert.That(session.WasOnline, Is.True);
        Assert.That(await session.SaveAsync(), Is.EqualTo(SaveOutcome.BlockedOnline));
        Assert.That(session.HasChanges, Is.True);

        this.server.Online.Set(login, false);
        Assert.That(await session.SaveAsync(), Is.EqualTo(SaveOutcome.Saved));
        Assert.That(session.HasChanges, Is.False);
    }

    /// <summary>
    /// Tests that account deletion is blocked while online.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AccountDeletionIsBlockedWhileOnlineAsync()
    {
        var login = await this.server.NewAccountAsync();
        this.server.Online.Set(login, true);

        Assert.ThrowsAsync<AccountManagerException>(() => this.Db.DeleteAccountAsync(login));

        this.server.Online.Set(login, false);
        await this.Db.DeleteAccountAsync(login);
        Assert.ThrowsAsync<AccountManagerException>(() => this.Db.OpenAccountAsync(login));
    }

    /// <summary>
    /// Tests that opening a missing account fails.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task OpeningAMissingAccountFailsAsync()
    {
        Assert.ThrowsAsync<AccountManagerException>(() => this.Db.OpenAccountAsync("nobody123"));
    }

    /// <summary>
    /// Tests that every item of the test accounts can be shown and edited.
    /// </summary>
    /// <param name="login">The login.</param>
    /// <returns>The task.</returns>
    [TestCase("test0")]
    [TestCase("testgm")]
    [TestCase("ancient")]
    [TestCase("socket")]
    [TestCase("test400")]
    public async Task EveryItemOfTheTestAccountsCanBeShownAndEditedAsync(string login)
    {
        using var session = await this.Db.OpenAccountAsync(login);

        Assert.That(session.Characters, Is.Not.Empty);
        foreach (var storage in session.Storages)
        {
            foreach (var item in storage.Items)
            {
                Assert.That(item.Definition, Is.Not.Null);
                Assert.That(storage.IsEquipmentSlot(item.ItemSlot) || storage.Areas.Any(a => a.Contains(item.ItemSlot)), Is.True, $"{item.Definition!.Name} at {item.ItemSlot} is outside of {storage.Title}");
                Assert.That(storage.ItemCovering(item.ItemSlot) == item || storage.FindOverlaps().Contains(item), Is.True, $"{item.Definition!.Name} is hidden but not reported");
                var editor = session.EditItem(item);
                Assert.That(ItemDisplay.Tooltip(item), Is.Not.Null);
                _ = editor.ExcellentChoices.Count + editor.AncientSetChoices.Count + editor.Sockets.Count;
            }
        }

        Assert.That(session.HasChanges, Is.False, "looking at items must not change them");
    }

    /// <summary>
    /// Tests that overlapping items are found and moved to free places.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task OverlappingItemsAreFoundAndMovedToFreePlacesAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var bottom = session.AddItem(session.Vault, this.Db.Kris(), slot: 0);
        var onTop = session.AddItem(session.Vault, this.Db.Jewel(), slot: 1);
        onTop.ItemSlot = 8; // what broken data looks like: on the second cell of the kris

        Assert.That(session.Vault.FindOverlaps(), Is.EqualTo(new[] { onTop }));

        Assert.That(session.ResolveOverlaps(session.Vault), Is.EqualTo(1));
        Assert.That(session.Vault.FindOverlaps(), Is.Empty);
        Assert.That(bottom.ItemSlot, Is.EqualTo(0));
        Assert.That(session.Vault.ItemCovering(onTop.ItemSlot), Is.SameAs(onTop));
    }

    /// <summary>
    /// Tests that the game master test account has two pendants in one slot.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TheGameMasterTestAccountHasTwoPendantsInOneSlotAsync()
    {
        // A flaw of OpenMU's test data, which MuManager reports and can repair.
        using var session = await this.Db.OpenAccountAsync("testgm");
        var inventory = session.Characters.First(c => c.Inventory.FindOverlaps().Count > 0).Inventory;

        foreach (var i in inventory.FindOverlaps())
        {
            Assert.That(i.ItemSlot, Is.EqualTo(9));
        }
    }
}
