// <copyright file="ComboChainConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Skills;

using System.ComponentModel;

/// <summary>
/// Configuration of the <see cref="ComboChainPlugIn"/>: how many follow-up combos a completed
/// skill combo triggers, how likely they are and how much damage each one deals.
/// </summary>
public class ComboChainConfiguration
{
    /// <summary>
    /// Gets or sets the chance of the second combo (0 to 1).
    /// </summary>
    [DefaultValue(1.0)]
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.ComboChainConfiguration_SecondComboChance_Name), Description = nameof(PlugInResources.ComboChainConfiguration_SecondComboChance_Description))]
    public double SecondComboChance { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the damage factor of the second combo, relative to the first one.
    /// </summary>
    [DefaultValue(0.6)]
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.ComboChainConfiguration_SecondComboDamageFactor_Name))]
    public double SecondComboDamageFactor { get; set; } = 0.6;

    /// <summary>
    /// Gets or sets the base chance of the third combo (0 to 1), before the agility bonus.
    /// </summary>
    [DefaultValue(0.15)]
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.ComboChainConfiguration_ThirdComboBaseChance_Name))]
    public double ThirdComboBaseChance { get; set; } = 0.15;

    /// <summary>
    /// Gets or sets the chance of the third combo added per point of total agility.
    /// </summary>
    [DefaultValue(0.00002)]
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.ComboChainConfiguration_ThirdComboChancePerAgility_Name), Description = nameof(PlugInResources.ComboChainConfiguration_ThirdComboChancePerAgility_Description))]
    public double ThirdComboChancePerAgility { get; set; } = 0.00002;

    /// <summary>
    /// Gets or sets the maximum chance of the third combo (0 to 1).
    /// </summary>
    [DefaultValue(0.65)]
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.ComboChainConfiguration_ThirdComboMaximumChance_Name))]
    public double ThirdComboMaximumChance { get; set; } = 0.65;

    /// <summary>
    /// Gets or sets the damage factor of the third combo, relative to the first one.
    /// </summary>
    [DefaultValue(0.4)]
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.ComboChainConfiguration_ThirdComboDamageFactor_Name))]
    public double ThirdComboDamageFactor { get; set; } = 0.4;

    /// <summary>
    /// Gets or sets the base chance of the fourth combo (0 to 1). It is only rolled after a third combo.
    /// </summary>
    [DefaultValue(0.02)]
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.ComboChainConfiguration_FourthComboBaseChance_Name), Description = nameof(PlugInResources.ComboChainConfiguration_FourthComboBaseChance_Description))]
    public double FourthComboBaseChance { get; set; } = 0.02;

    /// <summary>
    /// Gets or sets the factor by which the excellent damage chance adds to the fourth combo chance.
    /// </summary>
    [DefaultValue(0.5)]
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.ComboChainConfiguration_FourthComboChancePerExcellentChance_Name), Description = nameof(PlugInResources.ComboChainConfiguration_FourthComboChancePerExcellentChance_Description))]
    public double FourthComboChancePerExcellentChance { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets the maximum chance of the fourth combo (0 to 1).
    /// </summary>
    [DefaultValue(0.08)]
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.ComboChainConfiguration_FourthComboMaximumChance_Name))]
    public double FourthComboMaximumChance { get; set; } = 0.08;

    /// <summary>
    /// Gets or sets the damage factor of the fourth combo, relative to the first one.
    /// </summary>
    [DefaultValue(0.25)]
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.ComboChainConfiguration_FourthComboDamageFactor_Name))]
    public double FourthComboDamageFactor { get; set; } = 0.25;

    /// <summary>
    /// Gets or sets the delay between two combos, in milliseconds, so that their effects play one after the other.
    /// </summary>
    [DefaultValue(250)]
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.ComboChainConfiguration_DelayBetweenCombosMs_Name))]
    public int DelayBetweenCombosMs { get; set; } = 250;

    /// <summary>
    /// Rolls the follow-up combos of a completed combo.
    /// </summary>
    /// <param name="totalAgility">The total agility of the attacker.</param>
    /// <param name="excellentDamageChance">The excellent damage chance of the attacker (0 to 1).</param>
    /// <param name="roll">Returns <see langword="true"/> with the given probability.</param>
    /// <returns>The damage factors of the follow-up combos, in order; empty when none triggered.</returns>
    public IReadOnlyList<double> RollFollowUps(float totalAgility, float excellentDamageChance, Func<double, bool> roll)
    {
        var factors = new List<double>(3);
        if (!roll(Math.Clamp(this.SecondComboChance, 0, 1)))
        {
            return factors;
        }

        factors.Add(this.SecondComboDamageFactor);

        var thirdChance = Math.Min(this.ThirdComboMaximumChance, this.ThirdComboBaseChance + (totalAgility * this.ThirdComboChancePerAgility));
        if (!roll(Math.Clamp(thirdChance, 0, 1)))
        {
            return factors;
        }

        factors.Add(this.ThirdComboDamageFactor);

        var fourthChance = Math.Min(this.FourthComboMaximumChance, this.FourthComboBaseChance + (excellentDamageChance * this.FourthComboChancePerExcellentChance));
        if (roll(Math.Clamp(fourthChance, 0, 1)))
        {
            factors.Add(this.FourthComboDamageFactor);
        }

        return factors;
    }
}
