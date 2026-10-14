using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Stable result codes for era, technology and construction gates.</summary>
public enum ProgressionRequirementResult
{
    Allowed, MissingState, InvalidTargetEra, AlreadyAtOrAboveEra, EraLocked,
    PopulationRequirement, MissingLandmark, MissingBuilding, MissingTechnology,
    InsufficientResources, InvalidRequirement
}

[Serializable]
public sealed class ProgressionResourceRequirement
{
    public EconomyResourceType resource;
    [Min(0)] public int amount;
    public ProgressionResourceRequirement() { }
    public ProgressionResourceRequirement(EconomyResourceType resource, int amount)
    { this.resource = resource; this.amount = Mathf.Max(0, amount); }
}

[Serializable]
public sealed class EraTransitionRequirement
{
    public BuildingEra targetEra;
    [Min(0)] public int minimumPopulation;
    public string landmarkCode;
    public List<string> requiredBuildingCodes = new List<string>();
    public List<string> requiredTechnologyCodes = new List<string>();
    public List<ProgressionResourceRequirement> cost = new List<ProgressionResourceRequirement>();
    public EraTransitionRequirement() { }
    public EraTransitionRequirement(BuildingEra targetEra) { this.targetEra = targetEra; }
}

/// <summary>Pure era rules. Failed commands never mutate planning state.</summary>
public static class EraProgressionRules
{
    public static ProgressionRequirementResult Evaluate(StrategicPlanningState state, EraTransitionRequirement requirement)
    {
        if (state == null || requirement == null) return ProgressionRequirementResult.MissingState;
        if ((int)requirement.targetEra <= (int)state.era) return ProgressionRequirementResult.AlreadyAtOrAboveEra;
        if ((int)requirement.targetEra != (int)state.era + 1) return ProgressionRequirementResult.InvalidTargetEra;
        if (state.population < requirement.minimumPopulation) return ProgressionRequirementResult.PopulationRequirement;
        if (!string.IsNullOrWhiteSpace(requirement.landmarkCode) && !HasOperationalBuilding(state, requirement.landmarkCode)) return ProgressionRequirementResult.MissingLandmark;
        if (!HasAllBuildings(state, requirement.requiredBuildingCodes)) return ProgressionRequirementResult.MissingBuilding;
        if (!HasAllTechnologies(state, requirement.requiredTechnologyCodes)) return ProgressionRequirementResult.MissingTechnology;
        if (!HasAllResources(state, requirement.cost)) return ProgressionRequirementResult.InsufficientResources;
        return ProgressionRequirementResult.Allowed;
    }

    public static bool TryAdvance(StrategicPlanningState state, EraTransitionRequirement requirement)
    {
        if (Evaluate(state, requirement) != ProgressionRequirementResult.Allowed) return false;
        state.era = requirement.targetEra; return true;
    }

    private static bool HasAllBuildings(StrategicPlanningState state, IEnumerable<string> codes)
    { if (codes == null) return true; foreach (string code in codes) if (!string.IsNullOrWhiteSpace(code) && !HasOperationalBuilding(state, code)) return false; return true; }
    private static bool HasOperationalBuilding(StrategicPlanningState state, string code)
    {
        if (state.buildings == null) return false;
        for (int i = 0; i < state.buildings.Count; i++) { StrategicBuildingObservation b = state.buildings[i]; if (b != null && b.ownerId == state.ownerId && b.lifecycle == BuildingLifecycleState.Operational && string.Equals(b.code, code.Trim(), StringComparison.OrdinalIgnoreCase)) return true; }
        return false;
    }
    private static bool HasAllTechnologies(StrategicPlanningState state, IEnumerable<string> codes)
    {
        if (codes == null) return true;
        foreach (string code in codes) { bool found = false; if (state.unlockedTechnologyCodes != null) for (int i = 0; i < state.unlockedTechnologyCodes.Count; i++) if (string.Equals(state.unlockedTechnologyCodes[i], code == null ? string.Empty : code.Trim(), StringComparison.OrdinalIgnoreCase)) { found = true; break; } if (!string.IsNullOrWhiteSpace(code) && !found) return false; }
        return true;
    }
    private static bool HasAllResources(StrategicPlanningState state, IEnumerable<ProgressionResourceRequirement> costs)
    {
        if (costs == null) return true;
        foreach (ProgressionResourceRequirement cost in costs) { InGameResource legacy; if (cost == null || cost.amount < 0 || !cost.resource.TryGetLegacyResource(out legacy) || state.GetAvailableAmount(legacy) < cost.amount) return false; }
        return true;
    }
}

