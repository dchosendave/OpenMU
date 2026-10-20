// <copyright file="PackageTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AccountManager;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.Web.AdminPanel.AccountManager;

/// <summary>
/// Item packages: saving items as a package, the package file, and giving a package to a character.
/// </summary>
[TestFixture]
public sealed class PackageTests
{
    private InMemoryServer server = null!;

    private AccountManagerDatabase Db => this.server.Database;

    private CharacterClass DarkKnight => this.Class("Dark Knight");

    private CharacterClass DarkWizard => this.Class("Dark Wizard");

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
    /// Tests that a package gives items with all their options after going through the repository.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task APackageGivesItemsWithAllTheirOptionsAfterGoingThroughTheRepositoryAsync()
    {
        var repository = new InMemoryItemPackageRepository();
        using (var source = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync()))
        {
            var kris = source.AddItem(source.Vault, this.Db.Kris(), level: 15);
            source.EditItem(kris).MakeFullOption();
            var ancient = source.AddItem(source.Vault, this.Db.AncientItem(), level: 13);
            var ancientEditor = source.EditItem(ancient);
            ancientEditor.AncientSet = ancientEditor.AncientSetChoices.First();

            var package = new ItemPackage { Name = "Freebie", Zen = 5_000_000 };
            package.Items.AddRange(source.Vault.Items.Select(PackageConverter.FromItem));
            await new ItemPackageStore(repository).SaveAsync(package).ConfigureAwait(false);
        }

        var store = new ItemPackageStore(repository);
        await store.LoadAsync().ConfigureAwait(false);
        var loaded = store.Packages.Single();
        using var target = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var result = target.ApplyPackage(target.Vault, loaded, forClass: null);

