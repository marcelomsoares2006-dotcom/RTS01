using System.Collections.Generic;
using NUnit.Framework;

public sealed class LegacyEconomyRuntimeWorkerSimulationTests
{
    private static LegacyEconomyWorkerSnapshot Worker(string id, int owner,
        EconomyResourceType resource = EconomyResourceType.Food, bool alive = true)
    {
        return new LegacyEconomyWorkerSnapshot(id, owner, resource, 2f, 4f, 1, alive);
    }

    [Test]
    public void ReconcileRejectsDuplicateIdsAndSortsRegistrationDeterministically()
    {
        LegacyEconomyRuntimeWorkerSimulation simulation =
            new LegacyEconomyRuntimeWorkerSimulation();
        simulation.Reconcile(new List<LegacyEconomyWorkerSnapshot>
        {
            Worker("worker-b", 1),
            Worker("worker-a", 1),
            Worker("worker-a", 2)
        });

        Assert.AreEqual(2, simulation.RegisteredWorkerCount);
        EconomyPlayerState state;
        Assert.IsTrue(simulation.Adapter.TryGetPlayer(1, out state));
        Assert.AreEqual(2, state.population.current);
        Assert.IsFalse(simulation.Adapter.TryGetPlayer(2, out state));
    }

    [Test]
    public void OmittedOrDeadWorkerReleasesPopulationAndStopsCollection()
    {
        LegacyEconomyRuntimeWorkerSimulation simulation =
            new LegacyEconomyRuntimeWorkerSimulation();
        simulation.Reconcile(new[] { Worker("worker", 0) });
        simulation.Tick(1f);
        simulation.Tick(1f);
        simulation.Reconcile(new[] { Worker("worker", 0, alive: false) });
        simulation.Tick(1f);
        simulation.Reconcile(new LegacyEconomyWorkerSnapshot[0]);

        EconomyPlayerState state;
        Assert.IsTrue(simulation.Adapter.TryGetPlayer(0, out state));
        Assert.AreEqual(0, state.population.current);
        Assert.AreEqual(0, state.collectors.Count);
        Assert.AreEqual(2, state.stockpile.Get(EconomyResourceType.Food));
    }

    [Test]
    public void ChangedWorkerConfigurationRebindsWithoutDuplicatingPopulation()
    {
        LegacyEconomyRuntimeWorkerSimulation simulation =
            new LegacyEconomyRuntimeWorkerSimulation();
        simulation.Reconcile(new[] { Worker("worker", 0) });
        simulation.Reconcile(new[] { Worker("worker", 0, EconomyResourceType.Wood) });

        EconomyPlayerState state;
        Assert.IsTrue(simulation.Adapter.TryGetPlayer(0, out state));
        Assert.AreEqual(1, state.population.current);
        Assert.AreEqual(1, state.collectors.Count);
        Assert.AreEqual(EconomyResourceType.Wood, state.collectors[0].resource);
    }
}
