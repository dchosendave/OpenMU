// <copyright file="ShowCastleSiegeScoreboardPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.CastleSiege;

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using MUnique.OpenMU.GameLogic.Views.CastleSiege;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Sends the Castle Siege point mode scoreboard to the MuMain client, with the custom packet
/// <c>C2 FB 01</c>. The original clients don't know it and ignore it.
/// </summary>
/// <remarks>
/// Layout after the C2 header (size big endian), code 0xFB and sub code 0x01:
/// state (1 byte: 0 hidden, 1 running, 2 final), remaining seconds (2 bytes LE), seconds until the
/// next defense tick (2 bytes LE, 0xFFFF for none), guild count (1 byte, at most <see cref="MaximumGuilds"/>),
/// then per guild: name (8 bytes, zero padded), points (4 bytes LE), seals (1 byte),
/// flags (1 byte: 1 holds the castle, 2 the receiver's guild).
/// </remarks>
[PlugIn]
[Display(Name = "Show Castle Siege scoreboard", Description = "Sends the point mode scoreboard to the MuMain client (custom packet C2 FB 01).")]
[Guid("8D4F2A6B-1C93-4E7A-B5D0-3F6E9A2C7B18")]
public class ShowCastleSiegeScoreboardPlugIn : IShowCastleSiegeScoreboardPlugIn
{
    /// <summary>The packet code.</summary>
    public const byte Code = 0xFB;

    /// <summary>The sub code of the scoreboard.</summary>
    public const byte SubCode = 0x01;

    /// <summary>The most guilds one packet carries.</summary>
    public const int MaximumGuilds = 8;

    private const int HeaderSize = 11;
    private const int NameSize = 8;
    private const int GuildSize = NameSize + 4 + 1 + 1;
    private const ushort NoDefenseTick = 0xFFFF;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowCastleSiegeScoreboardPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowCastleSiegeScoreboardPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowScoreboardAsync(CastleSiegeScoreboardView scoreboard)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        int Write()
        {
            var guilds = scoreboard.Guilds.Take(MaximumGuilds).ToList();
            var size = HeaderSize + (guilds.Count * GuildSize);
            var span = connection.Output.GetSpan(size)[..size];
            span.Clear();
            span[0] = 0xC2;
            BinaryPrimitives.WriteUInt16BigEndian(span[1..], (ushort)size);
            span[3] = Code;
            span[4] = SubCode;
            span[5] = (byte)scoreboard.State;
            BinaryPrimitives.WriteUInt16LittleEndian(span[6..], ToSeconds(scoreboard.Remaining));
            BinaryPrimitives.WriteUInt16LittleEndian(span[8..], scoreboard.NextDefenseTick is { } tick ? ToSeconds(tick) : NoDefenseTick);
            span[10] = (byte)guilds.Count;
            for (var i = 0; i < guilds.Count; i++)
            {
                var guild = guilds[i];
                var entry = span.Slice(HeaderSize + (i * GuildSize), GuildSize);
                Encoding.ASCII.GetBytes(guild.GuildName.AsSpan(0, Math.Min(guild.GuildName.Length, NameSize)), entry);
                BinaryPrimitives.WriteInt32LittleEndian(entry[NameSize..], guild.Points);
                entry[NameSize + 4] = (byte)Math.Clamp(guild.Seals, 0, byte.MaxValue);
                entry[NameSize + 5] = (byte)((guild.HoldsCastle ? 1 : 0) | (guild.IsOwnGuild ? 2 : 0));
            }

            return size;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    private static ushort ToSeconds(TimeSpan time) => (ushort)Math.Clamp((int)Math.Ceiling(time.TotalSeconds), 0, NoDefenseTick - 1);
}
