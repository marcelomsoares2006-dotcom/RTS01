using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class ConstructionPlanningRulesTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object asset in _created)
            if (asset != null) Object.DestroyImmediate(asset);
        _created.Clear();
    }

    [Test]
    public void PlannerChoosesTownCenterBeforeOtherRoles()
    {
        BuildingContentCatalog catalog = Catalog(
            Spec("barracks", BuildingContentRole.Production, BuildingEra.Dark, 150),
            Spec("resource_dropoff", BuildingContentRole.ResourceDropoff, BuildingEra.Dark, 50),
            Spec("town_center", BuildingContentRole.TownCenter, BuildingEra.Dark, 400));
        StrategicPlanningState state = State(BuildingEra.Dark, 0, 500);

        StrategicActionIntent intent;
        string rationale;
        Assert.IsTrue(ConstructionPlanningRules.TryCreateIntent(state, catalog, out intent, out rationale));
        Assert.AreEqual("town_center", intent.targetCode);
        Assert.AreEqual(StrategicPriority.Critical, intent.priority);
    }

    [Test]
    public void PlannerChoosesDropoffAfterTownCenterAndRespectsRequirements()
    {
        BuildingContentSpec townCenter = Spec("town_center", BuildingContentRole.TownCenter, BuildingEra.Dark, 400);
        BuildingContentSpec dropoff = Spec("resource_dropoff", BuildingContentRole.ResourceDropoff, BuildingEra.Dark, 50);
        dropoff.requirements.Add(new BuildingRequirement { buildingCode = "town_center" });
        BuildingContentCatalog catalog = Catalog(townCenter, dropoff);
        StrategicPlanningState state = State(BuildingEra.Dark, 0, 50);
        state.buildings.Add(new StrategicBuildingObservation { code = "town_center", ownerId = 0, lifecycle = BuildingLifecycleState.Operational });

        StrategicActionIntent intent;
        string rationale;
        Assert.IsTrue(ConstructionPlanningRules.TryCreateIntent(state, catalog, out intent, out rationale));
        Assert.AreEqual("resource_dropoff", intent.targetCode);
    }

    [Test]
    public void PlannerChoosesProductionByStablePreferenceAndEra()
    {
        BuildingContentCatalog catalog = Catalog(
            Spec("stable", BuildingContentRole.Production, BuildingEra.Feudal, 150),
            Spec("archery_range", BuildingContentRole.Production, BuildingEra.Feudal, 150),
            Spec("barracks", BuildingContentRole.Production, BuildingEra.Dark, 150),
            Spec("town_center", BuildingContentRole.TownCenter, BuildingEra.Dark, 400),
            Spec("resource_dropoff", BuildingContentRole.ResourceDropoff, BuildingEra.Dark, 50));
        StrategicPlanningState state = State(BuildingEra.Dark, 0, 150);
        state.buildings.Add(new StrategicBuildingObservation { code = "town_center", ownerId = 0 });
        state.buildings.Add(new StrategicBuildingObservation { code = "resource_dropoff", ownerId = 0 });

        StrategicActionIntent intent;
        string rationale;
        Assert.IsTrue(ConstructionPlanningRules.TryCreateIntent(state, catalog, out intent, out rationale));
        Assert.AreEqual("barracks", intent.targetCode);

        state.era = BuildingEra.Feudal;
        state.buildings.Add(new StrategicBuildingObservation { code = "barracks", ownerId = 0 });
        Assert.IsTrue(ConstructionPlanningRules.TryCreateIntent(state, catalog, out intent, out rationale));
        Assert.AreEqual("archery_range", intent.targetCode);
    }

    [Test]
    public void BasicPlannerAddsAutomaticConstructionWithoutMutatingGoals()
    {
        BuildingContentCatalog catalog = Catalog(Spec("town_center", BuildingContentRole.TownCenter, BuildingEra.Dark, 400));
        StrategicPlanningState state = State(BuildingEra.Dark, 0, 400);
        StrategicPlan plan = new BasicStrategicPlanner(catalog).Plan(state);
        Assert.AreEqual(1, plan.intents.Count);
        Assert.AreEqual(StrategicActionKind.ConstructBuilding, plan.FirstAction.action);
        Assert.AreEqual("town_center", plan.FirstAction.targetCode);
        Assert.AreEqual(0, state.goals.Count);
    }

    [Test]
    public void PlannerUsesAvailableResourcesAndAggregatesDuplicateCosts()
    {
        BuildingContentSpec townCenter = Spec("town_center", BuildingContentRole.TownCenter, BuildingEra.Dark, 60);
        townCenter.constructionCost.Add(new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 60 });
        BuildingContentCatalog catalog = Catalog(townCenter);
        StrategicPlanningState state = State(BuildingEra.Dark, 0, 150);
        state.SetAmount(InGameResource.Wood, 150, 35);

        StrategicActionIntent intent;
        string rationale;
        Assert.IsFalse(ConstructionPlanningRules.TryCreateIntent(state, catalog, out intent, out rationale));
        Assert.IsNull(intent);
    }

    private StrategicPlanningState State(BuildingEra era, int owner, int wood)
    {
        StrategicPlanningState state = new StrategicPlanningState { era = era, ownerId = owner };
        state.SetAmount(InGameResource.Wood, wood);
        return state;
    }

    private BuildingContentCatalog Catalog(params BuildingContentSpec[] specs)
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        catalog.entries = new List<BuildingContentSpec>(specs);
        _created.Add(catalog);
        return catalog;
    }

    private BuildingContentSpec Spec(string code, BuildingContentRole role, BuildingEra era, int wood)
    {
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        data.code = code;
        _created.Add(data);
        BuildingContentSpec spec = new BuildingContentSpec
        {
            code = code,
            role = role,
            status = BuildingContentStatus.Playable,
            minimumEra = era,
            buildingData = data,
            constructionCost = new List<BuildingContentCost>
            {
                new BuildingContentCost { resource = EconomyResourceType.Wood, amount = wood }
            }
        };
        return spec;
    }
}
