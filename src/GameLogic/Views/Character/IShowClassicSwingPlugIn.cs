// <copyright file="IShowClassicSwingPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.Character;

/// <summary>
/// Interface of a view whose implementation tells the client whether it should play attack
/// swings in the classic 30 FPS rhythm at high attack speed.
/// </summary>
public interface IShowClassicSwingPlugIn : IViewPlugIn
{
    /// <summary>
    /// Tells the client whether the classic swing rhythm is enabled.
    /// </summary>
    /// <param name="enabled">If set to <see langword="true"/>, the classic swing rhythm is enabled.</param>
    ValueTask ShowClassicSwingAsync(bool enabled);
}
