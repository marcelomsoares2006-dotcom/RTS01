using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BuildingEconomyRulesTests
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
    public void OperationalTownCenterProvidesCatalogPopulationCapacity()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        _created.Add(data);
        catalog.entries.Add(new BuildingContentSpec
        {
            code = "town_center", buildingData = data,
            status = BuildingContentStatus.Playable, populationCapacity = 10
        });
        StrategicPlanningState state = State();
        state.population = 9;
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational
        });

        Assert.That(BuildingEconomyRules.CalculatePopulationCapacity(catalog, state), Is.EqualTo(10));
        Assert.That(BuildingEconomyRules.EvaluatePopulationRoom(catalog, state, 1),
            Is.EqualTo(BuildingPopulationCapacityResult.Allowed));
        Assert.That(BuildingEconomyRules.EvaluatePopulationRoom(catalog, state, 2),
            Is.EqualTo(BuildingPopulationCapacityResult.CapacityExceeded));
    }

    [Test]
    public void PlannedOrForeignTownCenterDoesNotCreatePopulationCapacity()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        catalog.entries.Add(new BuildingContentSpec
        {
            code = "town_center", populationCapacity = 10,
            status = BuildingContentStatus.Planned
        });
        StrategicPlanningState state = State();
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 7, code = "town_center", lifecycle = BuildingLifecycleState.Operational
        });
        Assert.That(BuildingEconomyRules.CalculatePopulationCapacity(catalog, state), Is.EqualTo(0));
        Assert.That(BuildingEconomyRules.EvaluatePopulationRoom(catalog, state, 1),
            Is.EqualTo(BuildingPopulationCapacityResult.NoCapacity));
    }

    [Test]
    public void ResourceRoutingRequiresOwnedOperationalDropoffAndAcceptedType()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        catalog.entries.Add(new BuildingContentSpec
        {
            code = "resource_dropoff", supportsDropoff = true,
            dropoffResources = new List<EconomyResourceType>
            {
                EconomyResourceType.Food, EconomyResourceType.Wood
            }
        });
        StrategicPlanningState state = State();
        Assert.That(BuildingEconomyRules.EvaluateResourceRouting(
            catalog, state, "resource_dropoff", EconomyResourceType.Wood),
            Is.EqualTo(BuildingResourceRoutingResult.BuildingNotOperational));
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = state.ownerId, code = "resource_dropoff",
            lifecycle = BuildingLifecycleState.Operational
        });
        Assert.That(BuildingEconomyRules.EvaluateResourceRouting(
            catalog, state, "resource_dropoff", EconomyResourceType.Wood),
            Is.EqualTo(BuildingResourceRoutingResult.Accepted));
        Assert.That(BuildingEconomyRules.EvaluateResourceRouting(
            catalog, state, "resource_dropoff", EconomyResourceType.Gold),
            Is.EqualTo(BuildingResourceRoutingResult.UnsupportedResource));
    }

    private static StrategicPlanningState State()
    {
        return new StrategicPlanningState
        {
            ownerId = 0, population = 0,
            buildings = new List<StrategicBuildingObservation>()
        };
    }
}
