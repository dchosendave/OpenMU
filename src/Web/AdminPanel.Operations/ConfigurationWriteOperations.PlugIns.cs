// <copyright file="ConfigurationWriteOperations.PlugIns.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Operations;

using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.AdminAuth;
using MUnique.OpenMU.PlugIns;

/// <content>Reading and changing the custom configuration of plugins.</content>
public sealed partial class ConfigurationWriteOperations
{
    /// <summary>The text shown instead of the value of a field which can hold a secret.</summary>
    internal const string HiddenValue = "(hidden)";

    /// <summary>Reads the custom configuration of a plugin. Fields which can hold secrets are hidden.</summary>
    /// <param name="plugInId">The plugin identifier from plugins_list.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The fields of the configuration with their values.</returns>
    [AdminOperation(AdminRole.Administrator)]
    public async ValueTask<JsonObject> GetPlugInConfigurationAsync(Guid plugInId, CancellationToken cancellationToken)
    {
        await executor.RequireAsync(AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        var (plugInType, configType, name) = this.ResolveConfigurablePlugIn(plugInId);
        var configuration = await configurations.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var stored = configuration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == plugInId);
        var current = this.LoadPlugInConfiguration(plugInType, configType, stored);
        return AdminReadResult.From(new
        {
            summary = "Change editable fields with plugins_update_configuration. Hidden fields can hold secrets; change them in the admin panel.",
            plugInId,
            name,
            active = stored?.IsActive ?? plugins.IsPlugInActive(plugInType),
            storedConfiguration = !string.IsNullOrWhiteSpace(stored?.CustomConfiguration),
            fields = GetPlugInConfigurationProperties(configType).Select(p => new
            {
                field = p.Name,
                caption = p.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? p.Name,
                description = p.GetCustomAttribute<DisplayAttribute>()?.GetDescription(),
                type = (Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType).Name,
                editable = IsEditablePlugInField(p),
                value = DescribePlugInValue(p, current),
            }),
        });
    }

    /// <summary>Changes scalar fields of the custom configuration of a plugin. Running servers apply it immediately.</summary>
    /// <param name="plugInId">The plugin identifier from plugins_list.</param>
    /// <param name="patch">The JSON merge patch with field names from plugins_get_configuration.</param>
    /// <param name="reason">The audit reason.</param>
    /// <param name="apply">Whether to apply a previewed change.</param>
    /// <param name="confirmationToken">The token of the preview, when applying.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The preview or the applied changes.</returns>
    [AdminOperation(AdminRole.Administrator, ReadOnly = false)]
    public ValueTask<JsonObject> UpdatePlugInConfigurationAsync(Guid plugInId, JsonObject patch, string reason, bool apply, string? confirmationToken, CancellationToken cancellationToken)
    {
        var (plugInType, configType, name) = this.ResolveConfigurablePlugIn(plugInId);
        var canonical = ConfigurationPatch.Canonicalize(patch);
        return this.RunAsync(
            "plugins_update_configuration",
            new { plugInId, patch = canonical },
            reason,
            apply,
            confirmationToken,
            (configuration, context) => this.PlanPlugInConfiguration(configuration, context, plugInType, configType, name, canonical),
            cancellationToken);
    }

    private static IEnumerable<PropertyInfo> GetPlugInConfigurationProperties(Type configType)
        => configType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

    private static bool IsSecretField(PropertyInfo property)
        => SecretFieldName().IsMatch(property.Name);

    private static bool IsEditablePlugInField(PropertyInfo property)
        => property.SetMethod?.IsPublic == true && ConfigurationModel.IsScalar(property.PropertyType) && !IsSecretField(property);

    private static JsonNode? DescribePlugInValue(PropertyInfo property, object current)
    {
        var value = property.GetValue(current);
        if (IsSecretField(property))
        {
            return value is null || (value is string text && text.Length == 0) ? null : JsonValue.Create(HiddenValue);
        }

        return value switch
        {
            null => null,
            string or LocalizedString => ConfigurationModel.ToJson(value),
            ICollection collection => new JsonObject { ["count"] = collection.Count },
            IEnumerable => JsonValue.Create("(list)"),
            _ when ConfigurationModel.IsScalar(value.GetType()) || ConfigurationModel.IsConfigurationType(value.GetType()) => ConfigurationModel.ToJson(value),
            _ => JsonValue.Create("(object)"),
        };
    }