        Assert.That(result.Added, Is.EqualTo(2));
        Assert.That(result.Problems, Is.Empty);
        Assert.That(target.Vault.Money, Is.EqualTo(5_000_000));
        var given = target.Vault.Items.Single(i => i.Definition == this.Db.Kris());
        Assert.That(given.Level, Is.EqualTo(15));
        Assert.That(ItemDisplay.Kind(given), Is.EqualTo("excellent"));
        Assert.That(given.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Luck), Is.True);
        Assert.That(loaded.Items.Single(i => i.Group == 0 && i.Number == 0).Options.Count, Is.EqualTo(PackageConverter.FromItem(given).Options.Count));
        Assert.That(target.Vault.Items.Any(i => i.ItemSetGroups.Any(s => s.AncientSetDiscriminator > 0)), Is.True);
        Assert.That(target.PendingChanges.Any(c => c.Contains("Freebie")), Is.True);
    }

    /// <summary>
    /// Tests that a character gets only the items of its class line.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ACharacterGetsOnlyTheItemsOfItsClassLineAsync()
    {
        var dkOnly = this.ItemFor(this.DarkKnight, notFor: this.DarkWizard, group: 8);
        var dwOnly = this.ItemFor(this.DarkWizard, notFor: this.DarkKnight, group: 8);
        var knightWings = this.ItemFor(this.DarkKnight.NextGenerationClass!, notFor: this.DarkWizard, group: 12);
        var package = Package("Everyone", dkOnly, dwOnly, knightWings, this.Db.Jewel());

        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var knight = await session.CreateCharacterAsync(InMemoryServer.NewCharacterName(), this.DarkKnight);
        var result = session.ApplyPackage(knight.Inventory, package, knight.CharacterClass);

        Assert.That(result.Added, Is.EqualTo(3));
        Assert.That(result.SkippedForClass, Is.EqualTo(1));
        Assert.That(knight.Inventory.Items.Select(i => i.Definition!).OrderBy(d => d.Group).ThenBy(d => d.Number), Is.EqualTo(new[] { dkOnly, knightWings, this.Db.Jewel() }));
        foreach (var i in knight.Inventory.Items)
        {
            Assert.That(knight.Inventory.IsEquipmentSlot(i.ItemSlot), Is.False);
        }
    }

    /// <summary>
    /// Tests that a package which does not fit adds nothing.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task APackageWhichDoesNotFitAddsNothingAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var capacity = session.Vault.Areas[0].SlotCount;
        for (var i = 0; i < capacity - 1; i++)
        {
            session.AddItem(session.Vault, this.Db.Jewel());
        }

        var package = Package("Two jewels", this.Db.Jewel(), this.Db.Jewel());
        package.Zen = 100;

        var error = Assert.Throws<AccountManagerException>(() => session.ApplyPackage(session.Vault, package, null));
        Assert.That(error?.Message, Does.Contain("nothing was added"));
        Assert.That(session.Vault.Items.Count(), Is.EqualTo(capacity - 1));
        Assert.That(session.Vault.Money, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that large items of a package are placed first so everything fits.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task LargeItemsOfAPackageArePlacedFirstSoEverythingFitsAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var character = await session.CreateCharacterAsync(InMemoryServer.NewCharacterName(), this.DarkKnight);
        var inventory = character.Inventory;
        var cells = inventory.AreasOf(StorageAreaKind.Inventory).Single().SlotCount;
        var large = this.Db.ItemDefinitions.Where(d => d.Width == 2 && d.Height == 4).First();

        // Jewels first in the list, then as many 2x4 items as fill the rest: only fits when the large ones go first.
        var largeCount = 4;
        var jewels = cells - (largeCount * 8);
        var package = Package("Tetris", [.. Enumerable.Repeat(this.Db.Jewel(), jewels), .. Enumerable.Repeat(large, largeCount)]);

        var result = session.ApplyPackage(inventory, package, null);

        Assert.That(result.Added, Is.EqualTo(jewels + largeCount));
        Assert.That(inventory.FindOverlaps(), Is.Empty);
    }

    /// <summary>
    /// Tests that items unknown to the server are reported and the rest is given.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ItemsUnknownToTheServerAreReportedAndTheRestIsGivenAsync()
    {
        var package = Package("Odd", this.Db.Jewel());
        package.Items.Add(new ItemPackageItem { Group = 15, Number = 999, Name = "Lost scroll" });
        package.Items[0].Options.Add(new ItemPackageItemOption { OptionId = Guid.NewGuid(), Level = 1 });

        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var result = session.ApplyPackage(session.Vault, package, null);

        Assert.That(result.Added, Is.EqualTo(1));
        Assert.That(result.Problems.Any(p => p.Contains("Lost scroll")), Is.True);
        Assert.That(result.Problems.Any(p => p.Contains("option")), Is.True);
    }

    /// <summary>
    /// Tests that a package with nothing for the class says so and changes nothing.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task APackageWithNothingForTheClassSaysSoAndChangesNothingAsync()
    {
        var package = Package("Wizard stuff", this.ItemFor(this.DarkWizard, notFor: this.DarkKnight, group: 8));
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());

        var error = Assert.Throws<AccountManagerException>(() => session.ApplyPackage(session.Vault, package, this.DarkKnight));
        Assert.That(error?.Message, Does.Contain("no items for Dark Knight"));
        Assert.Throws<AccountManagerException>(() => session.ApplyPackage(session.Vault, new ItemPackage { Name = "Empty" }, null));
        Assert.That(session.Vault.Items, Is.Empty);
        Assert.That(session.HasChanges, Is.False);

        // Zen alone is fine, and capped instead of overflowing.
        session.Vault.Money = int.MaxValue - 10;
        var result = session.ApplyPackage(session.Vault, new ItemPackage { Name = "Zen", Zen = 1_000 }, this.DarkKnight);
        Assert.That(result.Zen, Is.EqualTo(10));
        Assert.That(session.Vault.Money, Is.EqualTo(int.MaxValue));
    }

    /// <summary>
    /// Tests that package names are unique and required.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task PackageNamesAreUniqueAndRequiredAsync()
    {
        var store = new ItemPackageStore(new InMemoryItemPackageRepository());
        await store.SaveAsync(new ItemPackage { Name = "Freebie" }).ConfigureAwait(false);

        Assert.ThrowsAsync<AccountManagerException>(() => store.SaveAsync(new ItemPackage { Name = "freebie" }));
        Assert.ThrowsAsync<AccountManagerException>(() => store.SaveAsync(new ItemPackage { Name = "  " }));
        Assert.That(store.UniqueName("Freebie"), Is.EqualTo("Freebie (2)"));
        Assert.That(store.UniqueName("$10 pack"), Is.EqualTo("$10 pack"));
    }

    /// <summary>
    /// Tests that without the database of the admin panel no package is stored.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task WithoutTheDatabaseOfTheAdminPanelNoPackageIsStoredAsync()
    {
        var store = new ItemPackageStore(new UnavailableItemPackageRepository());
        await store.LoadAsync().ConfigureAwait(false);

        Assert.That(store.IsAvailable, Is.False);
        Assert.That(store.Packages, Is.Empty);
        Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(new ItemPackage { Name = "Freebie" }));
        Assert.That(store.Packages, Is.Empty);
    }

    /// <summary>
    /// Tests that the package editor edits items like the item editor and saves them.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ThePackageEditorEditsItemsLikeTheItemEditorAndSavesThemAsync()
    {
        var repository = new InMemoryItemPackageRepository();
        var store = new ItemPackageStore(repository);
        var package = Package("Starter", this.Db.Kris());
        await store.SaveAsync(package).ConfigureAwait(false);

        using (var editor = new PackageEditor(this.Db, store.Find(package.Id)!))
        {
            Assert.That(editor.HasChanges, Is.False);
            var itemEditor = editor.Edit(editor.Items.Single());
            itemEditor.Level = 15;
            itemEditor.MakeFullOption();
            editor.AddItem(this.Db.Jewel());
            editor.Zen = 1_000;
            Assert.That(editor.HasChanges, Is.True);
            Assert.That(store.Find(package.Id)!.Items, Has.Exactly(1).Items); // nothing is stored before saving
            await editor.SaveAsync(store).ConfigureAwait(false);
            Assert.That(editor.HasChanges, Is.False);
        }

        var reloaded = new ItemPackageStore(repository);
        await reloaded.LoadAsync().ConfigureAwait(false);
        var saved = reloaded.Find(package.Id)!;
        Assert.That(saved.Items, Has.Count.EqualTo(2));
        Assert.That(saved.Zen, Is.EqualTo(1_000));
        Assert.That(saved.Items[0].Level, Is.EqualTo(15));
        Assert.That(saved.Items[0].Options.Any(o => o.OptionId != Guid.Empty), Is.True);
    }

    /// <summary>
    /// Tests that classes of an item are its base classes.
    /// </summary>
    [Test]
    public void ClassesOfAnItemAreItsBaseClasses()
    {
        var converter = new PackageConverter(this.Db.Configuration);
        var dkArmor = Package("Armor", this.ItemFor(this.DarkKnight, notFor: this.DarkWizard, group: 8)).Items.Single();

        Assert.That(converter.ClassesOf(dkArmor), Does.Contain("Dark Knight"));
        Assert.That(converter.ClassesOf(dkArmor), Does.Not.Contain("Blade Knight"));
        Assert.That(converter.ClassesOf(new ItemPackageItem { Group = this.Db.Jewel().Group, Number = this.Db.Jewel().Number }), Is.Empty);
    }

    private static ItemPackage Package(string name, params ItemDefinition[] definitions) => new()
    {
        Name = name,
        Items = definitions.Select(d => new ItemPackageItem { Group = d.Group, Number = d.Number, Name = d.Name.ToString() ?? string.Empty, Durability = d.Durability }).ToList(),
    };

    private CharacterClass Class(string name) => this.Db.CharacterClasses.First(c => c.Name.ValueInNeutralLanguage == name);

    private ItemDefinition ItemFor(CharacterClass characterClass, CharacterClass notFor, byte group)
        => this.Db.ItemDefinitions.First(d => d.Group == group && d.QualifiedCharacters.Contains(characterClass) && !d.QualifiedCharacters.Contains(notFor));
}
