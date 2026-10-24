// <copyright file="McpConfigurationIntegrationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.Mcp;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.AdminAuth;
using MUnique.OpenMU.PlugIns;

/// <summary>Verifies configuration changes with preview and apply through a real MCP client.</summary>
[TestFixture]
public sealed class McpConfigurationIntegrationTests
{
    private McpIntegrationTests _host = null!;
    private InMemoryAdminAuditRepository _audit = null!;
    private GameConfiguration _configuration = null!;
    private Mock<IContext> _context = null!;

    /// <summary>Starts a real HTTP host with explicitly enabled writes and a controllable configuration context.</summary>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._host = new McpIntegrationTests { EnableWrites = true };
        await this._host.SetUpAsync().ConfigureAwait(false);
        this._audit = (InMemoryAdminAuditRepository)this._host.App.Services.GetRequiredService<IAdminAuditRepository>();
        var source = this._host.App.Services.GetRequiredService<IDataSource<GameConfiguration>>();
        this._configuration = await source.GetOwnerAsync().ConfigureAwait(false);
        this._context = new Mock<IContext>();
        this._context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        // The in-memory model's collections only accept objects which were created by its own context.
        var memoryContext = this._host.App.Services.GetRequiredService<IPersistenceContextProvider>().CreateNewContext();
        this._context.Setup(c => c.CreateNew<PlugInConfiguration>(It.IsAny<object?[]>())).Returns(() => memoryContext.CreateNew<PlugInConfiguration>());
        Mock.Get(source).Setup(s => s.GetContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(this._context.Object);
    }

    /// <summary>Stops the HTTP host.</summary>
    [TearDown]
    public async Task TearDownAsync() => await this._host.TearDownAsync().ConfigureAwait(false);

    /// <summary>A preview changes nothing; applying it with the token changes, saves and audits exactly the preview.</summary>
    [Test]
    public async Task RatesArePreviewedThenAppliedAsync()
    {
        await using var client = await this.AdminAsync().ConfigureAwait(false);
        var rates = await client.CallToolAsync("rates_get", new Dictionary<string, object?>()).ConfigureAwait(false);
        Assert.That(Structured(rates)["experienceRate"]!.GetValue<float>(), Is.EqualTo(3.0f));

        var preview = await client.CallToolAsync("rates_set", new Dictionary<string, object?> { ["experienceRate"] = 2, ["reason"] = "Weekend event" }).ConfigureAwait(false);
        Assert.That(preview.IsError, Is.Not.True, JsonSerializer.Serialize(preview));
        var change = Structured(preview)["changes"]!.AsArray().Single()!;
        Assert.That(change["field"]!.GetValue<string>(), Is.EqualTo("ExperienceRate"));
        Assert.That(change["before"]!.GetValue<float>(), Is.EqualTo(3.0f));
        Assert.That(change["after"]!.GetValue<float>(), Is.EqualTo(2.0f));
        Assert.That(this._configuration.ExperienceRate, Is.EqualTo(3.0f));
        Assert.That((await this._audit.ReadAsync(0, 100).ConfigureAwait(false)).Total, Is.Zero);

        var token = Structured(preview)["confirmationToken"]!.GetValue<string>();
        var applyArguments = new Dictionary<string, object?> { ["experienceRate"] = 2, ["reason"] = "Weekend event", ["apply"] = true, ["confirmationToken"] = token };
        var applied = await client.CallToolAsync("rates_set", applyArguments).ConfigureAwait(false);
        Assert.That(applied.IsError, Is.Not.True, JsonSerializer.Serialize(applied));
        Assert.That(this._configuration.ExperienceRate, Is.EqualTo(2.0f));
        this._context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        var entry = (await this._audit.ReadAsync(0, 100).ConfigureAwait(false)).Items.Single();
        Assert.That(entry.Operation, Is.EqualTo("rates_set"));
        Assert.That(entry.Outcome, Is.EqualTo("Succeeded"));
        Assert.That(entry.ResultJson, Does.Contain("ExperienceRate"));

        var reused = await client.CallToolAsync("rates_set", applyArguments).ConfigureAwait(false);
        Assert.That(reused.IsError, Is.True);
        Assert.That(Text(reused), Does.Contain("Preview the change again"));
    }

