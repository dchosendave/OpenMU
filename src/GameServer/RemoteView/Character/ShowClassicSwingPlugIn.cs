// <copyright file="ShowClassicSwingPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Character;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowClassicSwingPlugIn"/> which sends the setting
/// to the game client with a server command.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowClassicSwingPlugIn_Name), Description = nameof(PlugInResources.ShowClassicSwingPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5E8B2D41-7C3A-4F96-A0D1-9B4E6C2F8A17")]
public class ShowClassicSwingPlugIn : IShowClassicSwingPlugIn
{
    /// <summary>
    /// The server command type which the MuMain client reads as the classic swing setting.
    /// </summary>
    private const byte ClassicSwingCommandType = 200;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowClassicSwingPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowClassicSwingPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public ValueTask ShowClassicSwingAsync(bool enabled)
    {
        return this._player.Connection.SendServerCommandAsync(ClassicSwingCommandType, enabled ? (byte)1 : (byte)0, 0);
    }
}
