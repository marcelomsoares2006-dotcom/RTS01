using NUnit.Framework;

public class StrategicPlanningTests
{
    [Test] public void StateTracksAvailableAndReservedResources()
    {
        StrategicPlanningState state = new StrategicPlanningState(); state.SetAmount(InGameResource.Wood, 120, 35);
        Assert.AreEqual(120, state.GetAmount(InGameResource.Wood)); Assert.AreEqual(85, state.GetAvailableAmount(InGameResource.Wood));
    }
    [Test] public void DuplicateGoalsAreRejectedWithoutMutatingState()
    {
        StrategicPlanningState state = new StrategicPlanningState();
        Assert.IsTrue(state.TryAddGoal(new StrategicGoal("housing", StrategicActionKind.ConstructBuilding, "House")));
        Assert.IsFalse(state.TryAddGoal(new StrategicGoal(" HOUSING ", StrategicActionKind.ConstructBuilding, "House"))); Assert.AreEqual(1, state.goals.Count);
    }
    [Test] public void PlannerOrdersActiveGoalsDeterministicallyAndSkipsDisabled()
    {
        StrategicPlanningState state = new StrategicPlanningState(); state.TryAddGoal(new StrategicGoal("scout", StrategicActionKind.ScoutArea, "north", StrategicPriority.Normal)); state.TryAddGoal(new StrategicGoal("defend", StrategicActionKind.DefendArea, "town", StrategicPriority.Critical));
        StrategicGoal disabled = new StrategicGoal("disabled", StrategicActionKind.GatherResource, "Wood", StrategicPriority.Critical); disabled.enabled = false; state.TryAddGoal(disabled);
        StrategicPlan plan = new BasicStrategicPlanner().Plan(state);
        Assert.AreEqual(2, plan.intents.Count); Assert.AreEqual("defend", plan.intents[0].goalId); Assert.AreEqual("scout", plan.intents[1].goalId); Assert.AreEqual(StrategicActionKind.DefendArea, plan.FirstAction.action);
    }
    [Test] public void PlanningDoesNotMutateObservations()
    {
        StrategicPlanningState state = new StrategicPlanningState { population = 4, populationCap = 10 }; state.buildings.Add(new StrategicBuildingObservation { code = "House", ownerId = 0 }); state.units.Add(new StrategicUnitObservation { code = "Worker", ownerId = 0, idle = true }); state.TryAddGoal(new StrategicGoal("housing", StrategicActionKind.ConstructBuilding, "House"));
        new BasicStrategicPlanner().Plan(state);
        Assert.AreEqual(4, state.population); Assert.AreEqual(1, state.buildings.Count); Assert.AreEqual(1, state.units.Count);
    }
}

