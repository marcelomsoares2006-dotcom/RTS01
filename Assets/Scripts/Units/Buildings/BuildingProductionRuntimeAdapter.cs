using System;
using System.Collections.Generic;

/// <summary>Reasons a building production runtime operation can be rejected.</summary>
public enum BuildingProductionRuntimeResult
{
    Accepted,
    InvalidOwner,
    InvalidBuilding,
    BuildingUnavailable,
    ProductUnavailable,
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

/// <summary>
/// Data-driven bridge used by a live building runtime. It resolves products
/// from the building catalog and exposes the existing runtime and UI adapters
/// as one binding, while leaving spawning to the caller through
/// <see cref="ProductCompleted"/>. No prefab or scene asset is created here.
/// </summary>
public sealed class BuildingProductionRuntimeBinding
{
    private readonly BuildingProductionRuntimeAdapter _runtime;
    private readonly BuildingProductionUiAdapter _ui;

    private BuildingProductionRuntimeBinding(
        Building building,
        BuildingCatalogData catalog,
        EconomyPlayerState economy)
    {
        Building = building;
        Catalog = catalog;
        _runtime = BuildingProductionRuntimeAdapter.ForBuilding(
            building,
            economy.stockpile,
            economy.population,
            5,
            OnProductCompleted);
        _ui = new BuildingProductionUiAdapter(_runtime);
    }

    public Building Building { get; private set; }
    public BuildingCatalogData Catalog { get; private set; }
    public BuildingProductionRuntimeAdapter Runtime { get { return _runtime; } }
    public BuildingProductionUiAdapter Ui { get { return _ui; } }

    /// <summary>Raised after the deterministic queue completes a catalog product.</summary>
    public event Action<string> ProductCompleted;

    private void OnProductCompleted(string productCode)
    {
        Action<string> completed = ProductCompleted;
        if (completed != null) completed(productCode);
    }

    /// <summary>
    /// Creates a binding only when the live building, its catalog record and
    /// the owner's economy state agree on a stable building code.
    /// </summary>
    public static bool TryCreate(
        Building building,
        BuildingCatalogData catalog,
        EconomyPlayerState economy,
        out BuildingProductionRuntimeBinding binding,
        out string reason)
    {
        binding = null;
        reason = string.Empty;
        if (building == null) { reason = "Missing building."; return false; }
        if (catalog == null) { reason = "Missing building catalog entry."; return false; }
        if (economy == null || economy.stockpile == null || economy.population == null)
        {
            reason = "Missing owner economy state.";
            return false;
        }

        BuildingData data = building.Data as BuildingData;
        string buildingCode = data == null ? string.Empty : data.code;
        if (string.IsNullOrWhiteSpace(buildingCode)
            || !string.Equals(buildingCode.Trim(), catalog.Code, StringComparison.OrdinalIgnoreCase))
        {
            reason = "Building and catalog codes do not match.";
            return false;
        }

        binding = new BuildingProductionRuntimeBinding(
            building,
            catalog,
            economy);
        return true;
    }

    /// <summary>Finds a catalog product with a case-insensitive stable code.</summary>
    public static BuildingProductionEntry FindProduct(
        BuildingCatalogData catalog,
        string productCode)
    {
        if (catalog == null || catalog.production == null || string.IsNullOrWhiteSpace(productCode))
            return null;
        for (int i = 0; i < catalog.production.Count; i++)
        {
            BuildingProductionEntry entry = catalog.production[i];
            if (entry != null && string.Equals(
                entry.productCode == null ? string.Empty : entry.productCode.Trim(),
                productCode.Trim(),
                StringComparison.OrdinalIgnoreCase))
                return entry;
        }
        return null;
    }

    public BuildingProductionRuntimeResult TryEnqueue(
        int requesterOwnerId,
        string productCode,
        out BuildingProductionQueueResult queueResult)
    {
        BuildingProductionEntry entry = FindProduct(Catalog, productCode);
        if (entry == null)
        {
            queueResult = BuildingProductionQueueResult.MissingEntry;
            return BuildingProductionRuntimeResult.ProductUnavailable;
        }
        return _ui.TryEnqueue(requesterOwnerId, entry, out queueResult);
    }

    public BuildingProductionRuntimeResult Advance(
        int requesterOwnerId,
        float deltaTime,
        out List<string> completedProducts)
    {
        return _ui.Advance(requesterOwnerId, deltaTime, out completedProducts);
    }

    public BuildingProductionRuntimeResult TryCancel(
        int requesterOwnerId,
        int index,
        out BuildingProductionOrder cancelled)
    {
        return _ui.TryCancel(requesterOwnerId, index, out cancelled);
    }
}
