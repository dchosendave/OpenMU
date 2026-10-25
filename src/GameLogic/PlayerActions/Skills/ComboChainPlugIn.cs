// <copyright file="ComboChainPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Skills;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Lets a completed skill combo trigger up to three follow-up combos, each with less damage than
/// the one before. Every follow-up shows the combo effect again, so that the effects play one
/// after the other.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ComboChainPlugIn_Name), Description = nameof(PlugInResources.ComboChainPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3C0D5B7E-9A41-4E2F-8B6D-1F7A2C9E4D58")]
public class ComboChainPlugIn : IFeaturePlugIn, ISupportCustomConfiguration<ComboChainConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    /// <inheritdoc/>
    public ComboChainConfiguration? Configuration { get; set; }

    /// <summary>
    /// Triggers the follow-up combos of a completed combo, if the plug-in is active.
    /// </summary>
    /// <param name="player">The player who completed the combo.</param>
    /// <param name="skillEntry">The skill which completed the combo.</param>
    /// <param name="comboTarget">The target at which the combo effect is shown.</param>
    /// <param name="targets">The targets which got hit by the completing skill.</param>
    public static void TriggerFollowUps(Player player, SkillEntry skillEntry, IAttackable? comboTarget, IReadOnlyCollection<IAttackable> targets)
    {
        if (player.GameContext.FeaturePlugIns.GetPlugIn<ComboChainPlugIn>()?.Configuration is not { } configuration
            || player.Attributes is not { } attributes
            || targets.Count == 0)
        {
            return;
        }

        var factors = configuration.RollFollowUps(attributes[Stats.TotalAgility], attributes[Stats.ExcellentDamageChance], Rand.NextRandomBool);
        if (factors.Count == 0)
        {
            return;
        }

        var delay = TimeSpan.FromMilliseconds(Math.Max(0, configuration.DelayBetweenCombosMs));
        _ = Task.Run(async () =>
        {
            try
            {
                foreach (var factor in factors)
                {
                    await Task.Delay(delay).ConfigureAwait(false);
                    if (!await HitAgainAsync(player, skillEntry, targets, factor).ConfigureAwait(false))
                    {
                        return;
                    }

                    await player.ForEachWorldObserverAsync<IShowSkillAnimationPlugIn>(p => p.ShowComboAnimationAsync(player, comboTarget), true).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                player.Logger.LogError(ex, "Error during follow-up combo.");
            }
        });
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new ComboChainConfiguration();

    /// <summary>
    /// Hits the targets which are still valid once more.
    /// </summary>
    /// <returns><see langword="true"/>, if at least one target got hit.</returns>
    private static async ValueTask<bool> HitAgainAsync(Player player, SkillEntry skillEntry, IReadOnlyCollection<IAttackable> targets, double damageFactor)
    {
        if (!player.IsActive() || player.IsAtSafezone() || player.CurrentMap is not { } map)
        {
            return false;
        }

        var hitAny = false;
        foreach (var target in targets)
        {
            if (target.CurrentMap != map || !target.IsActive() || target.IsAtSafezone())
            {
                continue;
            }

            await target.AttackByAsync(player, skillEntry, true, damageFactor).ConfigureAwait(false);
            hitAny = true;
        }

        return hitAny;
    }
}
