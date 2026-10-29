// <copyright file="WebShopStockTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.Persistence.AccountManager;

/// <summary>
/// Tests the weeks of the web shop stock: they start on Monday, 00:00 UTC.
/// </summary>
[TestFixture]
public class WebShopStockTest
{
    /// <summary>
    /// Every day of a week belongs to the Monday which starts it.
    /// </summary>
    /// <param name="day">The day, as yyyy-MM-dd HH:mm.</param>
    [TestCase("2026-10-26 00:00")]
    [TestCase("2026-10-29 16:30")]
    [TestCase("2026-11-01 23:59")]
    public void DaysOfTheWeekStartOnMonday(string day)
    {
        var utc = DateTime.SpecifyKind(DateTime.Parse(day, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);
        var weekStart = WebShopStock.WeekStartOf(utc);
        Assert.That(weekStart, Is.EqualTo(new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc)));
        Assert.That(weekStart.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    /// <summary>
    /// The next Monday starts a new week, so the stock refills.
    /// </summary>
    [Test]
    public void MondayStartsANewWeek()
    {
        var monday = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
        Assert.That(WebShopStock.WeekStartOf(monday), Is.EqualTo(monday));
    }
}
