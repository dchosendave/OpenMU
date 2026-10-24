// <copyright file="ConfigurationWriteOperations.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Operations;

using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.AdminAuth;
using MUnique.OpenMU.PlugIns;

/// <summary>Audited configuration changes with the two-step preview and apply protocol.</summary>
/// <remarks>
/// A call with <c>apply=false</c> validates the change and returns a preview with a confirmation token; nothing
/// is changed. A call with <c>apply=true</c> and the token applies exactly the previewed change. Changes are
/// saved through the panel's shared configuration context, so running game servers receive them through the
/// usual configuration change propagation.
/// </remarks>
/// <param name="configurations">The shared game configuration source of the panel.</param>
/// <param name="plugins">The plugin manager.</param>
/// <param name="confirmations">The confirmation tokens.</param>
/// <param name="executor">The authorization and audit executor.</param>
/// <param name="settings">The write safety policy.</param>
/// <param name="logger">The logger.</param>
public sealed class ConfigurationWriteOperations(IDataSource<GameConfiguration> configurations, PlugInManager plugins, ConfigurationConfirmations confirmations, AdminOperationExecutor executor, AdminWriteSettings settings, ILogger<ConfigurationWriteOperations> logger)
{
    /// <summary>The highest accepted experience rate. It only prevents obvious mistakes.</summary>
    internal const float MaximumRate = 10_000;

    // The shared configuration context isn't thread safe; agent changes are applied one at a time.
    private static readonly SemaphoreSlim ApplyLock = new(1, 1);

    /// <summary>Changes scalar fields and single references of one configuration object.</summary>
    /// <param name="type">The configuration type from config_types.</param>
    /// <param name="id">The object identifier.</param>
    /// <param name="patch">The JSON merge patch with field names from config_describe.</param>
    /// <param name="reason">The audit reason.</param>
    /// <param name="apply">Whether to apply a previewed change.</param>
    /// <param name="confirmationToken">The token of the preview, when applying.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The preview or the applied changes.</returns>
    [AdminOperation(AdminRole.Administrator, ReadOnly = false)]
    public ValueTask<JsonObject> UpdateAsync(string type, Guid id, JsonObject patch, string reason, bool apply, string? confirmationToken, CancellationToken cancellationToken)
    {
        var modelType = ConfigurationModel.ResolveType(type);
        var canonical = ConfigurationPatch.Canonicalize(patch);
        return this.RunAsync(
            "config_update",
            new { type = modelType.Name, id, patch = canonical },
            reason,
            apply,
            confirmationToken,
            (configuration, _) =>
            {
                var target = ConfigurationModel.Find(configuration, modelType, id)
                    ?? throw new ArgumentException("Configuration object not found. Use config_query to find its identifier.", nameof(id));
                return ConfigurationPatch.Plan(target, modelType, canonical, configuration);
            },
            cancellationToken);
    }

    /// <summary>Reads the experience rates of the game configuration and the experience multipliers of the maps.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rates.</returns>
    [AdminOperation(AdminRole.Viewer)]
    public async ValueTask<JsonObject> GetRatesAsync(CancellationToken cancellationToken)
    {
        await executor.RequireAsync(AdminRoles.Viewer, cancellationToken).ConfigureAwait(false);
        var configuration = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var maps = configuration.Maps.ToList();
        return AdminReadResult.From(new
        {
            summary = "Change the global rates with rates_set, and a map's ExpMultiplier with config_update on its GameMapDefinition.",
            configuration.ExperienceRate,
            configuration.MasterExperienceRate,
            maps = maps.OrderBy(m => m.Number).Take(AdminReadResult.MaximumRows).Select(m => new { id = m.GetId(), m.Number, name = m.Name.ToString(), m.ExpMultiplier }),
            mapsTruncated = maps.Count > AdminReadResult.MaximumRows,
        });
    }

