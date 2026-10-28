// <copyright file="CastleSiegeSwitchHolderKillPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.CastleSiege.PointMode;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Reports player kills on the Castle Siege map to the point mode, which scores kills of switch holders.
/// </summary>
[Guid("5E9A1C47-B2D3-4F80-9C6E-1A7B3D8F2E54")]
[PlugIn]
[System.ComponentModel.DataAnnotations.Display(Name = "Castle Siege: switch holder kills", Description = "Scores kills of switch holders for the Castle Siege point mode.")]
public class CastleSiegeSwitchHolderKillPlugIn : IAttackableGotKilledPlugIn
{
    /// <inheritdoc />
    public async ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (killed is not Player victim
            || (killer as Player ?? (killer as Monster)?.SummonedBy) is not { } killingPlayer
            || CastleSiegeContextResolver.GetContext(victim) is not { Scoreboard: not null } context)
        {
            return;
        }

        await CastleSiegePointMode.OnPlayerKilledAsync(context, victim, killingPlayer).ConfigureAwait(false);
    }
}
