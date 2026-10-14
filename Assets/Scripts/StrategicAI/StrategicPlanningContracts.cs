using System;
using System.Collections.Generic;
using UnityEngine;

public enum StrategicActionKind { GatherResource, ConstructBuilding, ProduceUnit, ScoutArea, ResearchTechnology, DefendArea, PursueObjective }
public enum StrategicPriority { Low = 1, Normal = 2, High = 3, Critical = 4 }

[Serializable]
public sealed class StrategicResourceSnapshot
{
    public InGameResource resource;
    [Min(0)] public int amount;
    [Min(0)] public int reserved;
    public StrategicResourceSnapshot() { }
    public StrategicResourceSnapshot(InGameResource resource, int amount, int reserved = 0) { this.resource = resource; this.amount = Mathf.Max(0, amount); this.reserved = Mathf.Max(0, reserved); }
    public int Available => Mathf.Max(0, amount - reserved);
}

[Serializable]
public sealed class StrategicGoal
{
    public string id;
    public StrategicActionKind action;
    public StrategicPriority priority = StrategicPriority.Normal;
    public string targetCode;
    public Vector3 targetPosition;
    public int desiredCount = 1;
    public bool enabled = true;
    public StrategicGoal() { }
    public StrategicGoal(string id, StrategicActionKind action, string targetCode, StrategicPriority priority = StrategicPriority.Normal)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Goal id is required.", nameof(id));
        this.id = id.Trim(); this.action = action; this.targetCode = targetCode == null ? string.Empty : targetCode.Trim(); this.priority = priority;
    }
}

[Serializable]
public sealed class StrategicBuildingObservation { public string code; public BuildingLifecycleState lifecycle = BuildingLifecycleState.Operational; public Vector3 position; public int ownerId; }
[Serializable]
public sealed class StrategicUnitObservation { public string code; public Vector3 position; public bool alive = true; public bool idle; public int ownerId; }

[Serializable]
public sealed class StrategicPlanningState
{
    public int ownerId;
    public BuildingEra era = BuildingEra.Dark;
    [Min(0)] public int population;
    [Min(0)] public int populationCap;
    public List<StrategicResourceSnapshot> resources = new List<StrategicResourceSnapshot>();
    public List<StrategicBuildingObservation> buildings = new List<StrategicBuildingObservation>();
    public List<StrategicUnitObservation> units = new List<StrategicUnitObservation>();
    public List<string> unlockedTechnologyCodes = new List<string>();
    public List<StrategicGoal> goals = new List<StrategicGoal>();

    public int GetAmount(InGameResource resource) { StrategicResourceSnapshot s = FindResource(resource); return s == null ? 0 : s.amount; }
    public int GetAvailableAmount(InGameResource resource) { StrategicResourceSnapshot s = FindResource(resource); return s == null ? 0 : s.Available; }
    public void SetAmount(InGameResource resource, int amount, int reserved = 0)
    {
        StrategicResourceSnapshot s = FindResource(resource);
        if (s == null) { resources.Add(new StrategicResourceSnapshot(resource, amount, reserved)); return; }
        s.amount = Mathf.Max(0, amount); s.reserved = Mathf.Max(0, reserved);
    }
    public bool HasGoal(string goalId)
    {
        if (string.IsNullOrWhiteSpace(goalId)) return false;
        for (int i = 0; i < goals.Count; i++) if (goals[i] != null && string.Equals(goals[i].id, goalId.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
    public bool TryAddGoal(StrategicGoal goal) { if (goal == null || string.IsNullOrWhiteSpace(goal.id) || HasGoal(goal.id)) return false; goals.Add(goal); return true; }
    private StrategicResourceSnapshot FindResource(InGameResource resource)
    {
        if (resources == null) resources = new List<StrategicResourceSnapshot>();
        for (int i = 0; i < resources.Count; i++) if (resources[i] != null && resources[i].resource == resource) return resources[i];
        return null;
    }
}

[Serializable]
public sealed class StrategicActionIntent
{
    public StrategicActionKind action;
    public StrategicPriority priority;
    public string goalId;
    public string targetCode;
    public Vector3 targetPosition;
    public int quantity = 1;
    public StrategicActionIntent() { }
    public StrategicActionIntent(StrategicGoal goal)
    {
        if (goal == null) throw new ArgumentNullException(nameof(goal));
        action = goal.action; priority = goal.priority; goalId = goal.id; targetCode = goal.targetCode; targetPosition = goal.targetPosition; quantity = Mathf.Max(1, goal.desiredCount);
    }
}

public interface IStrategicPlanner { StrategicPlan Plan(StrategicPlanningState state); }

[Serializable]
public sealed class StrategicPlan
{
    public List<StrategicActionIntent> intents = new List<StrategicActionIntent>();
    public string rationale;
    public StrategicActionIntent FirstAction { get { return intents == null || intents.Count == 0 ? null : intents[0]; } }
}

