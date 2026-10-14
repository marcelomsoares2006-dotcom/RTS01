using System;

/// <summary>Shared gate for territory, defense and victory projections.</summary>
public static class StrategicBuildingEligibilityRules
{
    public static bool CanContributeToTerritory(StrategicBuildingObservation building)
    {
        return IsOperationalRuntimeReady(building) && building.influenceRadius > 0f;
    }

    public static bool CanContributeToDefense(StrategicBuildingObservation building)
    {
        return IsOperationalRuntimeReady(building);
    }

    public static bool IsOperationalRuntimeReady(StrategicBuildingObservation building)
    {
        return building != null
            && building.ownerId >= 0
            && building.lifecycle == BuildingLifecycleState.Operational
            && building.contentRuntimeReady
            && building.assetOrigin == BuildingContentAssetStage.RuntimeReady
            && !string.IsNullOrWhiteSpace(building.code);
    }
}
