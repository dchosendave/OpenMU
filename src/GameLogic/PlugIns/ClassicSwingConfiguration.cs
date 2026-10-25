// <copyright file="ClassicSwingConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.ComponentModel;

/// <summary>
/// Configuration of the <see cref="ClassicSwingPlugIn"/>.
/// </summary>
public class ClassicSwingConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether clients play attack swings in the classic 30 FPS rhythm at high attack speed.
    /// </summary>
    [DefaultValue(true)]
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.ClassicSwingConfiguration_Enabled_Name), Description = nameof(PlugInResources.ClassicSwingConfiguration_Enabled_Description))]
    public bool Enabled { get; set; } = true;
}
