using System.Collections.Generic;
using NUnit.Framework;

public sealed class BuildingProductionQueueTests
{
    [Test]
    public void EnqueueReservesCostAndPopulationAndAdvanceCompletesFifo()
    {
        EconomyLedger stockpile = new EconomyLedger();
        stockpile.Set(EconomyResourceType.Wood, 100);
        PopulationLedger population = new PopulationLedger(5, 0);
        BuildingProductionQueue queue = new BuildingProductionQueue(3);
        BuildingProductionEntry entry = Entry("spearman", 2f, 1, 30, InGameResource.Wood);

        Assert.AreEqual(BuildingProductionQueueResult.Accepted, queue.TryEnqueue(entry, stockpile, population));
        Assert.AreEqual(70, stockpile.Get(EconomyResourceType.Wood));
        Assert.AreEqual(1, population.current);
        Assert.IsEmpty(queue.Advance(1f));
        List<string> completed = queue.Advance(1f);
        CollectionAssert.AreEqual(new[] { "spearman" }, completed);
        Assert.AreEqual(0, queue.Count);
        Assert.AreEqual(1, population.current);
    }

    [Test]
    public void EnqueueIsAtomicWhenResourcesOrPopulationAreUnavailable()
    {
        EconomyLedger stockpile = new EconomyLedger();
        stockpile.Set(EconomyResourceType.Wood, 10);
        PopulationLedger population = new PopulationLedger(1, 1);
        BuildingProductionQueue queue = new BuildingProductionQueue(2);
        BuildingProductionEntry entry = Entry("archer", 1f, 1, 20, InGameResource.Wood);

        Assert.AreEqual(BuildingProductionQueueResult.InsufficientResources, queue.TryEnqueue(entry, stockpile, population));
        Assert.AreEqual(10, stockpile.Get(EconomyResourceType.Wood));
        Assert.AreEqual(1, population.current);

        stockpile.Set(EconomyResourceType.Wood, 20);
        Assert.AreEqual(BuildingProductionQueueResult.PopulationCap, queue.TryEnqueue(entry, stockpile, population));
        Assert.AreEqual(20, stockpile.Get(EconomyResourceType.Wood));
    }

    [Test]
    public void CancelRefundsCostAndPopulationReservation()
    {
        EconomyLedger stockpile = new EconomyLedger();
        stockpile.Set(EconomyResourceType.Gold, 50);
        PopulationLedger population = new PopulationLedger(4, 0);
        BuildingProductionQueue queue = new BuildingProductionQueue(2);
        BuildingProductionEntry entry = Entry("villager", 3f, 1, 25, InGameResource.Gold);

        Assert.AreEqual(BuildingProductionQueueResult.Accepted, queue.TryEnqueue(entry, stockpile, population));
        BuildingProductionOrder cancelled;
        Assert.IsTrue(queue.TryCancel(0, stockpile, population, out cancelled));
        Assert.AreEqual("villager", cancelled.productCode);
        Assert.AreEqual(50, stockpile.Get(EconomyResourceType.Gold));
        Assert.AreEqual(0, population.current);
        Assert.AreEqual(0, queue.Count);
    }

    [Test]
    public void EntryQueueLimitAndInvalidDurationAreEnforced()
    {
        EconomyLedger stockpile = new EconomyLedger();
        PopulationLedger population = new PopulationLedger(10, 0);
        BuildingProductionQueue queue = new BuildingProductionQueue(4);
        BuildingProductionEntry entry = Entry("unit", 1f, 1, 0, InGameResource.Wood);
        entry.queueLimit = 1;

        Assert.AreEqual(BuildingProductionQueueResult.Accepted, queue.TryEnqueue(entry, stockpile, population));
        Assert.AreEqual(BuildingProductionQueueResult.QueueFull, queue.TryEnqueue(entry, stockpile, population));
        BuildingProductionEntry invalid = Entry("broken", 0f, 1, 0, InGameResource.Wood);
        Assert.AreEqual(BuildingProductionQueueResult.InvalidDuration, queue.TryEnqueue(invalid, stockpile, population));
    }

    private static BuildingProductionEntry Entry(string code, float duration, int populationCost,
        int amount, InGameResource resource)
    {
        return new BuildingProductionEntry
        {
            productCode = code,
            productionTime = duration,
            populationCost = populationCost,
            cost = new List<ResourceValue> { new ResourceValue(resource, amount) }
        };
    }
}
