// <copyright file="AutoResetPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Resets characters by themselves when they reach the reset level, for the players who turned it on with
/// <c>/reset auto</c>. The setting is kept per character while the server runs, so it also works when the
/// player switches to offline leveling.
/// </summary>
[Guid("A41D6E92-7B3C-4F85-9E10-C2B8D5F37A61")]
[PlugIn]
[Display(Name = "Automatic reset", Description = "Characters with \"/reset auto\" turned on reset by themselves on reaching the reset level.")]
public class AutoResetPlugIn : ICharacterLevelUpPlugIn
{
    private static readonly ConcurrentDictionary<Guid, bool> EnabledCharacters = new();

    /// <summary>
    /// Turns the automatic reset on or off for a character.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="enabled">Whether it's on.</param>
    public static void SetEnabled(Guid characterId, bool enabled)
    {
        if (enabled)
        {
            EnabledCharacters[characterId] = true;
        }
        else
        {
            EnabledCharacters.TryRemove(characterId, out _);
        }
    }

    /// <summary>
    /// Gets a value indicating whether the automatic reset is on for a character.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <returns><c>true</c>, if it's on.</returns>
    public static bool IsEnabled(Guid characterId) => EnabledCharacters.ContainsKey(characterId);

    /// <inheritdoc />
    public void CharacterLeveledUp(Player player)
    {
        if (player.SelectedCharacter is not { } character
            || !IsEnabled(character.Id)
            || player.GameContext.FeaturePlugIns.GetPlugIn<ResetFeaturePlugIn>()?.Configuration is not { } configuration
            || player.Level < configuration.RequiredLevel)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await new ResetCharacterAction(player).ResetCharacterAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogError(ex, "Automatic reset of {Character} failed.", character.Name);
            }
        });
    }
}
