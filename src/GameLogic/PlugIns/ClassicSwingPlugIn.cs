// <copyright file="ClassicSwingPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tells the MuMain client whether it plays attack swings in the classic 30 FPS rhythm at high
/// attack speed: when a player enters the world, and to all online players when the setting changes.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ClassicSwingPlugIn_Name), Description = nameof(PlugInResources.ClassicSwingPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("9A6F3C28-4D1B-4E7A-8C25-B3E0D7F14A69")]
public class ClassicSwingPlugIn : IPlayerStateChangedPlugIn, ISupportCustomConfiguration<ClassicSwingConfiguration>, ISupportDefaultCustomConfiguration
{
    private readonly ConcurrentDictionary<IGameContext, byte> _gameContexts = new();

    private ClassicSwingConfiguration? _configuration;

    /// <inheritdoc/>
    public ClassicSwingConfiguration? Configuration
    {
        get => this._configuration;
        set
        {
            this._configuration = value;
            if (value is not null)
            {
                this.ShowToOnlinePlayers(value.Enabled);
            }
        }
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new ClassicSwingConfiguration();

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (previousState != PlayerState.CharacterSelection || currentState != PlayerState.EnteredWorld)
        {
            return;
        }

        this._gameContexts.TryAdd(player.GameContext, 0);
        var enabled = this.Configuration?.Enabled ?? true;
        await player.InvokeViewPlugInAsync<IShowClassicSwingPlugIn>(p => p.ShowClassicSwingAsync(enabled)).ConfigureAwait(false);
    }

    private void ShowToOnlinePlayers(bool enabled)
    {
        foreach (var gameContext in this._gameContexts.Keys)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await gameContext.ForEachPlayerAsync(player => player.PlayerState.CurrentState == PlayerState.EnteredWorld
                        ? player.InvokeViewPlugInAsync<IShowClassicSwingPlugIn>(p => p.ShowClassicSwingAsync(enabled)).AsTask()
                        : Task.CompletedTask).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    gameContext.LoggerFactory.CreateLogger<ClassicSwingPlugIn>().LogError(ex, "Error while sending the classic swing setting.");
                }
            });
        }
    }
}
