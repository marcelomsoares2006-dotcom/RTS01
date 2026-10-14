using NUnit.Framework;

public sealed class EconomyContractsTests
{
    [Test]
    public void LedgerAddsSpendsAndClampsNegativeAmounts()
    {
        EconomyLedger ledger = new EconomyLedger();
        ledger.Add(EconomyResourceType.Food, 20);
        Assert.AreEqual(20, ledger.Get(EconomyResourceType.Food));
        Assert.IsFalse(ledger.TrySpend(EconomyResourceType.Food, 21));
        Assert.IsTrue(ledger.TrySpend(EconomyResourceType.Food, 7));
        ledger.Add(EconomyResourceType.Food, -99);
        Assert.AreEqual(0, ledger.Get(EconomyResourceType.Food));
    }

    [Test]
    public void PopulationHonorsCapacity()
    {
        PopulationLedger population = new PopulationLedger(3, 2);
        Assert.IsFalse(population.TryAdd(2));
        Assert.IsTrue(population.TryAdd(1));
        Assert.AreEqual(3, population.current);
        Assert.IsTrue(population.TryRemove(2));
        Assert.AreEqual(1, population.current);
    }

    [Test]
    public void CollectorProducesFoodAndDepositsOnNextTick()
    {
        EconomyLedger ledger = new EconomyLedger();
        EconomyCollector collector = new EconomyCollector("worker-1", 0, EconomyResourceType.Food, 2f, 4f);
        Assert.AreEqual(2, collector.Tick(1f));
        Assert.AreEqual(0, ledger.Get(EconomyResourceType.Food));
        Assert.AreEqual(2, collector.Deposit(ledger));
        Assert.AreEqual(2, ledger.Get(EconomyResourceType.Food));
    }
}
