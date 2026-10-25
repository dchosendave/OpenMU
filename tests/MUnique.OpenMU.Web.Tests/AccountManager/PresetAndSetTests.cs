// <copyright file="PresetAndSetTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AccountManager;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.Web.AdminPanel.AccountManager;

/// <summary>
/// The tools for building packages quickly: armor sets, option presets, the class filter and changing
/// several items at once.
/// </summary>
[TestFixture]
public sealed class PresetAndSetTests
{
    private InMemoryServer server = null!;

    private static readonly ItemOptionPreset FullOption = new() { Name = "Full option", Skill = true, Luck = true, OptionLevel = ItemOptionPreset.MaximumOptionLevel, Excellent = true, Guardian = true };

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
    /// Tests that armor sets are the pieces with the same number.
    /// </summary>
    [Test]
    public void ArmorSetsAreThePiecesWithTheSameNumber()
    {
        var sets = this.Db.ArmorSets;

        var dragon = sets.Single(s => s.Name == "Dragon");
        Assert.That(dragon.Pieces.Select(p => (int)p.Group), Is.EqualTo(new[] { 7, 8, 9, 10, 11 }));
        foreach (var p in dragon.Pieces)
        {
            Assert.That(p.Name.ToString(), Does.StartWith("Dragon "));
        }

        Assert.That(sets.Any(s => s.Name == "Dragon Knight" && s.Number == dragon.Number), Is.False);

        var stormCrow = sets.Single(s => s.Name == "Storm Crow"); // Magic Gladiators wear no helm
        Assert.That(stormCrow.Pieces.Select(p => (int)p.Group), Is.EqualTo(new[] { 8, 9, 10, 11 }));
        Assert.That(stormCrow.Main.Group, Is.EqualTo(8));
        Assert.That(sets.Select(s => s.Name).Distinct().Count(), Is.EqualTo(sets.Count));
    }

    /// <summary>
    /// Tests that a preset gives only the options an item can have.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task APresetGivesOnlyTheOptionsAnItemCanHaveAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var armor = session.AddItem(session.Vault, this.Db.ArmorSets.Single(s => s.Name == "Dragon").Main, level: 15, preset: FullOption);
        var jewel = session.AddItem(session.Vault, this.Db.Jewel(), preset: FullOption);

        var editor = session.EditItem(armor);
        Assert.That(armor.Level, Is.EqualTo(15));
        Assert.That(editor.HasLuck, Is.True);
        Assert.That(armor.HasSkill, Is.False); // armor has no skill
        Assert.That(editor.NormalOptionLevel, Is.EqualTo(editor.NormalOptionMaximumLevel));
        foreach (var c in editor.ExcellentChoices)
        {
            Assert.That(c.IsChecked, Is.True, c.Caption);
        }

        Assert.That(armor.Durability, Is.EqualTo(editor.MaximumDurability));

        Assert.That(jewel.ItemOptions, Is.Empty);
        Assert.That(jewel.Durability, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that a preset with a level overrides the catalog level and a plain preset clears options.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task APresetWithALevelOverridesTheCatalogLevelAndAPlainPresetClearsOptionsAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var freebie = new ItemOptionPreset { Level = 13, Luck = true, OptionLevel = 3 };
        var item = session.AddItem(session.Vault, this.Db.Kris(), level: 5, preset: FullOption);

        session.EditItem(item).ApplyPreset(freebie);

        var editor = session.EditItem(item);
        Assert.That(item.Level, Is.EqualTo(13));
        Assert.That(item.HasSkill, Is.False);
        Assert.That(editor.HasLuck, Is.True);
        Assert.That(editor.NormalOptionLevel, Is.EqualTo(3));
        Assert.That(item.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent), Is.False);

