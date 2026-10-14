using NUnit.Framework;
using UnityEngine;

public sealed class StrategicRuntimeAdapterTests
{
    private sealed class SnapshotSource : IStrategicRuntimeSnapshotSource
    {
        public StrategicPlanningState state;
        public StrategicPlanningState Capture(int ownerId) { return state; }
    }

    private sealed class Executor : IStrategicIntentExecutor
    {
        public bool called;
        public bool shouldAccept;
        public bool TryExecute(int ownerId, StrategicActionIntent intent, out string failureReason)
        {
            called = true;
            failureReason = shouldAccept ? null : "test rejection";
            return shouldAccept;
        }
    }

    [Test]
    public void GatewayRejectsForeignTargetBeforeExecutor()
    {
        SnapshotSource source = new SnapshotSource
        {
            state = new StrategicPlanningState()
        };
        source.state.units.Add(new StrategicUnitObservation { code = "Enemy", ownerId = 1 });
        Executor executor = new Executor { shouldAccept = true };
        StrategicIntentResult result = new StrategicIntentGateway(source, executor)
            .TryExecute(0, new StrategicActionIntent { action = StrategicActionKind.DefendArea, targetCode = "enemy", quantity = 1 });

        Assert.IsFalse(result.accepted);
        Assert.AreEqual(StrategicIntentRejectionReason.TargetOwnedByAnotherPlayer, result.rejectionReason);
        Assert.IsFalse(executor.called);
    }

    [Test]
    public void GatewayAllowsOwnTargetByStableEntityId()
    {
        SnapshotSource source = new SnapshotSource { state = new StrategicPlanningState() };
        source.state.units.Add(new StrategicUnitObservation { entityId = "unit-ally-7", code = "Archer", ownerId = 0 });
        Executor executor = new Executor { shouldAccept = true };

        StrategicIntentResult result = new StrategicIntentGateway(source, executor)
            .TryExecute(0, new StrategicActionIntent
            {
                action = StrategicActionKind.DefendArea,
                targetCode = "Archer",
                targetEntityId = "unit-ally-7",
                quantity = 1
            });

        Assert.IsTrue(result.accepted);
        Assert.IsTrue(executor.called);
    }

    [Test]
    public void GatewayRejectsDuplicateCodeWithoutDisambiguator()
    {
        SnapshotSource source = new SnapshotSource { state = new StrategicPlanningState() };
        source.state.units.Add(new StrategicUnitObservation { entityId = "unit-1", code = "Archer", ownerId = 0 });
        source.state.units.Add(new StrategicUnitObservation { entityId = "unit-2", code = "Archer", ownerId = 1 });
        Executor executor = new Executor { shouldAccept = true };

        StrategicIntentResult result = new StrategicIntentGateway(source, executor)
            .TryExecute(0, new StrategicActionIntent
            {
                action = StrategicActionKind.DefendArea,
                targetCode = "Archer",
                quantity = 1
            });

        Assert.IsFalse(result.accepted);
        Assert.AreEqual(StrategicIntentRejectionReason.AmbiguousTarget, result.rejectionReason);
        Assert.IsFalse(executor.called);
    }

    [Test]
    public void GatewayResolvesDuplicateCodeByOwnerAndPosition()
    {
        SnapshotSource source = new SnapshotSource { state = new StrategicPlanningState() };
        source.state.units.Add(new StrategicUnitObservation { entityId = "unit-1", code = "Archer", ownerId = 0, position = new Vector3(2f, 0f, 2f) });
        source.state.units.Add(new StrategicUnitObservation { entityId = "unit-2", code = "Archer", ownerId = 1, position = new Vector3(20f, 0f, 20f) });
        Executor executor = new Executor { shouldAccept = true };

        StrategicIntentResult result = new StrategicIntentGateway(source, executor)
            .TryExecute(0, new StrategicActionIntent
            {
                action = StrategicActionKind.DefendArea,
                targetCode = "Archer",
                targetPosition = new Vector3(2.2f, 0f, 2.1f),
                hasTargetPosition = true,
                quantity = 1
            });

        Assert.IsTrue(result.accepted);
        Assert.IsTrue(executor.called);
    }

    [Test]
    public void GatewayReturnsExplicitExecutorRejection()
    {
        SnapshotSource source = new SnapshotSource { state = new StrategicPlanningState() };
        Executor executor = new Executor { shouldAccept = false };
        StrategicIntentResult result = new StrategicIntentGateway(source, executor)
            .TryExecute(0, new StrategicActionIntent { action = StrategicActionKind.ScoutArea, quantity = 1 });

        Assert.IsFalse(result.accepted);
        Assert.AreEqual(StrategicIntentRejectionReason.ExecutorRejected, result.rejectionReason);
        Assert.AreEqual("test rejection", result.message);
    }

    [Test]
    public void GatewayAllowsAbstractTargetsWhenExecutorAccepts()
    {
        SnapshotSource source = new SnapshotSource { state = new StrategicPlanningState() };
        Executor executor = new Executor { shouldAccept = true };
        StrategicIntentResult result = new StrategicIntentGateway(source, executor)
            .TryExecute(0, new StrategicActionIntent { action = StrategicActionKind.ConstructBuilding, targetCode = "FutureKeep", quantity = 1 });

        Assert.IsTrue(result.accepted);
        Assert.IsTrue(executor.called);
    }
}
