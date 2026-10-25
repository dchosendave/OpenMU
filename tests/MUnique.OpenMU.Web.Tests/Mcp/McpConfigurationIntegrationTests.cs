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
using MUnique.OpenMU.GameLogic.Discord;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Resets;
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
    private IContext _memoryContext = null!;

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
        this._memoryContext = this._host.App.Services.GetRequiredService<IPersistenceContextProvider>().CreateNewContext();
        this._context.Setup(c => c.CreateNew<PlugInConfiguration>(It.IsAny<object?[]>())).Returns(() => this._memoryContext.CreateNew<PlugInConfiguration>());
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

    /// <summary>A batch previews the changes of several objects together and applies them with one save.</summary>
    [Test]
    public async Task ConfigUpdateBatchAppliesAllChangesTogetherAsync()
    {
        var first = this._configuration.Items.Single();
        var second = this._memoryContext.CreateNew<ItemDefinition>();
        second.Name = "Test Shield";
        this._configuration.Items.Add(second);
        await using var client = await this.AdminAsync().ConfigureAwait(false);
        var changes = new JsonArray
        {
            new JsonObject { ["type"] = nameof(ItemDefinition), ["id"] = first.GetId().ToString(), ["patch"] = new JsonObject { ["Width"] = 2 } },
            new JsonObject { ["type"] = nameof(ItemDefinition), ["id"] = second.GetId().ToString(), ["patch"] = new JsonObject { ["Width"] = 3 } },
        };
        var preview = await client.CallToolAsync("config_update_batch", new Dictionary<string, object?> { ["changes"] = changes, ["reason"] = "Sizes" }).ConfigureAwait(false);
        Assert.That(preview.IsError, Is.Not.True, JsonSerializer.Serialize(preview));
        Assert.That(Structured(preview)["changes"]!.AsArray(), Has.Count.EqualTo(2));
        Assert.That(first.Width, Is.Not.EqualTo(2));

        await this.PreviewAndApplyAsync(client, "config_update_batch", new Dictionary<string, object?> { ["changes"] = changes, ["reason"] = "Sizes" }).ConfigureAwait(false);
        Assert.That(first.Width, Is.EqualTo(2));
        Assert.That(second.Width, Is.EqualTo(3));
        this._context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        var duplicate = new JsonArray { changes[0]!.DeepClone(), changes[0]!.DeepClone() };
        var rejected = await client.CallToolAsync("config_update_batch", new Dictionary<string, object?> { ["changes"] = duplicate, ["reason"] = "Twice" }).ConfigureAwait(false);
        Assert.That(rejected.IsError, Is.True);
        Assert.That(Text(rejected), Does.Contain("only appear once"));
    }

    /// <summary>A plugin's configuration is read with hidden secrets, and its scalar fields are patched into a stored configuration.</summary>
    [Test]
    public async Task PluginConfigurationIsReadAndPatchedAsync()
    {
        var plugins = this._host.App.Services.GetRequiredService<PlugInManager>();
        plugins.DiscoverAndRegisterPlugIns(typeof(ResetFeaturePlugIn).Assembly);
        var plugInId = typeof(ResetFeaturePlugIn).GUID;
        await using var client = await this.AdminAsync().ConfigureAwait(false);

        var read = await client.CallToolAsync("plugins_get_configuration", new Dictionary<string, object?> { ["plugInId"] = plugInId }).ConfigureAwait(false);
        Assert.That(read.IsError, Is.Not.True, JsonSerializer.Serialize(read));
        var requiredLevel = Structured(read)["fields"]!.AsArray().Single(f => f!["field"]!.GetValue<string>() == nameof(ResetConfiguration.RequiredLevel))!;
        Assert.That(requiredLevel["value"]!.GetValue<int>(), Is.EqualTo(400));
        Assert.That(requiredLevel["editable"]!.GetValue<bool>(), Is.True);

        await this.PreviewAndApplyAsync(client, "plugins_update_configuration", new Dictionary<string, object?> { ["plugInId"] = plugInId, ["patch"] = new JsonObject { ["RequiredLevel"] = 350, ["ResetStats"] = false }, ["reason"] = "Server rules" }).ConfigureAwait(false);
        var stored = this._configuration.PlugInConfigurations.Single(c => c.TypeId == plugInId);
        var configuration = stored.GetConfiguration<ResetConfiguration>(plugins.CustomConfigReferenceHandler)!;
        Assert.That(configuration.RequiredLevel, Is.EqualTo(350));
        Assert.That(configuration.ResetStats, Is.False);
        Assert.That(configuration.LevelAfterReset, Is.EqualTo(10), "Fields which aren't part of the patch keep their value.");

        var collection = await client.CallToolAsync("plugins_update_configuration", new Dictionary<string, object?> { ["plugInId"] = plugInId, ["patch"] = new JsonObject { ["PointsTiers"] = new JsonArray() }, ["reason"] = "Tiers" }).ConfigureAwait(false);
        Assert.That(collection.IsError, Is.True);
        Assert.That(Text(collection), Does.Contain("only scalar fields"));

        var discordId = typeof(DiscordIntegrationFeaturePlugIn).GUID;
        var discord = await client.CallToolAsync("plugins_get_configuration", new Dictionary<string, object?> { ["plugInId"] = discordId }).ConfigureAwait(false);
        var inviteUrl = Structured(discord)["fields"]!.AsArray().Single(f => f!["field"]!.GetValue<string>() == nameof(DiscordIntegrationConfiguration.InviteUrl))!;
        Assert.That(inviteUrl["editable"]!.GetValue<bool>(), Is.False);
        var secret = await client.CallToolAsync("plugins_update_configuration", new Dictionary<string, object?> { ["plugInId"] = discordId, ["patch"] = new JsonObject { ["InviteUrl"] = "https://example.org" }, ["reason"] = "Secret" }).ConfigureAwait(false);
        Assert.That(secret.IsError, Is.True);
        Assert.That(Text(secret), Does.Contain("can hold a secret"));
    }

    /// <summary>A spawn area is added to a map and removed again, each with preview and apply.</summary>
    [Test]
    public async Task SpawnsAreCreatedAndDeletedAsync()
    {
        var map = this._memoryContext.CreateNew<GameMapDefinition>();
        map.Name = "Devias";
        this._configuration.Maps.Add(map);
        var monster = this._memoryContext.CreateNew<MonsterDefinition>();
        monster.Designation = "Ice Queen";
        this._configuration.Monsters.Add(monster);
        this._context.Setup(c => c.CreateNew<MonsterSpawnArea>(It.IsAny<object?[]>())).Returns(() => this._memoryContext.CreateNew<MonsterSpawnArea>());
        this._context.Setup(c => c.DeleteAsync(It.IsAny<MonsterSpawnArea>())).ReturnsAsync(true);
        await using var client = await this.AdminAsync().ConfigureAwait(false);

        var arguments = new Dictionary<string, object?>
        {
            ["mapId"] = map.GetId(), ["monsterId"] = monster.GetId(), ["x1"] = 68, ["y1"] = 170, ["x2"] = 74, ["y2"] = 176, ["quantity"] = 1,
            ["maximumHealthOverride"] = int.MaxValue, ["reason"] = "Training dummy",
        };
        var preview = await client.CallToolAsync("spawns_create", arguments).ConfigureAwait(false);
        Assert.That(Structured(preview)["note"]!.GetValue<string>(), Does.Contain("restarts"));
        Assert.That(map.MonsterSpawns, Is.Empty);
        await this.PreviewAndApplyAsync(client, "spawns_create", arguments).ConfigureAwait(false);
        var area = map.MonsterSpawns.Single();
        Assert.That(area.MonsterDefinition, Is.SameAs(monster));
        Assert.That((area.X1, area.Y1, area.X2, area.Y2, area.Quantity), Is.EqualTo(((byte)68, (byte)170, (byte)74, (byte)176, (short)1)));
        Assert.That(area.MaximumHealthOverride, Is.EqualTo(int.MaxValue));
        Assert.That(area.SpawnTrigger, Is.EqualTo(SpawnTrigger.Automatic));

        var inverted = await client.CallToolAsync("spawns_create", new Dictionary<string, object?>(arguments) { ["x1"] = 80 }).ConfigureAwait(false);
        Assert.That(inverted.IsError, Is.True);
        Assert.That(Text(inverted), Does.Contain("at most x2"));

        await this.PreviewAndApplyAsync(client, "spawns_delete", new Dictionary<string, object?> { ["spawnId"] = area.GetId(), ["reason"] = "Not needed" }).ConfigureAwait(false);
        Assert.That(map.MonsterSpawns, Is.Empty);
        this._context.Verify(c => c.DeleteAsync(area), Times.Once);
    }

    /// <summary>Viewers can read the rates, but neither see nor call configuration writes.</summary>
    [Test]
    public async Task ViewersCanOnlyReadRatesAsync()
    {
        await using var viewer = await this._host.ConnectAsync().ConfigureAwait(false);
        var names = (await viewer.ListToolsAsync().ConfigureAwait(false)).Select(t => t.Name).ToList();
        Assert.That(names, Does.Contain("rates_get"));
        Assert.That(names, Has.None.AnyOf("rates_set", "config_update", "plugins_set_active", "config_update_batch", "plugins_update_configuration", "plugins_get_configuration", "spawns_create", "spawns_delete"));
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
