// <copyright file="ItemEditorModel.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.Web.Shared.Components.ItemEdit;

/// <summary>
/// Edits one item through the <see cref="ItemViewModel"/> of the admin panel's item editor, so only
/// options which the item can have are offered: excellent, wing, ancient, harmony, guardian and sockets.
/// </summary>
public sealed class ItemEditorModel
{
    private readonly Action _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemEditorModel"/> class.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="context">The context of the item.</param>
    /// <param name="changed">The action which is called after every change.</param>
    public ItemEditorModel(Item item, IContext context, Action changed)
    {
        this.Item = item;
        this._changed = changed;
        this.Model = new ItemViewModel(item, context);
        this.RebuildChoices();
    }

    /// <summary>
    /// Gets the item.
    /// </summary>
    public Item Item { get; }

    /// <summary>
    /// Gets the view model of the item editor.
    /// </summary>
    public ItemViewModel Model { get; }

    /// <summary>
    /// Gets the excellent options which the item can have.
    /// </summary>
    public IReadOnlyList<OptionChoice> ExcellentChoices { get; private set; } = Array.Empty<OptionChoice>();

    /// <summary>
    /// Gets the wing options which the item can have.
    /// </summary>
    public IReadOnlyList<OptionChoice> WingChoices { get; private set; } = Array.Empty<OptionChoice>();

    /// <summary>
    /// Gets the sockets of the item.
    /// </summary>
    public IReadOnlyList<SocketChoice> Sockets { get; private set; } = Array.Empty<SocketChoice>();

    /// <summary>
    /// Gets the ancient sets which the item can be part of.
    /// </summary>
    public IReadOnlyList<ItemSetGroup> AncientSetChoices => this.Model.PossibleAncientSets.ToList();

    /// <summary>
    /// Gets the normal options which the item can have.
    /// </summary>
    public IReadOnlyList<IncreasableItemOption> NormalOptionChoices => this.Model.PossibleNormalOptions.ToList();

    /// <summary>
    /// Gets the harmony options which the item can have.
    /// </summary>
    public IReadOnlyList<IncreasableItemOption> HarmonyOptionChoices => this.Model.PossibleHarmonyOptions.ToList();

    /// <summary>
    /// Gets or sets the definition of the item.
    /// </summary>
    public ItemDefinition? Definition
    {
        get => this.Model.Definition;
        set
        {
            if (value is null || value == this.Model.Definition)
            {
                return;
            }

            this.Model.Definition = value;
            if (this.Model.Durability <= 0)
            {
                this.Model.Durability = value.Durability;
            }

            this.RebuildChoices();
            this._changed();
        }
    }

    /// <summary>
    /// Gets the highest level which the item can have (+15 for equipment, lower for some items).
    /// </summary>
    public byte MaximumLevel => Math.Min((byte)15, this.Model.Definition?.MaximumItemLevel ?? 15);

    /// <summary>
    /// Gets or sets the item level.
    /// </summary>
    public byte Level
    {
        get => this.Model.Level;
        set => this.Change(() => this.Model.Level = Math.Min(value, this.MaximumLevel));
    }

    /// <summary>
    /// Gets or sets the durability.
    /// </summary>
    public double Durability
    {
        get => this.Model.Durability;
        set => this.Change(() => this.Model.Durability = Math.Clamp(value, 0, 255));
    }

    /// <summary>
    /// Gets or sets the stack size of stackable items (jewels, potions, ...), which is stored as durability.
    /// </summary>
    public byte Amount
    {
        get => this.Model.Amount;
        set => this.Change(() => this.Model.Amount = Math.Max((byte)1, value));
    }

    /// <summary>
    /// Gets a value indicating whether the item is stackable.
    /// </summary>
    public bool IsStackable => this.Item.Definition is not null && this.Item.IsStackable();

    /// <summary>
    /// Gets the highest durability (or stack size) which the item can have at its level.
    /// </summary>
    public double MaximumDurability => this.Item.Definition is null ? 0
        : this.Item.IsStackable() ? this.Item.Definition.Durability : this.Item.GetMaximumDurabilityOfOnePiece();

