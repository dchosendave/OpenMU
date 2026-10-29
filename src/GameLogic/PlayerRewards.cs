// <copyright file="PlayerRewards.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Globalization;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Views.Inventory;

/// <summary>
/// Gives players rewards in the game: coins on their account, and items into their inventory (or at
/// their feet, for them only, when it's full).
/// </summary>
internal static class PlayerRewards
{
    /// <summary>
    /// Adds coins to the account of a player who is in the game.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="coinType">The coin type.</param>
    /// <param name="amount">The amount.</param>
    public static void AddCoins(Account account, CashShopCoinType coinType, int amount)
    {
        switch (coinType)
        {
            case CashShopCoinType.WCoinP:
                account.WCoinP += amount;
                break;
            case CashShopCoinType.GoblinPoints:
                account.GoblinPoints += amount;
                break;
            default:
                account.WCoinC += amount;
                break;
        }
    }

    /// <summary>
    /// Parses comma separated <c>group:number:level:count</c> entries, where the number may be a range
    /// (e.g. <c>12:112-117:0:10</c> for ten random items of 112 to 117).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The entries.</returns>
    public static IReadOnlyList<(byte Group, short FirstNumber, short LastNumber, byte Level, int Count)> ParseItems(string? text)
    {
        var items = new List<(byte, short, short, byte, int)>();
        foreach (var entry in (text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 4)
            {
                continue;
            }

            var numbers = parts[1].Split('-', StringSplitOptions.TrimEntries);
            if (byte.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var group)
                && short.TryParse(numbers[0], NumberStyles.None, CultureInfo.InvariantCulture, out var first)
                && short.TryParse(numbers[^1], NumberStyles.None, CultureInfo.InvariantCulture, out var last)
                && byte.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var level)
                && int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var count)
                && last >= first)
            {
                items.Add((group, first, last, level, Math.Clamp(count, 0, 100)));
            }
        }

        return items;
    }

    /// <summary>
    /// Gives the items of <see cref="ParseItems"/> to a player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="items">The items, as comma separated <c>group:number:level:count</c>.</param>
    /// <returns>The task.</returns>
    public static async ValueTask GiveItemsAsync(Player player, string? items)
    {
        var definitions = player.GameContext.Configuration.Items;
        foreach (var (group, first, last, level, count) in ParseItems(items))
        {
            for (var i = 0; i < count; i++)
            {
                var number = (short)Rand.NextInt(first, last + 1);
                if (definitions.FirstOrDefault(d => d.Group == group && d.Number == number) is not { } definition)
                {
                    continue;
                }

                var item = player.PersistenceContext.CreateNew<Item>();
                item.Definition = definition;
                item.Level = Math.Min(level, definition.MaximumItemLevel);
                item.Durability = item.GetMaximumDurabilityOfOnePiece();
                await GiveAsync(player, item).ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask GiveAsync(Player player, Item item)
    {
        if (player.Inventory is not null && await player.Inventory.AddItemAsync(item).ConfigureAwait(false))
        {
            await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
            return;
        }

        if (player.CurrentMap is { } map)
        {
            await map.AddAsync(new DroppedItem(item, player.RandomPosition, map, player, player.GetAsEnumerable())).ConfigureAwait(false);
        }
    }
}
