// <copyright file="ClassicSwingPlugInTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views.Character;

/// <summary>
/// Tests for the <see cref="ClassicSwingPlugIn"/>.
/// </summary>
[TestFixture]
public class ClassicSwingPlugInTest
{
    /// <summary>
    /// Tests that the configured setting is sent when the player enters the world.
    /// </summary>
    /// <param name="enabled">The configured setting.</param>
    [TestCase(true)]
    [TestCase(false)]
    public async ValueTask SettingIsSentWhenEnteringWorldAsync(bool enabled)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = new ClassicSwingPlugIn { Configuration = new ClassicSwingConfiguration { Enabled = enabled } };

        await plugIn.PlayerStateChangedAsync(player, PlayerState.CharacterSelection, PlayerState.EnteredWorld).ConfigureAwait(false);

        Mock.Get(player.ViewPlugIns.GetPlugIn<IShowClassicSwingPlugIn>()!)
            .Verify(p => p.ShowClassicSwingAsync(enabled), Times.Once);
    }

    /// <summary>
    /// Tests that other state changes don't send the setting.
    /// </summary>
    [Test]
    public async ValueTask SettingIsNotSentOnOtherStateChangesAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = new ClassicSwingPlugIn { Configuration = new ClassicSwingConfiguration() };

        await plugIn.PlayerStateChangedAsync(player, PlayerState.TradeOpened, PlayerState.EnteredWorld).ConfigureAwait(false);

        Mock.Get(player.ViewPlugIns.GetPlugIn<IShowClassicSwingPlugIn>()!)
            .Verify(p => p.ShowClassicSwingAsync(It.IsAny<bool>()), Times.Never);
    }
}
