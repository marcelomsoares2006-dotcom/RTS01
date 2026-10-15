using NUnit.Framework;
using System.Collections.Generic;

public sealed class ConstructionReplayRuntimeRestoreTests
{
    [Test]
    public void RestorePropagatesDivergenceAndDoesNotPublishNullStateSilently()
    {
        ConstructionReplayLog log = new ConstructionReplayLog();
        log.TryAppend(new ConstructionReplayCommand {
            commandId = "build", ownerId = 0, simulationTick = 1,
            issuedAtMilliseconds = 1, kind = ConstructionReplayCommandKind.ConstructBuilding,
            targetCode = "house", quantity = 1
        });
        ConstructionReplaySnapshot wrong = new ConstructionReplaySnapshot { fingerprint = "WRONG" };
        ConstructionReplayResult result = ConstructionReplayRuntime.Restore(log.Commands, wrong);
        Assert.AreEqual(ConstructionReplayResult.DivergentReplay, result);
        Assert.AreEqual(result, ConstructionReplayRuntime.LastRestoreResult);
        Assert.IsNull(ConstructionReplayRuntime.State);
    }

    [Test]
    public void RestoreReturnsAcceptedAndPublishesDetachedState()
    {
        ConstructionReplayLog log = new ConstructionReplayLog();
        log.TryAppend(new ConstructionReplayCommand {
            commandId = "age", ownerId = 0, simulationTick = 1,
            issuedAtMilliseconds = 1, kind = ConstructionReplayCommandKind.AdvanceEra,
            targetCode = "age:feudal", quantity = 1
        });
        ConstructionReplaySnapshot expected;
        Assert.AreEqual(ConstructionReplayResult.Accepted,
            ConstructionReplayExecutor.Replay(log, null, out expected));
        Assert.AreEqual(ConstructionReplayResult.Accepted,
            ConstructionReplayRuntime.Restore(log.Commands, expected));
        Assert.IsNotNull(ConstructionReplayRuntime.State);
        Assert.AreEqual(BuildingEra.Feudal, ConstructionReplayRuntime.State.era);
    }

    [Test]
    public void LiveAdvanceEraCommandUpdatesReplayAndAdvancedEraState()
    {
        Assert.AreEqual(ConstructionReplayResult.Accepted,
            ConstructionReplayRuntime.Restore(new List<ConstructionReplayCommand>(), null));
        Assert.AreEqual(ConstructionReplayResult.Accepted,
            ConstructionReplayRuntime.TryAdvanceEra(0, BuildingEra.Feudal, 2, 2, 100, "live-age-1"));
        Assert.AreEqual(BuildingEra.Feudal, ConstructionReplayRuntime.State.era);
        Assert.AreEqual(EraImperial.Units.Advanced.ImperialEra.Classical, AdvancedEraState.Current);
        Assert.IsTrue(ConstructionReplayRuntime.ExportCommands().Exists(command => command.commandId == "live-age-1"));
        ConstructionReplayRuntime.Restore(new List<ConstructionReplayCommand>(), null);
    }
}
