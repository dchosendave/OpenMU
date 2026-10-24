// <copyright file="AutoFields.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Shared.Components.Form;

using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.Web.Shared.ComponentBuilders;
using MUnique.OpenMU.Web.Shared.Services;

/// <summary>
/// A razor component which automatically generates input fields for all properties for the type of the enclosing form model.
/// Must be used inside a <see cref="EditForm"/>.
/// </summary>
public class AutoFields : ComponentBase
{
    private static readonly IList<IComponentBuilder> Builders = new List<IComponentBuilder>();
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<PropertyMetadata>> CachedProperties = new();

    /// <summary>
    /// Initializes static members of the <see cref="AutoFields"/> class.
    /// </summary>
    static AutoFields()
    {
        Builders.Add(new PasswordHashFieldBuilder());
        Builders.Add(new TimeZoneIdFieldBuilder());
        Builders.Add(new TextFieldBuilder());
        Builders.Add(new LocalizedStringFieldBuilder());
        Builders.Add(new NumberFieldBuilder<long>());
        Builders.Add(new NumberFieldBuilder<int>());
        Builders.Add(new NumberFieldBuilder<decimal>());
        Builders.Add(new NumberFieldBuilder<double>());
        Builders.Add(new NumberFieldBuilder<float>());
        Builders.Add(new ByteFieldBuilder());
        Builders.Add(new ShortFieldBuilder());
        Builders.Add(new BooleanFieldBuilder());
        Builders.Add(new DateTimeFieldBuilder());
        Builders.Add(new DateOnlyFieldBuilder());
        Builders.Add(new TimeOnlyFieldBuilder());
        Builders.Add(new TimeSpanFieldBuilder());
        Builders.Add(new EnumFieldBuilder());
        Builders.Add(new FlagsEnumFieldBuilder());
        Builders.Add(new ExitGateFieldBuilder());
        Builders.Add(new ItemStorageFieldBuilder());
        Builders.Add(new LookupFieldBuilder());
        Builders.Add(new EmbeddedFormFieldBuilder());
        Builders.Add(new SkillListFieldBuilder());
        Builders.Add(new ObjectCollectionFieldBuilder());
        Builders.Add(new IntCollectionFieldBuilder());
        Builders.Add(new GameServerSelectionFieldBuilder());
        Builders.Add(new ByteArrayFieldBuilder());
        Builders.Add(new ValueListFieldBuilder());
        Builders.Add(new InvasionSpawnTableFieldBuilder());
    }

    private enum FieldLayout
    {
        /// <summary>
        /// A single cell of the field grid.
        /// </summary>
        Compact,

        /// <summary>
        /// A full row of the field grid.
        /// </summary>
        Wide,

        /// <summary>
        /// A full-width section below the field grid, e.g. a nested object or a collection.
        /// </summary>
        Section,
    }

    /// <summary>
    /// Gets or sets the context of the <see cref="EditForm"/>.
    /// </summary>
    /// <value>
    /// The context.
    /// </value>
    [CascadingParameter]
    public EditContext Context { get; set; } = null!;

    /// <summary>
    /// Gets or sets a value indicating whether to hide collections or not.
    /// </summary>
    [Parameter]
    public bool HideCollections { get; set; }

    /// <summary>
    /// Gets or sets the search term to filter properties by their name or display name.
    /// </summary>
    [Parameter]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Gets the properties which should be shown in this component.
    /// </summary>
    /// <returns>The properties which should be shown in this component.</returns>
    protected virtual IEnumerable<PropertyInfo> Properties
    {
        get
        {
            if (this.Context?.Model is null)
            {
                this.Logger.LogError(this.Context is null ? "Context is null" : "Model is null");
                return Enumerable.Empty<PropertyInfo>();
            }

            try
            {
                var modelType = this.Context.Model.GetType();
                var properties = CachedProperties.GetOrAdd(modelType, CreatePropertyMetadata);

                return properties
                    .Where(p => !this.HideCollections || !p.IsGenericType)
                    .Where(this.IsMatch)
                    .OrderBy(p => p.DisplayAttribute?.GetOrder())
                    .ThenByDescending(p => p.IsString)
                    .ThenByDescending(p => p.IsValueType)
                    .ThenByDescending(p => !p.IsGenericType)
                    .Select(p => p.Property)
                    .ToList();
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Error during determining properties of type {ModelType}: {Message}", this.Context.Model.GetType(), ex.Message);
            }

            return Enumerable.Empty<PropertyInfo>();
        }
    }

    /// <summary>
    /// Gets or sets the notification service.
    /// </summary>
    /// <value>
    /// The notification service.
    /// </value>
    [Inject]
    private IChangeNotificationService NotificationService { get; set; } = null!;

    [Inject]
    private ILogger<AutoFields> Logger { get; set; } = null!;

    /// <inheritdoc />
    /// <remarks>
    /// Simple fields are laid out in a responsive grid, grouped by <see cref="DisplayAttribute.GroupName"/>.
    /// Nested objects and collections follow as full-width sections, so they don't interrupt the grid.
    /// </remarks>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var entries = this.Properties
            .Select(p => (Property: p, Builder: Builders.FirstOrDefault(b => b.CanBuildComponent(p))))
            .Where(e => e.Builder is not null)
            .Select(e => (e.Property, Builder: e.Builder!, Layout: this.GetLayout(e.Property, e.Builder!)))
            .ToList();

