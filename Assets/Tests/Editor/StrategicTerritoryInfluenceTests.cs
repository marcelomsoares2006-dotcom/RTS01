using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class StrategicTerritoryInfluenceTests
{
    [Test]
    public void CatalogAwareResolutionIgnoresPlannedBuildingsThatHaveNoRuntimeAsset()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        try
        {
            catalog.entries.Add(new BuildingContentSpec { code = "InfluenceBuilding", status = BuildingContentStatus.Planned });
            StrategicPlanningState state = new StrategicPlanningState();
            state.buildings.Add(Building(1, Vector3.zero, 5f));

            StrategicTerritoryInfluence result = StrategicTerritoryInfluenceResolver.EvaluateAt(catalog, state, Vector3.zero);
            Assert.AreEqual(StrategicTerritoryControl.Neutral, result.control);
            Assert.AreEqual(0, result.contributingBuildings);
        }
        finally
        {
            Object.DestroyImmediate(catalog);
        }
    }

    private static StrategicBuildingObservation Building(int owner, Vector3 position, float radius, BuildingLifecycleState lifecycle = BuildingLifecycleState.Operational)
    {
        return new StrategicBuildingObservation
        {
            ownerId = owner,
            position = position,
            influenceRadius = radius,
            lifecycle = lifecycle,
            code = "InfluenceBuilding"
        };
    }

    [Test]
    public void OnlyOperationalBuildingsInsideRadiusContribute()
    {
        StrategicPlanningState state = new StrategicPlanningState();
        state.buildings.Add(Building(1, Vector3.zero, 5f));
        state.buildings.Add(Building(1, new Vector3(2f, 0f, 0f), 5f, BuildingLifecycleState.Damaged));
        state.buildings.Add(Building(1, new Vector3(1f, 0f, 0f), 0.5f));
        StrategicTerritoryInfluence result = StrategicTerritoryInfluenceResolver.EvaluateAt(state, Vector3.zero);
        Assert.AreEqual(StrategicTerritoryControl.Controlled, result.control);
        Assert.AreEqual(1, result.contributingBuildings);
        Assert.AreEqual(1, result.ownerId);
    }

    [Test]
    public void EqualOwnerScoresProduceContestedTerritory()
    {
        StrategicPlanningState state = new StrategicPlanningState();
        state.buildings.Add(Building(1, new Vector3(-2f, 0f, 0f), 5f));
        state.buildings.Add(Building(2, new Vector3(2f, 0f, 0f), 5f));
        StrategicTerritoryInfluence result = StrategicTerritoryInfluenceResolver.EvaluateAt(state, Vector3.zero);
        Assert.AreEqual(StrategicTerritoryControl.Contested, result.control);
        Assert.AreEqual(-1, result.ownerId);
    }

    [Test]
    public void StrongerOwnerControlsOverlap()
    {
        StrategicPlanningState state = new StrategicPlanningState();
        state.buildings.Add(Building(1, Vector3.zero, 6f));
        state.buildings.Add(Building(2, new Vector3(4f, 0f, 0f), 4f));
        StrategicTerritoryInfluence result = StrategicTerritoryInfluenceResolver.EvaluateAt(state, Vector3.zero);
        Assert.AreEqual(StrategicTerritoryControl.Controlled, result.control);
        Assert.AreEqual(1, result.ownerId);
        Assert.Greater(result.winningScore, result.opposingScore);
    }

    [Test]
    public void InvalidOwnersAndZeroRadiusAreIgnored()
    {
        StrategicPlanningState state = new StrategicPlanningState();
        state.buildings.Add(Building(-1, Vector3.zero, 8f));
        state.buildings.Add(Building(2, Vector3.zero, 0f));
        StrategicTerritoryInfluence result = StrategicTerritoryInfluenceResolver.EvaluateAt(state, Vector3.zero);
        Assert.AreEqual(StrategicTerritoryControl.Neutral, result.control);
        Assert.AreEqual(0, result.contributingBuildings);
    }

    [Test]
    public void ResultIsIndependentOfBuildingListOrder()
    {
        StrategicBuildingObservation a = Building(4, new Vector3(-1f, 0f, 0f), 5f);
        StrategicBuildingObservation b = Building(7, new Vector3(1f, 0f, 0f), 5f);
        StrategicPlanningState first = new StrategicPlanningState();
        first.buildings.Add(a); first.buildings.Add(b);
        StrategicPlanningState second = new StrategicPlanningState();
        second.buildings.Add(b); second.buildings.Add(a);
        StrategicTerritoryInfluence left = StrategicTerritoryInfluenceResolver.EvaluateAt(first, Vector3.zero);
        StrategicTerritoryInfluence right = StrategicTerritoryInfluenceResolver.EvaluateAt(second, Vector3.zero);
        Assert.AreEqual(left.control, right.control);
        Assert.AreEqual(left.ownerId, right.ownerId);
        Assert.AreEqual(left.winningScore, right.winningScore, 0.0001f);
        Assert.AreEqual(left.opposingScore, right.opposingScore, 0.0001f);
    }

    [Test]
    public void FieldSamplingUsesDeterministicRowMajorOrder()
    {
        StrategicPlanningState state = new StrategicPlanningState();
        state.buildings.Add(Building(3, Vector3.zero, 1.1f));
        List<StrategicTerritoryCell> cells = StrategicTerritoryInfluenceResolver.SampleField(state, Vector3.zero, 2, 2, 1f);
        Assert.AreEqual(4, cells.Count);
        Assert.AreEqual(new Vector3(0f, 0f, 0f), cells[0].position);
        Assert.AreEqual(new Vector3(1f, 0f, 0f), cells[1].position);
        Assert.AreEqual(new Vector3(0f, 0f, 1f), cells[2].position);
        Assert.AreEqual(StrategicTerritoryControl.Controlled, cells[0].influence.control);
        Assert.AreEqual(StrategicTerritoryControl.Neutral, cells[3].influence.control);
    }
}
