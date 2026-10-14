using NUnit.Framework;

public sealed class BuildingMaintenanceRuntimeBindingTests
{
    [Test]
    public void BindingRejectsMissingDependenciesWithoutCreatingRuntimeState()
    {
        BuildingMaintenanceRuntimeBinding binding;
        string reason;
        Assert.IsFalse(BuildingMaintenanceRuntimeBinding.TryCreate(null, null, null, out binding, out reason));
        Assert.IsNull(binding);
        Assert.IsNotEmpty(reason);
    }

    [Test]
    public void SnapshotReportsIdleStateForServiceWithoutOperation()
    {
        BuildingMaintenanceServiceTestHelper helper = new BuildingMaintenanceServiceTestHelper();
        BuildingMaintenanceRuntimeSnapshot snapshot = helper.Snapshot;
        Assert.AreEqual(BuildingMaintenanceOperation.None, snapshot.operation);
        Assert.IsFalse(snapshot.busy);
        Assert.AreEqual(0f, snapshot.progress);
    }

    [Test]
    public void RestoreOperationPreservesReservedCostWithoutSpendingTwice()
    {
        EconomyLedger ledger = new EconomyLedger();
        ledger.Set(EconomyResourceType.Wood, 40);
        BuildingRepairDemolitionService service = new BuildingRepairDemolitionService(
            "building-1", 0, 50, 100, BuildingLifecycleState.Damaged, ledger,
            new BuildingMaintenanceSpec { repairDuration = 4f });
        BuildingMaintenanceOperationState saved = new BuildingMaintenanceOperationState {
            operation = BuildingMaintenanceOperation.Repair,
            elapsed = 1f, duration = 4f, initialHealth = 50,
            initialLifecycle = BuildingLifecycleState.Damaged,
            reservedCost = new System.Collections.Generic.List<BuildingMaintenanceCost> {
                new BuildingMaintenanceCost(EconomyResourceType.Wood, 12)
            }
        };
        Assert.IsTrue(service.RestoreOperation(saved));
        Assert.AreEqual(40, ledger.Get(EconomyResourceType.Wood));
        Assert.IsTrue(service.IsBusy);
        Assert.AreEqual(12, service.Operation.reservedCost[0].amount);
    }

    private sealed class BuildingMaintenanceServiceTestHelper
    {
        public BuildingMaintenanceRuntimeSnapshot Snapshot { get; private set; }

        public BuildingMaintenanceServiceTestHelper()
        {
            EconomyLedger ledger = new EconomyLedger();
            BuildingRepairDemolitionService service = new BuildingRepairDemolitionService(
                "building-1", 0, 100, 100, BuildingLifecycleState.Operational,
                ledger, new BuildingMaintenanceSpec());
            Snapshot = new BuildingMaintenanceRuntimeSnapshot(null, service);
        }
    }
}
