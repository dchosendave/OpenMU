// <copyright file="ComplexPotionConsumeHandlerConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;

/// <summary>
/// The configuration of a <see cref="ComplexPotionConsumeHandlerPlugIn"/>: how much health and shield it recovers, and how fast.
/// </summary>
public class ComplexPotionConsumeHandlerConfiguration
{
    /// <summary>
    /// Gets or sets the health recovery.
    /// </summary>
    [Display(Name = "Health recovery")]
    public RecoverConsumeHandlerConfiguration Health { get; set; } = new();

    /// <summary>
    /// Gets or sets the shield (SD) recovery.
    /// </summary>
    [Display(Name = "Shield (SD) recovery")]
    public RecoverConsumeHandlerConfiguration Shield { get; set; } = new();
}
