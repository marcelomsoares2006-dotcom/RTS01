using System;
using System.Collections.Generic;

public enum BuildingPopulationCapacityResult
{
    Allowed,
    MissingCatalog,
    MissingState,
    NoCapacity,
    CapacityExceeded
}

public enum BuildingResourceRoutingResult
{
    Accepted,
    MissingCatalog,
    MissingState,
    BuildingNotOperational,
    UnsupportedResource,
    NotADropoff
}

/// <summary>
/// Pure economy rules derived from Building Content Catalog entries. These
/// rules are shared by UI, strategic AI and simulation adapters; they do not
/// instantiate assets or inspect scenes.
/// </summary>
public static class BuildingEconomyRules
{
    public static int CalculatePopulationCapacity(BuildingContentCatalog catalog, StrategicPlanningState state)
    {
        if (catalog == null || state == null || state.buildings == null) return 0;
        int capacity = 0;
        for (int i = 0; i < state.buildings.Count; i++)
        {
            StrategicBuildingObservation observation = state.buildings[i];
            if (observation == null || observation.ownerId != state.ownerId
                || observation.lifecycle != BuildingLifecycleState.Operational)
                continue;
            BuildingContentSpec entry = catalog.Find(observation.code);
            if (entry != null && entry.IsRuntimeReady)
                capacity += Math.Max(0, entry.populationCapacity);
        }
        return capacity;
    }

    public static BuildingPopulationCapacityResult EvaluatePopulationRoom(
        BuildingContentCatalog catalog, StrategicPlanningState state, int additionalPopulation)
    {
        if (catalog == null) return BuildingPopulationCapacityResult.MissingCatalog;
        if (state == null) return BuildingPopulationCapacityResult.MissingState;
        if (additionalPopulation < 0) return BuildingPopulationCapacityResult.CapacityExceeded;
        int capacity = CalculatePopulationCapacity(catalog, state);
        if (capacity <= 0) return BuildingPopulationCapacityResult.NoCapacity;
        return state.population + additionalPopulation <= capacity
            ? BuildingPopulationCapacityResult.Allowed
            : BuildingPopulationCapacityResult.CapacityExceeded;
    }

    public static BuildingResourceRoutingResult EvaluateResourceRouting(
        BuildingContentCatalog catalog, StrategicPlanningState state,
        string buildingCode, EconomyResourceType resource)
    {
        if (catalog == null) return BuildingResourceRoutingResult.MissingCatalog;
        if (state == null) return BuildingResourceRoutingResult.MissingState;
        BuildingContentSpec entry = catalog.Find(buildingCode);
        if (entry == null) return BuildingResourceRoutingResult.MissingCatalog;
        if (!IsOperationalOwned(state, entry.code))
            return BuildingResourceRoutingResult.BuildingNotOperational;
        if (!entry.supportsDropoff)
            return BuildingResourceRoutingResult.NotADropoff;
        if (entry.dropoffResources == null || !entry.dropoffResources.Contains(resource))
            return BuildingResourceRoutingResult.UnsupportedResource;
        return BuildingResourceRoutingResult.Accepted;
    }

    private static bool IsOperationalOwned(StrategicPlanningState state, string code)
    {
        if (state.buildings == null || string.IsNullOrWhiteSpace(code)) return false;
        for (int i = 0; i < state.buildings.Count; i++)
        {
            StrategicBuildingObservation observation = state.buildings[i];
            if (observation != null && observation.ownerId == state.ownerId
                && observation.lifecycle == BuildingLifecycleState.Operational
                && string.Equals(observation.code, code, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
