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
}
