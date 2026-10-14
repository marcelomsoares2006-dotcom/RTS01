using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BuildingConstructionSaveContractsTests
{
    [Test]
    public void SnapshotPreservesBlueprintProgressLifecycleWorkersReservationsAndContentStage()
    {
        BuildingConstructionSaveSnapshot snapshot;
        Assert.That(BuildingConstructionSaveRules.Capture(
            "town_center", 2, new Vector3(4f, 0f, 8f), 250, 1000,
            BuildingLifecycleState.UnderConstruction,
            new[] { " worker-a ", "worker-a", "worker-b" },
            new List<BuildingMaintenanceCost>
            {
                new BuildingMaintenanceCost(EconomyResourceType.Wood, 30),
                new BuildingMaintenanceCost(EconomyResourceType.Wood, 12),
                new BuildingMaintenanceCost(EconomyResourceType.Stone, 0)
            },
            BuildingContentAssetStage.RuntimeReady, true, out snapshot),
            Is.EqualTo(BuildingConstructionSnapshotResult.Captured));

        Assert.That(snapshot.constructionHP, Is.EqualTo(250));
        Assert.That(snapshot.lifecycle, Is.EqualTo(BuildingLifecycleState.UnderConstruction));
        Assert.That(snapshot.constructorIds, Is.EqualTo(new[] { "worker-a", "worker-b" }));
        Assert.That(snapshot.reservedCosts.Count, Is.EqualTo(1));
        Assert.That(snapshot.reservedCosts[0].amount, Is.EqualTo(42));
        Assert.That(BuildingConstructionSaveRules.Validate(snapshot, 2, true),
            Is.EqualTo(BuildingConstructionSnapshotResult.Restored));
    }

    [Test]
    public void IntermediateContentIsPersistedButNeverRuntimeRestored()
    {
        BuildingConstructionSaveSnapshot snapshot;
        Assert.That(BuildingConstructionSaveRules.Capture(
            "resource_dropoff", 0, Vector3.zero, 0, 500,
            BuildingLifecycleState.Blueprint, null, null,
            BuildingContentAssetStage.ModelIntermediate, false, out snapshot),
            Is.EqualTo(BuildingConstructionSnapshotResult.Captured));

        Assert.That(BuildingConstructionSaveRules.Validate(snapshot, 0, false),
            Is.EqualTo(BuildingConstructionSnapshotResult.Restored));
        Assert.That(BuildingConstructionSaveRules.Validate(snapshot, 0, true),
            Is.EqualTo(BuildingConstructionSnapshotResult.ContentUnavailable));
        Assert.That(BuildingConstructionSaveRules.CanRestoreRuntime(snapshot, 0), Is.False);
    }

    [Test]
    public void InvalidReservationAndOwnerAreRejected()
    {
        BuildingConstructionSaveSnapshot snapshot;
        Assert.That(BuildingConstructionSaveRules.Capture(
            "house", 0, Vector3.zero, 1, 10, BuildingLifecycleState.UnderConstruction,
            null,
            new[] { new BuildingMaintenanceCost(EconomyResourceType.Wood, -1) },
            BuildingContentAssetStage.RuntimeReady, true, out snapshot),
            Is.EqualTo(BuildingConstructionSnapshotResult.InvalidCost));

        Assert.That(BuildingConstructionSaveRules.Capture(
            "house", 0, Vector3.zero, 1, 10, BuildingLifecycleState.UnderConstruction,
            null, null, BuildingContentAssetStage.RuntimeReady, true, out snapshot),
            Is.EqualTo(BuildingConstructionSnapshotResult.Captured));
        Assert.That(BuildingConstructionSaveRules.Validate(snapshot, 1, true),
            Is.EqualTo(BuildingConstructionSnapshotResult.InvalidOwner));
    }
}
