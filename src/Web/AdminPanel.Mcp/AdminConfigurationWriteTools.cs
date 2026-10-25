// <copyright file="AdminConfigurationWriteTools.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Mcp;

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Server;
using MUnique.OpenMU.Web.AdminPanel.Operations;

/// <summary>Configuration changes with preview and apply, registered only when writes are explicitly enabled.</summary>
[McpServerToolType]
public sealed class AdminConfigurationWriteTools
{
    private const string ApplyDescription = "false (default) only previews the change and returns a confirmationToken; true applies the previewed change.";
    private const string TokenDescription = "The confirmationToken of the preview; required when apply is true.";
    private const string ReasonDescription = "Mandatory audit reason, 1-1000 characters.";

    private AdminConfigurationWriteTools()
    {
    }

    /// <summary>Change fields of one configuration object with a JSON merge patch.</summary>
    /// <param name="operations">The shared admin operations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="type">The configuration type.</param>
    /// <param name="id">The object identifier.</param>
    /// <param name="patch">The patch.</param>
    /// <param name="reason">The audit reason.</param>
    /// <param name="apply">Whether to apply a previewed change.</param>
    /// <param name="confirmationToken">The token of the preview.</param>
    /// <returns>The preview or the applied changes.</returns>
    [McpServerTool(Name = "config_update", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Change scalar fields and single references of one configuration object with a JSON merge patch, e.g. {\"ExpMultiplier\": 2} or {\"DropItemGroup\": {\"$ref\": \"<id>\"}}. Field names come from config_describe. Two steps: preview with apply=false, show the changes to the user, then apply=true with the confirmationToken. Collections can't be changed with this tool.")]
    [Authorize(Roles = "Administrator")]
    [ExposesOperation(typeof(ConfigurationWriteOperations), nameof(ConfigurationWriteOperations.UpdateAsync))]
    public static ValueTask<JsonObject> ConfigUpdateAsync(
        [FromServices] ConfigurationWriteOperations operations,
        CancellationToken cancellationToken,
        [Description("Invariant type name from config_types.")] string type,
        [Description("Object identifier from config_query or config_get.")] Guid id,
        [Description("JSON object with the new field values; null clears a nullable field.")] JsonElement patch,
        [Description(ReasonDescription)] string reason,
        [Description(ApplyDescription)] bool apply = false,
        [Description(TokenDescription)] string? confirmationToken = null)
        => AdminToolExecution.ExecuteAsync(() =>
        {
            var patchObject = patch.ValueKind == JsonValueKind.Object
                ? JsonObject.Create(patch)!
                : throw new ArgumentException("The patch must be a JSON object.", nameof(patch));
            return operations.UpdateAsync(type, id, patchObject, reason, apply, confirmationToken, cancellationToken);
        });

    /// <summary>Change the global experience rates.</summary>
    /// <param name="operations">The shared admin operations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="reason">The audit reason.</param>
    /// <param name="experienceRate">The new experience rate.</param>
    /// <param name="masterExperienceRate">The new master experience rate.</param>
    /// <param name="apply">Whether to apply a previewed change.</param>
    /// <param name="confirmationToken">The token of the preview.</param>
    /// <returns>The preview or the applied changes.</returns>
    [McpServerTool(Name = "rates_set", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Change the global experience and/or master experience rate (e.g. 2 for double experience). Running game servers use the new rate immediately. Two steps: preview with apply=false, then apply=true with the confirmationToken. OpenMU doesn't switch rates back by itself.")]
    [Authorize(Roles = "Administrator")]
    [ExposesOperation(typeof(ConfigurationWriteOperations), nameof(ConfigurationWriteOperations.SetRatesAsync))]
    public static ValueTask<JsonObject> RatesSetAsync(
        [FromServices] ConfigurationWriteOperations operations,
        CancellationToken cancellationToken,
        [Description(ReasonDescription)] string reason,
        [Description("New experience rate, greater than 0; omit to keep it.")] float? experienceRate = null,
        [Description("New master experience rate, greater than 0; omit to keep it.")] float? masterExperienceRate = null,
        [Description(ApplyDescription)] bool apply = false,
        [Description(TokenDescription)] string? confirmationToken = null)
        => AdminToolExecution.ExecuteAsync(() => operations.SetRatesAsync(experienceRate, masterExperienceRate, reason, apply, confirmationToken, cancellationToken));

    /// <summary>Activate or deactivate a plugin.</summary>
    /// <param name="operations">The shared admin operations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="plugInId">The plugin identifier.</param>
    /// <param name="active">Whether the plugin should be active.</param>
    /// <param name="reason">The audit reason.</param>
    /// <param name="apply">Whether to apply a previewed change.</param>
    /// <param name="confirmationToken">The token of the preview.</param>
    /// <returns>The preview or the applied changes.</returns>
    [McpServerTool(Name = "plugins_set_active", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Activate or deactivate a plugin by its id from plugins_list. Running game servers apply it immediately. Two steps: preview with apply=false, then apply=true with the confirmationToken.")]
    [Authorize(Roles = "Administrator")]
    [ExposesOperation(typeof(ConfigurationWriteOperations), nameof(ConfigurationWriteOperations.SetPlugInActiveAsync))]
    public static ValueTask<JsonObject> PluginsSetActiveAsync(
        [FromServices] ConfigurationWriteOperations operations,
        CancellationToken cancellationToken,
        [Description("Plugin id from plugins_list.")] Guid plugInId,
        [Description("true to activate, false to deactivate.")] bool active,
        [Description(ReasonDescription)] string reason,
        [Description(ApplyDescription)] bool apply = false,
        [Description(TokenDescription)] string? confirmationToken = null)
        => AdminToolExecution.ExecuteAsync(() => operations.SetPlugInActiveAsync(plugInId, active, reason, apply, confirmationToken, cancellationToken));

    /// <summary>Change fields of several configuration objects at once.</summary>
    /// <param name="operations">The shared admin operations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="changes">The changes.</param>
    /// <param name="reason">The audit reason.</param>
    /// <param name="apply">Whether to apply a previewed change.</param>
    /// <param name="confirmationToken">The token of the preview.</param>
    /// <returns>The preview or the applied changes.</returns>
    [McpServerTool(Name = "config_update_batch", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Change scalar fields and single references of up to 50 configuration objects at once, e.g. the same field of every character class. Each change is {\"type\": \"...\", \"id\": \"...\", \"patch\": {...}} like config_update takes it; at most 500 fields in total. One preview with apply=false lists all changes, then apply=true with the confirmationToken applies all of them together.")]
    [Authorize(Roles = "Administrator")]
    [ExposesOperation(typeof(ConfigurationWriteOperations), nameof(ConfigurationWriteOperations.UpdateBatchAsync))]
    public static ValueTask<JsonObject> ConfigUpdateBatchAsync(
        [FromServices] ConfigurationWriteOperations operations,
        CancellationToken cancellationToken,
        [Description("JSON array of changes, each an object with type, id and patch.")] JsonElement changes,
        [Description(ReasonDescription)] string reason,
        [Description(ApplyDescription)] bool apply = false,
        [Description(TokenDescription)] string? confirmationToken = null)
        => AdminToolExecution.ExecuteAsync(() =>
        {
            var changesArray = changes.ValueKind == JsonValueKind.Array
                ? JsonArray.Create(changes)!
                : throw new ArgumentException("The changes must be a JSON array.", nameof(changes));
            return operations.UpdateBatchAsync(changesArray, reason, apply, confirmationToken, cancellationToken);
        });

    /// <summary>Change fields of the custom configuration of a plugin.</summary>
    /// <param name="operations">The shared admin operations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="plugInId">The plugin identifier.</param>
    /// <param name="patch">The patch.</param>
    /// <param name="reason">The audit reason.</param>
    /// <param name="apply">Whether to apply a previewed change.</param>
    /// <param name="confirmationToken">The token of the preview.</param>
    /// <returns>The preview or the applied changes.</returns>
    [McpServerTool(Name = "plugins_update_configuration", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Change scalar fields of a plugin's custom configuration with a JSON merge patch, e.g. {\"RequiredLevel\": 400}. Field names and which ones are editable come from plugins_get_configuration; fields which can hold secrets can't be changed. Running game servers apply it immediately. Two steps: preview with apply=false, show the changes to the user, then apply=true with the confirmationToken.")]
    [Authorize(Roles = "Administrator")]
    [ExposesOperation(typeof(ConfigurationWriteOperations), nameof(ConfigurationWriteOperations.UpdatePlugInConfigurationAsync))]
    public static ValueTask<JsonObject> PluginsUpdateConfigurationAsync(
        [FromServices] ConfigurationWriteOperations operations,
        CancellationToken cancellationToken,
        [Description("Plugin id from plugins_list.")] Guid plugInId,
        [Description("JSON object with the new field values; null clears a nullable field.")] JsonElement patch,
        [Description(ReasonDescription)] string reason,
        [Description(ApplyDescription)] bool apply = false,
        [Description(TokenDescription)] string? confirmationToken = null)
        => AdminToolExecution.ExecuteAsync(() =>
        {
            var patchObject = patch.ValueKind == JsonValueKind.Object
                ? JsonObject.Create(patch)!
                : throw new ArgumentException("The patch must be a JSON object.", nameof(patch));
            return operations.UpdatePlugInConfigurationAsync(plugInId, patchObject, reason, apply, confirmationToken, cancellationToken);
        });

    /// <summary>Add a monster spawn area to a map.</summary>
    /// <param name="operations">The shared admin operations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="monsterId">The monster identifier.</param>
    /// <param name="x1">The lowest x coordinate.</param>
    /// <param name="y1">The lowest y coordinate.</param>
    /// <param name="x2">The highest x coordinate.</param>
    /// <param name="y2">The highest y coordinate.</param>
    /// <param name="quantity">The number of monsters.</param>
    /// <param name="reason">The audit reason.</param>
    /// <param name="maximumHealthOverride">The maximum health of the monsters.</param>
    /// <param name="direction">The direction.</param>
    /// <param name="spawnTrigger">The spawn trigger.</param>
    /// <param name="apply">Whether to apply a previewed change.</param>
    /// <param name="confirmationToken">The token of the preview.</param>
    /// <returns>The preview or the applied changes.</returns>
    [McpServerTool(Name = "spawns_create", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Add a monster spawn area to a map: monsters of one MonsterDefinition spawn at random spots inside the rectangle x1..x2, y1..y2. The new area appears after the game server restarts. Two steps: preview with apply=false, show the change to the user, then apply=true with the confirmationToken.")]
    [Authorize(Roles = "Administrator")]
    [ExposesOperation(typeof(ConfigurationWriteOperations), nameof(ConfigurationWriteOperations.CreateSpawnAsync))]
    public static ValueTask<JsonObject> SpawnsCreateAsync(
        [FromServices] ConfigurationWriteOperations operations,
        CancellationToken cancellationToken,
        [Description("Map id: a GameMapDefinition from config_query.")] Guid mapId,
        [Description("Monster id: a MonsterDefinition from config_query.")] Guid monsterId,
        [Description("Lowest x coordinate, 0-255.")] int x1,
        [Description("Lowest y coordinate, 0-255.")] int y1,
        [Description("Highest x coordinate, 0-255; equal to x1 for a single column.")] int x2,
        [Description("Highest y coordinate, 0-255; equal to y1 for a single row.")] int y2,
        [Description("Number of monsters, 1-1000.")] int quantity,
        [Description(ReasonDescription)] string reason,
        [Description("Maximum health of the spawned monsters; omit for the monster's own health.")] int? maximumHealthOverride = null,
        [Description("Direction the monsters face: Undefined (default), West, SouthWest, South, SouthEast, East, NorthEast, North or NorthWest.")] string? direction = null,
        [Description("Automatic (default) or another SpawnTrigger from config_describe of MonsterSpawnArea.")] string? spawnTrigger = null,
        [Description(ApplyDescription)] bool apply = false,
        [Description(TokenDescription)] string? confirmationToken = null)
        => AdminToolExecution.ExecuteAsync(() => operations.CreateSpawnAsync(mapId, monsterId, x1, y1, x2, y2, quantity, maximumHealthOverride, direction, spawnTrigger, reason, apply, confirmationToken, cancellationToken));

    /// <summary>Remove a monster spawn area from its map.</summary>
    /// <param name="operations">The shared admin operations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="spawnId">The spawn area identifier.</param>
    /// <param name="reason">The audit reason.</param>
    /// <param name="apply">Whether to apply a previewed change.</param>
    /// <param name="confirmationToken">The token of the preview.</param>
    /// <returns>The preview or the applied changes.</returns>
    [McpServerTool(Name = "spawns_delete", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Remove a monster spawn area from its map by its MonsterSpawnArea id from config_query. Running game servers remove its monsters immediately. Two steps: preview with apply=false, show the change to the user, then apply=true with the confirmationToken.")]
    [Authorize(Roles = "Administrator")]
    [ExposesOperation(typeof(ConfigurationWriteOperations), nameof(ConfigurationWriteOperations.DeleteSpawnAsync))]
    public static ValueTask<JsonObject> SpawnsDeleteAsync(
        [FromServices] ConfigurationWriteOperations operations,
        CancellationToken cancellationToken,
        [Description("Spawn area id: a MonsterSpawnArea from config_query.")] Guid spawnId,
        [Description(ReasonDescription)] string reason,
        [Description(ApplyDescription)] bool apply = false,
        [Description(TokenDescription)] string? confirmationToken = null)
        => AdminToolExecution.ExecuteAsync(() => operations.DeleteSpawnAsync(spawnId, reason, apply, confirmationToken, cancellationToken));
}
