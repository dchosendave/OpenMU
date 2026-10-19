// <copyright file="EvolveChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration.Quests;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameLogic.Views.Quest;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which evolves the character to its next class (2nd class, then the
/// master class), like the evolution quests do, without doing the quests.
/// </summary>
[Guid("BE82F9BC-A2A6-4DA8-8244-445DB697DB8B")]
[PlugIn]
[Display(Name = "Evolve chat command", Description = "/evo evolves the character to its 2nd class, and once more to its master (3rd) class, without the evolution quests.")]
[ChatCommandHelp(Command, typeof(EmptyChatCommandArgs), CharacterStatus.Normal)]
public class EvolveChatCommandPlugIn : ChatCommandPlugInBase<EmptyChatCommandArgs>, ISupportCustomConfiguration<EvolveChatCommandPlugIn.EvolveConfiguration>, ISupportDefaultCustomConfiguration
{
    private const string Command = "/evo";

    /// <summary>
    /// Gets or sets the configuration.
    /// </summary>
    public EvolveConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public object CreateDefaultConfig() => new EvolveConfiguration();

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, EmptyChatCommandArgs arguments)
    {
        if (player.SelectedCharacter is not { CharacterClass: { } currentClass } character)
        {
            return;
        }

        if (currentClass.NextGenerationClass is not { } nextClass)
        {
            await player.ShowBlueMessageAsync($"{currentClass.Name.GetTranslation(player.Culture)} is already the final class.").ConfigureAwait(false);
            return;
        }

        var configuration = this.Configuration ?? (EvolveConfiguration)this.CreateDefaultConfig();
        var toMasterClass = nextClass.IsMasterClass;
        var requiredLevel = toMasterClass ? configuration.MasterClassLevel : configuration.SecondClassLevel;
        var requiredMoney = toMasterClass ? configuration.MasterClassMoney : configuration.SecondClassMoney;
        if (player.Level < requiredLevel)
        {
            await player.ShowBlueMessageAsync($"You need level {requiredLevel} to become a {nextClass.Name.GetTranslation(player.Culture)}.").ConfigureAwait(false);
            return;
        }

        if (requiredMoney > 0 && !player.TryRemoveMoney(requiredMoney))
        {
            await player.ShowBlueMessageAsync($"You need {requiredMoney:N0} zen to become a {nextClass.Name.GetTranslation(player.Culture)}.").ConfigureAwait(false);
            return;
        }

        // The same as the reward of the evolution quests (QuestCompletionAction).
        character.CharacterClass = nextClass;
        var rewardType = toMasterClass ? QuestRewardType.CharacterEvolutionSecondToThird : QuestRewardType.CharacterEvolutionFirstToSecond;
        await player.ForEachWorldObserverAsync<ILegacyQuestRewardPlugIn>(p => p.ShowAsync(player, rewardType, 0, null), true).ConfigureAwait(false);
        if (toMasterClass)
        {
            await player.InvokeViewPlugInAsync<IUpdateMasterStatsPlugIn>(p => p.SendMasterStatsAsync()).ConfigureAwait(false);
        }

        if (requiredMoney > 0)
        {
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        }

        await player.ShowBlueMessageAsync($"Congratulations, you are now a {nextClass.Name.GetTranslation(player.Culture)}!").ConfigureAwait(false);
    }

    /// <summary>
    /// Configuration for the <see cref="EvolveChatCommandPlugIn"/>.
    /// </summary>
    public class EvolveConfiguration
    {
        /// <summary>
        /// Gets or sets the level needed for the 2nd class (the evolution quests need 150).
        /// </summary>
        [Display(Name = "Level for the 2nd class")]
        public int SecondClassLevel { get; set; } = 150;

        /// <summary>
        /// Gets or sets the zen the 2nd class costs.
        /// </summary>
        [Display(Name = "Zen for the 2nd class")]
        public int SecondClassMoney { get; set; }

        /// <summary>
        /// Gets or sets the level needed for the master (3rd) class (the evolution quests need 380).
        /// Dark Lord, Magic Gladiator and Rage Fighter evolve straight to their master class.
        /// </summary>
        [Display(Name = "Level for the master class")]
        public int MasterClassLevel { get; set; } = 380;

        /// <summary>
        /// Gets or sets the zen the master (3rd) class costs.
        /// </summary>
        [Display(Name = "Zen for the master class")]
        public int MasterClassMoney { get; set; }
    }
}
