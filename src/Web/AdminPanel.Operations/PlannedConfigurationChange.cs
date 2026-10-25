// <copyright file="PlannedConfigurationChange.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Operations;

/// <summary>A validated configuration change which hasn't been applied yet.</summary>
/// <param name="Change">The change as it is shown to the caller.</param>
/// <param name="Apply">Applies the change to the in-memory configuration.</param>
/// <param name="Revert">Reverts the applied change, when saving it failed.</param>
/// <param name="BeforeSave">Runs after all changes are applied and before they are saved, e.g. to mark a removed object for deletion.</param>
internal sealed record PlannedConfigurationChange(ConfigurationFieldChange Change, Action Apply, Action Revert, Func<ValueTask>? BeforeSave = null);
