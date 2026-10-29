// <copyright file="ResetChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which handles reset command.
/// </summary>
/// <seealso cref="MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.IChatCommandPlugIn" />
[Guid("90B35404-AADE-4F22-B5D2-4CD59B8BB4C8")]
[PlugIn]
[Display(Name = nameof(PlugInResources.ResetChatCommandPlugIn_Name), Description = nameof(PlugInResources.ResetChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command)]
public class ResetChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/reset";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        // "/reset auto" turns the automatic reset on, "/reset auto off" turns it off again.
        var arguments = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (arguments.Length > 1 && string.Equals(arguments[1], "auto", StringComparison.OrdinalIgnoreCase)
            && player.SelectedCharacter is { } character)
        {
            var enable = arguments.Length < 3 || !string.Equals(arguments[2], "off", StringComparison.OrdinalIgnoreCase);
            AutoResetPlugIn.SetEnabled(character.Id, enable);
            await player.ShowBlueMessageAsync(enable
                ? "Automatic reset is on: you reset by yourself on reaching the reset level. \"/reset auto off\" turns it off."
                : "Automatic reset is off.").ConfigureAwait(false);
            if (!enable)
            {
                return;
            }
        }

        var resetAction = new ResetCharacterAction(player);
        await resetAction.ResetCharacterAsync().ConfigureAwait(false);
    }
}