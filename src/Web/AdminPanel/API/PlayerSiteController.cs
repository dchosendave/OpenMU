// <copyright file="PlayerSiteController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

using System.ComponentModel.DataAnnotations;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MUnique.OpenMU.GameLogic.CastleSiege.PointMode;
using MUnique.OpenMU.GameLogic.WebMiniGames;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.Persistence.AdminAuth;
using MUnique.OpenMU.Web.AdminPanel.Auth;
using MUnique.OpenMU.Web.AdminPanel.Operations;
using MUnique.OpenMU.Web.AdminPanel.PlayerSite;

/// <summary>
/// API controller for the player website: the changes which logged in players request there.
/// </summary>
/// <remarks>
/// The caller is the server of the player website, which authenticates its players itself and only asks for
/// changes of the account of the logged in player. Every change is audited like the changes in the admin panel.
/// Refused changes are answered with <c>409</c> (account online), <c>404</c>, <c>400</c>, <c>402</c>
/// (balance too low) or <c>507</c> (no room), each with a message for the player.
/// </remarks>
[ApiController]
[Route("api/site/accounts/{loginName}")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
public class PlayerSiteController : ControllerBase
{
    private readonly PlayerSiteService _service;
    private readonly WebMiniGamesService _games;
    private readonly PlayerSiteFeaturesService _features;
    private readonly AdminOperationExecutor _executor;
    private readonly CastleSiegeSiteService _castleSiege;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerSiteController"/> class.
    /// </summary>
    /// <param name="service">The player site service.</param>
    /// <param name="games">The mini games of the player website.</param>
    /// <param name="features">The check-in, character tools, news and market.</param>
    /// <param name="executor">The executor, which audits the changes.</param>
    /// <param name="castleSiege">The Castle Siege point mode settings.</param>
    public PlayerSiteController(PlayerSiteService service, WebMiniGamesService games, PlayerSiteFeaturesService features, AdminOperationExecutor executor, CastleSiegeSiteService castleSiege)
    {
        this._castleSiege = castleSiege;
        this._service = service;
        this._games = games;
        this._features = features;
        this._executor = executor;
    }

    /// <summary>
    /// Sets a new password.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <param name="request">The new password.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    [HttpPost("password")]
    public Task<IActionResult> ChangePasswordAsync(string loginName, [FromBody] PasswordChange request, CancellationToken cancellationToken)
        => this.ExecuteAsync("site_password_change", new { loginName }, () => this._service.ChangePasswordAsync(loginName, request.Password), cancellationToken);

    /// <summary>
    /// Moves an item between the vault and the inventories of the characters of the account.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <param name="request">Which item to move where.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result, with the new slot of the item.</returns>
    [HttpPost("items/move")]
    public Task<IActionResult> MoveItemAsync(string loginName, [FromBody] ItemMove request, CancellationToken cancellationToken)
        => this.ExecuteAsync("site_item_move", new { loginName, request.From, request.Slot, request.To }, () => this._service.MoveItemAsync(loginName, request.From, request.Slot, request.To), cancellationToken);

    /// <summary>
    /// Buys an item package of the web shop. The items and zen of the package go to the vault.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <param name="packageId">The identifier of the item package.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result, with the remaining balance.</returns>
    [HttpPost("shop/{packageId:guid}")]
    public Task<IActionResult> BuyAsync(string loginName, Guid packageId, CancellationToken cancellationToken)
        => this.ExecuteAsync("site_shop_buy", new { loginName, packageId }, () => this._service.BuyPackageAsync(loginName, packageId), cancellationToken);

    /// <summary>
    /// Plays a mini game: takes the bet and pays the win. Plays are stored in their own history
    /// (admin.WebMiniGamePlay), not in the audit trail.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <param name="game">The game: color, wheel, dropball or scratch.</param>
    /// <param name="request">The bet and the choice of the player.</param>
    /// <returns>The result, with the outcome and the new balance.</returns>
    [HttpPost("games/{game}")]
    public async Task<IActionResult> PlayAsync(string loginName, string game, [FromBody] GamePlay request)
        => ToResponse(await this._games.PlayAsync(loginName, game, request.Bet, request.Choice).ConfigureAwait(false));

    /// <summary>
    /// Saves the settings of the mini games, e.g. from a game master on the player website.
    /// </summary>
    /// <param name="request">The settings and who changes them.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result, with the return to player of each game.</returns>
    [HttpPut("~/api/site/games/settings")]
    public Task<IActionResult> SaveGameSettingsAsync([FromBody] GameSettings request, CancellationToken cancellationToken)
        => this.ExecuteAsync("site_minigames_settings", new { request.ChangedBy, request.Active }, () => this._games.SaveSettingsAsync(request.Active, request.Configuration), cancellationToken);

    /// <summary>
    /// Saves the settings of the Castle Siege point mode: the points, and the rewards of the placements.
    /// </summary>
    /// <param name="request">The settings and who changes them.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    [HttpPut("~/api/site/castle-siege/settings")]
    public Task<IActionResult> SaveCastleSiegeSettingsAsync([FromBody] CastleSiegeSettings request, CancellationToken cancellationToken)
        => this.ExecuteAsync("site_castlesiege_settings", new { request.ChangedBy, request.Active }, () => this._castleSiege.SaveSettingsAsync(request.Active, request.Configuration), cancellationToken);

    /// <summary>
    /// Checks the account in for today and grants the reward of its streak day.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <returns>The result.</returns>
    [HttpPost("checkin")]
    public async Task<IActionResult> CheckInAsync(string loginName)
        => ToResponse(await this._features.CheckInAsync(loginName).ConfigureAwait(false));

    /// <summary>
    /// Uses a character tool: reset, unstuck, stats or pkclear.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <param name="characterName">The character.</param>
    /// <param name="tool">The tool.</param>
    /// <param name="request">For stats: the points per stat.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    [HttpPost("characters/{characterName}/{tool}")]
    public Task<IActionResult> UseCharacterToolAsync(string loginName, string characterName, string tool, [FromBody] CharacterToolRequest? request, CancellationToken cancellationToken)
        => this.ExecuteAsync(
            "site_character_" + tool,
            new { loginName, characterName, request?.Points },
            () => tool switch
            {
                "reset" => this._features.ResetAsync(loginName, characterName),
                "unstuck" => this._features.UnstuckAsync(loginName, characterName),
                "stats" => this._features.AddStatsAsync(loginName, characterName, request?.Points ?? new Dictionary<string, int>()),
                "pkclear" => this._features.ClearPlayerKillsAsync(loginName, characterName),
                _ => Task.FromResult(new PlayerSiteResult(PlayerSiteResult.NotFound, "Unknown tool.")),
            },
            cancellationToken);

    /// <summary>
    /// Lists an item of the vault on the player market.
    /// </summary>
    /// <param name="loginName">The login name of the seller.</param>
    /// <param name="request">The vault slot and the price.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    [HttpPost("market")]
    public Task<IActionResult> ListItemAsync(string loginName, [FromBody] MarketListingRequest request, CancellationToken cancellationToken)
        => this.ExecuteAsync("site_market_list", new { loginName, request.Slot, request.Price }, () => this._features.ListItemAsync(loginName, request.Slot, request.Price), cancellationToken);

    /// <summary>
    /// Takes a listed item back into the seller's vault.
    /// </summary>
    /// <param name="loginName">The login name of the seller.</param>
    /// <param name="listingId">The listing.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    [HttpDelete("market/{listingId:guid}")]
    public Task<IActionResult> CancelListingAsync(string loginName, Guid listingId, CancellationToken cancellationToken)
        => this.ExecuteAsync("site_market_cancel", new { loginName, listingId }, () => this._features.CancelListingAsync(loginName, listingId), cancellationToken);

    /// <summary>
    /// Buys a listed item.
    /// </summary>
    /// <param name="loginName">The login name of the buyer.</param>
    /// <param name="listingId">The listing.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    [HttpPost("market/{listingId:guid}/buy")]
    public Task<IActionResult> BuyListingAsync(string loginName, Guid listingId, CancellationToken cancellationToken)
        => this.ExecuteAsync("site_market_buy", new { loginName, listingId }, () => this._features.BuyListingAsync(loginName, listingId), cancellationToken);

    /// <summary>
    /// Saves a news post of the player website, written by a game master.
    /// </summary>
    /// <param name="post">The post; its author is the game master.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    [HttpPut("~/api/site/news")]
    public Task<IActionResult> SaveNewsAsync([FromBody] WebNewsPost post, CancellationToken cancellationToken)
        => this.ExecuteAsync("site_news_save", new { post.Id, post.Slug, post.Title, post.Author }, () => this._features.SaveNewsAsync(post), cancellationToken);

    /// <summary>
    /// Deletes a news post of the player website.
    /// </summary>
    /// <param name="id">The post.</param>
    /// <param name="by">The game master who deletes it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    [HttpDelete("~/api/site/news/{id:guid}")]
    public Task<IActionResult> DeleteNewsAsync(Guid id, [FromQuery] string? by, CancellationToken cancellationToken)
        => this.ExecuteAsync("site_news_delete", new { id, by }, () => this._features.DeleteNewsAsync(id), cancellationToken);

    private static IActionResult ToResponse(PlayerSiteResult result)
    {
        var statusCode = result.Status switch
        {
            PlayerSiteResult.Ok => StatusCodes.Status200OK,
            PlayerSiteResult.Online => StatusCodes.Status409Conflict,
            PlayerSiteResult.NotFound => StatusCodes.Status404NotFound,
            PlayerSiteResult.InsufficientFunds => StatusCodes.Status402PaymentRequired,
            PlayerSiteResult.NoSpace => StatusCodes.Status507InsufficientStorage,
            _ => StatusCodes.Status400BadRequest,
        };
        return new ObjectResult(new { status = result.Status, message = result.Message, data = result.Data }) { StatusCode = statusCode };
    }

    private async Task<IActionResult> ExecuteAsync(string operation, object arguments, Func<Task<PlayerSiteResult>> change, CancellationToken cancellationToken)
    {
        var result = await this._executor.ExecuteAsync(
            AdminRoles.Operator,
            operation,
            arguments,
            async _ => await change().ConfigureAwait(false),
            cancellationToken,
            r => r.Succeeded).ConfigureAwait(false);
        return ToResponse(result);
    }

    /// <summary>
    /// The new password of a password change.
    /// </summary>
    public sealed class PasswordChange
    {
        /// <summary>
        /// Gets or sets the new password, with the same rules as for a new account.
        /// </summary>
        [Required]
        [MinLength(3)]
        [MaxLength(20)]
        [RegularExpression(@"^[\x20-\x7E]+$")]
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// The arguments of a character tool.
    /// </summary>
    public sealed class CharacterToolRequest
    {
        /// <summary>
        /// Gets or sets the points to add per stat (strength, agility, vitality, energy, command).
        /// </summary>
        public Dictionary<string, int>? Points { get; set; }
    }

    /// <summary>
    /// An item to list on the market.
    /// </summary>
    public sealed class MarketListingRequest
    {
        /// <summary>
        /// Gets or sets the vault slot of the item.
        /// </summary>
        public byte Slot { get; set; }

        /// <summary>
        /// Gets or sets the price.
        /// </summary>
        public int Price { get; set; }
    }

    /// <summary>
    /// A play of a mini game.
    /// </summary>
    public sealed class GamePlay
    {
        /// <summary>
        /// Gets or sets the bet; ignored by the games with a fixed price.
        /// </summary>
        public int Bet { get; set; }

        /// <summary>
        /// Gets or sets the choice of the player, e.g. the color of the color game (0 to 5).
        /// </summary>
        public int Choice { get; set; }
    }

    /// <summary>
    /// The settings of the mini games.
    /// </summary>
    public sealed class GameSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether the games are open.
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Gets or sets the configuration.
        /// </summary>
        [Required]
        public WebMiniGamesConfiguration Configuration { get; set; } = new();

        /// <summary>
        /// Gets or sets the login name of the administrator who changes the settings, for the audit trail.
        /// </summary>
        [MaxLength(10)]
        public string? ChangedBy { get; set; }
    }

    /// <summary>
    /// The settings of the Castle Siege point mode.
    /// </summary>
    public sealed class CastleSiegeSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether the point mode is on; off means the classic rules.
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Gets or sets the configuration.
        /// </summary>
        [Required]
        public CastleSiegePointModeConfiguration Configuration { get; set; } = new();

        /// <summary>
        /// Gets or sets the login name of the administrator who changes the settings, for the audit trail.
        /// </summary>
        [MaxLength(10)]
        public string? ChangedBy { get; set; }
    }

    /// <summary>
    /// Which item to move where.
    /// </summary>
    public sealed class ItemMove
    {
        /// <summary>
        /// Gets or sets the name of the character which has the item, or <c>null</c> for the vault.
        /// </summary>
        public string? From { get; set; }

        /// <summary>
        /// Gets or sets the slot of the item.
        /// </summary>
        public byte Slot { get; set; }

        /// <summary>
        /// Gets or sets the name of the target character, or <c>null</c> for the vault.
        /// </summary>
        public string? To { get; set; }
    }
}