        var fieldGroups = entries
            .Where(e => e.Layout != FieldLayout.Section)
            .GroupBy(e => e.Property.GetCustomAttribute<DisplayAttribute>()?.GetGroupName());
        foreach (var group in fieldGroups)
        {
            if (group.Key is { } groupName)
            {
                builder.OpenElement(0, "fieldset");
                builder.AddAttribute(1, "class", "auto-field-group");
                builder.OpenElement(2, "legend");
                builder.AddContent(3, groupName);
                builder.CloseElement();
            }

            builder.OpenElement(4, "div");
            builder.AddAttribute(5, "class", "auto-fields-grid");
            foreach (var entry in group.Where(e => e.Builder is not BooleanFieldBuilder))
            {
                builder.OpenElement(6, "div");
                builder.AddAttribute(7, "class", entry.Layout == FieldLayout.Wide ? "auto-field auto-field-wide" : "auto-field");
                this.BuildField(builder, entry.Property, entry.Builder);
                builder.CloseElement();
            }

            // Switches are collected in one row at the end, so they don't break up the rows of inputs.
            var switches = group.Where(e => e.Builder is BooleanFieldBuilder).ToList();
            if (switches.Count > 0)
            {
                builder.OpenElement(8, "div");
                builder.AddAttribute(9, "class", "auto-field auto-field-wide auto-field-switches");
                foreach (var entry in switches)
                {
                    this.BuildField(builder, entry.Property, entry.Builder);
                }

                builder.CloseElement();
            }

            builder.CloseElement();

            if (group.Key is not null)
            {
                builder.CloseElement();
            }
        }

        foreach (var entry in entries.Where(e => e.Layout == FieldLayout.Section))
        {
            builder.OpenElement(10, "div");
            builder.AddAttribute(11, "class", "auto-field-section");
            this.BuildField(builder, entry.Property, entry.Builder);
            builder.CloseElement();
        }
    }

    private static IReadOnlyList<PropertyMetadata> CreatePropertyMetadata(Type type)
    {
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)
            .Where(p => p.GetCustomAttribute<TransientAttribute>() is null)
            .Where(p => p.GetCustomAttribute<BrowsableAttribute>()?.Browsable ?? true)
            .Where(p => !p.Name.StartsWith("Raw", StringComparison.Ordinal))
            .Where(p => !p.Name.StartsWith("Joined", StringComparison.Ordinal))
            .Where(p => !p.GetIndexParameters().Any())
            .Select(p => new PropertyMetadata(
                p,
                p.GetCustomAttribute<DisplayAttribute>(),
                p.PropertyType.IsGenericType,
                p.PropertyType == typeof(string),
                p.PropertyType.IsValueType))
            .ToList();
    }

    // The component builders number their frames themselves, so each field gets its own region.
    private void BuildField(RenderTreeBuilder builder, PropertyInfo propertyInfo, IComponentBuilder componentBuilder)
    {
        builder.OpenRegion(0);
        try
        {
            componentBuilder.BuildComponent(this.Context.Model, propertyInfo, builder, 0, this.NotificationService);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, $"Error building component for property {this.Context.Model.GetType().Name}.{propertyInfo.Name} with component builder {componentBuilder}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
        }

        builder.CloseRegion();
    }

    private FieldLayout GetLayout(PropertyInfo propertyInfo, IComponentBuilder componentBuilder)
    {
        switch (componentBuilder)
        {
            case EmbeddedFormFieldBuilder:
            case ObjectCollectionFieldBuilder:
            case SkillListFieldBuilder:
            case ItemStorageFieldBuilder:
            case InvasionSpawnTableFieldBuilder:
            case ValueListFieldBuilder:
                return FieldLayout.Section;
            case FlagsEnumFieldBuilder:
            case ExitGateFieldBuilder:
            case LocalizedStringFieldBuilder:
            case IntCollectionFieldBuilder:
            case ByteArrayFieldBuilder:
                return FieldLayout.Wide;
            case TextFieldBuilder:
                // Formulas, expressions and descriptions need the room; names and short values don't.
                var value = propertyInfo.GetValue(this.Context.Model) as string;
                return value?.Length > 40
                       || propertyInfo.Name.EndsWith("Formula", StringComparison.Ordinal)
                       || propertyInfo.Name.EndsWith("Description", StringComparison.Ordinal)
                    ? FieldLayout.Wide
                    : FieldLayout.Compact;
            default:
                return FieldLayout.Compact;
        }
    }

    private bool IsMatch(PropertyMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(this.SearchTerm))
        {
            return true;
        }

        return metadata.Property.Name.Contains(this.SearchTerm, StringComparison.OrdinalIgnoreCase)
               || (metadata.DisplayAttribute?.GetName()?.Contains(this.SearchTerm, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private sealed record PropertyMetadata(
        PropertyInfo Property,
        DisplayAttribute? DisplayAttribute,
        bool IsGenericType,
        bool IsString,
        bool IsValueType);
}
