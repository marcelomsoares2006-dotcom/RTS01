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
    QueueLimitExceeded,
    QueueNotEmpty,
    StateMismatch
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
    public List<string> unlockedTechnologyCodes = new List<string>();
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
            era = state.era, assetOrigin = entry.AssetStage,
            unlockedTechnologyCodes = CopyTechnologyCodes(state.unlockedTechnologyCodes)
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
                if (!MatchesCatalogOrder(catalog, entry.code, order, state))
                    return BuildingProductionSnapshotResult.InvalidSnapshot;
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
        // Restore is a replacement operation. Refusing a non-empty queue keeps
        // its existing resource/population reservations owned by the live
        // queue instead of silently orphaning them.
        if (queue.Count > 0) return BuildingProductionSnapshotResult.QueueNotEmpty;
        if (snapshot.ownerId != state.ownerId) return BuildingProductionSnapshotResult.InvalidSnapshot;
        BuildingContentSpec entry = catalog.Find(snapshot.buildingCode);
        if (entry == null) return BuildingProductionSnapshotResult.MissingCatalog;
        if (snapshot.era != state.era || snapshot.assetOrigin != entry.AssetStage
            || !TechnologyCodesMatch(snapshot.unlockedTechnologyCodes, state.unlockedTechnologyCodes))
            return BuildingProductionSnapshotResult.StateMismatch;
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
            if (!MatchesCatalogOrder(catalog, entry.code, saved, state))
                return BuildingProductionSnapshotResult.InvalidSnapshot;
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

    private static bool MatchesCatalogOrder(
        BuildingContentCatalog catalog,
        string buildingCode,
        BuildingProductionOrder order,
        StrategicPlanningState state)
    {
        if (order == null) return false;
        return MatchesCatalogOrder(catalog, buildingCode, order.productCode,
            order.duration, order.elapsed, order.populationCost, order.cost, state);
    }

    private static bool MatchesCatalogOrder(
        BuildingContentCatalog catalog,
        string buildingCode,
        BuildingProductionOrderSnapshot order,
        StrategicPlanningState state)
    {
        if (order == null) return false;
        return MatchesCatalogOrder(catalog, buildingCode, order.productCode,
            order.duration, order.elapsed, order.populationCost, order.cost, state);
    }

    private static bool MatchesCatalogOrder(
        BuildingContentCatalog catalog,
        string buildingCode,
        string productCode,
        float duration,
        float elapsed,
        int populationCost,
        List<ResourceValue> savedCost,
        StrategicPlanningState state)
    {
        BuildingProductionEntry expected;
        BuildingProductionCatalogResult result;
        if (!BuildingProductionCatalogRules.TryCreateLegacyQueueEntry(
            catalog, buildingCode, productCode, state, out expected, out result))
            return false;
        if (Math.Abs(expected.productionTime - duration) > 0.001f
            || expected.populationCost != populationCost
            || elapsed < 0f || elapsed > duration)
            return false;
        Dictionary<InGameResource, int> expectedCost = ToCostMap(expected.cost);
        Dictionary<InGameResource, int> actualCost = ToCostMap(savedCost);
        if (expectedCost.Count != actualCost.Count) return false;
        foreach (KeyValuePair<InGameResource, int> pair in expectedCost)
        {
            int amount;
            if (!actualCost.TryGetValue(pair.Key, out amount) || amount != pair.Value)
                return false;
        }
        return true;
    }

    private static Dictionary<InGameResource, int> ToCostMap(List<ResourceValue> values)
    {
        Dictionary<InGameResource, int> result = new Dictionary<InGameResource, int>();
        if (values == null) return result;
        for (int i = 0; i < values.Count; i++)
        {
            ResourceValue value = values[i];
            if (value == null || value.amount <= 0) return new Dictionary<InGameResource, int>();
            int current;
            result.TryGetValue(value.code, out current);
            long total = (long)current + value.amount;
            if (total > int.MaxValue) return new Dictionary<InGameResource, int>();
            result[value.code] = (int)total;
        }
        return result;
    }

    private static List<ResourceValue> CopyCost(List<ResourceValue> source)
    {
        List<ResourceValue> copy = new List<ResourceValue>();
        if (source == null) return copy;
        for (int i = 0; i < source.Count; i++)
            if (source[i] != null) copy.Add(new ResourceValue(source[i].code, source[i].amount));
        return copy;
    }

    private static List<string> CopyTechnologyCodes(List<string> source)
    {
        List<string> copy = new List<string>();
        if (source == null) return copy;
        for (int i = 0; i < source.Count; i++)
            if (!string.IsNullOrWhiteSpace(source[i])) copy.Add(source[i].Trim());
        copy.Sort(StringComparer.OrdinalIgnoreCase);
        return copy;
    }

    private static bool TechnologyCodesMatch(List<string> saved, List<string> current)
    {
        List<string> left = CopyTechnologyCodes(saved);
        List<string> right = CopyTechnologyCodes(current);
        if (left.Count != right.Count) return false;
        for (int i = 0; i < left.Count; i++)
            if (!string.Equals(left[i], right[i], StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}
