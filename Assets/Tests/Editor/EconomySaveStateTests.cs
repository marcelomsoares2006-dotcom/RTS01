using NUnit.Framework;

public sealed class EconomySaveStateTests
{
    [Test]
    public void EconomySnapshotRoundTripsResourcesPopulationCollectorsAndCarry()
    {
        EconomyPlayerState source = new EconomyPlayerState();
        source.stockpile.Set(EconomyResourceType.Food, 120);
        source.stockpile.Set(EconomyResourceType.Gold, 45);
        source.population = new PopulationLedger(30, 7);
        EconomyCollector collector = new EconomyCollector("worker-7", 2, EconomyResourceType.Food, 2f, 10f);
        collector.Tick(1.5f);
        source.collectors.Add(collector);

        EconomyPlayerSaveData snapshot = EconomyPlayerSaveData.Capture(source);
        EconomyPlayerState restored = snapshot.Restore();

        Assert.AreEqual(120, restored.stockpile.Get(EconomyResourceType.Food));
        Assert.AreEqual(45, restored.stockpile.Get(EconomyResourceType.Gold));
        Assert.AreEqual(7, restored.population.current);
        Assert.AreEqual(30, restored.population.capacity);
        Assert.AreEqual(1, restored.collectors.Count);
        Assert.AreEqual("worker-7", restored.collectors[0].collectorId);
        Assert.AreEqual(3f, restored.collectors[0].CarriedAmount, 0.001f);
    }

    [Test]
    public void MissingEconomyFieldRestoresLegacyDefaults()
    {
        EconomyPlayerState restored = new EconomyPlayerSaveData().Restore();
        Assert.AreEqual(0, restored.stockpile.Get(EconomyResourceType.Food));
        Assert.AreEqual(0, restored.population.current);
        Assert.AreEqual(0, restored.population.capacity);
        Assert.AreEqual(0, restored.collectors.Count);
    }
}