    [GeneratedRegex("token|secret|password|passwd|key|webhook|url|uri|connection|credential", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretFieldName();

    private (Type PlugInType, Type ConfigType, string Name) ResolveConfigurablePlugIn(Guid plugInId)
    {
        var plugInType = plugins.KnownPlugInTypes.FirstOrDefault(t => t.GUID == plugInId)
            ?? throw new ArgumentException("Unknown plugin. Use plugins_list to find its id.", nameof(plugInId));
        var configType = plugInType.GetCustomConfigurationSupportInterfaceType()?.GenericTypeArguments[0]
            ?? throw new ArgumentException("This plugin has no custom configuration.", nameof(plugInId));
        var name = plugInType.GetCustomAttributes(typeof(DisplayAttribute), inherit: true).OfType<DisplayAttribute>().FirstOrDefault()?.GetName() ?? plugInType.Name;
        return (plugInType, configType, name);
    }

    private object LoadPlugInConfiguration(Type plugInType, Type configType, PlugInConfiguration? stored)
    {
        if (stored?.GetConfiguration(configType, plugins.CustomConfigReferenceHandler) is { } existing)
        {
            return existing;
        }

        // Without a stored configuration the plugin runs with its default configuration.
        try
        {
            if (Activator.CreateInstance(plugInType) is ISupportDefaultCustomConfiguration withDefault && withDefault.CreateDefaultConfig() is { } defaultConfig && configType.IsInstanceOfType(defaultConfig))
            {
                return defaultConfig;
            }
        }
        catch (Exception ex) when (ex is MissingMethodException or TargetInvocationException or MemberAccessException)
        {
            logger.LogDebug(ex, "The default configuration of plugin {PlugInType} couldn't be created through the plugin.", plugInType);
        }

        return Activator.CreateInstance(configType)
            ?? throw new AdminOperationException("The configuration of this plugin can't be created.");
    }

    private IReadOnlyList<PlannedConfigurationChange> PlanPlugInConfiguration(GameConfiguration configuration, IContext context, Type plugInType, Type configType, string name, JsonObject patch)
    {
        var plugInId = plugInType.GUID;
        var stored = configuration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == plugInId);
        var working = this.LoadPlugInConfiguration(plugInType, configType, stored);
        var entity = stored;
        PlugInConfiguration? created = null;

        void Store()
        {
            if (entity is null)
            {
                created = context.CreateNew<PlugInConfiguration>();
                created.TypeId = plugInId;
                created.IsActive = plugins.IsPlugInActive(plugInType);
                configuration.PlugInConfigurations.Add(created);
                entity = created;
            }

            entity.SetConfiguration(working, plugins.CustomConfigReferenceHandler);
        }

        var changes = new List<PlannedConfigurationChange>();
        foreach (var (field, node) in patch)
        {
            var property = configType.GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
            if (property is null || !property.CanRead)
            {
                throw new ArgumentException($"'{field}' is not a field of this plugin's configuration. Use plugins_get_configuration to list the fields.", nameof(patch));
            }

            if (!IsEditablePlugInField(property))
            {
                var message = IsSecretField(property)
                    ? $"'{field}' can hold a secret; change it in the admin panel."
                    : $"'{field}' can't be changed here: only scalar fields can be changed with plugins_update_configuration.";
                throw new ArgumentException(message, nameof(patch));
            }

            var oldValue = property.GetValue(working);
            var newValue = ConfigurationPatch.Convert(property, node, configuration);
            if (Equals(oldValue, newValue))
            {
                continue;
            }

            var isFirst = changes.Count == 0;
            var change = new ConfigurationFieldChange($"Plugin '{name}'", property.Name, property.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? property.Name, ConfigurationModel.ToJson(oldValue), ConfigurationModel.ToJson(newValue));
            changes.Add(new PlannedConfigurationChange(
                change,
                () =>
                {
                    property.SetValue(working, newValue);
                    Store();
                },
                () =>
                {
                    property.SetValue(working, oldValue);
                    if (isFirst && created is not null)
                    {
                        // The first change is reverted last, so the created configuration can go.
                        configuration.PlugInConfigurations.Remove(created);
                        context.Detach(created);
                        created = null;
                        entity = null;
                    }
                    else
                    {
                        Store();
                    }
                }));
        }

        return changes;
    }
}
