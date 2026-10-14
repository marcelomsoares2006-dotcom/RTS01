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