    /// <summary>Changes the global experience rates.</summary>
    /// <param name="experienceRate">The new experience rate, or <c>null</c> to keep it.</param>
    /// <param name="masterExperienceRate">The new master experience rate, or <c>null</c> to keep it.</param>
    /// <param name="reason">The audit reason.</param>
    /// <param name="apply">Whether to apply a previewed change.</param>
    /// <param name="confirmationToken">The token of the preview, when applying.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The preview or the applied changes.</returns>
    [AdminOperation(AdminRole.Administrator, ReadOnly = false)]
    public ValueTask<JsonObject> SetRatesAsync(float? experienceRate, float? masterExperienceRate, string reason, bool apply, string? confirmationToken, CancellationToken cancellationToken)
    {
        var patch = new JsonObject();
        foreach (var (field, rate) in new[] { (nameof(GameConfiguration.ExperienceRate), experienceRate), (nameof(GameConfiguration.MasterExperienceRate), masterExperienceRate) })
        {
            if (rate is { } value)
            {
                if (!float.IsFinite(value) || value <= 0 || value > MaximumRate)
                {
                    throw new ArgumentException($"{field} must be greater than 0 and at most {MaximumRate}.", field);
                }

                patch[field] = value;
            }
        }

        if (patch.Count == 0)
        {
            throw new ArgumentException("Specify experienceRate, masterExperienceRate or both.", nameof(experienceRate));
        }

        return this.RunAsync(
            "rates_set",
            patch,
            reason,
            apply,
            confirmationToken,
            (configuration, _) => ConfigurationPatch.Plan(configuration, typeof(GameConfiguration), patch, configuration),
            cancellationToken);
    }

