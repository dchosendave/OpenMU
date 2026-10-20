// <copyright file="CharacterModel.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Attributes;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.Properties;
using MUnique.OpenMU.Web.ItemEditor;

/// <summary>
/// The editable parts of a character: identity, progress, position, stats and inventory.
/// </summary>
public sealed class CharacterModel
{
    /// <summary>
    /// The base stats in the order the game shows them. Command only exists for Dark Lords.
    /// </summary>
    private static readonly Guid[] BaseStatOrder =
    {
        Stats.BaseStrength.Id,
        Stats.BaseAgility.Id,
        Stats.BaseVitality.Id,
        Stats.BaseEnergy.Id,
        Stats.BaseLeadership.Id,
    };

    private readonly Action _changed;
    private readonly int _maximumLevel;

    /// <summary>
    /// Initializes a new instance of the <see cref="CharacterModel"/> class.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <param name="context">The context of the editing session.</param>
    /// <param name="configuration">The game configuration.</param>
    /// <param name="changed">The action which is called after every change.</param>
    public CharacterModel(Character character, IContext context, GameConfiguration configuration, Action changed)
    {
        this.Character = character;
        this._changed = changed;
        this._maximumLevel = Math.Max((short)1, configuration.MaximumLevel);
        character.Inventory ??= context.CreateNew<ItemStorage>();
        this.Inventory = new StorageModel(
            character.Inventory,
            StorageType.Inventory,
            (byte)Math.Clamp(character.InventoryExtensions, 0, InventoryConstants.MaximumNumberOfExtensions),
            string.Format(Resources.AccountManagerInventoryOf, character.Name));
    }

    /// <summary>
    /// Gets the character.
    /// </summary>
    public Character Character { get; }

    /// <summary>
    /// Gets the inventory.
    /// </summary>
    public StorageModel Inventory { get; }

    /// <summary>
    /// Gets the stat attributes: strength, agility, vitality, energy (and command) first, like in the
    /// character window of the game, then the others by name.
    /// </summary>
    public IReadOnlyList<StatAttribute> Attributes => this.Character.Attributes
        .OrderBy(a => Array.IndexOf(BaseStatOrder, a.Definition?.Id ?? Guid.Empty) is var i and >= 0 ? i : BaseStatOrder.Length)
        .ThenBy(a => a.Definition?.GetName())
        .ToList();

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name
    {
        get => this.Character.Name;
        set => this.Update(() => this.Character.Name = value.Trim());
    }

    /// <summary>
    /// Gets or sets the character class.
    /// </summary>
    public CharacterClass? CharacterClass
    {
        get => this.Character.CharacterClass;
        set => this.Update(() => this.Character.CharacterClass = value);
    }

    /// <summary>
    /// Gets the highest level of the server.
    /// </summary>
    public int MaximumLevel => this._maximumLevel;

    /// <summary>
    /// Gets or sets the level, which is the "Level" stat attribute.
    /// </summary>
    public int Level
    {
        get => (int)(this.Attribute(Stats.Level)?.Value ?? 0);
        set
        {
            if (this.Attribute(Stats.Level) is { } level)
            {
                this.Update(() => level.Value = Math.Clamp(value, 1, this._maximumLevel));
            }
        }
    }

    /// <summary>
    /// Gets or sets the experience.
    /// </summary>
    public long Experience
    {
        get => this.Character.Experience;
        set => this.Update(() => this.Character.Experience = Math.Max(0, value));
    }

    /// <summary>
    /// Gets or sets the master experience.
    /// </summary>
    public long MasterExperience
    {
        get => this.Character.MasterExperience;
        set => this.Update(() => this.Character.MasterExperience = Math.Max(0, value));
    }

    /// <summary>
    /// Gets or sets the level up points.
    /// </summary>
    public int LevelUpPoints
    {
        get => this.Character.LevelUpPoints;
        set => this.Update(() => this.Character.LevelUpPoints = Math.Max(0, value));
    }

