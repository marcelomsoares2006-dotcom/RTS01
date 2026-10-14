using System.Collections.Generic;
using NUnit.Framework;

public sealed class AgeUpTechnologyPlanningRulesTests
{
    [Test]
    public void MissingTownCenterIsCriticalConstructionIntent()
    {
        StrategicActionIntent intent;
        string rationale;
        Assert.That(AgeUpTechnologyPlanningRules.TryCreateIntent(State(), AgeUp(), null, out intent, out rationale), Is.True);
        Assert.That(intent.action, Is.EqualTo(StrategicActionKind.ConstructBuilding));
        Assert.That(intent.targetCode, Is.EqualTo("town_center"));
        Assert.That(intent.priority, Is.EqualTo(StrategicPriority.Critical));
    }

    [Test]
    public void MissingAgeUpBuildingPrecedesTechnologyAndEraAdvance()
    {
        StrategicPlanningState state = State();
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational });
        EraTransitionRequirement ageUp = AgeUp();
        ageUp.requiredBuildingCodes.Add("landmark_feudal");
        TechnologyDefinition tech = new TechnologyDefinition { code = "wheel", minimumEra = BuildingEra.Dark };
        StrategicActionIntent intent;
        string rationale;
        Assert.That(AgeUpTechnologyPlanningRules.TryCreateIntent(state, ageUp, new[] { tech }, out intent, out rationale), Is.True);
        Assert.That(intent.action, Is.EqualTo(StrategicActionKind.ConstructBuilding));
        Assert.That(intent.targetCode, Is.EqualTo("landmark_feudal"));
    }

    [Test]
    public void MissingTechnologyThenResearchesBeforeAdvancingEra()
    {
        StrategicPlanningState state = State();
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational });
        state.SetAmount(InGameResource.Wood, 100);
        EraTransitionRequirement ageUp = AgeUp();
        ageUp.requiredTechnologyCodes.Add("wheel");
        TechnologyDefinition tech = new TechnologyDefinition { code = "wheel", minimumEra = BuildingEra.Dark };
        tech.cost.Add(new ProgressionResourceRequirement(EconomyResourceType.Wood, 50));
        StrategicActionIntent intent;
        string rationale;
        Assert.That(AgeUpTechnologyPlanningRules.TryCreateIntent(state, ageUp, new[] { tech }, out intent, out rationale), Is.True);
        Assert.That(intent.action, Is.EqualTo(StrategicActionKind.ResearchTechnology));
        Assert.That(intent.targetCode, Is.EqualTo("wheel"));
        state.unlockedTechnologyCodes.Add("wheel");
        Assert.That(AgeUpTechnologyPlanningRules.TryCreateIntent(state, ageUp, new[] { tech }, out intent, out rationale), Is.True);
        Assert.That(intent.action, Is.EqualTo(StrategicActionKind.AdvanceEra));
    }

    [Test]
    public void InsufficientAgeUpResourcesDoNotEmitAdvanceIntent()
    {
        StrategicPlanningState state = State();
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational });
        EraTransitionRequirement ageUp = AgeUp();
        ageUp.cost.Add(new ProgressionResourceRequirement(EconomyResourceType.Food, 200));
        StrategicActionIntent intent;
        string rationale;
        Assert.That(AgeUpTechnologyPlanningRules.TryCreateIntent(state, ageUp, null, out intent, out rationale), Is.False);
        Assert.That(intent, Is.Null);
        Assert.That(rationale, Does.Contain("InsufficientResources"));
    }

    private static StrategicPlanningState State()
    {
        return new StrategicPlanningState { ownerId = 0, era = BuildingEra.Dark, population = 10, buildings = new List<StrategicBuildingObservation>() };
    }

    private static EraTransitionRequirement AgeUp()
    {
        return new EraTransitionRequirement(BuildingEra.Feudal) { minimumPopulation = 1 };
    }
}
