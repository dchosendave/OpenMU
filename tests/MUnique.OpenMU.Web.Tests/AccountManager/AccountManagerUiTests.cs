// <copyright file="AccountManagerUiTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AccountManager;

using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.Web.AdminPanel.AccountManager;
using MUnique.OpenMU.Web.AdminPanel.Components.AccountManager;
using MUnique.OpenMU.Web.Shared.Components.Modal;
using MUnique.OpenMU.Web.Shared.Components.Toast;

/// <summary>
/// The components of the account manager, driven like a user would: searching, clicking, typing and saving.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class AccountManagerUiTests
{
    private InMemoryServer _server = null!;

    private BunitContext _context = null!;

    private Mock<IToastService> _toastService = null!;

    private InMemoryItemPackageRepository _packages = null!;

    private CultureInfo _previousUiCulture = null!;

    private AccountManagerState State => this._context.Services.GetRequiredService<AccountManagerState>();

    /// <summary>
    /// Sets up the test context with the services of the account manager.
    /// </summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        this._server = await InMemoryServer.GetAsync().ConfigureAwait(false);
        this._toastService = new Mock<IToastService>();
        this._packages = new InMemoryItemPackageRepository();

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton<IPersistenceContextProvider>(this._server.Provider);
        this._context.Services.AddSingleton<IAccountOnlineStatus>(this._server.Online);
        this._context.Services.AddSingleton<IItemPackageRepository>(this._packages);
        this._context.Services.AddSingleton<IItemOptionPresetRepository>(new InMemoryItemOptionPresetRepository());
        this._context.Services.AddSingleton(this._toastService.Object);
        this._context.Services.AddSingleton(Mock.Of<IModalService>());
        this._context.Services.AddSingleton(Mock.Of<ILogger<AccountManagerState>>());
        this._context.Services.AddScoped<ItemPackageStore>();
        this._context.Services.AddScoped<ItemOptionPresetStore>();
        this._context.Services.AddScoped<AccountManagerState>();
        await this.State.InitializeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Restores the culture and disposes the test context.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
        CultureInfo.CurrentUICulture = this._previousUiCulture;
    }

    /// <summary>
    /// Tests that the search lists the matching accounts and that picking one opens it.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SearchingAndPickingAnAccountOpensItAsync()
    {
        var login = await this._server.NewAccountAsync().ConfigureAwait(false);
        var search = this._context.Render<AccountSearch>(parameters => parameters
            .Add(p => p.AccountPicked, (string name) => this.State.OpenAccountAsync(name)));

        search.Find("input.search").Input(login);
        search.Find("form").Submit();
        search.WaitForElement($"button.account[data-login='{login}']").Click();

        search.WaitForAssertion(() => Assert.That(this.State.Session?.Account.LoginName, Is.EqualTo(login)));
        Assert.That(search.FindAll("button.account"), Is.Empty, "the results close after picking");
    }

    /// <summary>
    /// Tests that the items tab shows the items of the selected character and of the vault as tiles.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TheItemsTabShowsTheInventoryAndTheVaultAsync()
    {
        await this.OpenAccountWithCharacterAsync().ConfigureAwait(false);
        var session = this.State.Session!;
        session.AddItem(this.State.SelectedCharacter!.Inventory, this._server.Database.Kris());
        session.AddItem(session.Vault, this._server.Database.Jewel());
        session.AddItem(session.Vault, this._server.Database.Jewel());

        var tab = this._context.Render<ItemsTab>();

        Assert.That(tab.FindAll(".grid[data-area='Inventory'] .tile"), Has.Count.EqualTo(1));
        Assert.That(tab.FindAll(".grid[data-area='Vault'] .tile"), Has.Count.EqualTo(2));
        Assert.That(tab.FindAll(".equip-slot"), Has.Count.EqualTo(12));
    }

    /// <summary>
    /// Tests that "Add" in the catalog puts the item into the inventory of the selected character.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AddingFromTheCatalogPutsTheItemIntoTheInventoryAsync()
    {
        await this.OpenAccountWithCharacterAsync().ConfigureAwait(false);
        var panel = this._context.Render<ItemSidePanel>();

        panel.Find("input.catalog-search").Input("Kris");
        panel.FindAll("button.add-item")[0].Click();

        var inventory = this.State.SelectedCharacter!.Inventory;
        Assert.That(inventory.Items.Select(i => i.Definition), Does.Contain(this._server.Database.Kris()));
        Assert.That(this.State.Session!.HasChanges, Is.True);
        this._toastService.Verify(t => t.ShowSuccess(It.Is<string>(m => m.Contains("Kris")), null), Times.Once);
    }

    /// <summary>
    /// Tests that clicking a tile opens the item editor, and that "Full option" gives the item its excellent options.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ClickingATileOpensTheItemEditorAsync()
    {
        await this.OpenAccountWithCharacterAsync().ConfigureAwait(false);
        var item = this.State.Session!.AddItem(this.State.SelectedCharacter!.Inventory, this._server.Database.Kris());
        var tab = this._context.Render<ItemsTab>();

        tab.Find(".grid[data-area='Inventory'] .tile").Click();
        tab.Find("button.full-option").Click();

        Assert.That(this.State.SelectedItem, Is.SameAs(item));
        Assert.That(ItemDisplay.Kind(item), Is.EqualTo("excellent"));
        Assert.That(tab.Find(".item-editor .fw-semibold").TextContent, Does.Contain("Kris"));
    }

    /// <summary>
    /// Tests that the character tab changes the level of the character, within the limits of the server.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TheCharacterTabChangesTheLevelAsync()
    {
        await this.OpenAccountWithCharacterAsync().ConfigureAwait(false);
        var tab = this._context.Render<CharacterTab>();

        tab.Find("input.character-level").Change("50");
        Assert.That(this.State.SelectedCharacter!.Level, Is.EqualTo(50));

        tab.Find("input.character-level").Change("100000");
        Assert.That(this.State.SelectedCharacter.Level, Is.EqualTo(this._server.Database.Configuration.MaximumLevel));
        Assert.That(this.State.Session!.HasChanges, Is.True);
    }

    /// <summary>
    /// Tests that saving an account which is online is refused with an error, and that the changes stay.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SavingAnOnlineAccountShowsAnErrorAsync()
    {
        var login = await this.OpenAccountWithCharacterAsync().ConfigureAwait(false);
        this.State.Session!.VaultZen = 1_000;
        this._server.Online.Set(login, true);
        try
        {
            var view = this._context.Render<AccountView>();
            view.Find("button.save").Click();

            view.WaitForAssertion(() => this._toastService.Verify(t => t.ShowError(It.Is<string>(m => m.Contains("logged in")), null), Times.Once));
            Assert.That(this.State.Session.HasChanges, Is.True);
        }
        finally
        {
            this._server.Online.Set(login, false);
        }
    }

    /// <summary>
    /// Tests that a package which is built in the package manager is stored with its items.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task APackageBuiltInThePackageManagerIsStoredAsync()
    {
        await this.State.NewPackageAsync().ConfigureAwait(false);
        var manager = this._context.Render<PackageManager>();

        manager.Find("button.sets-filter").Click();
        manager.FindAll("button.add-set")[0].Click();
        manager.Find("button.save-package").Click();

        manager.WaitForAssertion(() => Assert.That(this.State.PackageEditor!.HasChanges, Is.False));
        var stored = (await this._packages.GetAllAsync().ConfigureAwait(false)).Single();
        Assert.That(stored.Items, Has.Count.EqualTo(this._server.Database.ArmorSets[0].Pieces.Count));
    }

    private async Task<string> OpenAccountWithCharacterAsync()
    {
        var login = await this._server.NewAccountAsync().ConfigureAwait(false);
        await this.State.OpenAccountAsync(login).ConfigureAwait(false);
        var characterClass = this._server.Database.CharacterClasses.First(c => c.CanGetCreated);
        await this.State.CreateCharacterAsync(InMemoryServer.NewCharacterName(), characterClass).ConfigureAwait(false);
        return login;
    }
}