    /// <summary>A token only applies the previewed arguments, and it is used up by a mismatching attempt.</summary>
    [Test]
    public async Task TokenIsBoundToThePreviewedArgumentsAsync()
    {
        await using var client = await this.AdminAsync().ConfigureAwait(false);
        var preview = await client.CallToolAsync("rates_set", new Dictionary<string, object?> { ["experienceRate"] = 2, ["reason"] = "Event" }).ConfigureAwait(false);
        var token = Structured(preview)["confirmationToken"]!.GetValue<string>();
        var changed = await client.CallToolAsync("rates_set", new Dictionary<string, object?> { ["experienceRate"] = 50, ["reason"] = "Event", ["apply"] = true, ["confirmationToken"] = token }).ConfigureAwait(false);
        Assert.That(changed.IsError, Is.True);
        var original = await client.CallToolAsync("rates_set", new Dictionary<string, object?> { ["experienceRate"] = 2, ["reason"] = "Event", ["apply"] = true, ["confirmationToken"] = token }).ConfigureAwait(false);
        Assert.That(original.IsError, Is.True);
        Assert.That(this._configuration.ExperienceRate, Is.EqualTo(3.0f));
        this._context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A generic patch changes scalar and localized fields, and rejects fields which can't be patched.</summary>
    [Test]
    public async Task ConfigUpdatePatchesSupportedFieldsOnlyAsync()
    {
        var item = this._configuration.Items.Single();
        await using var client = await this.AdminAsync().ConfigureAwait(false);
        var patch = new JsonObject { ["Name"] = "Better Sword", ["Width"] = 2 };
        await this.PreviewAndApplyAsync(client, "config_update", new Dictionary<string, object?> { ["type"] = nameof(ItemDefinition), ["id"] = item.GetId(), ["patch"] = patch, ["reason"] = "Rename" }).ConfigureAwait(false);
        Assert.That(item.Name.ValueInNeutralLanguage, Is.EqualTo("Better Sword"));
        Assert.That(item.Width, Is.EqualTo(2));

        foreach (var (invalid, expected) in new[]
        {
            (new JsonObject { ["Unknown"] = 1 }, "not a writable field"),
            (new JsonObject { ["QualifiedCharacters"] = new JsonArray() }, "not a writable field"),
            (new JsonObject { ["Width"] = "wide" }, "expects Byte"),
            (new JsonObject { ["Width"] = 1.5 }, "expects Byte"),
            (new JsonObject { ["Width"] = null }, "can't be null"),
        })
        {
            var result = await client.CallToolAsync("config_update", new Dictionary<string, object?> { ["type"] = nameof(ItemDefinition), ["id"] = item.GetId(), ["patch"] = invalid, ["reason"] = "Invalid" }).ConfigureAwait(false);
            Assert.That(result.IsError, Is.True, invalid.ToJsonString());
            Assert.That(Text(result), Does.Contain(expected), invalid.ToJsonString());
        }

        var plugins = await client.CallToolAsync("config_update", new Dictionary<string, object?> { ["type"] = nameof(PlugInConfiguration), ["id"] = Guid.NewGuid(), ["patch"] = new JsonObject { ["IsActive"] = false }, ["reason"] = "Secrets" }).ConfigureAwait(false);
        Assert.That(plugins.IsError, Is.True);
    }

    /// <summary>Unsaved edits in the panel's shared context are never saved together with an agent's change.</summary>
    [Test]
    public async Task UnsavedPanelEditsBlockApplyAsync()
    {
        await using var client = await this.AdminAsync().ConfigureAwait(false);
        var preview = await client.CallToolAsync("rates_set", new Dictionary<string, object?> { ["experienceRate"] = 2, ["reason"] = "Event" }).ConfigureAwait(false);
        this._context.SetupGet(c => c.HasChanges).Returns(true);
        var applied = await client.CallToolAsync("rates_set", new Dictionary<string, object?> { ["experienceRate"] = 2, ["reason"] = "Event", ["apply"] = true, ["confirmationToken"] = Structured(preview)["confirmationToken"]!.GetValue<string>() }).ConfigureAwait(false);
        Assert.That(applied.IsError, Is.True);
        Assert.That(Text(applied), Does.Contain("unsaved configuration edits"));
        Assert.That(this._configuration.ExperienceRate, Is.EqualTo(3.0f));
        this._context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.That((await this._audit.ReadAsync(0, 100).ConfigureAwait(false)).Items.Single().Outcome, Is.EqualTo("Failed"));
    }

    /// <summary>A change of the configuration between preview and apply invalidates the preview.</summary>
    [Test]
    public async Task ChangesSinceThePreviewInvalidateItAsync()
    {
        await using var client = await this.AdminAsync().ConfigureAwait(false);
        var preview = await client.CallToolAsync("rates_set", new Dictionary<string, object?> { ["experienceRate"] = 2, ["reason"] = "Event" }).ConfigureAwait(false);
        this._configuration.ExperienceRate = 5;
        var applied = await client.CallToolAsync("rates_set", new Dictionary<string, object?> { ["experienceRate"] = 2, ["reason"] = "Event", ["apply"] = true, ["confirmationToken"] = Structured(preview)["confirmationToken"]!.GetValue<string>() }).ConfigureAwait(false);
        Assert.That(applied.IsError, Is.True);
        Assert.That(Text(applied), Does.Contain("changed since the preview"));
        Assert.That(this._configuration.ExperienceRate, Is.EqualTo(5.0f));
    }

    /// <summary>A failed save leaves the shared configuration as it was.</summary>
    [Test]
    public async Task FailedSaveRevertsTheChangeAsync()
    {
        this._context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("database down"));
        await using var client = await this.AdminAsync().ConfigureAwait(false);
        var preview = await client.CallToolAsync("rates_set", new Dictionary<string, object?> { ["experienceRate"] = 2, ["masterExperienceRate"] = 4, ["reason"] = "Event" }).ConfigureAwait(false);
        var applied = await client.CallToolAsync("rates_set", new Dictionary<string, object?> { ["experienceRate"] = 2, ["masterExperienceRate"] = 4, ["reason"] = "Event", ["apply"] = true, ["confirmationToken"] = Structured(preview)["confirmationToken"]!.GetValue<string>() }).ConfigureAwait(false);
        Assert.That(applied.IsError, Is.True);
        Assert.That(Text(applied), Does.Contain("reverted").And.Not.Contain("database down"));
        Assert.That(this._configuration.ExperienceRate, Is.EqualTo(3.0f));
        Assert.That(this._configuration.MasterExperienceRate, Is.EqualTo(1.0f));
    }

