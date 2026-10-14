using NUnit.Framework;

public sealed class VictoryObjectiveContractsTests
{
    [Test]
    public void LandmarkRequiresOperationalBuildingAndCompletesImmediatelyWhenNoHold()
    {
        StrategicPlanningState state = State(BuildingEra.Castle, 20);
        VictoryObjectiveDefinition definition = Definition("landmark", VictoryObjectiveKind.Landmark, BuildingEra.Castle, "landmark_alpha", 0f);
        VictoryObjectiveProgress progress = new VictoryObjectiveProgress("landmark", 1);

        Assert.That(VictoryObjectiveRules.Evaluate(state, definition, progress), Is.EqualTo(VictoryObjectiveResult.MissingBuilding));
        state.buildings.Add(Building("landmark_alpha", BuildingLifecycleState.Operational));
        Assert.That(VictoryObjectiveRules.TryAdvance(state, definition, progress, 0f), Is.True);
        Assert.That(progress.completed, Is.True);
    }

    [Test]
    public void SacredObjectiveAccumulatesOnlyForMatchingControlledSite()
    {
        StrategicPlanningState state = State(BuildingEra.Feudal, 10);
        VictoryObjectiveDefinition definition = Definition("sacred", VictoryObjectiveKind.Sacred, BuildingEra.Feudal, "", 30f);
        definition.targetCode = "site-7";
        VictoryObjectiveProgress progress = new VictoryObjectiveProgress("sacred", 1);

        Assert.That(VictoryObjectiveRules.TryAdvance(state, definition, progress, 10f,
            new VictoryObjectiveObservation("other", true)), Is.False);
        Assert.That(progress.controlledSeconds, Is.EqualTo(0f));
        Assert.That(VictoryObjectiveRules.TryAdvance(state, definition, progress, 10f,
            new VictoryObjectiveObservation("site-7", true)), Is.False);
        Assert.That(progress.controlledSeconds, Is.EqualTo(10f));
        Assert.That(VictoryObjectiveRules.TryAdvance(state, definition, progress, 20f,
            new VictoryObjectiveObservation("site-7", true)), Is.True);
    }

    [Test]
    public void WonderHonorsTechnologyAndSupportingBuilding()
    {
        StrategicPlanningState state = State(BuildingEra.Imperial, 50);
        state.buildings.Add(Building("wonder", BuildingLifecycleState.Operational));
        state.buildings.Add(Building("keep", BuildingLifecycleState.Operational));
        VictoryObjectiveDefinition definition = Definition("wonder", VictoryObjectiveKind.Wonder, BuildingEra.Imperial, "wonder", 60f);
        definition.requiredBuildingCodes.Add("keep");
        definition.requiredTechnologyCodes.Add("engineering");
        VictoryObjectiveProgress progress = new VictoryObjectiveProgress("wonder", 1);

        Assert.That(VictoryObjectiveRules.Evaluate(state, definition, progress), Is.EqualTo(VictoryObjectiveResult.MissingTechnology));
        state.unlockedTechnologyCodes.Add("engineering");
        Assert.That(VictoryObjectiveRules.Evaluate(state, definition, progress), Is.EqualTo(VictoryObjectiveResult.InProgress));
        Assert.That(VictoryObjectiveRules.TryAdvance(state, definition, progress, 60f), Is.True);
    }

    [Test]
    public void ForeignOwnerCannotCompleteObjective()
    {
        StrategicPlanningState state = State(BuildingEra.Castle, 20);
        state.buildings.Add(Building("landmark", BuildingLifecycleState.Operational, 2));
        VictoryObjectiveDefinition definition = Definition("landmark", VictoryObjectiveKind.Landmark, BuildingEra.Castle, "landmark", 0f);
        VictoryObjectiveProgress progress = new VictoryObjectiveProgress("landmark", 1);
        Assert.That(VictoryObjectiveRules.Evaluate(state, definition, progress), Is.EqualTo(VictoryObjectiveResult.MissingBuilding));
        progress.ownerId = 2;
        Assert.That(VictoryObjectiveRules.Evaluate(state, definition, progress), Is.EqualTo(VictoryObjectiveResult.InvalidOwner));
    }

    private static StrategicPlanningState State(BuildingEra era, int population)
    {
        return new StrategicPlanningState { ownerId = 1, era = era, population = population };
    }

    private static VictoryObjectiveDefinition Definition(string code, VictoryObjectiveKind kind, BuildingEra era, string building, float hold)
    {
        return new VictoryObjectiveDefinition { code = code, kind = kind, minimumEra = era, requiredBuildingCode = building, requiredHoldSeconds = hold };
    }

    private static StrategicBuildingObservation Building(string code, BuildingLifecycleState lifecycle, int owner = 1)
    {
        return new StrategicBuildingObservation { code = code, lifecycle = lifecycle, ownerId = owner };
    }
}