        session.EditItem(item).ApplyPreset(new ItemOptionPreset());
        Assert.That(item.ItemOptions, Is.Empty);
        Assert.That(item.Level, Is.EqualTo(13));
    }

    /// <summary>
    /// Tests that a set is added completely or not at all.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ASetIsAddedCompletelyOrNotAtAllAsync()
    {
        using var session = await this.Db.OpenAccountAsync(await this.server.NewAccountAsync());
        var dragon = this.Db.ArmorSets.Single(s => s.Name == "Dragon");

        var pieces = session.AddItems(session.Vault, dragon.Pieces, "Dragon set", 15, FullOption);
        Assert.That(pieces.Count, Is.EqualTo(5));
        foreach (var p in pieces)
        {
            Assert.That(p.Level, Is.EqualTo(15));
        }

        Assert.That(session.Vault.FindOverlaps(), Is.Empty);

        // Leave three free cells: room for some pieces, not for all.
        var used = session.Vault.Items.Sum(i => i.Definition!.Width * i.Definition.Height);
        for (var i = used; i < session.Vault.Areas[0].SlotCount - 3; i++)
        {
            session.AddItem(session.Vault, this.Db.Jewel());
        }

        var count = session.Vault.Items.Count();
        var error = Assert.Throws<AccountManagerException>(() => session.AddItems(session.Vault, dragon.Pieces, "Dragon set"));
        Assert.That(error?.Message, Does.Contain("nothing was added"));
        Assert.That(session.Vault.Items.Count(), Is.EqualTo(count));
    }

    /// <summary>
    /// Tests that the package editor changes several items at once.
    /// </summary>
    [Test]
    public void ThePackageEditorChangesSeveralItemsAtOnce()
    {
        using var editor = new PackageEditor(this.Db, new ItemPackage { Name = "Bulk" });
        var dragon = editor.AddItems(this.Db.ArmorSets.Single(s => s.Name == "Dragon").Pieces, 0, FullOption);
        var kris = editor.AddItem(this.Db.Kris(), 15, FullOption);

        editor.Apply(dragon, new ItemOptionPreset { Luck = true, OptionLevel = 2 }, level: 13);

        foreach (var p in dragon)
        {
            Assert.That(p.Level, Is.EqualTo(13));
            Assert.That(p.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Luck), Is.True);
            Assert.That(p.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent), Is.False);
        }

        Assert.That(kris.Level, Is.EqualTo(15));
        Assert.That(kris.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent), Is.True);
        Assert.That(editor.ToPackage().Items.Count, Is.EqualTo(6));
    }

    /// <summary>
    /// Tests that the class filter offers the first class of each line.
    /// </summary>
    [Test]
    public void TheClassFilterOffersTheFirstClassOfEachLine()
    {
        var converter = new PackageConverter(this.Db.Configuration);
        var names = converter.BaseClasses.Select(c => c.Name.ValueInNeutralLanguage).ToList();

        Assert.That(names, Does.Contain("Dark Knight"));
        Assert.That(names, Does.Contain("Dark Wizard"));
        Assert.That(names, Does.Contain("Magic Gladiator"));
        Assert.That(names, Does.Not.Contain("Blade Knight"));
        Assert.That(names, Does.Not.Contain("Blade Master"));

        var knight = converter.BaseClasses.First(c => c.Name.ValueInNeutralLanguage == "Dark Knight");
        var wizard = converter.BaseClasses.First(c => c.Name.ValueInNeutralLanguage == "Dark Wizard");
        Assert.That(converter.IsFor(this.Db.ArmorSets.Single(s => s.Name == "Dragon").Main, knight), Is.True);
        Assert.That(converter.IsFor(this.Db.ArmorSets.Single(s => s.Name == "Dragon").Main, wizard), Is.False);
        Assert.That(converter.IsFor(this.Db.Jewel(), wizard), Is.True);
    }

    /// <summary>
    /// Tests that presets are corrected and kept in their order.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task PresetsAreCorrectedAndKeptInTheirOrderAsync()
    {
        var repository = new InMemoryItemOptionPresetRepository();
        var store = new ItemOptionPresetStore(repository);
        await store.SaveAsync(new ItemOptionPreset { Name = "First", CreatedAt = DateTime.UtcNow.AddMinutes(-2) }).ConfigureAwait(false);
        await store.SaveAsync(new ItemOptionPreset { Name = "Second", CreatedAt = DateTime.UtcNow.AddMinutes(-1) }).ConfigureAwait(false);
        await store.SaveAsync(new ItemOptionPreset { Name = "Third" }).ConfigureAwait(false);

        var preset = store.Presets[0].Clone();
        preset.Level = 20;
        preset.OptionLevel = 99;
        await store.SaveAsync(preset).ConfigureAwait(false);
        await store.DeleteAsync(store.Presets[1].Id).ConfigureAwait(false);

        var reloaded = new ItemOptionPresetStore(repository);
        await reloaded.LoadAsync().ConfigureAwait(false);
        Assert.That(reloaded.Presets.Select(p => p.Name), Is.EqualTo(new[] { "First", "Third" }));
        Assert.That(reloaded.Presets[0].Level, Is.EqualTo((byte)15));
        Assert.That(reloaded.Presets[0].OptionLevel, Is.EqualTo(ItemOptionPreset.MaximumOptionLevel));
        Assert.ThrowsAsync<AccountManagerException>(() => store.SaveAsync(new ItemOptionPreset { Name = " " }));
        Assert.That(store.UniqueName("third"), Is.EqualTo("third (2)"));
    }
}
