// <copyright file="IShowComboStepPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// A view which shows a follow-up combo of the combo chain (see <see cref="PlayerActions.Skills.ComboChainPlugIn"/>)
/// with its step, so the client can draw the 2nd, 3rd and 4th combo apart. Clients without it get the
/// usual combo animation.
/// </summary>
public interface IShowComboStepPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows a follow-up combo.
    /// </summary>
    /// <param name="attacker">The attacker who completed the combo.</param>
    /// <param name="target">The target at which the combo effect is shown, or <c>null</c> for the attacker.</param>
    /// <param name="step">The step of the combo: 2, 3 or 4.</param>
    /// <returns>A task which represents the asynchronous operation.</returns>
    ValueTask ShowComboStepAsync(IAttacker attacker, IAttackable? target, byte step);
}
