// <copyright file="LifeJewelConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;

using System.ComponentModel.DataAnnotations;
using System.Globalization;

/// <summary>
/// The configuration of the <see cref="LifeJewelConsumeHandlerPlugIn"/>: Jewels of Life are easy to find,
/// but the higher the option, the more often adding it fails.
/// </summary>
public class LifeJewelConfiguration
{
    /// <summary>
    /// Gets or sets the success chances in percent for each option level, comma separated, from the
    /// first level (+4) up. Levels beyond the list use its last value.
    /// </summary>
    [Display(Name = "Success per option level (%)", Description = "Comma separated, from +4 up: the chance that adding or raising the option to that level succeeds.")]
    public string SuccessPercentPerLevel { get; set; } = "60, 50, 40, 30, 20, 15, 10";

    /// <summary>
    /// Gets or sets what happens to the option when it fails.
    /// </summary>
    [Display(Name = "On a fail", Description = "DecreaseOneLevel: the option drops one level. RemoveOption: the option is gone.")]
    public ItemUpgradeConsumeHandlerPlugIn.ItemFailResult FailResult { get; set; } = ItemUpgradeConsumeHandlerPlugIn.ItemFailResult.DecreaseOneLevel;

    /// <summary>
    /// Gets the chance that the option reaches the given level.
    /// </summary>
    /// <param name="level">The option level, 1 for +4.</param>
    /// <returns>The chance, from 0 to 1.</returns>
    public double ChanceOf(int level)
    {
        var percents = this.SuccessPercentPerLevel
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN)
            .Where(p => !double.IsNaN(p))
            .ToList();
        if (percents.Count == 0)
        {
            return 0.5;
        }

        var index = Math.Clamp(level - 1, 0, percents.Count - 1);
        return Math.Clamp(percents[index] / 100.0, 0, 1);
    }
}
