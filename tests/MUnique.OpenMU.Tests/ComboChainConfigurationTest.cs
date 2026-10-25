// <copyright file="ComboChainConfigurationTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic.PlayerActions.Skills;
using NUnit.Framework;

/// <summary>
/// Tests for the follow-up combo rolls of the <see cref="ComboChainConfiguration"/>.
/// </summary>
[TestFixture]
public class ComboChainConfigurationTest
{
    /// <summary>
    /// Tests that all follow-ups trigger in order with their damage factors when every roll succeeds.
    /// </summary>
    [Test]
    public void RollFollowUps_AllRollsSucceed_ReturnsDecreasingFactors()
    {
        var configuration = new ComboChainConfiguration();

        var factors = configuration.RollFollowUps(1000, 0.1f, _ => true);

        Assert.That(factors, Is.EqualTo(new[] { 0.6, 0.4, 0.25 }));
    }

    /// <summary>
    /// Tests that no follow-up triggers when the second combo fails, even if later rolls would succeed.
    /// </summary>
    [Test]
    public void RollFollowUps_SecondComboFails_ReturnsNone()
    {
        var configuration = new ComboChainConfiguration { SecondComboChance = 0 };

        var factors = configuration.RollFollowUps(32767, 1, chance => chance > 0);

        Assert.That(factors, Is.Empty);
    }

    /// <summary>
    /// Tests that the fourth combo is only rolled after a third combo.
    /// </summary>
    [Test]
    public void RollFollowUps_ThirdComboFails_StopsAfterSecond()
    {
        var configuration = new ComboChainConfiguration();
        var rolls = new Queue<bool>(new[] { true, false, true });

        var factors = configuration.RollFollowUps(1000, 0.1f, _ => rolls.Dequeue());

        Assert.That(factors, Is.EqualTo(new[] { 0.6 }));
        Assert.That(rolls, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Tests that the third combo chance grows with agility up to its maximum.
    /// </summary>
    [Test]
    public void RollFollowUps_ThirdComboChance_ScalesWithAgilityUpToMaximum()
    {
        var configuration = new ComboChainConfiguration();

        Assert.That(GetRolledChances(configuration, 0, 0)[1], Is.EqualTo(0.15).Within(1e-9));
        Assert.That(GetRolledChances(configuration, 10000, 0)[1], Is.EqualTo(0.35).Within(1e-9));
        Assert.That(GetRolledChances(configuration, 32767, 0)[1], Is.EqualTo(0.65).Within(1e-9));
    }

    /// <summary>
    /// Tests that the fourth combo chance grows with the excellent damage chance up to its maximum.
    /// </summary>
    [Test]
    public void RollFollowUps_FourthComboChance_ScalesWithExcellentChanceUpToMaximum()
    {
        var configuration = new ComboChainConfiguration();

        Assert.That(GetRolledChances(configuration, 0, 0)[2], Is.EqualTo(0.02).Within(1e-6));
        Assert.That(GetRolledChances(configuration, 0, 0.1f)[2], Is.EqualTo(0.07).Within(1e-6));
        Assert.That(GetRolledChances(configuration, 0, 1)[2], Is.EqualTo(0.08).Within(1e-6));
    }

    private static List<double> GetRolledChances(ComboChainConfiguration configuration, float agility, float excellentChance)
    {
        var chances = new List<double>();
        configuration.RollFollowUps(agility, excellentChance, chance =>
        {
            chances.Add(chance);
            return true;
        });

        return chances;
    }
}
