using System.Collections.Generic;
using NUnit.Framework;

public sealed class BuildingRepairDemolitionContractTests
{
    private static BuildingMaintenanceSpec Spec()
    {
        return new BuildingMaintenanceSpec {
            repairCost = new List<BuildingMaintenanceCost> {
                new BuildingMaintenanceCost(EconomyResourceType.Wood, 10)
            },
            repairDuration = 4f,
            demolitionCost = new List<BuildingMaintenanceCost> {
                new BuildingMaintenanceCost(EconomyResourceType.Food, 2)
            },
            demolitionDuration = 2f,
            demolitionRefund = new List<BuildingMaintenanceCost> {
                new BuildingMaintenanceCost(EconomyResourceType.Wood, 20)
            },
            demolitionRefundRatio = 0.5f
        };
    }

    [Test]
    public void RepairReservesCostAndCompletesOperationalBuilding()
    {
        EconomyLedger economy = new EconomyLedger();
        economy.Set(EconomyResourceType.Wood, 20);
        BuildingRepairDemolitionService service = new BuildingRepairDemolitionService(
            "tower-1", 3, 40, 100, BuildingLifecycleState.Damaged, economy, Spec());

        Assert.AreEqual(BuildingMaintenanceResult.Accepted, service.TryBeginRepair(3));
        Assert.AreEqual(10, economy.Get(EconomyResourceType.Wood));
        bool complete;
        service.Advance(3, 2f, out complete);
        Assert.IsFalse(complete);
        Assert.AreEqual(70, service.CurrentHealth);
        service.Advance(3, 2f, out complete);
        Assert.IsTrue(complete);
        Assert.AreEqual(BuildingLifecycleState.Operational, service.Lifecycle);
        Assert.AreEqual(100, service.CurrentHealth);
    }

    [Test]
    public void WrongOwnerCannotStartOrAdvanceRepair()
    {
        EconomyLedger economy = new EconomyLedger();
        economy.Set(EconomyResourceType.Wood, 20);
        BuildingRepairDemolitionService service = new BuildingRepairDemolitionService(
            "tower-1", 3, 40, 100, BuildingLifecycleState.Damaged, economy, Spec());
        Assert.AreEqual(BuildingMaintenanceResult.InvalidOwner, service.TryBeginRepair(4));
        Assert.AreEqual(BuildingMaintenanceResult.InvalidOwner, service.Advance(4, 1f, out _));
        Assert.AreEqual(20, economy.Get(EconomyResourceType.Wood));
    }

    [Test]
    public void CancellingRepairRollsBackHealthStateAndReservation()
    {
        EconomyLedger economy = new EconomyLedger();
        economy.Set(EconomyResourceType.Wood, 20);
        BuildingRepairDemolitionService service = new BuildingRepairDemolitionService(
            "tower-1", 3, 0, 100, BuildingLifecycleState.Destroyed, economy, new BuildingMaintenanceSpec {
                repairCost = new List<BuildingMaintenanceCost> { new BuildingMaintenanceCost(EconomyResourceType.Wood, 10) },
                repairDuration = 4f,
                allowDestroyedRepair = true
            });
        Assert.AreEqual(BuildingMaintenanceResult.Accepted, service.TryBeginRepair(3));
        service.Advance(3, 1f, out _);
        Assert.AreEqual(BuildingMaintenanceResult.Accepted, service.TryCancel(3));
        Assert.AreEqual(20, economy.Get(EconomyResourceType.Wood));
        Assert.AreEqual(0, service.CurrentHealth);
        Assert.AreEqual(BuildingLifecycleState.Destroyed, service.Lifecycle);
        Assert.IsFalse(service.IsBusy);
    }

    [Test]
    public void DemolitionChargesCostAndRefundsOnlyOnCompletion()
    {
        EconomyLedger economy = new EconomyLedger();
        economy.Set(EconomyResourceType.Food, 5);
        economy.Set(EconomyResourceType.Wood, 0);
        BuildingRepairDemolitionService service = new BuildingRepairDemolitionService(
            "house-1", 1, 100, 100, BuildingLifecycleState.Operational, economy, Spec());

        Assert.AreEqual(BuildingMaintenanceResult.Accepted, service.TryBeginDemolition(1));
        Assert.AreEqual(3, economy.Get(EconomyResourceType.Food));
        Assert.AreEqual(0, economy.Get(EconomyResourceType.Wood));
        service.Advance(1, 1f, out var complete);
        Assert.IsFalse(complete);
        Assert.AreEqual(BuildingLifecycleState.Operational, service.Lifecycle);
        service.Advance(1, 1f, out complete);
        Assert.IsTrue(complete);
        Assert.AreEqual(BuildingLifecycleState.Ruined, service.Lifecycle);
        Assert.AreEqual(10, economy.Get(EconomyResourceType.Wood));
        Assert.AreEqual(0, economy.Get(EconomyResourceType.Food));
    }

    [Test]
    public void FailedAffordabilityAndInvalidCostsDoNotMutateLedger()
    {
        EconomyLedger economy = new EconomyLedger();
        economy.Set(EconomyResourceType.Wood, 3);
        BuildingRepairDemolitionService service = new BuildingRepairDemolitionService(
            "tower-1", 0, 50, 100, BuildingLifecycleState.Damaged, economy, new BuildingMaintenanceSpec {
                repairCost = new List<BuildingMaintenanceCost> {
                    new BuildingMaintenanceCost(EconomyResourceType.Wood, 2),
                    new BuildingMaintenanceCost(EconomyResourceType.Wood, 2)
                },
                repairDuration = 1f
            });
        Assert.AreEqual(BuildingMaintenanceResult.InsufficientResources, service.TryBeginRepair(0));
        Assert.AreEqual(3, economy.Get(EconomyResourceType.Wood));
        Assert.IsFalse(service.IsBusy);
    }
}
