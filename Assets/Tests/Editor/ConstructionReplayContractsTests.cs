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

    private static ConstructionReplayCommand Command(string id, long tick, long time, int priority,
        ConstructionReplayCommandKind kind, string target, string reservation)
    {
        return new ConstructionReplayCommand { commandId = id, simulationTick = tick,
            issuedAtMilliseconds = time, priority = priority, ownerId = 0, kind = kind,
            targetCode = target, reservationId = reservation, quantity = 1 };
    }
}