    /// <summary>
    /// Gets or sets a value indicating whether the item has its skill.
    /// </summary>
    public bool HasSkill
    {
        get => this.Model.HasSkill;
        set => this.Change(() => this.Model.HasSkill = value);
    }

    /// <summary>
    /// Gets a value indicating whether the item can have a skill.
    /// </summary>
    public bool CanHaveSkill => this.Model.Definition?.Skill is not null;

    /// <summary>
    /// Gets or sets a value indicating whether the item has luck.
    /// </summary>
    public bool HasLuck
    {
        get => this.Model.HasLuck;
        set => this.Change(() => this.Model.HasLuck = value);
    }

    /// <summary>
    /// Gets a value indicating whether the item can have luck.
    /// </summary>
    public bool CanHaveLuck => this.Model.PossibleLuckOption is not null;

    /// <summary>
    /// Gets or sets the normal option.
    /// </summary>
    public IncreasableItemOption? NormalOption
    {
        get => this.Model.NormalOption;
        set => this.Change(() =>
        {
            this.Model.NormalOption = value;
            if (this.Model.NormalOptionLink is { Level: 0 } link)
            {
                link.Level = 1;
            }
        });
    }

    /// <summary>
    /// Gets or sets the level of the normal option (+4 per level for damage and defense, up to 4 = +16, 7 for some items).
    /// </summary>
    public int NormalOptionLevel
    {
        get => this.Model.NormalOptionLink?.Level ?? 0;
        set
        {
            if (this.Model.NormalOptionLink is { } link)
            {
                this.Change(() => link.Level = Math.Clamp(value, 1, this.NormalOptionMaximumLevel));
            }
        }
    }

    /// <summary>
    /// Gets the highest level of the normal option.
    /// </summary>
    public int NormalOptionMaximumLevel => Math.Max(1, this.Model.NormalOption?.LevelDependentOptions.Select(o => o.Level).DefaultIfEmpty(4).Max() ?? 4);

    /// <summary>
    /// Gets or sets the harmony option. A newly chosen one starts at its <see cref="HarmonyOptionDefaultLevel"/>.
    /// </summary>
    public IncreasableItemOption? HarmonyOption
    {
        get => this.Model.HarmonyOption;
        set => this.Change(() =>
        {
            this.Model.HarmonyOption = value;
            if (this.Model.HarmonyOptionLink is { } link)
            {
                link.Level = this.HarmonyOptionDefaultLevel;
            }
        });
    }

    /// <summary>
    /// Gets or sets the level of the harmony option.
    /// </summary>
    public int HarmonyOptionLevel
    {
        get => this.Model.HarmonyOptionLink?.Level ?? 0;
        set
        {
            if (this.Model.HarmonyOptionLink is { } link)
            {
                this.Change(() => link.Level = Math.Clamp(value, this.HarmonyOptionDefaultLevel, this.HarmonyOptionMaximumLevel));
            }
        }
    }

    /// <summary>
    /// Gets the lowest level of the harmony option, which refining with a Jewel of Harmony gives.
    /// </summary>
    public int HarmonyOptionDefaultLevel => this.HarmonyLevels.Select(l => l.Level).DefaultIfEmpty(0).Min();

    /// <summary>
    /// Gets the highest level of the harmony option which the item allows. Each level of a harmony
    /// option needs a minimum item level, so a +15 item can have the highest one.
    /// </summary>
    public int HarmonyOptionMaximumLevel => this.HarmonyLevels
        .Where(l => l.RequiredItemLevel <= this.Model.Level)
        .Select(l => l.Level)
        .DefaultIfEmpty(this.HarmonyOptionDefaultLevel)
        .Max();

