using System;
using System.Collections.Generic;
using UnityEngine;

public enum VictoryObjectiveKind { Landmark, Sacred, Wonder }

public enum VictoryObjectiveResult
{
    Allowed, MissingState, InvalidDefinition, InvalidOwner, EraLocked,
    PopulationRequirement, MissingBuilding, MissingTechnology, NotControlled,
    InProgress, Completed, Failed
}

[Serializable]
public sealed class VictoryObjectiveDefinition
{
    public string code;
    public VictoryObjectiveKind kind;
    public BuildingEra minimumEra = BuildingEra.Dark;
    public string targetCode;
    public string requiredBuildingCode;
    public List<string> requiredBuildingCodes = new List<string>();
    public List<string> requiredTechnologyCodes = new List<string>();
    [Min(0)] public int minimumPopulation;
    [Min(0f)] public float requiredHoldSeconds;

    public bool IsValid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(code) || minimumPopulation < 0 || requiredHoldSeconds < 0f)
                return false;
            return kind == VictoryObjectiveKind.Sacred
                ? !string.IsNullOrWhiteSpace(targetCode)
                : !string.IsNullOrWhiteSpace(requiredBuildingCode);
        }
    }
}

[Serializable]
public sealed class VictoryObjectiveProgress
{
    public string objectiveCode;
    public int ownerId;
    [Min(0f)] public float controlledSeconds;
    public bool completed;
    public bool failed;

    public VictoryObjectiveProgress() { }
    public VictoryObjectiveProgress(string objectiveCode, int ownerId)
    {
        this.objectiveCode = objectiveCode == null ? string.Empty : objectiveCode.Trim();
        this.ownerId = ownerId;
    }

    public void ResetControl()
    {
        controlledSeconds = 0f;
        completed = false;
    }
}

[Serializable]
public sealed class VictoryObjectiveObservation
{
    public string targetCode;
    public bool controlledByOwner;

    public VictoryObjectiveObservation() { }
    public VictoryObjectiveObservation(string targetCode, bool controlledByOwner)
    {
        this.targetCode = targetCode == null ? string.Empty : targetCode.Trim();
        this.controlledByOwner = controlledByOwner;
    }
}

/// <summary>Pure objective rules shared by scenario, AI, save and UI adapters.</summary>
public static class VictoryObjectiveRules
{
    /// <summary>
    /// Catalog-aware objective evaluation. Required buildings must be backed
    /// by runtime-ready content before they can satisfy victory conditions.
    /// </summary>
    public static VictoryObjectiveResult Evaluate(
        BuildingContentCatalog catalog,
        StrategicPlanningState state,
        VictoryObjectiveDefinition definition,
        VictoryObjectiveProgress progress,
        VictoryObjectiveObservation observation = null)
    {
        if (catalog == null) return VictoryObjectiveResult.MissingBuilding;
        if (state == null || definition == null) return Evaluate(state, definition, progress, observation);

        if (!string.IsNullOrWhiteSpace(definition.requiredBuildingCode))
        {
            BuildingContentSpec required = catalog.Find(definition.requiredBuildingCode.Trim());
            if (required == null || !required.IsRuntimeReady)
                return VictoryObjectiveResult.MissingBuilding;
        }
        if (definition.requiredBuildingCodes != null)
        {
            for (int i = 0; i < definition.requiredBuildingCodes.Count; i++)
            {
                string code = definition.requiredBuildingCodes[i];
                if (string.IsNullOrWhiteSpace(code)) continue;
                BuildingContentSpec required = catalog.Find(code.Trim());
                if (required == null || !required.IsRuntimeReady)
                    return VictoryObjectiveResult.MissingBuilding;
            }
        }
        return Evaluate(state, definition, progress, observation);
    }

