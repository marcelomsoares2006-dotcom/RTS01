using System;
using System.Collections.Generic;

/// <summary>Failure reasons returned by the deterministic building production contract.</summary>
public enum BuildingProductionQueueResult
{
    Accepted,
    MissingEntry,
    MissingProductCode,
    InvalidDuration,
    QueueFull,
    MissingStockpile,
    MissingPopulationLedger,
    InvalidCost,
    InsufficientResources,
    PopulationCap
}

/// <summary>
/// A serializable snapshot of one production reservation. Resources and
/// population are reserved on enqueue, which makes cancellation and replay
/// deterministic and prevents two callers from spending the same stockpile.
/// </summary>
[Serializable]
public sealed class BuildingProductionOrder
{
    public string productCode;
    public float duration;
    public float elapsed;
    public int populationCost;
    public List<ResourceValue> cost = new List<ResourceValue>();

    public float Remaining
    {
        get { return Math.Max(0f, duration - elapsed); }
    }

    public BuildingProductionOrder() { }

    public BuildingProductionOrder(BuildingProductionEntry entry)
    {
        productCode = entry.productCode.Trim();
        duration = entry.productionTime;
        populationCost = entry.populationCost;
        if (entry.cost != null)
        {
            for (int i = 0; i < entry.cost.Count; i++)
            {
                ResourceValue value = entry.cost[i];
                if (value != null && value.amount > 0)
                    cost.Add(new ResourceValue(value.code, value.amount));
            }
        }
    }
}

/// <summary>
/// UI and engine independent FIFO queue for production from a building.
/// The queue owns reservations, while callers remain responsible for spawning
/// the completed product when <see cref="Advance"/> returns its code.
/// </summary>
[Serializable]
public sealed class BuildingProductionQueue
{
    public List<BuildingProductionOrder> orders = new List<BuildingProductionOrder>();

    private readonly int _maximumQueueSize;

    public BuildingProductionQueue(int maximumQueueSize = 5)
    {
        _maximumQueueSize = Math.Max(1, maximumQueueSize);
    }

    public int Count { get { return orders == null ? 0 : orders.Count; } }
    public int MaximumQueueSize { get { return _maximumQueueSize; } }
    public BuildingProductionOrder Peek
    {
        get { return Count == 0 ? null : orders[0]; }
    }

    public BuildingProductionQueueResult TryEnqueue(
        BuildingProductionEntry entry,
        EconomyLedger stockpile,
        PopulationLedger population)
    {
        if (entry == null) return BuildingProductionQueueResult.MissingEntry;
        if (string.IsNullOrWhiteSpace(entry.productCode)) return BuildingProductionQueueResult.MissingProductCode;
        if (float.IsNaN(entry.productionTime) || float.IsInfinity(entry.productionTime)
            || entry.productionTime <= 0f)
            return BuildingProductionQueueResult.InvalidDuration;
        if (stockpile == null) return BuildingProductionQueueResult.MissingStockpile;
        if (population == null) return BuildingProductionQueueResult.MissingPopulationLedger;
        if (orders == null) orders = new List<BuildingProductionOrder>();

        int entryLimit = entry.queueLimit <= 0 ? _maximumQueueSize : entry.queueLimit;
        int limit = Math.Min(_maximumQueueSize, entryLimit);
        if (orders.Count >= limit) return BuildingProductionQueueResult.QueueFull;

        List<ResourceValue> normalizedCost;
        if (!TryNormalizeCost(entry.cost, out normalizedCost))
            return BuildingProductionQueueResult.InvalidCost;
        if (!CanAfford(normalizedCost, stockpile))
            return BuildingProductionQueueResult.InsufficientResources;
        if (!population.CanAdd(entry.populationCost))
            return BuildingProductionQueueResult.PopulationCap;

        // All preconditions have passed. Commit the reservation atomically
        // from the caller's point of view: no mutation occurs before this line.
        Spend(normalizedCost, stockpile);
        population.TryAdd(entry.populationCost);
        BuildingProductionOrder order = new BuildingProductionOrder(entry);
        order.cost = normalizedCost;
        orders.Add(order);
        return BuildingProductionQueueResult.Accepted;
    }

