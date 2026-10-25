// <copyright file="ConfigNavMenu.razor.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Components.Layout;

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Web.AdminPanel.Properties;
using MUnique.OpenMU.Web.Shared.Services;

/// <summary>
/// Navigation menu of the game configuration, with its entries divided into collapsible groups.
/// Only one group is open at a time; the group of the current page opens automatically.
/// </summary>
public partial class ConfigNavMenu : IDisposable
{
    private bool _expandGameConfig;

    private ConfigGroup? _openGroup;

    private IReadOnlyList<ConfigGroup>? _groups;

    private Guid _groupsConfigurationId;

    /// <summary>
    /// Gets or sets the game configuration identifier.
    /// </summary>
    [Parameter]
    [Required]
    public Guid GameConfigurationId { get; set; }

    [Inject]
    private NavigationHistory NavigationHistory { get; set; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    /// <summary>
    /// Gets the groups. They're only created again when the configuration changes, because the
    /// open group is identified by its instance.
    /// </summary>
    private IReadOnlyList<ConfigGroup> Groups
    {
        get
        {
            if (this._groups is null || this._groupsConfigurationId != this.GameConfigurationId)
            {
                this._groups = this.CreateGroups();
                this._groupsConfigurationId = this.GameConfigurationId;
                this._openGroup = null;
            }

            return this._groups;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.NavigationManager.LocationChanged -= this.OnLocationChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        this.NavigationManager.LocationChanged += this.OnLocationChanged;
        this.OpenGroupOfCurrentPage();
    }

    private static string GridOf<T>() => $"edit-config-grid/{typeof(T).FullName}/";

    private static ConfigLink GridLink<T>(Func<string> caption) => new(GridOf<T>(), caption);

    private static ConfigLink GridLink<T>() => new(GridOf<T>(), () => ModelResourceProvider.GetPluralizedTypeCaption<T>());

    private IReadOnlyList<ConfigGroup> CreateGroups() =>
    [
        new(
            () => Resources.ConfigGroupGeneral,
            [
                new($"edit-config/{typeof(GameConfiguration).FullName}/{this.GameConfigurationId}/hide-collections", () => Resources.General),
                new($"edit-config/{typeof(SystemConfiguration).FullName}/", () => Resources.System),
                GridLink<GameClientDefinition>(() => Resources.GameClients),
                new($"edit-config/{typeof(GameConfiguration).FullName}/", () => Resources.FullConfiguration, Match: NavLinkMatch.All),
            ]),
        new(
            () => Resources.ConfigGroupCharacters,
            [
                GridLink<CharacterClass>(() => Resources.CharacterClasses),
                GridLink<Skill>(() => Resources.Skills),
            ]),
        new(
            () => Resources.Items,
            [
                GridLink<ItemDefinition>(() => Resources.Items),
                GridLink<ItemOptionDefinition>(),
                GridLink<ItemOptionType>(),
                GridLink<ItemSetGroup>(),
                GridLink<JewelMix>(() => Resources.JewelMixes),
            ]),
        new(
            () => Resources.ConfigGroupDropsAndShops,
            [
                GridLink<DropItemGroup>(() => Resources.DropItemGroups),
                new("edit-item-drops", () => Resources.ItemDrops),
                new("merchants/", () => Resources.MerchantStores),
                new("cash-shop-catalog", () => Resources.CashShopCatalog, AdminOnly: true),
            ]),
        new(
            () => Resources.ConfigGroupWorld,
            [
                GridLink<GameMapDefinition>(() => Resources.GameMaps),
                GridLink<WarpInfo>(() => Resources.WarpList),
                GridLink<MonsterDefinition>(() => Resources.Monsters),
                GridLink<MiniGameDefinition>(() => Resources.MiniGames),
                new("map-editor", () => Resources.MapEditor),
                new("terrain-editor", () => Resources.TerrainEditor),
            ]),
        new(
            () => Resources.ConfigGroupExtensions,
            [
                new("content-activation", () => Resources.ContentActivation),
                new("plugins", () => Resources.Plugins),
                new("chat-commands", () => Resources.ChatCommands),
            ]),
    ];

    private void ToggleGameConfig()
    {
        this._expandGameConfig = !this._expandGameConfig;
    }

    private void ToggleGroup(ConfigGroup group)
    {
        this._openGroup = this._openGroup == group ? null : group;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (this.OpenGroupOfCurrentPage())
        {
            _ = this.InvokeAsync(this.StateHasChanged);
        }
    }

    /// <summary>
    /// Opens the group which contains the current page, and the game configuration menu itself.
    /// A page which isn't in the menu (e.g. the details of a grid entry) keeps the open group.
    /// </summary>
    /// <returns><c>true</c>, if a group was found.</returns>
    private bool OpenGroupOfCurrentPage()
    {
        var path = this.NavigationManager.ToBaseRelativePath(this.NavigationManager.Uri).Split('?', '#')[0];
        var match = this.Groups
            .SelectMany(group => group.Links.Select(link => (Group: group, Link: link)))
            .Where(entry => entry.Link.Match == NavLinkMatch.All
                ? string.Equals(path, entry.Link.Href, StringComparison.OrdinalIgnoreCase)
                : path.StartsWith(entry.Link.Href, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.Link.Href.Length)
            .Select(entry => entry.Group)
            .FirstOrDefault();
        if (match is null)
        {
            return false;
        }

        this._openGroup = match;
        this._expandGameConfig = true;
        return true;
    }

    private sealed record ConfigGroup(Func<string> Caption, IReadOnlyList<ConfigLink> Links);

    private sealed record ConfigLink(string Href, Func<string> Caption, bool AdminOnly = false, NavLinkMatch Match = NavLinkMatch.Prefix);
}