    /// <summary>
    /// Gets or sets the master level up points.
    /// </summary>
    public int MasterLevelUpPoints
    {
        get => this.Character.MasterLevelUpPoints;
        set => this.Update(() => this.Character.MasterLevelUpPoints = Math.Max(0, value));
    }

    /// <summary>
    /// Gets or sets the map on which the character is.
    /// </summary>
    public GameMapDefinition? CurrentMap
    {
        get => this.Character.CurrentMap;
        set => this.Update(() => this.Character.CurrentMap = value);
    }

    /// <summary>
    /// Gets or sets the x coordinate of the position.
    /// </summary>
    public byte PositionX
    {
        get => this.Character.PositionX;
        set => this.Update(() => this.Character.PositionX = value);
    }

    /// <summary>
    /// Gets or sets the y coordinate of the position.
    /// </summary>
    public byte PositionY
    {
        get => this.Character.PositionY;
        set => this.Update(() => this.Character.PositionY = value);
    }

    /// <summary>
    /// Gets or sets the zen of the inventory.
    /// </summary>
    public int Zen
    {
        get => this.Inventory.Money;
        set => this.Update(() => this.Inventory.Money = value);
    }

    /// <summary>
    /// Gets or sets the number of inventory extensions. The bigger grid shows after reloading the account.
    /// </summary>
    public int InventoryExtensions
    {
        get => this.Character.InventoryExtensions;
        set => this.Update(() => this.Character.InventoryExtensions = Math.Clamp(value, 0, InventoryConstants.MaximumNumberOfExtensions));
    }

    /// <summary>
    /// Gets or sets the status, e.g. game master.
    /// </summary>
    public CharacterStatus Status
    {
        get => this.Character.CharacterStatus;
        set => this.Update(() => this.Character.CharacterStatus = value);
    }

    /// <summary>
    /// Gets the base stats (strength, agility, vitality, energy, command) for which the server configures
    /// a maximum value, e.g. on a reset server.
    /// </summary>
    public IReadOnlyList<StatAttribute> CappedBaseStats => this.Attributes
        .Where(a => Array.IndexOf(BaseStatOrder, a.Definition?.Id ?? Guid.Empty) >= 0 && a.Definition?.MaximumValue is > 0)
        .ToList();

    /// <summary>
    /// Gets a value indicating whether a capped base stat is below its maximum.
    /// </summary>
    public bool CanMaximizeBaseStats => this.CappedBaseStats.Any(a => a.Value < a.Definition!.MaximumValue);

    /// <summary>
    /// Sets the value of a stat attribute.
    /// </summary>
    /// <param name="attribute">The attribute.</param>
    /// <param name="value">The value.</param>
    public void SetAttribute(StatAttribute attribute, float value) => this.Update(() => attribute.Value = Math.Max(0, value));

    /// <summary>
    /// Sets every capped base stat to the maximum value of the server. Other stats stay as they are.
    /// </summary>
    public void MaximizeBaseStats() => this.Update(() =>
    {
        foreach (var stat in this.CappedBaseStats)
        {
            stat.Value = stat.Definition!.MaximumValue!.Value;
        }
    });

    /// <summary>
    /// Moves the character to the spawn area of a map, like a warp. The position is the middle of a spawn gate.
    /// </summary>
    /// <param name="map">The map.</param>
    public void WarpTo(GameMapDefinition map)
    {
        this.Update(() =>
        {
            this.Character.CurrentMap = map;
            if (map.ExitGates.FirstOrDefault(g => g.IsSpawnGate) is { } gate)
            {
                this.Character.PositionX = (byte)((gate.X1 + gate.X2) / 2);
                this.Character.PositionY = (byte)((gate.Y1 + gate.Y2) / 2);
            }
        });
    }

    private StatAttribute? Attribute(AttributeDefinition definition) => this.Character.Attributes.FirstOrDefault(a => a.Definition?.Id == definition.Id);

    private void Update(Action change)
    {
        change();
        this._changed();
    }
}
