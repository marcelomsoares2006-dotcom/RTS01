using System;
using System.Collections.Generic;

/// <summary>
/// Presentation-safe copy of one production order. It deliberately contains
/// no Unity objects, so UI code can render it without mutating simulation state.
/// </summary>
[Serializable]
public sealed class BuildingProductionUiOrder
{
    public int index;
    public string productCode;
    public float elapsed;
    public float duration;
    public float progress;
    public float remaining;
    public int populationCost;
    public List<ResourceValue> cost = new List<ResourceValue>();

    internal BuildingProductionUiOrder(int orderIndex, BuildingProductionOrder source)
    {
        index = orderIndex;
        productCode = source == null || source.productCode == null ? string.Empty : source.productCode;
        elapsed = source == null ? 0f : Math.Max(0f, source.elapsed);
        duration = source == null ? 0f : Math.Max(0f, source.duration);
        remaining = source == null ? 0f : source.Remaining;
        populationCost = source == null ? 0 : source.populationCost;
        progress = duration <= 0f ? 0f : Clamp01(elapsed / duration);
        if (source != null && source.cost != null)
            for (int i = 0; i < source.cost.Count; i++)
            {
                ResourceValue value = source.cost[i];
                if (value != null)
                    cost.Add(new ResourceValue(value.code, value.amount));
            }
    }

    private static float Clamp01(float value)
    {
        if (value <= 0f) return 0f;
        if (value >= 1f) return 1f;
        return value;
    }
}

/// <summary>Immutable-by-convention snapshot consumed by production UI.</summary>
[Serializable]
public sealed class BuildingProductionUiState
{
    public string buildingId;
    public int ownerId;
    public bool available;
    public int queueCount;
    public int queueLimit;
    public List<BuildingProductionUiOrder> orders = new List<BuildingProductionUiOrder>();

    internal BuildingProductionUiState(BuildingProductionRuntimeAdapter source)
    {
        buildingId = source == null ? string.Empty : source.BuildingId;
        ownerId = source == null ? -1 : source.OwnerId;
        available = source != null && source.IsAvailable;
        if (source == null || source.Queue == null) return;
        queueCount = source.Queue.Count;
        queueLimit = source.Queue.MaximumQueueSize;
        if (source.Queue.orders == null) return;
        for (int i = 0; i < source.Queue.orders.Count; i++)
        {
            BuildingProductionOrder order = source.Queue.orders[i];
            if (order != null)
                orders.Add(new BuildingProductionUiOrder(i, order));
        }
    }
}

/// <summary>
/// Adapter between the deterministic production runtime and the existing UI.
/// UI may subscribe to <see cref="StateChanged"/> and render orders, progress,
/// costs, and a cancel button without receiving mutable queue references.
/// </summary>
public sealed class BuildingProductionUiAdapter
{
    private readonly BuildingProductionRuntimeAdapter _runtime;

    public event Action<BuildingProductionUiState> StateChanged;

    public BuildingProductionUiAdapter(BuildingProductionRuntimeAdapter runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public BuildingProductionUiState State { get { return CreateState(); } }

    public BuildingProductionRuntimeResult TryEnqueue(
        int requesterOwnerId,
        BuildingProductionEntry entry,
        out BuildingProductionQueueResult queueResult)
    {
        BuildingProductionRuntimeResult result = _runtime.TryEnqueue(
            requesterOwnerId, entry, out queueResult);
        if (result == BuildingProductionRuntimeResult.Accepted)
            NotifyChanged();
        return result;
    }

    public BuildingProductionRuntimeResult Advance(
        int requesterOwnerId,
        float deltaTime,
        out List<string> completedProducts)
    {
        BuildingProductionRuntimeResult result = _runtime.Advance(
            requesterOwnerId, deltaTime, out completedProducts);
        if (result == BuildingProductionRuntimeResult.Accepted)
            NotifyChanged();
        return result;
    }

    public BuildingProductionRuntimeResult TryCancel(
        int requesterOwnerId,
        int index,
        out BuildingProductionOrder cancelled)
    {
        BuildingProductionRuntimeResult result = _runtime.TryCancel(
            requesterOwnerId, index, out cancelled);
        if (result == BuildingProductionRuntimeResult.Accepted)
            NotifyChanged();
        return result;
    }

    public BuildingProductionUiState CreateState()
    {
        return new BuildingProductionUiState(_runtime);
    }

    public void Refresh()
    {
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        Action<BuildingProductionUiState> changed = StateChanged;
        if (changed != null)
            changed(CreateState());
    }
}
