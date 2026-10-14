using System;
using System.Collections.Generic;
using UnityEngine;

public enum FoundationPlacementResult
{
    Allowed,
    AuditOnly,
    ContentNotRuntimeReady,
    MissingCatalog,
    MissingCode,
    NotFoundationRole,
    EraLocked,
    PopulationRequirement,
    TechnologyRequirement,
    BuildingRequirement,
    InvalidFootprint,
    TerrainUnavailable,
    TerrainTooSteep,
    BlockedByBuilding,
    InvalidConstructionCost,
    InsufficientResources,
    MissingDropoffCoverage,
    MissingPopulationCapacity
}

/// <summary>
/// Scene-independent placement gate for the first economy foundations. The
/// request contains observations supplied by a future Unity adapter; this
/// class never queries physics, creates objects or mutates economy state.
/// </summary>
public sealed class FoundationPlacementRequest
{
    public string code;
    public StrategicPlanningState state;
    public Vector3 terrainNormal = Vector3.up;
    public bool terrainHit = true;
    public float maximumSlopeDegrees = 25f;
    public float footprintRadius = 1f;
    public float minimumDistance;
    public IEnumerable<Vector3> existingBuildingPositions;
    public Vector3 position;
    /// <summary>Runtime authorization stays closed unless explicitly requested.</summary>
    public bool requireRuntimeReady = true;
}

public static class FoundationPlacementRules
{
    public static FoundationPlacementResult Evaluate(
        BuildingContentCatalog catalog,
        FoundationPlacementRequest request)
    {
        if (catalog == null) return FoundationPlacementResult.MissingCatalog;
        if (request == null || string.IsNullOrWhiteSpace(request.code))
            return FoundationPlacementResult.MissingCode;
        if (request.state == null) return FoundationPlacementResult.MissingCatalog;

        BuildingContentSpec entry = catalog.Find(request.code.Trim());
        if (entry == null) return FoundationPlacementResult.MissingCode;
        if (entry.role != BuildingContentRole.TownCenter
            && entry.role != BuildingContentRole.ResourceDropoff)
            return FoundationPlacementResult.NotFoundationRole;

        bool auditOnly = !entry.IsRuntimeReady;
        if (auditOnly && request.requireRuntimeReady)
            return FoundationPlacementResult.ContentNotRuntimeReady;

        BuildingAvailabilityResult availability = BuildingContentProgressionRules.Validate(
            catalog, entry.code, request.state, false);
        FoundationPlacementResult availabilityResult = MapAvailability(availability);
        if (availabilityResult != FoundationPlacementResult.Allowed)
            return availabilityResult;

        FoundationPlacementResult contract = ValidateFoundationContract(entry);
        if (contract != FoundationPlacementResult.Allowed) return contract;
        if (float.IsNaN(request.footprintRadius) || float.IsInfinity(request.footprintRadius)
            || request.footprintRadius <= 0f)
            return FoundationPlacementResult.InvalidFootprint;
        if (!request.terrainHit) return FoundationPlacementResult.TerrainUnavailable;
        if (!BuildingConstructionRules.IsTerrainSlopeAllowed(
            request.terrainNormal, request.maximumSlopeDegrees))
            return FoundationPlacementResult.TerrainTooSteep;
        if (!BuildingConstructionRules.IsBuildingDistanceAllowed(
            request.position, request.existingBuildingPositions,
            request.minimumDistance + request.footprintRadius))
            return FoundationPlacementResult.BlockedByBuilding;
        FoundationPlacementResult costResult = EvaluateCost(entry.constructionCost, request.state);
        return costResult == FoundationPlacementResult.Allowed && auditOnly
            ? FoundationPlacementResult.AuditOnly
            : costResult;
    }

    private static FoundationPlacementResult ValidateFoundationContract(BuildingContentSpec entry)
    {
        if (entry.constructionCost == null || entry.constructionCost.Count == 0)
            return FoundationPlacementResult.InvalidConstructionCost;
        if (entry.role == BuildingContentRole.TownCenter && entry.populationCapacity <= 0)
            return FoundationPlacementResult.MissingPopulationCapacity;
        if (!entry.supportsDropoff || entry.dropoffResources == null || entry.dropoffResources.Count == 0)
            return FoundationPlacementResult.MissingDropoffCoverage;
        return FoundationPlacementResult.Allowed;
    }

    private static FoundationPlacementResult EvaluateCost(
        IEnumerable<BuildingContentCost> costs, StrategicPlanningState state)
    {
        Dictionary<InGameResource, long> required = new Dictionary<InGameResource, long>();
        foreach (BuildingContentCost cost in costs)
        {
            if (cost == null || cost.amount <= 0)
                return FoundationPlacementResult.InvalidConstructionCost;
            InGameResource resource;
            if (!cost.resource.TryGetLegacyResource(out resource))
                return FoundationPlacementResult.InvalidConstructionCost;
            long current;
            required.TryGetValue(resource, out current);
            required[resource] = current + cost.amount;
        }
        foreach (KeyValuePair<InGameResource, long> requirement in required)
            if (state.GetAvailableAmount(requirement.Key) < requirement.Value)
                return FoundationPlacementResult.InsufficientResources;
        return FoundationPlacementResult.Allowed;
    }

    private static FoundationPlacementResult MapAvailability(BuildingAvailabilityResult result)
    {
        switch (result)
        {
            case BuildingAvailabilityResult.Available: return FoundationPlacementResult.Allowed;
            case BuildingAvailabilityResult.EraLocked: return FoundationPlacementResult.EraLocked;
            case BuildingAvailabilityResult.PopulationRequirement: return FoundationPlacementResult.PopulationRequirement;
            case BuildingAvailabilityResult.TechnologyRequirement: return FoundationPlacementResult.TechnologyRequirement;
            case BuildingAvailabilityResult.BuildingRequirement: return FoundationPlacementResult.BuildingRequirement;
            default: return FoundationPlacementResult.MissingCode;
        }
    }
}
