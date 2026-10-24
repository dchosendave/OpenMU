// <copyright file="ConfigurationModel.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Operations;

using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;

/// <summary>Reflection helpers which describe the configuration in the same way as the read tools.</summary>
internal static class ConfigurationModel
{
    // Plugin custom configurations can contain tokens and webhook URLs, so they are excluded like in the read tools.
    private static readonly Type[] ConfigurationTypes = GameConfigurationHelper.Enumerables.Keys.Where(t => t != typeof(PlugInConfiguration)).OrderBy(t => t.Name).ToArray();

    /// <summary>Resolves a configuration type by its invariant name.</summary>
    /// <param name="type">The type name from config_types.</param>
    /// <returns>The configuration type.</returns>
    internal static Type ResolveType(string type) => ConfigurationTypes.FirstOrDefault(t => t.Name == type)
        ?? throw new ArgumentException("Unknown or excluded configuration type. Use config_types to list supported types.", nameof(type));

    /// <summary>Determines whether a type is a configuration type which is referenced by its identifier.</summary>
    /// <param name="type">The type.</param>
    /// <returns><c>true</c> for configuration types.</returns>
    internal static bool IsConfigurationType(Type type) => ConfigurationTypes.Any(t => t.IsAssignableFrom(type) || type.IsAssignableFrom(t));

    /// <summary>Determines whether a field type is a scalar value.</summary>
    /// <param name="type">The field type.</param>
    /// <returns><c>true</c> for scalar values.</returns>
    internal static bool IsScalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Guid) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(TimeSpan);
    }

    /// <summary>Determines whether a field holds a localized text.</summary>
    /// <param name="type">The field type.</param>
    /// <returns><c>true</c> for <see cref="LocalizedString"/> fields.</returns>
    internal static bool IsLocalizedString(Type type) => (Nullable.GetUnderlyingType(type) ?? type) == typeof(LocalizedString);

    /// <summary>Finds a configuration object of a type by its identifier.</summary>
    /// <param name="configuration">The game configuration.</param>
    /// <param name="type">The expected type, or a base type of it.</param>
    /// <param name="id">The identifier.</param>
    /// <returns>The object, or <c>null</c> when it doesn't exist.</returns>
    internal static object? Find(GameConfiguration configuration, Type type, Guid id)
    {
        foreach (var (key, enumerable) in GameConfigurationHelper.Enumerables)
        {
            if (key == typeof(PlugInConfiguration) || !type.IsAssignableFrom(key))
            {
                continue;
            }

            foreach (var item in enumerable(configuration))
            {
                if (item is not null && item.GetId() == id)
                {
                    return item;
                }
            }
        }

        return null;
    }

    /// <summary>Gets the caption of a configuration object, like the read tools do.</summary>
    /// <param name="item">The configuration object.</param>
    /// <returns>The caption.</returns>
    internal static string Caption(object item)
    {
        var type = item.GetType();
        var name = type.GetProperty("Name")?.GetValue(item)?.ToString() ?? type.GetProperty("Designation")?.GetValue(item)?.ToString() ?? type.GetTypeCaption();
        return name.Length > 256 ? name[..256] : name;
    }

    /// <summary>Creates the JSON representation of a field value, like the read tools do.</summary>
    /// <param name="value">The field value.</param>
    /// <returns>The JSON value; references are represented by <c>$ref</c> and <c>caption</c>.</returns>
    internal static JsonNode? ToJson(object? value) => value switch
    {
        null => null,
        string text => JsonValue.Create(text.Length > 4096 ? text[..4096] : text),
        LocalizedString localized => ToJson(localized),
        Enum => JsonValue.Create(value.ToString()),
        IEnumerable => null,
        _ when !IsScalar(value.GetType()) => new JsonObject { ["$ref"] = value.GetId().ToString(), ["caption"] = Caption(value) },
        _ => JsonSerializer.SerializeToNode(value),
    };

    /// <summary>Creates the JSON representation of a localized text: the neutral text, or all languages when it has translations.</summary>
    /// <param name="value">The localized text.</param>
    /// <returns>The JSON value.</returns>
    internal static JsonNode? ToJson(LocalizedString value)
    {
        var translations = value.GetTranslations().ToList();
        if (translations.Count == 0)
        {
            return value.Value is null ? null : JsonValue.Create(value.ValueInNeutralLanguage);
        }

        var result = new JsonObject { [LocalizedString.NeutralLanguageCode] = value.ValueInNeutralLanguage };
        foreach (var (language, text) in translations.Where(t => t.Key != LocalizedString.NeutralLanguageCode))
        {
            result[language] = text;
        }

        return result;
    }

    /// <summary>Gets the localized caption of a field.</summary>
    /// <param name="type">The declaring configuration type.</param>
    /// <param name="property">The field.</param>
    /// <returns>The caption, or the field name.</returns>
    internal static string FieldCaption(Type type, PropertyInfo property) => type.GetPropertyCaption(property.Name) is { Length: > 0 } caption ? caption : property.Name;
}
