using System;
using System.Collections.Generic;

/// <summary>Result codes for cancelling an in-progress construction.</summary>
public enum BuildingConstructionCancellationResult
{
    Accepted,
    InvalidOwner,
    InvalidSnapshot,
    InvalidState,
    ContentUnavailable,
    MissingStockpile,
    InvalidCost,
    InvalidRefundRatio
}

/// <summary>
/// Pure transaction for cancelling a blueprint or under-construction building.
/// It refunds the saved reservation, releases saved workers, and returns a
/// detached post-cancellation snapshot. No prefab or runtime asset is created.
/// </summary>
public static class BuildingConstructionCancellationRules
{
    public static BuildingConstructionCancellationResult TryCancel(
        BuildingConstructionSaveSnapshot snapshot,
        int requesterOwnerId,
        EconomyLedger stockpile,
        float refundRatio,
        out BuildingConstructionSaveSnapshot cancelled,
        bool requireRuntimeReady = true)
    {
        cancelled = null;
        if (snapshot == null) return BuildingConstructionCancellationResult.InvalidSnapshot;
        BuildingConstructionSnapshotResult valid = BuildingConstructionSaveRules.Validate(
            snapshot, requesterOwnerId, requireRuntimeReady);
        if (valid == BuildingConstructionSnapshotResult.InvalidOwner)
            return BuildingConstructionCancellationResult.InvalidOwner;
        if (valid == BuildingConstructionSnapshotResult.ContentUnavailable)
            return BuildingConstructionCancellationResult.ContentUnavailable;
        if (valid == BuildingConstructionSnapshotResult.InvalidCost)
            return BuildingConstructionCancellationResult.InvalidCost;
        if (valid != BuildingConstructionSnapshotResult.Restored)
            return BuildingConstructionCancellationResult.InvalidSnapshot;
        if (snapshot.lifecycle != BuildingLifecycleState.Blueprint
            && snapshot.lifecycle != BuildingLifecycleState.UnderConstruction)
            return BuildingConstructionCancellationResult.InvalidState;
        if (stockpile == null) return BuildingConstructionCancellationResult.MissingStockpile;
        if (float.IsNaN(refundRatio) || float.IsInfinity(refundRatio)
            || refundRatio < 0f || refundRatio > 1f)
            return BuildingConstructionCancellationResult.InvalidRefundRatio;

        List<BuildingMaintenanceCost> reservation = Normalize(snapshot.reservedCosts);
        if (reservation == null) return BuildingConstructionCancellationResult.InvalidCost;
        for (int i = 0; i < reservation.Count; i++)
        {
            int refund = (int)Math.Floor(reservation[i].amount * refundRatio);
            if (refund > 0) stockpile.Add(reservation[i].resource, refund);
        }

        cancelled = new BuildingConstructionSaveSnapshot
        {
            buildingCode = snapshot.buildingCode.Trim(),
            ownerId = snapshot.ownerId,
            position = snapshot.position,
            constructionHP = 0,
            maxHP = snapshot.maxHP,
            lifecycle = BuildingLifecycleState.Ruined,
            constructorIds = new string[0],
            reservedCosts = new List<BuildingMaintenanceCost>(),
            assetOrigin = snapshot.assetOrigin,
            contentRuntimeReady = snapshot.contentRuntimeReady
        };
        return BuildingConstructionCancellationResult.Accepted;
    }

    private static List<BuildingMaintenanceCost> Normalize(List<BuildingMaintenanceCost> source)
    {
        List<BuildingMaintenanceCost> result = new List<BuildingMaintenanceCost>();
        if (source == null) return result;
        for (int i = 0; i < source.Count; i++)
        {
            BuildingMaintenanceCost item = source[i];
            if (item == null || item.amount < 0
                || !Enum.IsDefined(typeof(EconomyResourceType), item.resource)) return null;
            if (item.amount == 0) continue;
            BuildingMaintenanceCost existing = result.Find(x => x.resource == item.resource);
            if (existing == null) result.Add(new BuildingMaintenanceCost(item.resource, item.amount));
            else
            {
                long total = (long)existing.amount + item.amount;
                if (total > int.MaxValue) return null;
                existing.amount = (int)total;
            }
        }
        return result;
    }
}
