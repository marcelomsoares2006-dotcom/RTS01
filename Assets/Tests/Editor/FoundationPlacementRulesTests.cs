using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class FoundationPlacementRulesTests
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
    public void TownCenterPlacementAcceptsValidTerrainAndResources()
    {
        BuildingContentCatalog catalog = Catalog(Foundation("town_center", BuildingContentRole.TownCenter, 400, 10));
        StrategicPlanningState state = State(500);
        FoundationPlacementRequest request = Request("town_center", state);

        Assert.AreEqual(FoundationPlacementResult.Allowed,
            FoundationPlacementRules.Evaluate(catalog, request));
    }

    [Test]
    public void PlacementRejectsSteepTerrainAndInvalidFootprint()
    {
        BuildingContentCatalog catalog = Catalog(Foundation("town_center", BuildingContentRole.TownCenter, 100, 10));
        StrategicPlanningState state = State(100);
        FoundationPlacementRequest request = Request("town_center", state);
        request.terrainNormal = Vector3.forward;
        Assert.AreEqual(FoundationPlacementResult.TerrainTooSteep,
            FoundationPlacementRules.Evaluate(catalog, request));

        request.terrainNormal = Vector3.up;
        request.footprintRadius = 0f;
        Assert.AreEqual(FoundationPlacementResult.InvalidFootprint,
            FoundationPlacementRules.Evaluate(catalog, request));
    }

    [Test]
    public void PlacementRejectsResourceShortageAndReservedResources()
    {
        BuildingContentCatalog catalog = Catalog(Foundation("town_center", BuildingContentRole.TownCenter, 100, 10));
        StrategicPlanningState state = State(100);
        state.SetAmount(InGameResource.Wood, 100, 1);
        Assert.AreEqual(FoundationPlacementResult.InsufficientResources,
            FoundationPlacementRules.Evaluate(catalog, Request("town_center", state)));
    }

    [Test]
    public void DropoffRequiresCoverageAndIsBlockedByNearbyFootprint()
    {
        BuildingContentSpec dropoff = Foundation("resource_dropoff", BuildingContentRole.ResourceDropoff, 50, 0);
        BuildingContentCatalog catalog = Catalog(dropoff);
        StrategicPlanningState state = State(50);
        FoundationPlacementRequest request = Request("resource_dropoff", state);
        request.existingBuildingPositions = new[] { Vector3.zero };
        request.position = new Vector3(1f, 0f, 0f);
        request.footprintRadius = 2f;
        request.minimumDistance = 1f;
        Assert.AreEqual(FoundationPlacementResult.BlockedByBuilding,
            FoundationPlacementRules.Evaluate(catalog, request));

        dropoff.supportsDropoff = false;
        request.existingBuildingPositions = null;
        Assert.AreEqual(FoundationPlacementResult.MissingDropoffCoverage,
            FoundationPlacementRules.Evaluate(catalog, request));
    }

    [Test]
    public void PlannedFoundationsCanBeGeometricallyAuditedWithoutRuntimeAssets()
    {
        BuildingContentCatalog catalog = Catalog(Foundation("resource_dropoff", BuildingContentRole.ResourceDropoff, 50, 0));
        StrategicPlanningState state = State(50);
        FoundationPlacementRequest request = Request("resource_dropoff", state);
        Assert.AreEqual(FoundationPlacementResult.Allowed,
            FoundationPlacementRules.Evaluate(catalog, request));
    }

    private StrategicPlanningState State(int wood)
    {
        StrategicPlanningState state = new StrategicPlanningState { ownerId = 0, era = BuildingEra.Dark };
        state.SetAmount(InGameResource.Wood, wood);
        return state;
    }

    private FoundationPlacementRequest Request(string code, StrategicPlanningState state)
    {
        return new FoundationPlacementRequest
        {
            code = code,
            state = state,
            terrainHit = true,
            terrainNormal = Vector3.up,
            footprintRadius = 2f,
            position = Vector3.zero
        };
    }

    private BuildingContentCatalog Catalog(params BuildingContentSpec[] entries)
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        catalog.entries = new List<BuildingContentSpec>(entries);
        _created.Add(catalog);
        return catalog;
    }

    private BuildingContentSpec Foundation(string code, BuildingContentRole role, int wood, int capacity)
    {
        return new BuildingContentSpec
        {
            code = code,
            role = role,
            minimumEra = BuildingEra.Dark,
            constructionCost = new List<BuildingContentCost>
            {
                new BuildingContentCost { resource = EconomyResourceType.Wood, amount = wood }
            },
            supportsDropoff = true,
            dropoffResources = new List<EconomyResourceType>
            {
                EconomyResourceType.Wood,
                EconomyResourceType.Gold,
                EconomyResourceType.Stone
            },
            populationCapacity = capacity
        };
    }
}
