// <copyright file="ComplexPotionConsumeHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;

using MUnique.OpenMU.PlugIns;

/// <summary>
/// Abstract consume handler for complex potions which combines a <see cref="HealthPotionConsumeHandlerPlugIn"/> and a <see cref="ShieldPotionConsumeHandlerPlugIn"/>.
/// Its configuration has a part for each of them; by default the ones of the health and shield potion of the same size.
/// </summary>
public abstract class ComplexPotionConsumeHandlerPlugIn : BaseConsumeHandlerPlugIn, ISupportCustomConfiguration<ComplexPotionConsumeHandlerConfiguration>, ISupportDefaultCustomConfiguration
{
    private readonly HealthPotionConsumeHandlerPlugIn _healthPotionConsumeHandlerPlugIn;
    private readonly ShieldPotionConsumeHandlerPlugIn _shieldPotionConsumeHandlerPlugIn;
    private ComplexPotionConsumeHandlerConfiguration? _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplexPotionConsumeHandlerPlugIn"/> class.
    /// </summary>
    /// <param name="healthPotionConsumeHandlerPlugIn">The health potion consume handler.</param>
    /// <param name="shieldPotionConsumeHandlerPlugIn">The shield potion consume handler.</param>
    protected ComplexPotionConsumeHandlerPlugIn(HealthPotionConsumeHandlerPlugIn healthPotionConsumeHandlerPlugIn, ShieldPotionConsumeHandlerPlugIn shieldPotionConsumeHandlerPlugIn)
    {
        this._healthPotionConsumeHandlerPlugIn = healthPotionConsumeHandlerPlugIn ?? throw new ArgumentNullException(nameof(healthPotionConsumeHandlerPlugIn));
        this._shieldPotionConsumeHandlerPlugIn = shieldPotionConsumeHandlerPlugIn ?? throw new ArgumentNullException(nameof(shieldPotionConsumeHandlerPlugIn));
    }

    /// <summary>
    /// Gets or sets the configuration of the health and the shield recovery.
    /// </summary>
    public ComplexPotionConsumeHandlerConfiguration? Configuration
    {
        get => this._configuration;
        set
        {
            this._configuration = value;
            this._healthPotionConsumeHandlerPlugIn.Configuration = value?.Health;
            this._shieldPotionConsumeHandlerPlugIn.Configuration = value?.Shield;
        }
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new ComplexPotionConsumeHandlerConfiguration
    {
        Health = (RecoverConsumeHandlerConfiguration)this._healthPotionConsumeHandlerPlugIn.CreateDefaultConfig(),
        Shield = (RecoverConsumeHandlerConfiguration)this._shieldPotionConsumeHandlerPlugIn.CreateDefaultConfig(),
    };

    /// <inheritdoc />
    public override async ValueTask<bool> ConsumeItemAsync(Player player, Item item, Item? targetItem, FruitUsage fruitUsage)
    {
        if (await base.ConsumeItemAsync(player, item, targetItem, fruitUsage).ConfigureAwait(false))
        {
            await this._healthPotionConsumeHandlerPlugIn.RecoverAsync(player, item).ConfigureAwait(false);
            await this._shieldPotionConsumeHandlerPlugIn.RecoverAsync(player, item).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    protected override bool CheckPreconditions(Player player, Item item)
    {
        return base.CheckPreconditions(player, item)
               && player.PotionCooldownUntil <= DateTime.UtcNow;
    }
}