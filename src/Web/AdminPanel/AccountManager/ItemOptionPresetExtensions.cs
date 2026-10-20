// <copyright file="ItemOptionPresetExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.Web.AdminPanel.Properties;

/// <summary>
/// Extensions for the <see cref="ItemOptionPreset"/>.
/// </summary>
public static class ItemOptionPresetExtensions
{
    /// <summary>
    /// Gets a short description of the preset, e.g. "+15 · Skill · Luck · +16 · Excellent".
    /// </summary>
    /// <param name="preset">The preset.</param>
    /// <returns>The description.</returns>
    public static string GetSummary(this ItemOptionPreset preset)
    {
        var parts = new List<string>();
        if (preset.Level is { } level)
        {
            parts.Add($"+{level}");
        }

        if (preset.Skill)
        {
            parts.Add(Resources.AccountManagerSkill);
        }

        if (preset.Luck)
        {
            parts.Add(Resources.AccountManagerLuck);
        }

        if (preset.OptionLevel > 0)
        {
            parts.Add(preset.OptionLevel >= ItemOptionPreset.MaximumOptionLevel ? Resources.AccountManagerMaxOption : $"+{preset.OptionLevel * 4}");
        }

        if (preset.Excellent)
        {
            parts.Add(Resources.AccountManagerExcellent);
        }

        if (preset.Guardian)
        {
            parts.Add("380");
        }

        return parts.Count == 0 ? Resources.AccountManagerNoOptions : string.Join(" · ", parts);
    }
}
