using System;
using System.Collections.Generic;

/// <summary>Reasons a building production runtime operation can be rejected.</summary>
public enum BuildingProductionRuntimeResult
{
    Accepted,
    InvalidOwner,
    InvalidBuilding,
    BuildingUnavailable,
    QueueRejected
}

/// <summary>
/// Runtime boundary for one building's production queue.
///
/// The adapter deliberately has no scene, UI, or unit spawning dependency. A
/// gameplay owner supplies the product callback after completion and decides
/// how the product code is materialized. This keeps simulation/replay behavior
/// deterministic while making ownership checks unavoidable at the boundary.
/// </summary>
public sealed class BuildingProductionRuntimeAdapter
{
    private readonly string _buildingId;
    private readonly int _ownerId;
    private readonly BuildingProductionQueue _queue;
    private readonly EconomyLedger _stockpile;
    private readonly PopulationLedger _population;
    private readonly Func<bool> _isAvailable;
    private readonly Action<string> _productCompleted;

    public BuildingProductionRuntimeAdapter(
        string buildingId,
        int ownerId,
        EconomyLedger stockpile,
        PopulationLedger population,
        int maximumQueueSize = 5,
        Func<bool> isAvailable = null,
        Action<string> productCompleted = null)
    {
        _buildingId = buildingId == null ? string.Empty : buildingId.Trim();
        if (ownerId < 0) throw new ArgumentOutOfRangeException(nameof(ownerId));
        _ownerId = ownerId;
        _stockpile = stockpile ?? throw new ArgumentNullException(nameof(stockpile));
        _population = population ?? throw new ArgumentNullException(nameof(population));
        _queue = new BuildingProductionQueue(maximumQueueSize);
        _isAvailable = isAvailable;
        _productCompleted = productCompleted;
    }

    /// <summary>Stable identity of the building represented by this adapter.</summary>
    public string BuildingId { get { return _buildingId; } }
    public int OwnerId { get { return _ownerId; } }
    public BuildingProductionQueue Queue { get { return _queue; } }
    public bool IsAvailable { get { return _isAvailable == null || _isAvailable(); } }

    /// <summary>
    /// Creates an adapter bound to the live building identity and ownership.
    /// Production remains unavailable until construction has completed.
    /// </summary>
    public static BuildingProductionRuntimeAdapter ForBuilding(
        Building building,
        EconomyLedger stockpile,
        PopulationLedger population,
        int maximumQueueSize = 5,
        Action<string> productCompleted = null)
    {
        if (building == null) throw new ArgumentNullException(nameof(building));
        return new BuildingProductionRuntimeAdapter(
            building.Uid,
            building.Owner,
            stockpile,
            population,
            maximumQueueSize,
            () => building.IsAlive,
            productCompleted);
    }

    public BuildingProductionRuntimeResult TryEnqueue(
        int requesterOwnerId,
        BuildingProductionEntry entry,
        out BuildingProductionQueueResult queueResult)
    {
        queueResult = BuildingProductionQueueResult.MissingEntry;
        BuildingProductionRuntimeResult access = ValidateAccess(requesterOwnerId);
        if (access != BuildingProductionRuntimeResult.Accepted) return access;

        queueResult = _queue.TryEnqueue(entry, _stockpile, _population);
        return queueResult == BuildingProductionQueueResult.Accepted
            ? BuildingProductionRuntimeResult.Accepted
            : BuildingProductionRuntimeResult.QueueRejected;
    }

    /// <summary>
    /// Advances this building only when requested by its owner. Products are
    /// delivered in the exact order returned by the deterministic queue.
    /// </summary>
    public BuildingProductionRuntimeResult Advance(
        int requesterOwnerId,
        float deltaTime,
        out List<string> completedProducts)
    {
        completedProducts = new List<string>();
        BuildingProductionRuntimeResult access = ValidateAccess(requesterOwnerId);
        if (access != BuildingProductionRuntimeResult.Accepted) return access;

        completedProducts = _queue.Advance(deltaTime);
        if (_productCompleted != null)
            for (int i = 0; i < completedProducts.Count; i++)
                _productCompleted(completedProducts[i]);
        return BuildingProductionRuntimeResult.Accepted;
    }

    public BuildingProductionRuntimeResult TryCancel(
        int requesterOwnerId,
        int index,
        out BuildingProductionOrder cancelled)
    {
        cancelled = null;
        BuildingProductionRuntimeResult access = ValidateAccess(requesterOwnerId);
        if (access != BuildingProductionRuntimeResult.Accepted) return access;
        return _queue.TryCancel(index, _stockpile, _population, out cancelled)
            ? BuildingProductionRuntimeResult.Accepted
            : BuildingProductionRuntimeResult.QueueRejected;
    }

    private BuildingProductionRuntimeResult ValidateAccess(int requesterOwnerId)
    {
        if (requesterOwnerId != _ownerId) return BuildingProductionRuntimeResult.InvalidOwner;
        if (string.IsNullOrEmpty(_buildingId)) return BuildingProductionRuntimeResult.InvalidBuilding;
        if (!IsAvailable) return BuildingProductionRuntimeResult.BuildingUnavailable;
        return BuildingProductionRuntimeResult.Accepted;
    }
}
