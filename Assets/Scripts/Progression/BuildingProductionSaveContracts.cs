using System;
using System.Collections.Generic;

public enum BuildingProductionSnapshotResult
{
    Captured,
    Restored,
    MissingCatalog,
    MissingState,
    ContentUnavailable,
    ProductUnavailable,
    UnsupportedEconomyResource,
    InvalidSnapshot,
    QueueLimitExceeded
}

[Serializable]
public sealed class BuildingProductionOrderSnapshot
{
    public string productCode;
    public float duration;
    public float elapsed;
    public int populationCost;
    public List<ResourceValue> cost = new List<ResourceValue>();
}

[Serializable]
public sealed class BuildingProductionQueueSnapshot
{
    public string buildingCode;
    public int ownerId;
    public BuildingEra era;
    public BuildingContentAssetStage assetOrigin;
    public List<BuildingProductionOrderSnapshot> orders = new List<BuildingProductionOrderSnapshot>();
}

/// <summary>
/// Save/load boundary for catalog-backed production. It copies queue data and
/// never serializes a prefab or a live queue reference.
/// </summary>
public static class BuildingProductionSaveRules
{
    public static BuildingProductionSnapshotResult Capture(
        BuildingContentCatalog catalog,
        StrategicPlanningState state,
        BuildingProductionQueue queue,
        string buildingCode,
        out BuildingProductionQueueSnapshot snapshot)
    {
        snapshot = null;
        if (catalog == null) return BuildingProductionSnapshotResult.MissingCatalog;
        if (state == null) return BuildingProductionSnapshotResult.MissingState;
        if (queue == null) return BuildingProductionSnapshotResult.InvalidSnapshot;
        BuildingContentSpec entry = catalog.Find(buildingCode);
        if (entry == null) return BuildingProductionSnapshotResult.MissingCatalog;
        if (BuildingContentProgressionRules.Validate(catalog, entry.code, state)
            != BuildingAvailabilityResult.Available)
            return BuildingProductionSnapshotResult.ContentUnavailable;

        BuildingProductionQueueSnapshot candidate = new BuildingProductionQueueSnapshot
        {
            buildingCode = entry.code.Trim(), ownerId = state.ownerId,
            era = state.era, assetOrigin = entry.AssetStage
        };
        if (queue.orders != null)
            for (int i = 0; i < queue.orders.Count; i++)
            {
                BuildingProductionOrder order = queue.orders[i];
                if (order == null || string.IsNullOrWhiteSpace(order.productCode))
                    return BuildingProductionSnapshotResult.InvalidSnapshot;
                BuildingProductionCatalogResult productResult = BuildingProductionCatalogRules.Evaluate(
                    catalog, entry.code, order.productCode, state);
                if (productResult == BuildingProductionCatalogResult.UnsupportedEconomyResource)
                    return BuildingProductionSnapshotResult.UnsupportedEconomyResource;
                if (productResult != BuildingProductionCatalogResult.Available)
                    return BuildingProductionSnapshotResult.ProductUnavailable;
                candidate.orders.Add(Copy(order));
            }
        snapshot = candidate;
        return BuildingProductionSnapshotResult.Captured;
    }

    public static BuildingProductionSnapshotResult Restore(
        BuildingContentCatalog catalog,
        StrategicPlanningState state,
        BuildingProductionQueue queue,
        BuildingProductionQueueSnapshot snapshot)
    {
        if (catalog == null) return BuildingProductionSnapshotResult.MissingCatalog;
        if (state == null) return BuildingProductionSnapshotResult.MissingState;
        if (queue == null || snapshot == null || string.IsNullOrWhiteSpace(snapshot.buildingCode))
            return BuildingProductionSnapshotResult.InvalidSnapshot;
        if (snapshot.ownerId != state.ownerId) return BuildingProductionSnapshotResult.InvalidSnapshot;
        BuildingContentSpec entry = catalog.Find(snapshot.buildingCode);
        if (entry == null) return BuildingProductionSnapshotResult.MissingCatalog;
        if (BuildingContentProgressionRules.Validate(catalog, entry.code, state)
            != BuildingAvailabilityResult.Available)
            return BuildingProductionSnapshotResult.ContentUnavailable;
        if (snapshot.orders == null || snapshot.orders.Count > queue.MaximumQueueSize)
            return BuildingProductionSnapshotResult.QueueLimitExceeded;

        List<BuildingProductionOrder> restored = new List<BuildingProductionOrder>();
        for (int i = 0; i < snapshot.orders.Count; i++)
        {
            BuildingProductionOrderSnapshot saved = snapshot.orders[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.productCode)
                || saved.duration <= 0f || saved.elapsed < 0f || saved.elapsed > saved.duration)
                return BuildingProductionSnapshotResult.InvalidSnapshot;
            BuildingProductionCatalogResult productResult = BuildingProductionCatalogRules.Evaluate(
                catalog, entry.code, saved.productCode, state);
            if (productResult == BuildingProductionCatalogResult.UnsupportedEconomyResource)
                return BuildingProductionSnapshotResult.UnsupportedEconomyResource;
            if (productResult != BuildingProductionCatalogResult.Available)
                return BuildingProductionSnapshotResult.ProductUnavailable;
            BuildingProductionOrder order = new BuildingProductionOrder
            {
                productCode = saved.productCode.Trim(), duration = saved.duration,
                elapsed = saved.elapsed, populationCost = saved.populationCost,
                cost = CopyCost(saved.cost)
            };
            restored.Add(order);
        }
        queue.orders = restored;
        return BuildingProductionSnapshotResult.Restored;
    }

    private static BuildingProductionOrderSnapshot Copy(BuildingProductionOrder order)
    {
        return new BuildingProductionOrderSnapshot
        {
            productCode = order.productCode.Trim(), duration = order.duration,
            elapsed = order.elapsed, populationCost = order.populationCost,
            cost = CopyCost(order.cost)
        };
    }

    private static List<ResourceValue> CopyCost(List<ResourceValue> source)
    {
        List<ResourceValue> copy = new List<ResourceValue>();
        if (source == null) return copy;
        for (int i = 0; i < source.Count; i++)
            if (source[i] != null) copy.Add(new ResourceValue(source[i].code, source[i].amount));
        return copy;
    }
}