    /// <summary>
    /// Gets or sets the ancient set.
    /// </summary>
    public ItemSetGroup? AncientSet
    {
        get => this.Model.AncientSet;
        set => this.Change(() => this.Model.AncientSet = value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the item has the 380 (guardian) option.
    /// </summary>
    public bool HasGuardianOption
    {
        get => this.Model.HasGuardianOption;
        set => this.Change(() => this.Model.HasGuardianOption = value);
    }

    /// <summary>
    /// Gets a value indicating whether the item can have the 380 (guardian) option.
    /// </summary>
    public bool CanHaveGuardianOption => this.Model.PossibleGuardianOption is not null;

    /// <summary>
    /// Gets or sets the number of sockets.
    /// </summary>
    public int SocketCount
    {
        get => this.Model.SocketCount;
        set => this.Change(() =>
        {
            this.Model.SocketCount = Math.Clamp(value, 0, this.MaximumSockets);
            this.RebuildSockets();
        });
    }

    /// <summary>
    /// Gets the maximum number of sockets.
    /// </summary>
    public int MaximumSockets => this.Model.Definition?.MaximumSockets ?? 0;

    /// <summary>
    /// Gets the slot of the item.
    /// </summary>
    public byte ItemSlot => this.Model.ItemSlot;

    private IEnumerable<ItemOptionOfLevel> HarmonyLevels => this.Model.HarmonyOption?.LevelDependentOptions ?? Enumerable.Empty<ItemOptionOfLevel>();

    /// <summary>
    /// Makes a "full option" item: every excellent (or wing) option, luck, skill, the highest normal
    /// option, the 380 option when the item can have it, and the highest durability at its level.
    /// </summary>
    public void MakeFullOption()
    {
        foreach (var choice in this.ExcellentChoices.Concat(this.WingChoices))
        {
            choice.IsChecked = true;
        }

        if (this.CanHaveSkill)
        {
            this.HasSkill = true;
        }

        if (this.CanHaveLuck)
        {
            this.HasLuck = true;
        }

        if (this.CanHaveGuardianOption)
        {
            this.HasGuardianOption = true;
        }

        if (this.NormalOption is null && this.NormalOptionChoices.FirstOrDefault() is { } option)
        {
            this.NormalOption = option;
        }

        this.NormalOptionLevel = this.NormalOptionMaximumLevel;
        if (!this.IsStackable)
        {
            this.Durability = this.MaximumDurability;
        }
    }

    /// <summary>
    /// Gives the item the options of the preset (and its level, when it has one). Options which the item
    /// can't have are left out. Ancient set, harmony option and sockets stay as they are.
    /// </summary>
    /// <param name="preset">The preset.</param>
    public void ApplyPreset(ItemOptionPreset preset)
    {
        if (preset.Level is { } level)
        {
            this.Level = level;
        }

        this.HasSkill = preset.Skill && this.CanHaveSkill;
        this.HasLuck = preset.Luck && this.CanHaveLuck;
        if (this.CanHaveGuardianOption)
        {
            this.HasGuardianOption = preset.Guardian;
        }

        if (preset.OptionLevel <= 0)
        {
            if (this.NormalOption is not null)
            {
                this.NormalOption = null;
            }
        }
        else if ((this.NormalOption ?? this.NormalOptionChoices.FirstOrDefault()) is { } option)
        {
            this.NormalOption = option;
            this.NormalOptionLevel = preset.OptionLevel;
        }

        foreach (var choice in this.ExcellentChoices.Concat(this.WingChoices))
        {
            choice.IsChecked = preset.Excellent;
        }

        if (!this.IsStackable)
        {
            this.Durability = this.MaximumDurability;
        }
    }

    private void Change(Action change)
    {
        change();
        this._changed();
    }

    private void RebuildChoices()
    {
        this.ExcellentChoices = this.Model.PossibleExcellentOptions.SelectMany(d => d.PossibleOptions).Distinct()
            .Select(o => new OptionChoice(o, this.Model.ExcellentOptions, this._changed)).ToList();
        this.WingChoices = this.Model.PossibleWingOptions.SelectMany(d => d.PossibleOptions).Distinct()
            .Select(o => new OptionChoice(o, this.Model.WingOptions, this._changed)).ToList();
        this.RebuildSockets();
    }

    private void RebuildSockets()
    {
        this.Sockets = this.Model.Sockets.Take(this.Model.SocketCount)
            .Select(s => new SocketChoice(s, this.Model.PossibleSocketOptions.ToList(), this._changed)).ToList();
    }
}