[Serializable]
public sealed class TechnologyDefinition
{
    public string code;
    public BuildingEra minimumEra = BuildingEra.Dark;
    public List<string> prerequisiteTechnologyCodes = new List<string>();
    public List<string> requiredBuildingCodes = new List<string>();
    [Min(0)] public float researchTime = 1f;
    public List<ProgressionResourceRequirement> cost = new List<ProgressionResourceRequirement>();
}

/// <summary>Deterministic research eligibility shared by UI, AI and commands.</summary>
public static class TechnologyProgressionRules
{
    public static ProgressionRequirementResult Evaluate(StrategicPlanningState state, TechnologyDefinition definition)
    {
        if (state == null || definition == null) return ProgressionRequirementResult.MissingState;
        if (string.IsNullOrWhiteSpace(definition.code)) return ProgressionRequirementResult.InvalidRequirement;
        if ((int)state.era < (int)definition.minimumEra) return ProgressionRequirementResult.EraLocked;
        if (HasTechnology(state, definition.code)) return ProgressionRequirementResult.AlreadyAtOrAboveEra;
        if (!HasTechnologies(state, definition.prerequisiteTechnologyCodes)) return ProgressionRequirementResult.MissingTechnology;
        if (!HasBuildings(state, definition.requiredBuildingCodes)) return ProgressionRequirementResult.MissingBuilding;
        if (!HasResources(state, definition.cost)) return ProgressionRequirementResult.InsufficientResources;
        return ProgressionRequirementResult.Allowed;
    }
    public static bool TryUnlock(StrategicPlanningState state, TechnologyDefinition definition)
    {
        if (Evaluate(state, definition) != ProgressionRequirementResult.Allowed) return false;
        if (state.unlockedTechnologyCodes == null) state.unlockedTechnologyCodes = new List<string>();
        state.unlockedTechnologyCodes.Add(definition.code.Trim()); return true;
    }
    private static bool HasTechnology(StrategicPlanningState state, string code) { return state.unlockedTechnologyCodes != null && state.unlockedTechnologyCodes.Exists(value => string.Equals(value, code.Trim(), StringComparison.OrdinalIgnoreCase)); }
    private static bool HasTechnologies(StrategicPlanningState state, IEnumerable<string> codes) { if (codes == null) return true; foreach (string code in codes) if (!string.IsNullOrWhiteSpace(code) && !HasTechnology(state, code)) return false; return true; }
    private static bool HasBuildings(StrategicPlanningState state, IEnumerable<string> codes)
    {
        if (codes == null) return true;
        foreach (string code in codes) { bool found = false; if (state.buildings != null) for (int i = 0; i < state.buildings.Count; i++) { StrategicBuildingObservation b = state.buildings[i]; if (b != null && b.ownerId == state.ownerId && b.lifecycle == BuildingLifecycleState.Operational && string.Equals(b.code, code == null ? string.Empty : code.Trim(), StringComparison.OrdinalIgnoreCase)) { found = true; break; } } if (!string.IsNullOrWhiteSpace(code) && !found) return false; }
        return true;
    }
    private static bool HasResources(StrategicPlanningState state, IEnumerable<ProgressionResourceRequirement> costs)
    { if (costs == null) return true; foreach (ProgressionResourceRequirement cost in costs) { InGameResource legacy; if (cost == null || cost.amount < 0 || !cost.resource.TryGetLegacyResource(out legacy) || state.GetAvailableAmount(legacy) < cost.amount) return false; } return true; }
}

/// <summary>Adapter that feeds the shared planning snapshot into the catalog gate.</summary>
public static class ProgressionConstructionRules
{
    public static BuildingAvailabilityResult Validate(
        BuildingCatalogResolver resolver, string buildingCode, StrategicPlanningState state)
    {
        if (resolver == null || state == null) return BuildingAvailabilityResult.NotFound;
        List<string> ownedBuildings = new List<string>();
        if (state.buildings != null)
            for (int i = 0; i < state.buildings.Count; i++)
            {
                StrategicBuildingObservation building = state.buildings[i];
                if (building != null && building.ownerId == state.ownerId
                    && building.lifecycle == BuildingLifecycleState.Operational
                    && !string.IsNullOrWhiteSpace(building.code))
                    ownedBuildings.Add(building.code);
            }
        return resolver.Validate(buildingCode, state.era, state.population,
            state.unlockedTechnologyCodes, ownedBuildings);
    }
}
