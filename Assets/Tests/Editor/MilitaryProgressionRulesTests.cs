using System.Collections.Generic;
using NUnit.Framework;

public sealed class MilitaryProgressionRulesTests
{
    [Test]
    public void ArcheryAndStableRequireFeudalTownCenterBeforeUnlock()
    {
        StrategicPlanningState state = State(BuildingEra.Dark);
        Assert.That(MilitaryProgressionRules.EvaluateMilitaryUnlock(state, "archery_range"),
            Is.EqualTo(ProgressionRequirementResult.EraLocked));
        state.era = BuildingEra.Feudal;
        Assert.That(MilitaryProgressionRules.EvaluateMilitaryUnlock(state, "archery_range"),
            Is.EqualTo(ProgressionRequirementResult.MissingBuilding));
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = state.ownerId, code = "town_center",
            lifecycle = BuildingLifecycleState.Operational
        });
        Assert.That(MilitaryProgressionRules.TryUnlockMilitaryTechnology(state, "archery_range"), Is.True);
        Assert.That(state.unlockedTechnologyCodes, Does.Contain("fletching"));
        Assert.That(MilitaryProgressionRules.TryUnlockMilitaryTechnology(state, "stable"), Is.True);
        Assert.That(state.unlockedTechnologyCodes, Does.Contain("riding"));
    }

    [Test]
    public void UnknownOrDarkAgeBarracksDoesNotInventTechnology()
    {
        StrategicPlanningState state = State(BuildingEra.Dark);
        Assert.That(MilitaryProgressionRules.EvaluateMilitaryUnlock(state, "barracks"),
            Is.EqualTo(ProgressionRequirementResult.InvalidRequirement));
        Assert.That(MilitaryProgressionRules.TryUnlockMilitaryTechnology(state, "barracks"), Is.False);
        Assert.That(state.unlockedTechnologyCodes, Is.Empty);
    }

    [Test]
    public void ForeignOrIncompleteTownCenterCannotUnlockMilitaryTechnology()
    {
        StrategicPlanningState state = State(BuildingEra.Feudal);
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 9, code = "town_center", lifecycle = BuildingLifecycleState.Operational
        });
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = state.ownerId, code = "town_center", lifecycle = BuildingLifecycleState.UnderConstruction
        });
        Assert.That(MilitaryProgressionRules.TryUnlockMilitaryTechnology(state, "stable"), Is.False);
        Assert.That(state.unlockedTechnologyCodes, Is.Empty);
    }

    private static StrategicPlanningState State(BuildingEra era)
    {
        return new StrategicPlanningState
        {
            ownerId = 0, era = era, population = 10,
            buildings = new List<StrategicBuildingObservation>(),
            unlockedTechnologyCodes = new List<string>()
        };
    }
}
