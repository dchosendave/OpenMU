// <copyright file="ConfigurationWriteOperations.Spawns.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Operations;

using System.Text.Json.Nodes;
using System.Threading;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.AdminAuth;

/// <content>Adding and removing monster spawn areas.</content>
public sealed partial class ConfigurationWriteOperations
{
    /// <summary>The highest accepted quantity of a spawn area. It only prevents obvious mistakes.</summary>
    internal const int MaximumSpawnQuantity = 1000;

    private const string SpawnCreatedNote = "A new spawn area appears after the game server restarts; changes of existing spawn areas, including deleting them, apply immediately.";

    /// <summary>Adds a monster spawn area to a map.</summary>
    /// <param name="mapId">The map identifier (a GameMapDefinition from config_query).</param>
    /// <param name="monsterId">The monster identifier (a MonsterDefinition from config_query).</param>
    /// <param name="x1">The lowest x coordinate.</param>
    /// <param name="y1">The lowest y coordinate.</param>
    /// <param name="x2">The highest x coordinate.</param>
    /// <param name="y2">The highest y coordinate.</param>
    /// <param name="quantity">The number of monsters.</param>
    /// <param name="maximumHealthOverride">The maximum health of the spawned monsters, or <c>null</c> for the monster's own.</param>
    /// <param name="direction">The direction the monsters face, or <c>null</c> for Undefined.</param>
    /// <param name="spawnTrigger">The spawn trigger, or <c>null</c> for Automatic.</param>
    /// <param name="reason">The audit reason.</param>
    /// <param name="apply">Whether to apply a previewed change.</param>
    /// <param name="confirmationToken">The token of the preview, when applying.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The preview or the applied changes.</returns>
    [AdminOperation(AdminRole.Administrator, ReadOnly = false)]
    public ValueTask<JsonObject> CreateSpawnAsync(Guid mapId, Guid monsterId, int x1, int y1, int x2, int y2, int quantity, int? maximumHealthOverride, string? direction, string? spawnTrigger, string reason, bool apply, string? confirmationToken, CancellationToken cancellationToken)
    {
        foreach (var (name, value) in new[] { (nameof(x1), x1), (nameof(y1), y1), (nameof(x2), x2), (nameof(y2), y2) })
        {
            if (value is < byte.MinValue or > byte.MaxValue)
            {
                throw new ArgumentException($"{name} must be a map coordinate between 0 and 255.", name);
            }
        }

        if (x1 > x2 || y1 > y2)
        {
            throw new ArgumentException("x1 must be at most x2, and y1 at most y2.", nameof(x1));
        }

        if (quantity is < 1 or > MaximumSpawnQuantity)
        {
            throw new ArgumentException($"quantity must be between 1 and {MaximumSpawnQuantity}.", nameof(quantity));
        }

        if (maximumHealthOverride is <= 0)
        {
            throw new ArgumentException("maximumHealthOverride must be greater than 0, or omitted for the monster's own health.", nameof(maximumHealthOverride));
        }

        var parsedDirection = ParseEnum(direction, Direction.Undefined, nameof(direction));
        var parsedTrigger = ParseEnum(spawnTrigger, SpawnTrigger.Automatic, nameof(spawnTrigger));
        return this.RunAsync(
            "spawns_create",
            new { mapId, monsterId, x1, y1, x2, y2, quantity, maximumHealthOverride, direction = parsedDirection.ToString(), spawnTrigger = parsedTrigger.ToString() },
            reason,
            apply,
            confirmationToken,
            (configuration, context) =>
            {
                var map = ConfigurationModel.Find(configuration, typeof(GameMapDefinition), mapId) as GameMapDefinition
                    ?? throw new ArgumentException("Map not found. Use config_query on GameMapDefinition to find its identifier.", nameof(mapId));
                var monster = ConfigurationModel.Find(configuration, typeof(MonsterDefinition), monsterId) as MonsterDefinition
                    ?? throw new ArgumentException("Monster not found. Use config_query on MonsterDefinition to find its identifier.", nameof(monsterId));
                var summary = DescribeSpawn(monster, (byte)x1, (byte)y1, (byte)x2, (byte)y2, (short)quantity, maximumHealthOverride, parsedDirection, parsedTrigger);
                var change = new ConfigurationFieldChange($"{nameof(GameMapDefinition)} '{ConfigurationModel.Caption(map)}'", nameof(GameMapDefinition.MonsterSpawns), "Monster spawns (added)", null, summary);
                MonsterSpawnArea? created = null;
                return
                [
                    new PlannedConfigurationChange(
                        change,
                        () =>
                        {
                            created = context.CreateNew<MonsterSpawnArea>();
                            created.GameMap = map;
                            created.MonsterDefinition = monster;
                            created.X1 = (byte)x1;
                            created.Y1 = (byte)y1;
                            created.X2 = (byte)x2;
                            created.Y2 = (byte)y2;
                            created.Quantity = (short)quantity;
                            created.MaximumHealthOverride = maximumHealthOverride;
                            created.Direction = parsedDirection;
                            created.SpawnTrigger = parsedTrigger;
                            map.MonsterSpawns.Add(created);
                        },
                        () =>
                        {
                            if (created is not null)
                            {
                                map.MonsterSpawns.Remove(created);
                                context.Detach(created);
                            }
                        }),
                ];
            },
            cancellationToken,
            SpawnCreatedNote);
    }

