using System;
using System.Collections.Generic;

/// <summary>Reason a construction objective is not currently actionable.</summary>
public enum BuildingObjectiveGate
{
    Available,
    MissingCatalogEntry,
    ContentUnavailable,
    EraLocked,
    TechnologyRequirement,
    BuildingRequirement,
    PopulationRequirement,
    LifecycleUnavailable
}

/// <summary>
/// Prefab-free objective projection shared by construction UI and strategic AI.
/// It deliberately distinguishes "can plan this building" from "it currently
/// counts as an operational objective", so planned content cannot grant game
/// progress by merely appearing in a roadmap.
/// </summary>
public sealed class BuildingObjectiveProjection
{
    public string code;
    public string displayName;
    public BuildingContentRole role;
    public BuildingEra minimumEra;
    public BuildingContentAssetStage assetOrigin;
    public bool runtimeReady;
    public bool constructionEligible;
    public bool objectiveEligible;
    public bool hasOperationalOwnerBuilding;
    public BuildingAvailabilityResult availability;
    public BuildingObjectiveGate gate;
}

public static class BuildingObjectiveProjectionRules
{
    public static bool TryProject(
        BuildingContentCatalog catalog,
        StrategicPlanningState state,
        string buildingCode,
        out BuildingObjectiveProjection projection)
    {
        projection = null;
        if (catalog == null || string.IsNullOrWhiteSpace(buildingCode)) return false;
        BuildingContentSpec entry = catalog.Find(buildingCode.Trim());
        if (entry == null) return false;

        BuildingAvailabilityResult availability = state == null
            ? BuildingAvailabilityResult.NotFound
            : BuildingContentProgressionRules.Validate(catalog, entry.code, state);
        bool hasOperational = state != null && VictoryObjectiveRules.HasOperationalBuilding(state, entry.code);
        bool constructionEligible = availability == BuildingAvailabilityResult.Available;
        projection = new BuildingObjectiveProjection
        {
            code = entry.code.Trim(),
            displayName = entry.displayName,
            role = entry.role,
            minimumEra = entry.minimumEra,
            assetOrigin = entry.AssetStage,
            runtimeReady = entry.IsRuntimeReady,
            constructionEligible = constructionEligible,
            objectiveEligible = constructionEligible && hasOperational,
            hasOperationalOwnerBuilding = hasOperational,
            availability = availability,
            gate = ToGate(availability, hasOperational)
        };
        return true;
    }

    public static List<BuildingObjectiveProjection> BuildAll(
        BuildingContentCatalog catalog,
        StrategicPlanningState state)
    {
        List<BuildingObjectiveProjection> result = new List<BuildingObjectiveProjection>();
        if (catalog == null || catalog.entries == null) return result;
        for (int i = 0; i < catalog.entries.Count; i++)
        {
            BuildingContentSpec entry = catalog.entries[i];
            BuildingObjectiveProjection projection;
            if (entry != null && TryProject(catalog, state, entry.code, out projection))
                result.Add(projection);
        }
        return result;
    }

    private static BuildingObjectiveGate ToGate(BuildingAvailabilityResult availability, bool hasOperational)
    {
        if (availability == BuildingAvailabilityResult.Available)
            return hasOperational ? BuildingObjectiveGate.Available : BuildingObjectiveGate.LifecycleUnavailable;
        switch (availability)
        {
            case BuildingAvailabilityResult.ContentUnavailable: return BuildingObjectiveGate.ContentUnavailable;
            case BuildingAvailabilityResult.EraLocked: return BuildingObjectiveGate.EraLocked;
            case BuildingAvailabilityResult.TechnologyRequirement: return BuildingObjectiveGate.TechnologyRequirement;
            case BuildingAvailabilityResult.BuildingRequirement: return BuildingObjectiveGate.BuildingRequirement;
            case BuildingAvailabilityResult.PopulationRequirement: return BuildingObjectiveGate.PopulationRequirement;
            case BuildingAvailabilityResult.NotFound: return BuildingObjectiveGate.MissingCatalogEntry;
            default: return BuildingObjectiveGate.MissingCatalogEntry;
        }
    }
}
