using NUnit.Framework;

public sealed class ConstructionReplayContractsTests
{
    [Test]
    public void ReplayCanonicalizesConstructionProductionCancelReserveAndAgeUp()
    {
        ConstructionReplayLog log = new ConstructionReplayLog();
        Assert.That(log.TryAppend(Command("cancel", 4, 100, 2, ConstructionReplayCommandKind.CancelConstruction, "house", "r1")), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(log.TryAppend(Command("build", 3, 90, 3, ConstructionReplayCommandKind.ConstructBuilding, "house", "r1")), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(log.TryAppend(Command("age", 5, 120, 4, ConstructionReplayCommandKind.AdvanceEra, "", "")), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(log.TryAppend(Command("queue", 3, 90, 1, ConstructionReplayCommandKind.ProduceUnit, "spearman", "r2")), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(log.TryAppend(Command("reserve", 3, 90, 2, ConstructionReplayCommandKind.ReserveResources, "house", "r1")), Is.EqualTo(ConstructionReplayResult.Accepted));
        ConstructionReplayState state = new ConstructionReplayState();
        Assert.That(log.TryReplay(state), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.AreEqual(5, state.appliedCommandIds.Count);
        Assert.AreEqual("build", state.appliedCommandIds[0]);
        Assert.AreEqual("age", state.appliedCommandIds[4]);
        string first = state.fingerprint;
        Assert.That(log.TryReplay(state), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.AreEqual(first, state.fingerprint);
    }

    [Test]
    public void DuplicateOrInvalidCommandsAreRejectedWithoutChangingReplay()
    {
        ConstructionReplayLog log = new ConstructionReplayLog();
        Assert.That(log.TryAppend(Command("same", 1, 1, 1, ConstructionReplayCommandKind.ConstructBuilding, "house", "r1")), Is.EqualTo(ConstructionReplayResult.Accepted));
        string before = ConstructionReplayLog.ComputeFingerprint(log.Commands);
        Assert.That(log.TryAppend(Command("same", 2, 2, 1, ConstructionReplayCommandKind.ConstructBuilding, "house", "r2")), Is.EqualTo(ConstructionReplayResult.DuplicateCommandId));
        Assert.That(log.TryAppend(Command("bad", -1, 1, 1, ConstructionReplayCommandKind.AdvanceEra, "", "")), Is.EqualTo(ConstructionReplayResult.InvalidTimestamp));
        Assert.AreEqual(before, ConstructionReplayLog.ComputeFingerprint(log.Commands));
    }

    [Test]
    public void ChangedCommandPayloadProducesDivergentReplay()
    {
        ConstructionReplayLog log = new ConstructionReplayLog();
        log.TryAppend(Command("build", 1, 10, 1, ConstructionReplayCommandKind.ConstructBuilding, "house", "r1"));
        ConstructionReplayState state = new ConstructionReplayState();
        Assert.That(log.TryReplay(state), Is.EqualTo(ConstructionReplayResult.Accepted));
        state.fingerprint = "DIFFERENT";
        Assert.That(log.TryReplay(state), Is.EqualTo(ConstructionReplayResult.DivergentReplay));
    }

    [Test]
    public void DetachedExecutorReplaysTwiceToTheSameState()
    {
        ConstructionReplayLog log = new ConstructionReplayLog();
        log.TryAppend(Command("build", 1, 1, 3, ConstructionReplayCommandKind.ConstructBuilding, "house", "r1"));
        log.TryAppend(Command("reserve", 1, 1, 2, ConstructionReplayCommandKind.ReserveResources, "house", "r1"));
        log.TryAppend(Command("queue", 2, 2, 1, ConstructionReplayCommandKind.ProduceUnit, "spearman", "r2"));
        log.TryAppend(Command("age", 3, 3, 4, ConstructionReplayCommandKind.AdvanceEra, "age:feudal", ""));
        ConstructionReplaySnapshot first;
        ConstructionReplaySnapshot second;
        Assert.That(ConstructionReplayExecutor.Replay(log, null, out first), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(ConstructionReplayExecutor.Replay(log, first, out second), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.AreEqual(first.fingerprint, second.fingerprint);
        Assert.AreEqual(first.era, second.era);
        Assert.AreEqual(first.activeBuildings, second.activeBuildings);
        Assert.AreEqual(first.producedUnits, second.producedUnits);
        Assert.AreEqual(first.reservations, second.reservations);
    }

    [Test]
    public void DetachedExecutorRejectsNonSequentialAgeUp()
    {
        ConstructionReplayLog log = new ConstructionReplayLog();
        log.TryAppend(Command("age", 1, 1, 1, ConstructionReplayCommandKind.AdvanceEra, "age:castle", ""));
        ConstructionReplaySnapshot snapshot;
        Assert.That(ConstructionReplayExecutor.Replay(log, null, out snapshot), Is.EqualTo(ConstructionReplayResult.InvalidCommand));
    }

    [Test]
    public void EconomicReplayPreventsDoubleSpendAndRefundsCancellation()
    {
        ConstructionReplayLog log = new ConstructionReplayLog();
        ConstructionReplayCommand reserve = Command("reserve", 1, 1, 1,
            ConstructionReplayCommandKind.ReserveResources, "house", "r1");
        reserve.populationCost = 1;
        reserve.costs.Add(new BuildingMaintenanceCost(EconomyResourceType.Wood, 30));
        log.TryAppend(reserve);
        EconomyLedger economy = new EconomyLedger();
        economy.Set(EconomyResourceType.Wood, 50);
        PopulationLedger population = new PopulationLedger(2, 0);
        ConstructionReplaySnapshot snapshot;
        Assert.That(ConstructionReplayExecutor.Replay(log, economy, population, null, out snapshot), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.AreEqual(20, economy.Get(EconomyResourceType.Wood));
        Assert.AreEqual(1, population.current);
        Assert.That(ConstructionReplayExecutor.Replay(log, economy, population, null, out snapshot), Is.EqualTo(ConstructionReplayResult.DivergentReplay));
        Assert.AreEqual(20, economy.Get(EconomyResourceType.Wood));
        Assert.AreEqual(1, population.current);
    }

    [Test]
    public void EconomicReplayAppliesProductionCostAndPopulationPayload()
    {
        ConstructionReplayCommand produce = Command("produce", 1, 1, 3,
            ConstructionReplayCommandKind.ProduceUnit, "spearman", "");
        produce.populationCost = 1;
        produce.costs.Add(new BuildingMaintenanceCost(EconomyResourceType.Food, 80));
        produce.costs.Add(new BuildingMaintenanceCost(EconomyResourceType.Wood, 30));
        ConstructionReplayLog log = new ConstructionReplayLog();
        Assert.That(log.TryAppend(produce), Is.EqualTo(ConstructionReplayResult.Accepted));

        EconomyLedger economy = new EconomyLedger();
        economy.Set(EconomyResourceType.Food, 100);
        economy.Set(EconomyResourceType.Wood, 50);
        PopulationLedger population = new PopulationLedger(2, 0);
        ConstructionReplaySnapshot snapshot;
        Assert.That(ConstructionReplayExecutor.Replay(log, economy, population, null, out snapshot),
            Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.AreEqual(20, economy.Get(EconomyResourceType.Food));
        Assert.AreEqual(20, economy.Get(EconomyResourceType.Wood));
        Assert.AreEqual(1, population.current);
        CollectionAssert.AreEqual(new[] { "spearman" }, snapshot.producedUnits);
    }

    [Test]
    public void EconomicReplayRejectsUnaffordableProductionAtomically()
    {
        ConstructionReplayCommand produce = Command("produce", 1, 1, 3,
            ConstructionReplayCommandKind.ProduceUnit, "horseman", "");
        produce.populationCost = 2;
        produce.costs.Add(new BuildingMaintenanceCost(EconomyResourceType.Food, 100));
        ConstructionReplayLog log = new ConstructionReplayLog();
        log.TryAppend(produce);

        EconomyLedger economy = new EconomyLedger();
        economy.Set(EconomyResourceType.Food, 99);
        PopulationLedger population = new PopulationLedger(2, 0);
        ConstructionReplaySnapshot snapshot;
        Assert.That(ConstructionReplayExecutor.Replay(log, economy, population, null, out snapshot),
            Is.EqualTo(ConstructionReplayResult.DivergentReplay));
        Assert.AreEqual(99, economy.Get(EconomyResourceType.Food));
        Assert.AreEqual(0, population.current);
        Assert.IsNull(snapshot);
    }

    private static ConstructionReplayCommand Command(string id, long tick, long time, int priority,
        ConstructionReplayCommandKind kind, string target, string reservation)
    {
        return new ConstructionReplayCommand { commandId = id, simulationTick = tick,
            issuedAtMilliseconds = time, priority = priority, ownerId = 0, kind = kind,
            targetCode = target, reservationId = reservation, quantity = 1 };
    }
}
