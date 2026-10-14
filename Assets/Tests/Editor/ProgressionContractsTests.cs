using NUnit.Framework;
using System.Collections.Generic;

public sealed class ProgressionContractsTests
{
    [Test] public void EraRequiresSequentialTransitionAndOperationalLandmark()
    {
        StrategicPlanningState state = State(BuildingEra.Dark, 20);
        EraTransitionRequirement req = new EraTransitionRequirement(BuildingEra.Feudal) { landmarkCode = "landmark" };
        Assert.That(EraProgressionRules.Evaluate(state, req), Is.EqualTo(ProgressionRequirementResult.MissingLandmark));
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 1, code = "landmark", lifecycle = BuildingLifecycleState.Operational });
        Assert.That(EraProgressionRules.Evaluate(state, req), Is.EqualTo(ProgressionRequirementResult.MissingLandmark));
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "landmark", lifecycle = BuildingLifecycleState.Operational });
        Assert.That(EraProgressionRules.TryAdvance(state, req), Is.True);
        Assert.That(state.era, Is.EqualTo(BuildingEra.Feudal));
        Assert.That(EraProgressionRules.TryAdvance(state, req), Is.False);
    }
    [Test] public void TechnologyChecksPrerequisitesBuildingsAndCostBeforeUnlock()
    {
        StrategicPlanningState state = State(BuildingEra.Feudal, 10); state.SetAmount(InGameResource.Wood, 100);
        TechnologyDefinition tech = new TechnologyDefinition { code = "masonry", minimumEra = BuildingEra.Feudal };
        tech.prerequisiteTechnologyCodes.Add("wheel"); tech.requiredBuildingCodes.Add("blacksmith"); tech.cost.Add(new ProgressionResourceRequirement(EconomyResourceType.Wood, 50));
        Assert.That(TechnologyProgressionRules.Evaluate(state, tech), Is.EqualTo(ProgressionRequirementResult.MissingTechnology));
        state.unlockedTechnologyCodes.Add("wheel"); Assert.That(TechnologyProgressionRules.Evaluate(state, tech), Is.EqualTo(ProgressionRequirementResult.MissingBuilding));
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "blacksmith", lifecycle = BuildingLifecycleState.Operational });
        Assert.That(TechnologyProgressionRules.TryUnlock(state, tech), Is.True);
        Assert.That(TechnologyProgressionRules.Evaluate(state, tech), Is.EqualTo(ProgressionRequirementResult.AlreadyAtOrAboveEra));
    }
    [Test] public void FailedEraRequirementDoesNotMutatePlanningState()
    { StrategicPlanningState state = State(BuildingEra.Dark, 1); Assert.That(EraProgressionRules.TryAdvance(state, new EraTransitionRequirement(BuildingEra.Feudal) { minimumPopulation = 2 }), Is.False); Assert.That(state.era, Is.EqualTo(BuildingEra.Dark)); }

    [Test] public void ConstructionAdapterUsesOperationalOwnedBuildingsAndPlanningEra()
    {
        BuildingData data = UnityEngine.ScriptableObject.CreateInstance<BuildingData>();
        BuildingCatalogData catalog = UnityEngine.ScriptableObject.CreateInstance<BuildingCatalogData>();
        catalog.buildingData = data; data.code = "university"; catalog.minimumEra = BuildingEra.Castle;
        StrategicPlanningState state = State(BuildingEra.Feudal, 12);
        BuildingCatalogResolver resolver = new BuildingCatalogResolver(new[] { catalog }, new[] { data });
        Assert.That(ProgressionConstructionRules.Validate(resolver, "university", state), Is.EqualTo(BuildingAvailabilityResult.EraLocked));
        state.era = BuildingEra.Castle;
        Assert.That(ProgressionConstructionRules.Validate(resolver, "university", state), Is.EqualTo(BuildingAvailabilityResult.Available));
        UnityEngine.Object.DestroyImmediate(catalog); UnityEngine.Object.DestroyImmediate(data);
    }
    private static StrategicPlanningState State(BuildingEra era, int population) { return new StrategicPlanningState { ownerId = 0, era = era, population = population, buildings = new List<StrategicBuildingObservation>() }; }
}
