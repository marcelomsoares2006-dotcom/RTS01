using System;
using System.Collections.Generic;

/// <summary>
/// Bridges the authoritative building content roadmap to the shared
/// era/technology planning state.  This keeps planned entries visible to
/// design tools while preventing them from leaking into runtime construction
/// before their authored BuildingData exists.
/// </summary>
public static class BuildingContentProgressionRules
{
    public static BuildingAvailabilityResult Validate(
        BuildingContentCatalog catalog,
        string buildingCode,
        StrategicPlanningState state,
        bool requireRuntimeReady = true)
    {
        if (catalog == null || state == null)
            return BuildingAvailabilityResult.NotFound;
        if (string.IsNullOrWhiteSpace(buildingCode))
            return BuildingAvailabilityResult.MissingCode;

        BuildingContentSpec entry = catalog.Find(buildingCode.Trim());
        if (entry == null)
            return BuildingAvailabilityResult.NotFound;
        if (requireRuntimeReady && !entry.IsRuntimeReady)
            return BuildingAvailabilityResult.ContentUnavailable;
        if ((int)state.era < (int)entry.minimumEra)
            return BuildingAvailabilityResult.EraLocked;

        if (state.population < 0)
            return BuildingAvailabilityResult.PopulationRequirement;

        HashSet<string> technologies = ToCodeSet(state.unlockedTechnologyCodes);
        HashSet<string> buildings = OperationalOwnedBuildingCodes(state);
        if (entry.requirements != null)
        {
            for (int i = 0; i < entry.requirements.Count; i++)
            {
                BuildingRequirement requirement = entry.requirements[i];
                if (requirement == null) continue;
                if (state.population < requirement.minimumPopulation)
                    return BuildingAvailabilityResult.PopulationRequirement;
                if (!string.IsNullOrWhiteSpace(requirement.technologyCode)
                    && !technologies.Contains(requirement.technologyCode.Trim()))
                    return BuildingAvailabilityResult.TechnologyRequirement;
                if (!string.IsNullOrWhiteSpace(requirement.buildingCode)
                    && !buildings.Contains(requirement.buildingCode.Trim()))
                    return BuildingAvailabilityResult.BuildingRequirement;
            }
        }
        return BuildingAvailabilityResult.Available;
    }

    /// <summary>
    /// Uses the content catalog as the source of truth for a construction
    /// command while retaining the existing resolver overload for legacy and
    /// BuildingCatalogData backed scenes.
    /// </summary>
    public static BuildingAvailabilityResult Validate(
        BuildingContentCatalog catalog,
        string buildingCode,
        StrategicPlanningState state)
    {
        return Validate(catalog, buildingCode, state, true);
    }

    private static HashSet<string> ToCodeSet(IEnumerable<string> codes)
    {
        HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (codes == null) return result;
        foreach (string code in codes)
            if (!string.IsNullOrWhiteSpace(code)) result.Add(code.Trim());
        return result;
    }

    private static HashSet<string> OperationalOwnedBuildingCodes(StrategicPlanningState state)
    {
        HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (state.buildings == null) return result;
        for (int i = 0; i < state.buildings.Count; i++)
        {
            StrategicBuildingObservation building = state.buildings[i];
            if (building != null && building.ownerId == state.ownerId
                && building.lifecycle == BuildingLifecycleState.Operational
                && !string.IsNullOrWhiteSpace(building.code))
                result.Add(building.code.Trim());
        }
        return result;
    }
}
