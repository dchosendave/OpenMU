// <copyright file="ConfigurationPatch.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Operations;

using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.PlugIns;

/// <summary>Plans restricted JSON merge patches (RFC 7396) for one configuration object.</summary>
/// <remarks>
/// Only scalar fields and single references to other configuration objects can be patched. Collections are
/// changed by dedicated operations, and plugin configurations are excluded because they can contain secrets.
/// </remarks>
internal static class ConfigurationPatch
{
    /// <summary>The maximum number of fields in one patch.</summary>
    internal const int MaximumFields = 50;

    /// <summary>Returns the patch with its fields sorted, so equal patches have equal hashes.</summary>
    /// <param name="patch">The patch.</param>
    /// <returns>The canonical patch.</returns>
    internal static JsonObject Canonicalize(JsonObject patch)
    {
        if (patch.Count is 0 or > MaximumFields)
        {
            throw new ArgumentException($"A patch changes 1-{MaximumFields} fields.", nameof(patch));
        }

        return new JsonObject(patch.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone())));
    }

    /// <summary>Validates a patch and plans its changes. Fields which already have the value are skipped.</summary>
    /// <param name="target">The configuration object.</param>
    /// <param name="type">The configuration type of the object.</param>
    /// <param name="patch">The patch.</param>
    /// <param name="configuration">The game configuration, to resolve references.</param>
    /// <returns>The planned changes.</returns>
    internal static IReadOnlyList<PlannedConfigurationChange> Plan(object target, Type type, JsonObject patch, GameConfiguration configuration)
    {
        var changes = new List<PlannedConfigurationChange>();
        var targetCaption = $"{type.Name} '{ConfigurationModel.Caption(target)}'";
        foreach (var (field, node) in patch)
        {
            var property = GetPatchableProperty(type, field);
            var oldValue = property.GetValue(target);
            var newValue = ConfigurationModel.IsLocalizedString(property.PropertyType) ? ConvertLocalized(property, node, oldValue as LocalizedString?) : Convert(property, node, configuration);
            Validate(target, property, newValue);
            if (Equals(oldValue, newValue))
            {
                continue;
            }

            var change = new ConfigurationFieldChange(targetCaption, property.Name, ConfigurationModel.FieldCaption(type, property), ConfigurationModel.ToJson(oldValue), ConfigurationModel.ToJson(newValue));
            changes.Add(new PlannedConfigurationChange(change, () => property.SetValue(target, newValue), () => property.SetValue(target, oldValue)));
        }

        return changes;
    }

    /// <summary>Converts a JSON value to the value of a scalar field or a single reference.</summary>
    /// <param name="property">The field.</param>
    /// <param name="node">The JSON value.</param>
    /// <param name="configuration">The game configuration, to resolve references.</param>
    /// <returns>The converted value.</returns>
    internal static object? Convert(PropertyInfo property, JsonNode? node, GameConfiguration configuration)
    {
        var propertyType = property.PropertyType;
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (node is null)
        {
            if (propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) is null)
            {
                throw new ArgumentException($"'{property.Name}' can't be null.", property.Name);
            }

            return null;
        }

        if (!ConfigurationModel.IsScalar(type))
        {
            return ResolveReference(property, node, type, configuration);
        }

        if (node is not JsonValue value)
        {
            throw new ArgumentException($"'{property.Name}' expects a single value of type {type.Name}.", property.Name);
        }

        try
        {
            if (type == typeof(string))
            {
                return value.GetValue<string>();
            }

            if (type == typeof(bool))
            {
                return value.GetValue<bool>();
            }

            if (type.IsEnum)
            {
                var name = value.GetValue<string>();
                var parsed = Enum.Parse(type, name, ignoreCase: false);
                if (!type.IsDefined(typeof(FlagsAttribute)) && !Enum.IsDefined(type, parsed))
                {
                    throw new ArgumentException("Undefined enum value.");
                }

                return parsed;
            }

            if (type == typeof(Guid))
            {
                return Guid.Parse(value.GetValue<string>());
            }

            if (type == typeof(TimeSpan))
            {
                return TimeSpan.Parse(value.GetValue<string>(), CultureInfo.InvariantCulture);
            }

            if (type == typeof(DateTime))
            {
                return DateTime.Parse(value.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }

            // The JSON text is parsed, so a number is accepted independently of how the node was created; quoted numbers aren't.
            var number = decimal.Parse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);
            if (type != typeof(float) && type != typeof(double) && type != typeof(decimal) && number != decimal.Truncate(number))
            {
                throw new ArgumentException("A whole number is required.");
            }

            return System.Convert.ChangeType(number, type, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException or OverflowException or ArgumentException or InvalidCastException or JsonException)
        {
            var expected = type.IsEnum ? "one of " + string.Join(", ", Enum.GetNames(type)) : type.Name;
            throw new ArgumentException($"'{property.Name}' expects {expected} in invariant format.", property.Name);
        }
    }

    private static PropertyInfo GetPatchableProperty(Type type, string field)
    {
        var property = type.GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
        if (property is null || !property.CanRead || property.SetMethod?.IsPublic != true || property.GetIndexParameters().Length > 0)
        {
            throw new ArgumentException($"'{field}' is not a writable field of {type.Name}. Use config_describe to list the fields.", nameof(field));
        }

        var propertyType = property.PropertyType;
        if (property.Name == "Id"
            || propertyType == typeof(byte[])
            || typeof(PlugInConfiguration).IsAssignableFrom(propertyType)
            || (propertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(propertyType))
            || !(ConfigurationModel.IsScalar(propertyType) || ConfigurationModel.IsLocalizedString(propertyType) || ConfigurationModel.IsConfigurationType(propertyType)))
        {
            throw new ArgumentException($"'{field}' can't be patched: only scalar fields and single references can be changed with config_update.", nameof(field));
        }

        return property;
    }

    private static LocalizedString ConvertLocalized(PropertyInfo property, JsonNode? node, LocalizedString? oldValue)
    {
        // Translations which aren't part of the patch are kept.
        var result = oldValue ?? new LocalizedString(null);
        var texts = node switch
        {
            JsonValue value when value.TryGetValue<string>(out var text) => [KeyValuePair.Create(LocalizedString.NeutralLanguageCode, (JsonNode?)value)],
            JsonObject languages => languages.ToList(),
            _ => throw new ArgumentException($"'{property.Name}' expects a text, or an object with texts per language like {{\"en\": \"...\", \"de\": \"...\"}}.", property.Name),
        };

        foreach (var (language, text) in texts)
        {
            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo(language, predefinedOnly: true);
            }
            catch (CultureNotFoundException)
            {
                throw new ArgumentException($"'{language}' is not a known language code.", property.Name);
            }

            if (text is not null && (text is not JsonValue textValue || !textValue.TryGetValue<string>(out _)))
            {
                throw new ArgumentException($"The text of '{language}' must be a string, or null to remove the translation.", property.Name);
            }

            result = result.WithTranslation(culture, text?.GetValue<string>());
        }

        return result;
    }

    private static object ResolveReference(PropertyInfo property, JsonNode node, Type type, GameConfiguration configuration)
    {
        var text = node is JsonObject reference && reference.Count == 1 && reference["$ref"] is JsonValue id ? id.GetValue<string>()
            : node is JsonValue plain && plain.TryGetValue<string>(out var value) ? value
            : null;
        if (!Guid.TryParse(text, out var guid))
        {
            throw new ArgumentException($"'{property.Name}' expects a reference like {{\"$ref\": \"<id>\"}} to a {type.Name}.", property.Name);
        }

        return ConfigurationModel.Find(configuration, type, guid)
            ?? throw new ArgumentException($"No {type.Name} with id {guid} exists. Use config_query to find it.", property.Name);
    }

    private static void Validate(object target, PropertyInfo property, object? value)
    {
        var attributes = property.GetCustomAttributes<ValidationAttribute>().ToArray();
        var results = new List<ValidationResult>();
        if (attributes.Length > 0 && !Validator.TryValidateValue(value!, new ValidationContext(target) { MemberName = property.Name }, results, attributes))
        {
            throw new ArgumentException($"'{property.Name}': {string.Join(" ", results.Select(r => r.ErrorMessage))}", property.Name);
        }
    }
}