    /// <summary>Removes a monster spawn area from its map. Running servers remove its monsters immediately.</summary>
    /// <param name="spawnId">The spawn area identifier (a MonsterSpawnArea from config_query).</param>
    /// <param name="reason">The audit reason.</param>
    /// <param name="apply">Whether to apply a previewed change.</param>
    /// <param name="confirmationToken">The token of the preview, when applying.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The preview or the applied changes.</returns>
    [AdminOperation(AdminRole.Administrator, ReadOnly = false)]
    public ValueTask<JsonObject> DeleteSpawnAsync(Guid spawnId, string reason, bool apply, string? confirmationToken, CancellationToken cancellationToken)
        => this.RunAsync(
            "spawns_delete",
            new { spawnId },
            reason,
            apply,
            confirmationToken,
            (configuration, context) =>
            {
                var area = ConfigurationModel.Find(configuration, typeof(MonsterSpawnArea), spawnId) as MonsterSpawnArea
                    ?? throw new ArgumentException("Spawn area not found. Use config_query on MonsterSpawnArea to find its identifier.", nameof(spawnId));
                var map = configuration.Maps.FirstOrDefault(m => m.MonsterSpawns.Contains(area))
                    ?? throw new ArgumentException("This spawn area doesn't belong to a map, e.g. it belongs to a mini game wave; change it in the admin panel.", nameof(spawnId));
                var summary = DescribeSpawn(area.MonsterDefinition, area.X1, area.Y1, area.X2, area.Y2, area.Quantity, area.MaximumHealthOverride, area.Direction, area.SpawnTrigger);
                var change = new ConfigurationFieldChange($"{nameof(GameMapDefinition)} '{ConfigurationModel.Caption(map)}'", nameof(GameMapDefinition.MonsterSpawns), "Monster spawns (removed)", summary, null);
                return
                [
                    new PlannedConfigurationChange(
                        change,
                        () => map.MonsterSpawns.Remove(area),
                        () => map.MonsterSpawns.Add(area),
                        async () => await context.DeleteAsync(area).ConfigureAwait(false)),
                ];
            },
            cancellationToken);

    private static TEnum ParseEnum<TEnum>(string? value, TEnum defaultValue, string parameterName)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new ArgumentException($"{parameterName} must be one of {string.Join(", ", Enum.GetNames<TEnum>())}.", parameterName);
    }

    private static JsonObject DescribeSpawn(MonsterDefinition? monster, byte x1, byte y1, byte x2, byte y2, short quantity, int? maximumHealthOverride, Direction direction, SpawnTrigger spawnTrigger) => new()
    {
        ["monster"] = monster is null ? null : ConfigurationModel.ToJson(monster),
        ["quantity"] = quantity,
        ["x1"] = x1,
        ["y1"] = y1,
        ["x2"] = x2,
        ["y2"] = y2,
        ["maximumHealthOverride"] = maximumHealthOverride,
        ["direction"] = direction.ToString(),
        ["spawnTrigger"] = spawnTrigger.ToString(),
    };
}
