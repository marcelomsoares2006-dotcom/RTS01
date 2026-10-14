using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BuildingConstructionCancellationRulesTests
{
    private static BuildingConstructionSaveSnapshot Snapshot(BuildingLifecycleState lifecycle)
    {
        BuildingConstructionSaveSnapshot snapshot;
        Assert.That(BuildingConstructionSaveRules.Capture(
            "town_center", 0, Vector3.zero, lifecycle == BuildingLifecycleState.Blueprint ? 0 : 40,
            100, lifecycle, new[] { "worker-a", "worker-b" },
            new List<BuildingMaintenanceCost>
            {
                new BuildingMaintenanceCost(EconomyResourceType.Wood, 30),
                new BuildingMaintenanceCost(EconomyResourceType.Wood, 10)
            }, BuildingContentAssetStage.RuntimeReady, true, out snapshot),
            Is.EqualTo(BuildingConstructionSnapshotResult.Captured));
        return snapshot;
    }

    [TestCase(BuildingLifecycleState.Blueprint)]
    [TestCase(BuildingLifecycleState.UnderConstruction)]
    public void CancelsConstructionRefundsReservationAndReleasesWorkers(BuildingLifecycleState lifecycle)
    {
        EconomyLedger stockpile = new EconomyLedger();
        BuildingConstructionSaveSnapshot cancelled;
        Assert.That(BuildingConstructionCancellationRules.TryCancel(
            Snapshot(lifecycle), 0, stockpile, 0.5f, out cancelled),
            Is.EqualTo(BuildingConstructionCancellationResult.Accepted));
        Assert.That(stockpile.Get(EconomyResourceType.Wood), Is.EqualTo(20));
        Assert.That(cancelled.lifecycle, Is.EqualTo(BuildingLifecycleState.Ruined));
        Assert.That(cancelled.constructionHP, Is.EqualTo(0));
        Assert.That(cancelled.constructorIds, Is.Empty);
        Assert.That(cancelled.reservedCosts, Is.Empty);
        Assert.That(BuildingConstructionSaveRules.Validate(cancelled, 0, true),
            Is.EqualTo(BuildingConstructionSnapshotResult.Restored));
    }

    [TestCase(BuildingLifecycleState.Operational)]
    [TestCase(BuildingLifecycleState.Damaged)]
    [TestCase(BuildingLifecycleState.Destroyed)]
    [TestCase(BuildingLifecycleState.Ruined)]
    public void CompletedOrCombatStatesCannotUseConstructionCancellation(BuildingLifecycleState lifecycle)
    {
        BuildingConstructionSaveSnapshot cancelled;
        Assert.That(BuildingConstructionCancellationRules.TryCancel(
            Snapshot(lifecycle), 0, new EconomyLedger(), 1f, out cancelled),
            Is.EqualTo(BuildingConstructionCancellationResult.InvalidState));
    }

    [Test]
    public void IntermediateContentCanBeAuditedButCannotBeRuntimeCancelled()
    {
        BuildingConstructionSaveSnapshot snapshot;
        Assert.That(BuildingConstructionSaveRules.Capture(
            "resource_dropoff", 0, Vector3.zero, 0, 100, BuildingLifecycleState.Blueprint,
            new[] { "worker-a" }, null, BuildingContentAssetStage.ModelIntermediate, false,
            out snapshot), Is.EqualTo(BuildingConstructionSnapshotResult.Captured));
        BuildingConstructionSaveSnapshot cancelled;
        Assert.That(BuildingConstructionCancellationRules.TryCancel(
            snapshot, 0, new EconomyLedger(), 1f, out cancelled),
            Is.EqualTo(BuildingConstructionCancellationResult.ContentUnavailable));
        Assert.That(BuildingConstructionCancellationRules.TryCancel(
            snapshot, 0, new EconomyLedger(), 1f, out cancelled, false),
            Is.EqualTo(BuildingConstructionCancellationResult.Accepted));
    }

    [Test]
    public void InvalidOwnerAndRefundRatioDoNotMutateStockpile()
    {
        EconomyLedger stockpile = new EconomyLedger();
        BuildingConstructionSaveSnapshot cancelled;
        Assert.That(BuildingConstructionCancellationRules.TryCancel(
            Snapshot(BuildingLifecycleState.Blueprint), 4, stockpile, 1f, out cancelled),
            Is.EqualTo(BuildingConstructionCancellationResult.InvalidOwner));
        Assert.That(BuildingConstructionCancellationRules.TryCancel(
            Snapshot(BuildingLifecycleState.Blueprint), 0, stockpile, 2f, out cancelled),
            Is.EqualTo(BuildingConstructionCancellationResult.InvalidRefundRatio));
        Assert.That(stockpile.Get(EconomyResourceType.Wood), Is.EqualTo(0));
    }
}
