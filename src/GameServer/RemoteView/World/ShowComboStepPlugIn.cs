// <copyright file="ShowComboStepPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.World;

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Sends a follow-up combo with its step to the MuMain client, which draws each step in a deeper blue:
/// custom packet C1 09 FB 02, then the attacker's and the target's id (big endian, like the skill
/// animation) and the step.
/// </summary>
[PlugIn]
[Display(Name = "Show combo step", Description = "Sends the step of a follow-up combo to the MuMain client (custom packet C1 FB 02).")]
[Guid("5E2B9C47-8A16-4F3D-9C72-1B6E4D8A0F35")]
public class ShowComboStepPlugIn : IShowComboStepPlugIn
{
    /// <summary>The packet code, shared with the Castle Siege scoreboard (FB 01).</summary>
    public const byte Code = 0xFB;

    /// <summary>The packet sub code.</summary>
    public const byte SubCode = 0x02;

    /// <summary>The size of the packet.</summary>
    public const int Size = 9;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowComboStepPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowComboStepPlugIn(RemotePlayer player) => this._player = player;

    /// <summary>
    /// Writes the packet.
    /// </summary>
    /// <param name="span">The target, at least <see cref="Size"/> bytes.</param>
    /// <param name="attackerId">The attacker's id.</param>
    /// <param name="targetId">The target's id.</param>
    /// <param name="step">The step.</param>
    public static void Write(Span<byte> span, ushort attackerId, ushort targetId, byte step)
    {
        span[0] = 0xC1;
        span[1] = Size;
        span[2] = Code;
        span[3] = SubCode;
        BinaryPrimitives.WriteUInt16BigEndian(span[4..], attackerId);
        BinaryPrimitives.WriteUInt16BigEndian(span[6..], targetId);
        span[8] = step;
    }

    /// <inheritdoc />
    public async ValueTask ShowComboStepAsync(IAttacker attacker, IAttackable? target, byte step)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        var attackerId = attacker.GetId(this._player);
        var targetId = ((IIdentifiable?)target ?? attacker).GetId(this._player);

        int Write()
        {
            ShowComboStepPlugIn.Write(connection.Output.GetSpan(Size), attackerId, targetId, step);
            return Size;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}
