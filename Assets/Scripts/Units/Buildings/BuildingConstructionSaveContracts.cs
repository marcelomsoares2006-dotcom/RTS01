using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Stable result codes for the construction save boundary.</summary>
public enum BuildingConstructionSnapshotResult
{
    Captured,
    Restored,
    InvalidSnapshot,
    ContentUnavailable,
    InvalidOwner,
    InvalidCost
}

/// <summary>
/// Detached save data for one construction. It contains state only: no prefab,
/// BuildingData reference or scene object is serialized. Planned/intermediate
/// content can therefore remain visible in a save without becoming spawnable.
/// </summary>
[Serializable]
public sealed class BuildingConstructionSaveSnapshot
{
    public string buildingCode;
    public int ownerId;
    public Vector3 position;
    public int constructionHP;
    public int maxHP;
    public BuildingLifecycleState lifecycle;
    public string[] constructorIds = new string[0];
    public List<BuildingMaintenanceCost> reservedCosts = new List<BuildingMaintenanceCost>();
    public string reservationId;
    public int reservedPopulation;
    public BuildingContentAssetStage assetOrigin;
    public bool contentRuntimeReady;
}

/// <summary>
/// Pure capture/validation rules used by save/load adapters and editor-free
/// tests. Runtime restoration must still resolve a real authored asset.
/// </summary>
public static class BuildingConstructionSaveRules
{
    public static BuildingConstructionSnapshotResult Capture(
        string buildingCode,
        int ownerId,
        Vector3 position,
        int constructionHP,
        int maxHP,
        BuildingLifecycleState lifecycle,
        IEnumerable<string> constructorIds,
        IEnumerable<BuildingMaintenanceCost> reservedCosts,
        BuildingContentAssetStage assetOrigin,
        bool contentRuntimeReady,
        out BuildingConstructionSaveSnapshot snapshot)
    {
        snapshot = null;
        if (string.IsNullOrWhiteSpace(buildingCode) || ownerId < 0 || maxHP <= 0)
            return BuildingConstructionSnapshotResult.InvalidSnapshot;
        if (constructionHP < 0 || constructionHP > maxHP)
            return BuildingConstructionSnapshotResult.InvalidSnapshot;
        if (!IsFinite(position) || !Enum.IsDefined(typeof(BuildingLifecycleState), lifecycle))
            return BuildingConstructionSnapshotResult.InvalidSnapshot;

        List<BuildingMaintenanceCost> costs;
        if (!TryNormalizeCosts(reservedCosts, out costs))
            return BuildingConstructionSnapshotResult.InvalidCost;

        snapshot = new BuildingConstructionSaveSnapshot
        {
            buildingCode = buildingCode.Trim(),
            ownerId = ownerId,
            position = position,
            constructionHP = constructionHP,
            maxHP = maxHP,
            lifecycle = lifecycle,
            constructorIds = NormalizeConstructorIds(constructorIds),
            reservedCosts = costs,
            assetOrigin = assetOrigin,
            contentRuntimeReady = contentRuntimeReady && assetOrigin == BuildingContentAssetStage.RuntimeReady
        };
        return BuildingConstructionSnapshotResult.Captured;
    }

    public static BuildingConstructionSnapshotResult Validate(
        BuildingConstructionSaveSnapshot snapshot,
        int expectedOwnerId,
        bool requireRuntimeReady)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.buildingCode)
            || snapshot.ownerId < 0 || snapshot.ownerId != expectedOwnerId
            || snapshot.maxHP <= 0 || snapshot.constructionHP < 0
            || snapshot.constructionHP > snapshot.maxHP || !IsFinite(snapshot.position)
            || !Enum.IsDefined(typeof(BuildingLifecycleState), snapshot.lifecycle))
            return snapshot != null && snapshot.ownerId != expectedOwnerId
                ? BuildingConstructionSnapshotResult.InvalidOwner
                : BuildingConstructionSnapshotResult.InvalidSnapshot;
        List<BuildingMaintenanceCost> costs;
        if (!TryNormalizeCosts(snapshot.reservedCosts, out costs))
            return BuildingConstructionSnapshotResult.InvalidCost;
        if (requireRuntimeReady && (!snapshot.contentRuntimeReady
            || snapshot.assetOrigin != BuildingContentAssetStage.RuntimeReady))
            return BuildingConstructionSnapshotResult.ContentUnavailable;
        return BuildingConstructionSnapshotResult.Restored;
    }

    public static bool CanRestoreRuntime(BuildingConstructionSaveSnapshot snapshot, int expectedOwnerId)
    {
        return Validate(snapshot, expectedOwnerId, true) == BuildingConstructionSnapshotResult.Restored;
    }

    private static string[] NormalizeConstructorIds(IEnumerable<string> source)
    {
        List<string> result = new List<string>();
        if (source == null) return result.ToArray();
        foreach (string raw in source)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            string id = raw.Trim();
            if (!result.Contains(id)) result.Add(id);
        }
        return result.ToArray();
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    private static bool TryNormalizeCosts(
        IEnumerable<BuildingMaintenanceCost> source,
        out List<BuildingMaintenanceCost> result)
    {
        result = new List<BuildingMaintenanceCost>();
        if (source == null) return true;
        foreach (BuildingMaintenanceCost item in source)
        {
            if (item == null || item.amount < 0) return false;
            if (item.amount == 0) continue;
            BuildingMaintenanceCost existing = result.Find(x => x.resource == item.resource);
            if (existing == null) result.Add(new BuildingMaintenanceCost(item.resource, item.amount));
            else
            {
                long total = (long)existing.amount + item.amount;
                if (total > int.MaxValue) return false;
                existing.amount = (int)total;
            }
        }
        return true;
    }
}
