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
    /// <summary>
    /// Audits the roster's age graph without loading scenes or prefabs. A
    /// dropoff or production building must be anchored to an operational Town
    /// Center, while forward defenses remain free to be placed independently.
    /// </summary>
    public static IEnumerable<string> ValidateEraContracts(BuildingContentCatalog catalog)
    {
        if (catalog == null)
        {
            yield return "Building content catalog is missing.";
            yield break;
        }

        BuildingContentSpec townCenter = catalog.Find("town_center");
        if (townCenter == null)
            yield return "town_center is required as the era progression anchor.";
        else if (townCenter.minimumEra != BuildingEra.Dark)
            yield return "town_center must be available in the Dark Age.";

        if (catalog.entries == null) yield break;
        for (int i = 0; i < catalog.entries.Count; i++)
        {
            BuildingContentSpec entry = catalog.entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.code)) continue;
            if ((int)entry.minimumEra < (int)BuildingEra.Dark
                || (int)entry.minimumEra > (int)BuildingEra.Imperial)
                yield return entry.code + " has an invalid minimum era.";

            bool anchoredRole = entry.role == BuildingContentRole.ResourceDropoff
                || entry.role == BuildingContentRole.Production;
            if (anchoredRole && !string.Equals(entry.code, "town_center", StringComparison.OrdinalIgnoreCase)
                && !HasBuildingRequirement(entry, "town_center"))
                yield return entry.code + " must require an operational town_center.";

            if (entry.requirements == null) continue;
            for (int j = 0; j < entry.requirements.Count; j++)
            {
                BuildingRequirement requirement = entry.requirements[j];
                if (requirement != null && !string.IsNullOrWhiteSpace(requirement.buildingCode)
                    && string.Equals(requirement.buildingCode.Trim(), entry.code.Trim(), StringComparison.OrdinalIgnoreCase))
                    yield return entry.code + " cannot require itself.";
            }
        }
    }

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

    private static bool HasBuildingRequirement(BuildingContentSpec entry, string code)
    {
        if (entry.requirements == null) return false;
        for (int i = 0; i < entry.requirements.Count; i++)
        {
            BuildingRequirement requirement = entry.requirements[i];
            if (requirement != null && string.Equals(requirement.buildingCode, code, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
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
