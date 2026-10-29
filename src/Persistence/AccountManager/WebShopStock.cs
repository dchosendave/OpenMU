// <copyright file="WebShopStock.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// How many pieces of a web shop package were sold in one week. Packages with a weekly stock
/// (<see cref="ItemPackage.WebShopWeeklyStock"/>) sell out until the next week starts.
/// </summary>
public class WebShopStock
{
    /// <summary>
    /// Gets or sets the identifier of the item package.
    /// </summary>
    public Guid ItemPackageId { get; set; }

    /// <summary>
    /// Gets or sets the start of the week (Monday, 00:00 UTC).
    /// </summary>
    public DateTime WeekStart { get; set; }

    /// <summary>
    /// Gets or sets how many pieces were sold in this week.
    /// </summary>
    public int Sold { get; set; }

    /// <summary>
    /// Gets the start of the week (Monday, 00:00 UTC) of a point in time.
    /// </summary>
    /// <param name="utcNow">The point in time, in UTC.</param>
    /// <returns>The start of its week.</returns>
    public static DateTime WeekStartOf(DateTime utcNow)
    {
        var daysSinceMonday = ((int)utcNow.DayOfWeek + 6) % 7;
        return DateTime.SpecifyKind(utcNow.Date.AddDays(-daysSinceMonday), DateTimeKind.Utc);
    }
}
