using System.Collections.Generic;
using NUnit.Framework;

public sealed class LegacyEconomyRuntimeAdapterTests
{
    [Test]
    public void WorkerRegistrationReservesOwnedPopulationAndRejectsDuplicate()
    {
        LegacyEconomyRuntimeAdapter adapter = new LegacyEconomyRuntimeAdapter();
        Assert.IsTrue(adapter.TryRegisterWorker("worker-b", 2, EconomyResourceType.Food, 2f, 4f));
        Assert.IsFalse(adapter.TryRegisterWorker("worker-b", 2, EconomyResourceType.Wood, 2f, 4f));
        EconomyPlayerState state;
        Assert.IsTrue(adapter.TryGetPlayer(2, out state));
        Assert.AreEqual(1, state.population.current);
        Assert.AreEqual(1, state.collectors.Count);
    }

    [Test]
    public void TickIsDeterministicAndKeepsOwnersSeparate()
    {
        LegacyEconomyRuntimeAdapter adapter = new LegacyEconomyRuntimeAdapter();
        adapter.TryRegisterWorker("worker-b", 1, EconomyResourceType.Food, 2f, 4f);
        adapter.TryRegisterWorker("worker-a", 1, EconomyResourceType.Food, 1f, 4f);
        adapter.TryRegisterWorker("worker-z", 2, EconomyResourceType.Food, 10f, 4f);
        adapter.Tick(1f);
        adapter.Tick(1f);

        EconomyPlayerState ownerOne;
        EconomyPlayerState ownerTwo;
        adapter.TryGetPlayer(1, out ownerOne);
        adapter.TryGetPlayer(2, out ownerTwo);
        Assert.AreEqual(3, ownerOne.stockpile.Get(EconomyResourceType.Food));
        Assert.AreEqual(4, ownerTwo.stockpile.Get(EconomyResourceType.Food));
        Assert.AreEqual("worker-a", ownerOne.collectors[0].collectorId);
    }

    [Test]
    public void LegacyBridgeCopiesSharedResourcesAndPreservesFood()
    {
        EconomyLedger ledger = new EconomyLedger();
        ledger.Set(EconomyResourceType.Food, 7);
        Dictionary<InGameResource, GameResource> legacy = new Dictionary<InGameResource, GameResource>
        {
            { InGameResource.Gold, new GameResource("Gold", 3) },
            { InGameResource.Wood, new GameResource("Wood", 5) },
            { InGameResource.Stone, new GameResource("Stone", 9) }
        };

        LegacyEconomyRuntimeAdapter.ImportLegacyStockpile(ledger, legacy);
        Assert.AreEqual(3, ledger.Get(EconomyResourceType.Gold));
        Assert.AreEqual(5, ledger.Get(EconomyResourceType.Wood));
        Assert.AreEqual(9, ledger.Get(EconomyResourceType.Stone));
        Assert.AreEqual(7, ledger.Get(EconomyResourceType.Food));

        ledger.Set(EconomyResourceType.Wood, 21);
        LegacyEconomyRuntimeAdapter.ExportLegacyStockpile(ledger, legacy);
        Assert.AreEqual(21, legacy[InGameResource.Wood].Amount);
    }

    [Test]
    public void UnregisterReleasesPopulationOnlyForOwnedWorker()
    {
        LegacyEconomyRuntimeAdapter adapter = new LegacyEconomyRuntimeAdapter();
        adapter.TryRegisterWorker("worker", 0, EconomyResourceType.Food, 1f, 2f);
        Assert.IsFalse(adapter.TryUnregisterWorker("worker", 1));
        Assert.IsTrue(adapter.TryUnregisterWorker("worker", 0));
        EconomyPlayerState state;
        adapter.TryGetPlayer(0, out state);
        Assert.AreEqual(0, state.population.current);
    }
}