    /// <summary>Advances FIFO work and returns every product completed this tick.</summary>
    public List<string> Advance(float deltaTime)
    {
        List<string> completed = new List<string>();
        if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)
            || orders == null)
            return completed;

        float remainingDelta = deltaTime;
        while (remainingDelta > 0f && orders.Count > 0)
        {
            BuildingProductionOrder order = orders[0];
            if (order == null || string.IsNullOrWhiteSpace(order.productCode)
                || order.duration <= 0f || float.IsNaN(order.duration)
                || float.IsInfinity(order.duration))
            {
                // Invalid restored records cannot block every later item.
                orders.RemoveAt(0);
                continue;
            }

            float step = Math.Min(remainingDelta, order.Remaining);
            order.elapsed += step;
            remainingDelta -= step;
            if (order.Remaining > 0.000001f) break;

            completed.Add(order.productCode);
            orders.RemoveAt(0);
        }
        return completed;
    }

    /// <summary>
    /// Cancels one queued item, refunds its full reservation and releases its
    /// population reservation. Completed items are already removed by Advance.
    /// </summary>
    public bool TryCancel(int index, EconomyLedger stockpile, PopulationLedger population,
        out BuildingProductionOrder cancelled)
    {
        cancelled = null;
        if (orders == null || index < 0 || index >= orders.Count
            || stockpile == null || population == null)
            return false;

        BuildingProductionOrder order = orders[index];
        if (order == null) return false;
        AddCost(order.cost, stockpile);
        if (!population.TryRemove(order.populationCost))
        {
            // Restore the stockpile if a malformed restored queue has no
            // matching population reservation.
            Spend(order.cost, stockpile);
            return false;
        }
        orders.RemoveAt(index);
        cancelled = order;
        return true;
    }

    private static bool TryNormalizeCost(List<ResourceValue> values, out List<ResourceValue> result)
    {
        result = new List<ResourceValue>();
        if (values == null) return true;
        for (int i = 0; i < values.Count; i++)
        {
            ResourceValue value = values[i];
            if (value == null) return false;
            if (value.amount < 0) return false;
            if (value.amount == 0) continue;

            ResourceValue existing = null;
            for (int j = 0; j < result.Count; j++)
                if (result[j].code == value.code) { existing = result[j]; break; }
            if (existing == null) result.Add(new ResourceValue(value.code, value.amount));
            else
            {
                long total = (long)existing.amount + value.amount;
                if (total > int.MaxValue) return false;
                existing.amount = (int)total;
            }
        }
        return true;
    }

    private static bool CanAfford(List<ResourceValue> cost, EconomyLedger stockpile)
    {
        for (int i = 0; i < cost.Count; i++)
        {
            EconomyResourceType resource;
            if (!TryMap(cost[i].code, out resource) || !stockpile.CanSpend(resource, cost[i].amount))
                return false;
        }
        return true;
    }

    private static void Spend(List<ResourceValue> cost, EconomyLedger stockpile)
    {
        for (int i = 0; i < cost.Count; i++)
        {
            EconomyResourceType resource;
            if (TryMap(cost[i].code, out resource)) stockpile.TrySpend(resource, cost[i].amount);
        }
    }

    private static void AddCost(List<ResourceValue> cost, EconomyLedger stockpile)
    {
        if (cost == null) return;
        for (int i = 0; i < cost.Count; i++)
        {
            EconomyResourceType resource;
            if (cost[i] != null && TryMap(cost[i].code, out resource))
                stockpile.Add(resource, cost[i].amount);
        }
    }

    private static bool TryMap(InGameResource resource, out EconomyResourceType mapped)
    {
        switch (resource)
        {
            case InGameResource.Gold: mapped = EconomyResourceType.Gold; return true;
            case InGameResource.Wood: mapped = EconomyResourceType.Wood; return true;
            case InGameResource.Stone: mapped = EconomyResourceType.Stone; return true;
            default: mapped = default(EconomyResourceType); return false;
        }
    }
}
