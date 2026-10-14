using System;
using System.Collections.Generic;

public sealed class BasicStrategicPlanner : IStrategicPlanner
{
    private readonly BuildingContentCatalog _buildingCatalog;

    public BasicStrategicPlanner() { }

    public BasicStrategicPlanner(BuildingContentCatalog buildingCatalog)
    {
        _buildingCatalog = buildingCatalog;
    }

    public StrategicPlan Plan(StrategicPlanningState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        StrategicPlan plan = new StrategicPlan();
        List<StrategicGoal> active = new List<StrategicGoal>();
        if (state.goals != null)
            for (int i = 0; i < state.goals.Count; i++)
                if (state.goals[i] != null && state.goals[i].enabled && !string.IsNullOrWhiteSpace(state.goals[i].id)) active.Add(state.goals[i]);

        bool hasConstructionGoal = false;
        for (int i = 0; i < active.Count; i++)
            if (active[i].action == StrategicActionKind.ConstructBuilding) { hasConstructionGoal = true; break; }

        if (!hasConstructionGoal && _buildingCatalog != null
            && ConstructionPlanningRules.TryCreateIntent(state, _buildingCatalog, out StrategicActionIntent construction, out _))
        {
            active.Add(new StrategicGoal(construction.goalId, construction.action, construction.targetCode, construction.priority));
        }

        active.Sort(CompareGoals);
        for (int i = 0; i < active.Count; i++) plan.intents.Add(new StrategicActionIntent(active[i]));
        plan.rationale = plan.intents.Count == 0 ? "No active strategic goals." : "Active goals ordered by priority; execution is delegated to game adapters.";
        return plan;
    }
    private static int CompareGoals(StrategicGoal left, StrategicGoal right)
    {
        int priority = ((int)right.priority).CompareTo((int)left.priority);
        return priority != 0 ? priority : string.Compare(left.id, right.id, StringComparison.OrdinalIgnoreCase);
    }
}

