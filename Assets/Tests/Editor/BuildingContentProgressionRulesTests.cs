using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BuildingContentProgressionRulesTests
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
    public void PlannedBuildingIsVisibleToDesignButBlockedFromRuntimeConstruction()
    {
        BuildingContentCatalog catalog = CreateCatalog();
        catalog.entries.Add(new BuildingContentSpec { code = "archery_range", minimumEra = BuildingEra.Feudal });
        StrategicPlanningState state = State(BuildingEra.Feudal);

        Assert.That(BuildingContentProgressionRules.Validate(catalog, "archery_range", state),
            Is.EqualTo(BuildingAvailabilityResult.ContentUnavailable));
        Assert.That(BuildingContentProgressionRules.Validate(catalog, "archery_range", state, false),
            Is.EqualTo(BuildingAvailabilityResult.Available));
    }

    [Test]
    public void RuntimeReadyBuildingHonorsEraTechnologyAndOperationalOwnerRequirements()
    {
        BuildingContentCatalog catalog = CreateCatalog();
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        data.code = "university";
        _created.Add(data);
        BuildingContentSpec entry = new BuildingContentSpec
        {
            code = "university",
            minimumEra = BuildingEra.Castle,
            buildingData = data,
            status = BuildingContentStatus.Playable
        };
        entry.requirements.Add(new BuildingRequirement { technologyCode = "writing" });
        entry.requirements.Add(new BuildingRequirement { buildingCode = "town_center" });
        catalog.entries.Add(entry);
        StrategicPlanningState state = State(BuildingEra.Feudal);

        Assert.That(BuildingContentProgressionRules.Validate(catalog, "university", state),
            Is.EqualTo(BuildingAvailabilityResult.EraLocked));
        state.era = BuildingEra.Castle;
        Assert.That(BuildingContentProgressionRules.Validate(catalog, "university", state),
            Is.EqualTo(BuildingAvailabilityResult.TechnologyRequirement));
        state.unlockedTechnologyCodes.Add("writing");
        Assert.That(BuildingContentProgressionRules.Validate(catalog, "university", state),
            Is.EqualTo(BuildingAvailabilityResult.BuildingRequirement));
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = state.ownerId,
            code = "town_center",
            lifecycle = BuildingLifecycleState.Operational
        });
        Assert.That(BuildingContentProgressionRules.Validate(catalog, "university", state),
            Is.EqualTo(BuildingAvailabilityResult.Available));
    }

    [Test]
    public void ForeignOrIncompleteBuildingsDoNotSatisfyContentPrerequisites()
    {
        BuildingContentCatalog catalog = CreateCatalog();
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        data.code = "stable";
        _created.Add(data);
        BuildingContentSpec entry = new BuildingContentSpec
        {
            code = "stable", buildingData = data, status = BuildingContentStatus.Playable
        };
        entry.requirements.Add(new BuildingRequirement { buildingCode = "town_center" });
        catalog.entries.Add(entry);
        StrategicPlanningState state = State(BuildingEra.Feudal);
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 7, code = "town_center", lifecycle = BuildingLifecycleState.Operational });
        state.buildings.Add(new StrategicBuildingObservation { ownerId = state.ownerId, code = "town_center", lifecycle = BuildingLifecycleState.UnderConstruction });

        Assert.That(BuildingContentProgressionRules.Validate(catalog, "stable", state),
            Is.EqualTo(BuildingAvailabilityResult.BuildingRequirement));
    }

    [Test]
    public void ContentConstructionGateChecksDeclarativeCostAfterProgression()
    {
        BuildingContentCatalog catalog = CreateCatalog();
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        data.code = "house";
        _created.Add(data);
        catalog.entries.Add(new BuildingContentSpec
        {
            code = "house",
            buildingData = data,
            status = BuildingContentStatus.Playable,
            constructionCost = new List<BuildingContentCost>
            {
                new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 50 }
            }
        });
        StrategicPlanningState state = State(BuildingEra.Dark);
        Dictionary<InGameResource, int> stockpile = new Dictionary<InGameResource, int>
        {
            { InGameResource.Wood, 49 }
        };

        Assert.That(BuildingConstructionGate.Evaluate(catalog, "house", state, true, stockpile),
            Is.EqualTo(BuildingConstructionGateResult.InsufficientResources));
        stockpile[InGameResource.Wood] = 50;
        Assert.That(BuildingConstructionGate.Evaluate(catalog, "house", state, true, stockpile),
            Is.EqualTo(BuildingConstructionGateResult.Allowed));
    }

    [Test]
    public void ResolvedContractDetachesTownCenterCostAndDropoffDeclaration()
    {
        BuildingContentCatalog catalog = CreateCatalog();
        BuildingContentSpec townCenter = new BuildingContentSpec
        {
            code = "town_center",
            role = BuildingContentRole.TownCenter,
            status = BuildingContentStatus.Planned,
            minimumEra = BuildingEra.Dark,
            constructionCost = new List<BuildingContentCost>
            {
                new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 400 }
            },
            supportsDropoff = true,
            dropoffResources = new List<EconomyResourceType>
            {
                EconomyResourceType.Food, EconomyResourceType.Wood,
                EconomyResourceType.Gold, EconomyResourceType.Stone
            }
        };
        catalog.entries.Add(townCenter);
        StrategicPlanningState state = State(BuildingEra.Dark);
        BuildingConstructionContentContract contract;
        BuildingAvailabilityResult result;

        Assert.That(BuildingContentProgressionRules.TryResolveConstructionContract(
            catalog, "town_center", state, out contract, out result, false), Is.True);
        Assert.That(result, Is.EqualTo(BuildingAvailabilityResult.Available));
        Assert.That(contract.code, Is.EqualTo("town_center"));
        Assert.That(contract.constructionCost[0].amount, Is.EqualTo(400));
        CollectionAssert.AreEquivalent(townCenter.dropoffResources, contract.dropoffResources);

        contract.constructionCost[0].amount = 1;
        Assert.That(townCenter.constructionCost[0].amount, Is.EqualTo(400),
            "Resolved contracts must not mutate the catalog asset.");
    }

    private BuildingContentCatalog CreateCatalog()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        return catalog;
    }

    private static StrategicPlanningState State(BuildingEra era)
    {
        return new StrategicPlanningState
        {
            ownerId = 0,
            era = era,
            population = 10,
            unlockedTechnologyCodes = new List<string>(),
            buildings = new List<StrategicBuildingObservation>()
        };
    }
}
