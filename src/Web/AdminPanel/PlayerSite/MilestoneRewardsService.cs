// <copyright file="MilestoneRewardsService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.PlayerSite;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The grand reset milestone rewards on the player website: the game server adds a claim when a character
/// reaches a milestone (see <see cref="ResetRewardsPlugIn"/>), and the player picks a part of the
/// milestone's ancient sets here, which goes into the vault.
/// </summary>
public sealed class MilestoneRewardsService
{
    /// <summary>The number of excellent options of an excellent part.</summary>
    public const int ExcellentOptionCount = 2;

    private static readonly Guid PlugInId = typeof(ResetRewardsPlugIn).GUID;

    private readonly PlayerSiteService _playerSite;
    private readonly IDataSource<GameConfiguration> _configurationSource;
    private readonly IPlayerSiteRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="MilestoneRewardsService"/> class.
    /// </summary>
    /// <param name="playerSite">The player site service, which changes accounts.</param>
    /// <param name="configurationSource">The game configuration, which holds the milestones and sets.</param>
    /// <param name="repository">The stored claims.</param>
    public MilestoneRewardsService(PlayerSiteService playerSite, IDataSource<GameConfiguration> configurationSource, IPlayerSiteRepository repository)
    {
        this._playerSite = playerSite;
        this._configurationSource = configurationSource;
        this._repository = repository;
    }

    /// <summary>
    /// Gets the milestone rewards of an account, with the parts to pick from for the open ones.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <returns>The result, with the claims as data.</returns>
    public async Task<PlayerSiteResult> GetClaimsAsync(string loginName)
    {
        var claims = await this._repository.GetMilestoneClaimsAsync(loginName).ConfigureAwait(false);
        var configuration = await this._configurationSource.GetOwnerAsync().ConfigureAwait(false);
        var milestones = this.GetMilestones(configuration);
        var data = claims.Select(claim =>
        {
            var milestone = milestones.FirstOrDefault(m => m.GrandResets == claim.GrandResets);
            return new
            {
                id = claim.Id,
                character = claim.CharacterName,
                grandResets = claim.GrandResets,
                createdAt = claim.CreatedAt,
                claimedAt = claim.ClaimedAt,
                claimedItem = claim.ClaimedItem,
                level = milestone?.Level ?? 0,
                allowsExcellent = milestone?.AllowsExcellent ?? false,
                sets = claim.ClaimedAt is not null || milestone is null
                    ? []
                    : SetsOf(configuration, milestone).Select(set => new
                    {
                        name = set.Name.ToString(),
                        parts = set.Items
                            .Where(part => part.ItemDefinition is not null)
                            .OrderBy(part => part.ItemDefinition!.Group).ThenBy(part => part.ItemDefinition!.Number)
                            .Select(part => new { id = part.GetId(), name = part.ItemDefinition!.Name.ToString() })
                            .ToList(),
                    }).Cast<object>().ToList(),
            };
        }).ToList();

        return new PlayerSiteResult(PlayerSiteResult.Ok, Data: data);
    }

    /// <summary>
    /// Picks the reward of an open milestone: the part goes into the vault, as ancient item or, where the
    /// milestone allows it, as excellent item. A failed delivery opens the claim again.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="claimId">The claim.</param>
    /// <param name="setItemId">The part of the ancient set.</param>
    /// <param name="excellent">If set to <c>true</c>, the part comes as excellent item instead of ancient.</param>
    /// <returns>The result.</returns>
    public async Task<PlayerSiteResult> ClaimAsync(string loginName, Guid claimId, Guid setItemId, bool excellent)
    {
        var claim = await this._repository.GetMilestoneClaimAsync(claimId).ConfigureAwait(false);
        if (claim is null || !string.Equals(claim.LoginName, loginName, StringComparison.OrdinalIgnoreCase))
        {
            return new PlayerSiteResult(PlayerSiteResult.NotFound, "This reward doesn't exist.");
        }

        if (claim.ClaimedAt is not null)
        {
            return new PlayerSiteResult(PlayerSiteResult.Invalid, "This reward was already picked.");
        }

        var marked = false;
        var result = await this._playerSite.ChangeAccountAsync(loginName, async session =>
        {
            var milestone = this.GetMilestones(session.Configuration).FirstOrDefault(m => m.GrandResets == claim.GrandResets)
                ?? throw PlayerSiteService.Refuse(PlayerSiteResult.NotFound, "This milestone has no reward anymore.");
            if (excellent && !milestone.AllowsExcellent)
            {
                throw PlayerSiteService.Refuse(PlayerSiteResult.Invalid, "This milestone gives ancient items only.");
            }

            var set = SetsOf(session.Configuration, milestone).FirstOrDefault(s => s.Items.Any(i => i.GetId() == setItemId))
                ?? throw PlayerSiteService.Refuse(PlayerSiteResult.Invalid, "This part isn't one of the choices of this milestone.");
            var part = set.Items.First(i => i.GetId() == setItemId);
            var definition = part.ItemDefinition ?? throw PlayerSiteService.Refuse(PlayerSiteResult.Invalid, "This part has no item.");
            var name = excellent
                ? $"Excellent {definition.Name} +{milestone.Level}"
                : $"{set.Name} {definition.Name} +{milestone.Level}";
            if (name.Length > WebMilestoneClaim.ClaimedItemMaximumLength)
            {
                name = name[..WebMilestoneClaim.ClaimedItemMaximumLength];
            }

            if (!await this._repository.TryMarkMilestoneClaimedAsync(claimId, name, DateTime.UtcNow).ConfigureAwait(false))
            {
                throw PlayerSiteService.Refuse(PlayerSiteResult.Invalid, "This reward was already picked.");
            }

            marked = true;
            try
            {
                session.AddRewardItem(session.Vault, definition, milestone.Level, excellent ? null : part, ExcellentOptionCount, name);
            }
            catch (AccountManager.AccountManagerException ex)
            {
                throw PlayerSiteService.Refuse(PlayerSiteResult.NoSpace, ex.Message);
            }

            return new { item = name };
        }).ConfigureAwait(false);

        if (marked && !result.Succeeded)
        {
            await this._repository.ReopenMilestoneClaimAsync(claimId).ConfigureAwait(false);
        }

        return result;
    }

    private static IEnumerable<ItemSetGroup> SetsOf(GameConfiguration configuration, GrandResetMilestone milestone)
        => configuration.ItemSetGroups
            .Where(g => g.Items.Any(i => i.AncientSetDiscriminator != 0) && milestone.Includes(g.Name.ToString() ?? string.Empty))
            .OrderBy(g => milestone.Sets.ToList().FindIndex(s => string.Equals(s, g.Name.ToString(), StringComparison.OrdinalIgnoreCase)));

    private IReadOnlyList<GrandResetMilestone> GetMilestones(GameConfiguration configuration)
    {
        var plugIn = configuration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == PlugInId);
        if (plugIn is { IsActive: false })
        {
            return [];
        }

        var settings = plugIn?.GetConfiguration<ResetRewardsConfiguration>(new ByDataSourceReferenceHandler(this._configurationSource));
        return (settings ?? new ResetRewardsConfiguration()).ParseMilestones();
    }
}
