// <copyright file="ResetRewardsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Rewards resets: coins for every reset and extra for every 10th, and at every grand reset (every 100th
/// reset by default) more coins and a box of jewel packs and seed spheres (game-design.md, "Progression").
/// The reset count keeps growing; the grand resets are the reset count divided by the resets per grand reset.
/// </summary>
[Guid("2C8E5A71-9D34-4B6F-A0E8-5F1C7B3D9E42")]
[PlugIn]
[Display(Name = "Reset rewards", Description = "Coins for every reset, extra every 10th, and a grand reset reward (coins and a box of jewels) every 100 resets.")]
public class ResetRewardsPlugIn : IFeaturePlugIn, ISupportCustomConfiguration<ResetRewardsConfiguration>, ISupportDefaultCustomConfiguration
{
    private readonly Lazy<IPlayerSiteRepository?>? _siteRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetRewardsPlugIn"/> class.
    /// </summary>
    public ResetRewardsPlugIn()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetRewardsPlugIn"/> class.
    /// </summary>
    /// <param name="services">The services, which may offer the storage of the milestone rewards.</param>
    [ActivatorUtilitiesConstructor]
    public ResetRewardsPlugIn(IServiceProvider services)
    {
        // Resolved on first use: the plugins are created while the services are still being built.
        this._siteRepository = new Lazy<IPlayerSiteRepository?>(services.GetService<IPlayerSiteRepository>);
    }

    /// <inheritdoc />
    public ResetRewardsConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the coins a reset earns.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="resetCount">The reset count after the reset.</param>
    /// <returns>The coins.</returns>
    public static int CoinsFor(ResetRewardsConfiguration configuration, int resetCount)
    {
        var coins = configuration.CoinsPerReset;
        if (resetCount % 10 == 0)
        {
            coins += configuration.CoinsPerTenthReset;
        }

        if (IsGrandReset(configuration, resetCount))
        {
            coins += configuration.CoinsPerGrandReset;
        }

        return coins;
    }

    /// <summary>
    /// Gets a value indicating whether the reset completes a grand reset.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="resetCount">The reset count after the reset.</param>
    /// <returns><c>true</c> for a grand reset.</returns>
    public static bool IsGrandReset(ResetRewardsConfiguration configuration, int resetCount)
        => resetCount > 0 && resetCount % Math.Max(1, configuration.ResetsPerGrandReset) == 0;

    /// <inheritdoc />
    public object CreateDefaultConfig() => new ResetRewardsConfiguration();

    /// <summary>
    /// Gives the rewards of a reset which just happened.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="resetCount">The reset count after the reset.</param>
    /// <returns>The task.</returns>
    public async ValueTask RewardAsync(Player player, int resetCount)
    {
        if (this.Configuration is not { } configuration || player.Account is not { } account)
        {
            return;
        }

        var coins = CoinsFor(configuration, resetCount);
        PlayerRewards.AddCoins(account, configuration.CoinType, coins);
        if (!IsGrandReset(configuration, resetCount))
        {
            await player.ShowBlueMessageAsync($"Reset {resetCount}: +{coins} {configuration.CoinType}.").ConfigureAwait(false);
            return;
        }

        var grandResets = resetCount / Math.Max(1, configuration.ResetsPerGrandReset);
        await player.ShowBlueMessageAsync($"Grand reset {grandResets}! +{coins} {configuration.CoinType} and a reward box.").ConfigureAwait(false);
        await player.GameContext.SendGlobalMessageAsync($"{player.Name} reached grand reset {grandResets}!", Interfaces.MessageType.GoldenCenter).ConfigureAwait(false);
        await PlayerRewards.GiveItemsAsync(player, configuration.GrandResetItems).ConfigureAwait(false);
        await this.AddMilestoneClaimAsync(player, account.LoginName, configuration, grandResets).ConfigureAwait(false);
    }

    private async ValueTask AddMilestoneClaimAsync(Player player, string loginName, ResetRewardsConfiguration configuration, int grandResets)
    {
        if (configuration.ParseMilestones().FirstOrDefault(m => m.GrandResets == grandResets) is null)
        {
            return;
        }

        if (this._siteRepository?.Value is not { } repository)
        {
            player.Logger.LogWarning("Grand reset milestone {GrandResets} of {Name}: there's no website storage, so no reward was added.", grandResets, player.Name);
            return;
        }

        try
        {
            var claim = new WebMilestoneClaim { LoginName = loginName, CharacterName = player.Name, GrandResets = grandResets };
            if (await repository.TryAddMilestoneClaimAsync(claim).ConfigureAwait(false))
            {
                await player.ShowBlueMessageAsync($"Milestone: {grandResets} grand resets! Pick your ancient item on the website, under Account.").ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Couldn't add the grand reset milestone {GrandResets} of {Name}.", grandResets, player.Name);
        }
    }
}
