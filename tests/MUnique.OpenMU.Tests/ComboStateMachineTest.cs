// <copyright file="ComboStateMachineTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using BasicModel = MUnique.OpenMU.Persistence.BasicModel;

/// <summary>
/// Tests for the <see cref="ComboStateMachine"/>.
/// </summary>
[TestFixture]
public class ComboStateMachineTest
{
    private readonly Skill _cyclone = new BasicModel.Skill { Number = 1 };
    private readonly Skill _twistingSlash = new BasicModel.Skill { Number = 2 };
    private readonly Skill _deathStab = new BasicModel.Skill { Number = 3 };
    private readonly Skill _buff = new BasicModel.Skill { Number = 4 };

    /// <summary>
    /// Tests that three skills of the steps in a row complete a combo.
    /// </summary>
    [Test]
    public async ValueTask CompletesComboAsync()
    {
        var combo = this.CreateCombo();

        Assert.That(await combo.RegisterSkillAsync(this._cyclone).ConfigureAwait(false), Is.False);
        Assert.That(await combo.RegisterSkillAsync(this._twistingSlash).ConfigureAwait(false), Is.False);
        Assert.That(await combo.RegisterSkillAsync(this._deathStab).ConfigureAwait(false), Is.True);
    }

    /// <summary>
    /// Tests that a skill which breaks the chain starts a new combo when it is a first step.
    /// </summary>
    [Test]
    public async ValueTask SkillBreakingTheChainStartsANewComboAsync()
    {
        var combo = this.CreateCombo();

        await combo.RegisterSkillAsync(this._cyclone).ConfigureAwait(false);
        await combo.RegisterSkillAsync(this._cyclone).ConfigureAwait(false); // breaks the chain, starts a new one
        Assert.That(await combo.RegisterSkillAsync(this._twistingSlash).ConfigureAwait(false), Is.False);
        Assert.That(await combo.RegisterSkillAsync(this._deathStab).ConfigureAwait(false), Is.True);
    }

    /// <summary>
    /// Tests that a skill which is no step resets the combo.
    /// </summary>
    [Test]
    public async ValueTask OtherSkillResetsComboAsync()
    {
        var combo = this.CreateCombo();

        await combo.RegisterSkillAsync(this._cyclone).ConfigureAwait(false);
        await combo.RegisterSkillAsync(this._twistingSlash).ConfigureAwait(false);
        await combo.RegisterSkillAsync(this._buff).ConfigureAwait(false);
        Assert.That(await combo.RegisterSkillAsync(this._deathStab).ConfigureAwait(false), Is.False);
    }

    private ComboStateMachine CreateCombo()
    {
        var definition = new BasicModel.SkillComboDefinition { Name = "Test", MaximumCompletionTime = TimeSpan.FromSeconds(30) };
        definition.Steps.Add(new BasicModel.SkillComboStep { Order = 1, Skill = this._cyclone });
        definition.Steps.Add(new BasicModel.SkillComboStep { Order = 2, Skill = this._twistingSlash });
        definition.Steps.Add(new BasicModel.SkillComboStep { Order = 2, Skill = this._deathStab });
        definition.Steps.Add(new BasicModel.SkillComboStep { Order = 3, Skill = this._twistingSlash, IsFinalStep = true });
        definition.Steps.Add(new BasicModel.SkillComboStep { Order = 3, Skill = this._deathStab, IsFinalStep = true });
        return ComboStateMachine.Create(definition);
    }
}