    /// <summary>Plugins get an explicit stored state; running servers pick it up through the change propagation.</summary>
    [Test]
    public async Task PluginsCanBeDeactivatedAsync()
    {
        var plugins = this._host.App.Services.GetRequiredService<PlugInManager>();
        plugins.DiscoverAndRegisterPlugIns(typeof(PeriodicSaveProgressPlugIn).Assembly);
        var plugInId = typeof(PeriodicSaveProgressPlugIn).GUID;
        Assert.That(plugins.IsPlugInActive(plugInId), Is.True);
        await using var client = await this.AdminAsync().ConfigureAwait(false);
        await this.PreviewAndApplyAsync(client, "plugins_set_active", new Dictionary<string, object?> { ["plugInId"] = plugInId, ["active"] = false, ["reason"] = "Maintenance" }).ConfigureAwait(false);
        var stored = this._configuration.PlugInConfigurations.Single(c => c.TypeId == plugInId);
        Assert.That(stored.IsActive, Is.False);
        var unchanged = await client.CallToolAsync("plugins_set_active", new Dictionary<string, object?> { ["plugInId"] = plugInId, ["active"] = false, ["reason"] = "Again" }).ConfigureAwait(false);
        Assert.That(Structured(unchanged)["confirmationToken"], Is.Null);
    }

    /// <summary>Viewers can read the rates, but neither see nor call configuration writes.</summary>
    [Test]
    public async Task ViewersCanOnlyReadRatesAsync()
    {
        await using var viewer = await this._host.ConnectAsync().ConfigureAwait(false);
        var names = (await viewer.ListToolsAsync().ConfigureAwait(false)).Select(t => t.Name).ToList();
        Assert.That(names, Does.Contain("rates_get"));
        Assert.That(names, Has.None.AnyOf("rates_set", "config_update", "plugins_set_active"));
        Assert.ThrowsAsync<McpProtocolException>(async () => await viewer.CallToolAsync("rates_set", new Dictionary<string, object?> { ["experienceRate"] = 2, ["reason"] = "Event" }).ConfigureAwait(false));
        Assert.That((await viewer.CallToolAsync("rates_get", new Dictionary<string, object?>()).ConfigureAwait(false)).IsError, Is.Not.True);
    }

    private static JsonNode Structured(CallToolResult result) => JsonSerializer.SerializeToNode(result.StructuredContent)!;

    private static string Text(CallToolResult result) => string.Join(' ', result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    private async Task PreviewAndApplyAsync(McpClient client, string tool, Dictionary<string, object?> arguments)
    {
        var preview = await client.CallToolAsync(tool, arguments).ConfigureAwait(false);
        Assert.That(preview.IsError, Is.Not.True, JsonSerializer.Serialize(preview));
        var token = Structured(preview)["confirmationToken"]?.GetValue<string>();
        Assert.That(token, Is.Not.Null, JsonSerializer.Serialize(preview));
        var applied = await client.CallToolAsync(tool, new Dictionary<string, object?>(arguments) { ["apply"] = true, ["confirmationToken"] = token }).ConfigureAwait(false);
        Assert.That(applied.IsError, Is.Not.True, JsonSerializer.Serialize(applied));
    }

    private ValueTask<McpClient> AdminAsync() => this._host.ConnectAsync("omu_mcp-admin-test");
}
