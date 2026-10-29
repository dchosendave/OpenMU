// <copyright file="ComboStepPacketTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameServer.RemoteView.World;
using NUnit.Framework;

/// <summary>
/// Tests the custom combo step packet (C1 09 FB 02), which MuMain's GameLogic/Combat/ComboStep reads.
/// </summary>
[TestFixture]
public class ComboStepPacketTest
{
    /// <summary>
    /// Tests the bytes: header, code, sub code, attacker and target id in big endian, step.
    /// </summary>
    [Test]
    public void Write_TheLayoutMuMainReads()
    {
        var packet = new byte[ShowComboStepPlugIn.Size];

        ShowComboStepPlugIn.Write(packet, 0x1234, 0x0A0B, 3);

        Assert.That(packet, Is.EqualTo(new byte[] { 0xC1, 0x09, 0xFB, 0x02, 0x12, 0x34, 0x0A, 0x0B, 0x03 }));
    }
}
