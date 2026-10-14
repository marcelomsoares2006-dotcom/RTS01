using NUnit.Framework;
using UnityEngine;

public sealed class StrategicBuildingEligibilityRulesTests
{
    [TestCase(BuildingLifecycleState.Blueprint)]
    [TestCase(BuildingLifecycleState.UnderConstruction)]
    [TestCase(BuildingLifecycleState.Damaged)]
    [TestCase(BuildingLifecycleState.Destroyed)]
    [TestCase(BuildingLifecycleState.Ruined)]
    public void NonOperationalLifecycleCannotInfluenceOrDefend(BuildingLifecycleState lifecycle)
    {
        StrategicBuildingObservation building = Building(lifecycle, BuildingContentAssetStage.RuntimeReady, true);
        Assert.IsFalse(StrategicBuildingEligibilityRules.CanContributeToTerritory(building));
        Assert.IsFalse(StrategicBuildingEligibilityRules.CanContributeToDefense(building));
    }

    [Test]
    public void IntermediateContentCannotInfluenceOrDefendEvenWhenMarkedOperational()
    {
        StrategicBuildingObservation building = Building(
            BuildingLifecycleState.Operational, BuildingContentAssetStage.ModelIntermediate, false);
        Assert.IsFalse(StrategicBuildingEligibilityRules.CanContributeToTerritory(building));
        Assert.IsFalse(StrategicBuildingEligibilityRules.CanContributeToDefense(building));
    }

    [Test]
    public void RuntimeReadyOperationalBuildingCanInfluenceAndDefend()
    {
        StrategicBuildingObservation building = Building(
            BuildingLifecycleState.Operational, BuildingContentAssetStage.RuntimeReady, true);
        Assert.IsTrue(StrategicBuildingEligibilityRules.CanContributeToTerritory(building));
        Assert.IsTrue(StrategicBuildingEligibilityRules.CanContributeToDefense(building));
    }

    private static StrategicBuildingObservation Building(
        BuildingLifecycleState lifecycle, BuildingContentAssetStage stage, bool ready)
    {
        return new StrategicBuildingObservation {
            ownerId = 0, code = "town_center", lifecycle = lifecycle,
            influenceRadius = 10f, position = Vector3.zero,
            assetOrigin = stage, contentRuntimeReady = ready
        };
    }
}