    /// <summary>Activates or deactivates a plugin. Running servers apply it immediately.</summary>
    /// <param name="plugInId">The plugin identifier from plugins_list.</param>
    /// <param name="active">Whether the plugin should be active.</param>
    /// <param name="reason">The audit reason.</param>
    /// <param name="apply">Whether to apply a previewed change.</param>
    /// <param name="confirmationToken">The token of the preview, when applying.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The preview or the applied changes.</returns>
    [AdminOperation(AdminRole.Administrator, ReadOnly = false)]
    public ValueTask<JsonObject> SetPlugInActiveAsync(Guid plugInId, bool active, string reason, bool apply, string? confirmationToken, CancellationToken cancellationToken)
    {
        var plugInType = plugins.KnownPlugInTypes.FirstOrDefault(t => t.GUID == plugInId)
            ?? throw new ArgumentException("Unknown plugin. Use plugins_list to find its id.", nameof(plugInId));
        var name = plugInType.GetCustomAttributes(typeof(DisplayAttribute), inherit: true).OfType<DisplayAttribute>().FirstOrDefault()?.GetName() ?? plugInType.Name;
        return this.RunAsync(
            "plugins_set_active",
            new { plugInId, active },
            reason,
            apply,
            confirmationToken,
            (configuration, context) =>
            {
                var existing = configuration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == plugInId);
                var current = existing?.IsActive ?? plugins.IsPlugInActive(plugInType);
                if (current == active)
                {
                    return [];
                }

                var change = new ConfigurationFieldChange($"Plugin '{name}'", nameof(PlugInConfiguration.IsActive), "Active", current, active);
                if (existing is not null)
                {
                    return [new PlannedConfigurationChange(change, () => existing.IsActive = active, () => existing.IsActive = current)];
                }

                // Without a stored configuration the plugin runs with its default state; storing one makes the choice explicit.
                PlugInConfiguration? created = null;
                return
                [
                    new PlannedConfigurationChange(
                        change,
                        () =>
                        {
                            created = context.CreateNew<PlugInConfiguration>();
                            created.TypeId = plugInId;
                            created.IsActive = active;
                            configuration.PlugInConfigurations.Add(created);
                        },
                        () =>
                        {
                            if (created is not null)
                            {
                                configuration.PlugInConfigurations.Remove(created);
                                context.Detach(created);
                            }
                        }),
                ];
            },
            cancellationToken);
    }

    private static string Hash(string operation, object arguments)
    {
        var json = operation + "\n" + JsonSerializer.Serialize(arguments);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private async ValueTask<JsonObject> RunAsync(string operation, object arguments, string reason, bool apply, string? confirmationToken, Func<GameConfiguration, IContext, IReadOnlyList<PlannedConfigurationChange>> plan, CancellationToken cancellationToken)
    {
        var hash = Hash(operation, arguments);
        if (!apply)
        {
            return await this.PreviewAsync(operation, hash, reason, plan, cancellationToken).ConfigureAwait(false);
        }

        return await executor.ExecuteAsync(
            AdminRoles.Administrator,
            operation,
            new { arguments, reason },
            async actor =>
            {
                AccountWriteOperations.ValidateReason(reason);
                var previewed = confirmations.Redeem(actor, operation, hash, confirmationToken);
                await ApplyLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var configuration = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    var context = await configurations.GetContextAsync(cancellationToken).ConfigureAwait(false);
                    if (context.HasChanges)
                    {
                        throw new AdminOperationException("The admin panel has unsaved configuration edits. Save or discard them in the panel first, then preview the change again.");
                    }

                    var planned = plan(configuration, context);
                    if (!planned.Select(p => p.Change.Fingerprint).SequenceEqual(previewed.Select(p => p.Fingerprint)))
                    {
                        throw new AdminOperationException("The configuration changed since the preview. Preview the change again.");
                    }

                    await this.SaveAsync(context, planned).ConfigureAwait(false);
                    return AdminReadResult.From(new
                    {
                        applied = true,
                        summary = "Applied and saved. Running game servers receive the change through the usual configuration update; a few settings only take effect after a restart.",
                        changes = planned.Select(p => p.Change),
                    });
                }
                finally
                {
                    ApplyLock.Release();
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<JsonObject> PreviewAsync(string operation, string hash, string reason, Func<GameConfiguration, IContext, IReadOnlyList<PlannedConfigurationChange>> plan, CancellationToken cancellationToken)
    {
        var actor = await executor.RequireAsync(AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        if (actor.Frontend == "mcp" && settings.ReadOnly)
        {
            throw new AdminOperationException("MCP writes are disabled. Enable AdminPanel:Mcp:ReadOnly=false explicitly.");
        }

        AccountWriteOperations.ValidateReason(reason);
        var configuration = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var context = await configurations.GetContextAsync(cancellationToken).ConfigureAwait(false);
        var changes = plan(configuration, context).Select(p => p.Change).ToList();
        if (changes.Count == 0)
        {
            return AdminReadResult.From(new { applied = false, summary = "Nothing to change: the configuration already has these values.", changes });
        }

        var (token, expiresAt) = confirmations.Issue(actor, operation, hash, changes);
        return AdminReadResult.From(new
        {
            applied = false,
            summary = "Preview only, nothing was changed. Show the changes to the user. After the user confirmed, call the tool again with the same arguments, apply=true and the confirmationToken.",
            changes,
            restartRequired = (bool?)null,
            confirmationToken = token,
            expiresAt,
        });
    }

    private async ValueTask SaveAsync(IContext context, IReadOnlyList<PlannedConfigurationChange> planned)
    {
        var applied = new List<PlannedConfigurationChange>();
        try
        {
            foreach (var change in planned)
            {
                change.Apply();
                applied.Add(change);
            }

            // The change must not stay half applied in the shared configuration when the caller cancels.
            await context.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not AdminOperationException)
        {
            logger.LogError(ex, "Saving a configuration change of an agent failed; the change is reverted.");
            foreach (var change in Enumerable.Reverse(applied))
            {
                change.Revert();
            }

            throw new AdminOperationException("The change couldn't be saved and was reverted. Check the server log for details.", AdminOperationErrorKind.Unavailable);
        }
    }
}
