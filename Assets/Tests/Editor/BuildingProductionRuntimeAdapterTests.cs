using System.Collections.Generic;
using NUnit.Framework;

public sealed class BuildingProductionRuntimeAdapterTests
{
    [Test]
    public void OnlyOwnerCanEnqueueAdvanceOrCancel()
    {
        EconomyLedger stockpile = new EconomyLedger();
        stockpile.Set(EconomyResourceType.Wood, 100);
        PopulationLedger population = new PopulationLedger(5, 0);
        BuildingProductionRuntimeAdapter adapter = NewAdapter(stockpile, population);
        BuildingProductionEntry entry = Entry("spearman", 1f, 1, 20);
        BuildingProductionQueueResult queueResult;

        Assert.AreEqual(BuildingProductionRuntimeResult.InvalidOwner,
            adapter.TryEnqueue(8, entry, out queueResult));
        Assert.AreEqual(0, adapter.Queue.Count);
        Assert.AreEqual(100, stockpile.Get(EconomyResourceType.Wood));

        Assert.AreEqual(BuildingProductionRuntimeResult.Accepted,
            adapter.TryEnqueue(3, entry, out queueResult));
        List<string> completed;
        Assert.AreEqual(BuildingProductionRuntimeResult.InvalidOwner,
            adapter.Advance(8, 1f, out completed));
        Assert.AreEqual(1, adapter.Queue.Count);

        BuildingProductionOrder cancelled;
        Assert.AreEqual(BuildingProductionRuntimeResult.InvalidOwner,
            adapter.TryCancel(8, 0, out cancelled));
        Assert.IsNull(cancelled);
    }

    [Test]
    public void AdvanceDeliversCompletedProductsInFifoOrderAndCancelRefunds()
    {
        EconomyLedger stockpile = new EconomyLedger();
        stockpile.Set(EconomyResourceType.Wood, 100);
        PopulationLedger population = new PopulationLedger(5, 0);
        List<string> delivered = new List<string>();
        BuildingProductionRuntimeAdapter adapter = new BuildingProductionRuntimeAdapter(
            "town-center", 2, stockpile, population, 3, null,
            product => delivered.Add(product));
        BuildingProductionQueueResult queueResult;

        Assert.AreEqual(BuildingProductionRuntimeResult.Accepted,
            adapter.TryEnqueue(2, Entry("villager", 1f, 1, 20), out queueResult));
        Assert.AreEqual(BuildingProductionRuntimeResult.Accepted,
            adapter.TryEnqueue(2, Entry("scout", 2f, 1, 30), out queueResult));
        List<string> completed;
        Assert.AreEqual(BuildingProductionRuntimeResult.Accepted,
            adapter.Advance(2, 1.5f, out completed));
        CollectionAssert.AreEqual(new[] { "villager" }, completed);
        CollectionAssert.AreEqual(new[] { "villager" }, delivered);
        Assert.AreEqual(1, adapter.Queue.Count);

        BuildingProductionOrder cancelled;
        Assert.AreEqual(BuildingProductionRuntimeResult.Accepted,
            adapter.TryCancel(2, 0, out cancelled));
        Assert.AreEqual("scout", cancelled.productCode);
        Assert.AreEqual(100, stockpile.Get(EconomyResourceType.Wood));
        Assert.AreEqual(0, population.current);
    }

    [Test]
    public void UnavailableBuildingDoesNotMutateQueueOrLedger()
    {
        EconomyLedger stockpile = new EconomyLedger();
        stockpile.Set(EconomyResourceType.Wood, 50);
        PopulationLedger population = new PopulationLedger(3, 0);
        bool available = false;
        BuildingProductionRuntimeAdapter adapter = new BuildingProductionRuntimeAdapter(
            "barracks", 1, stockpile, population, 2, () => available);
        BuildingProductionQueueResult queueResult;

        Assert.AreEqual(BuildingProductionRuntimeResult.BuildingUnavailable,
            adapter.TryEnqueue(1, Entry("spearman", 1f, 1, 10), out queueResult));
        Assert.AreEqual(50, stockpile.Get(EconomyResourceType.Wood));
        Assert.AreEqual(0, population.current);
        Assert.AreEqual(0, adapter.Queue.Count);
    }

    private static BuildingProductionRuntimeAdapter NewAdapter(
        EconomyLedger stockpile, PopulationLedger population)
    {
        return new BuildingProductionRuntimeAdapter("barracks", 3, stockpile, population);
    }

    private static BuildingProductionEntry Entry(string code, float time, int populationCost, int wood)
    {
        return new BuildingProductionEntry
        {
            productCode = code,
            productionTime = time,
            populationCost = populationCost,
            cost = new List<ResourceValue> { new ResourceValue(InGameResource.Wood, wood) }
        };
    }
}
