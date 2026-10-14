using System.Collections.Generic;
using NUnit.Framework;

public sealed class BuildingProductionUiContractTests
{
    [Test]
    public void SnapshotExposesOrderProgressCostAndQueueLimit()
    {
        EconomyLedger stockpile = new EconomyLedger();
        stockpile.Set(EconomyResourceType.Wood, 100);
        BuildingProductionRuntimeAdapter runtime = NewRuntime(stockpile);
        BuildingProductionUiAdapter ui = new BuildingProductionUiAdapter(runtime);
        BuildingProductionQueueResult queueResult;

        Assert.AreEqual(BuildingProductionRuntimeResult.Accepted,
            ui.TryEnqueue(4, Entry("spearman", 4f, 25), out queueResult));
        BuildingProductionUiState state = ui.State;

        Assert.AreEqual("barracks", state.buildingId);
        Assert.AreEqual(1, state.queueCount);
        Assert.AreEqual(2, state.queueLimit);
        Assert.AreEqual(1, state.orders.Count);
        Assert.AreEqual("spearman", state.orders[0].productCode);
        Assert.AreEqual(0f, state.orders[0].progress, 0.0001f);
        Assert.AreEqual(25, state.orders[0].cost[0].amount);
        Assert.AreEqual(1, state.orders[0].populationCost);

        List<string> completed;
        ui.Advance(4, 1f, out completed);
        Assert.AreEqual(0.25f, ui.State.orders[0].progress, 0.0001f);
        Assert.AreEqual(3f, ui.State.orders[0].remaining, 0.0001f);
    }

    [Test]
    public void CancelCommandUpdatesSnapshotAndRefundsReservation()
    {
        EconomyLedger stockpile = new EconomyLedger();
        stockpile.Set(EconomyResourceType.Wood, 60);
        PopulationLedger population = new PopulationLedger(4, 0);
        BuildingProductionRuntimeAdapter runtime = new BuildingProductionRuntimeAdapter(
            "stable", 2, stockpile, population, 3);
        BuildingProductionUiAdapter ui = new BuildingProductionUiAdapter(runtime);
        BuildingProductionQueueResult queueResult;

        ui.TryEnqueue(2, Entry("scout", 2f, 30), out queueResult);
        BuildingProductionOrder cancelled;
        Assert.AreEqual(BuildingProductionRuntimeResult.Accepted,
            ui.TryCancel(2, 0, out cancelled));
        Assert.AreEqual(0, ui.State.queueCount);
        Assert.AreEqual(60, stockpile.Get(EconomyResourceType.Wood));
        Assert.AreEqual(0, population.current);
        Assert.AreEqual("scout", cancelled.productCode);
    }

    [Test]
    public void SnapshotIsDetachedAndWrongOwnerCannotCancel()
    {
        EconomyLedger stockpile = new EconomyLedger();
        stockpile.Set(EconomyResourceType.Wood, 50);
        BuildingProductionUiAdapter ui = new BuildingProductionUiAdapter(NewRuntime(stockpile));
        BuildingProductionQueueResult queueResult;
        ui.TryEnqueue(4, Entry("archer", 1f, 10), out queueResult);
        BuildingProductionUiState snapshot = ui.State;
        snapshot.orders[0].productCode = "tampered";
        snapshot.orders[0].cost[0].amount = 9999;

        Assert.AreEqual("archer", ui.State.orders[0].productCode);
        Assert.AreEqual(10, ui.State.orders[0].cost[0].amount);
        BuildingProductionOrder cancelled;
        Assert.AreEqual(BuildingProductionRuntimeResult.InvalidOwner,
            ui.TryCancel(99, 0, out cancelled));
        Assert.AreEqual(1, ui.State.queueCount);
    }

    [Test]
    public void StateChangedPublishesOnlyAfterAcceptedMutation()
    {
        EconomyLedger stockpile = new EconomyLedger();
        stockpile.Set(EconomyResourceType.Wood, 50);
        BuildingProductionUiAdapter ui = new BuildingProductionUiAdapter(NewRuntime(stockpile));
        int notifications = 0;
        ui.StateChanged += state => notifications++;
        BuildingProductionQueueResult queueResult;

        ui.TryEnqueue(99, Entry("invalid", 1f, 5), out queueResult);
        Assert.AreEqual(0, notifications);
        ui.TryEnqueue(4, Entry("valid", 1f, 5), out queueResult);
        Assert.AreEqual(1, notifications);
        ui.Refresh();
        Assert.AreEqual(2, notifications);
    }

    private static BuildingProductionRuntimeAdapter NewRuntime(EconomyLedger stockpile)
    {
        return new BuildingProductionRuntimeAdapter(
            "barracks", 4, stockpile, new PopulationLedger(10, 0), 2);
    }

    private static BuildingProductionEntry Entry(string code, float duration, int wood)
    {
        return new BuildingProductionEntry
        {
            productCode = code,
            productionTime = duration,
            populationCost = 1,
            cost = new List<ResourceValue>
            {
                new ResourceValue(InGameResource.Wood, wood)
            }
        };
    }
}
