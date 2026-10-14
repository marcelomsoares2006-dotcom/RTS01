using System;
using System.Collections.Generic;

public sealed class BasicStrategicPlanner : IStrategicPlanner
{
    public StrategicPlan Plan(StrategicPlanningState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        StrategicPlan plan = new StrategicPlan();
        if (state.goals == null || state.goals.Count == 0) { plan.rationale = "No active strategic goals."; return plan; }
        List<StrategicGoal> active = new List<StrategicGoal>();
        for (int i = 0; i < state.goals.Count; i++) if (state.goals[i] != null && state.goals[i].enabled && !string.IsNullOrWhiteSpace(state.goals[i].id)) active.Add(state.goals[i]);
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