    public static VictoryObjectiveResult Evaluate(
        StrategicPlanningState state,
        VictoryObjectiveDefinition definition,
        VictoryObjectiveProgress progress,
        VictoryObjectiveObservation observation = null)
    {
        if (state == null || progress == null) return VictoryObjectiveResult.MissingState;
        if (definition == null || !definition.IsValid) return VictoryObjectiveResult.InvalidDefinition;
        if (progress.ownerId != state.ownerId) return VictoryObjectiveResult.InvalidOwner;
        if (string.IsNullOrWhiteSpace(progress.objectiveCode)
            || !string.Equals(progress.objectiveCode.Trim(), definition.code.Trim(), StringComparison.OrdinalIgnoreCase))
            return VictoryObjectiveResult.InvalidDefinition;
        if (progress.failed) return VictoryObjectiveResult.Failed;
        if (progress.completed) return VictoryObjectiveResult.Completed;
        if ((int)state.era < (int)definition.minimumEra) return VictoryObjectiveResult.EraLocked;
        if (state.population < definition.minimumPopulation) return VictoryObjectiveResult.PopulationRequirement;
        if (!HasAllTechnologies(state, definition.requiredTechnologyCodes)) return VictoryObjectiveResult.MissingTechnology;
        if (!HasAllBuildings(state, definition.requiredBuildingCodes)) return VictoryObjectiveResult.MissingBuilding;

        if (definition.kind == VictoryObjectiveKind.Sacred)
        {
            if (observation == null
                || !string.Equals(observation.targetCode, definition.targetCode.Trim(), StringComparison.OrdinalIgnoreCase)
                || !observation.controlledByOwner)
                return VictoryObjectiveResult.NotControlled;
        }
        else if (!HasOperationalBuilding(state, definition.requiredBuildingCode))
        {
            return VictoryObjectiveResult.MissingBuilding;
        }

        return progress.controlledSeconds >= Mathf.Max(0f, definition.requiredHoldSeconds)
            ? VictoryObjectiveResult.Allowed
            : VictoryObjectiveResult.InProgress;
    }

    public static bool TryAdvance(
        StrategicPlanningState state,
        VictoryObjectiveDefinition definition,
        VictoryObjectiveProgress progress,
        float deltaSeconds,
        VictoryObjectiveObservation observation = null)
    {
        if (deltaSeconds < 0f) return false;
        VictoryObjectiveResult result = Evaluate(state, definition, progress, observation);
        if (result == VictoryObjectiveResult.Completed) return true;
        if (result != VictoryObjectiveResult.Allowed && result != VictoryObjectiveResult.InProgress) return false;
        if (definition.requiredHoldSeconds <= 0f)
        {
            progress.completed = true;
            return true;
        }

        progress.controlledSeconds = Mathf.Min(
            Mathf.Max(0f, definition.requiredHoldSeconds),
            progress.controlledSeconds + deltaSeconds);
        if (progress.controlledSeconds >= definition.requiredHoldSeconds)
            progress.completed = true;
        return progress.completed;
    }

    public static bool HasOperationalBuilding(StrategicPlanningState state, string code)
    {
        if (state == null || state.buildings == null || string.IsNullOrWhiteSpace(code)) return false;
        string normalized = code.Trim();
        for (int i = 0; i < state.buildings.Count; i++)
        {
            StrategicBuildingObservation building = state.buildings[i];
            if (StrategicBuildingEligibilityRules.IsOperationalRuntimeReady(building)
                && building.ownerId == state.ownerId
                && string.Equals(building.code, normalized, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool HasAllBuildings(StrategicPlanningState state, IEnumerable<string> codes)
    {
        if (codes == null) return true;
        foreach (string code in codes)
            if (!string.IsNullOrWhiteSpace(code) && !HasOperationalBuilding(state, code)) return false;
        return true;
    }

    private static bool HasAllTechnologies(StrategicPlanningState state, IEnumerable<string> codes)
    {
        if (codes == null) return true;
        foreach (string code in codes)
        {
            if (string.IsNullOrWhiteSpace(code)) continue;
            bool found = state.unlockedTechnologyCodes != null
                && state.unlockedTechnologyCodes.Exists(value =>
                    string.Equals(value, code.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!found) return false;
        }
        return true;
    }
}
