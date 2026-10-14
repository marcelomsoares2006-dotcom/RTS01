using System;
using System.Collections.Generic;

/// <summary>Operation currently reserving a building for repair or demolition.</summary>
public enum BuildingMaintenanceOperation
{
    None,
    Repair,
    Demolition
}

/// <summary>Stable result codes for ownership and transaction failures.</summary>
public enum BuildingMaintenanceResult
{
    Accepted,
    InvalidOwner,
    InvalidBuilding,
    InvalidState,
    AlreadyInProgress,
    MissingStockpile,
    InvalidCost,
    InsufficientResources,
    InvalidDuration,
    InvalidDeltaTime,
    NothingToCancel
}

[Serializable]
public sealed class BuildingMaintenanceCost
{
    public EconomyResourceType resource;
    public int amount;

    public BuildingMaintenanceCost() { }

    public BuildingMaintenanceCost(EconomyResourceType resource, int amount)
    {
        this.resource = resource;
        this.amount = amount;
    }
}

[Serializable]
public sealed class BuildingMaintenanceSpec
{
    public List<BuildingMaintenanceCost> repairCost = new List<BuildingMaintenanceCost>();
    public float repairDuration = 1f;
    public bool allowDestroyedRepair;

    public List<BuildingMaintenanceCost> demolitionCost = new List<BuildingMaintenanceCost>();
    public float demolitionDuration = 1f;
    /// <summary>Clamped to [0,1] when a demolition completes.</summary>
    public float demolitionRefundRatio = 1f;
    public List<BuildingMaintenanceCost> demolitionRefund = new List<BuildingMaintenanceCost>();
}

/// <summary>
/// Serializable operation state. The original state and reservation are kept
/// so cancellation can restore the exact pre-operation snapshot.
/// </summary>
[Serializable]
public sealed class BuildingMaintenanceOperationState
{
    public BuildingMaintenanceOperation operation;
    public float elapsed;
    public float duration;
    public int initialHealth;
    public BuildingLifecycleState initialLifecycle;
    public List<BuildingMaintenanceCost> reservedCost = new List<BuildingMaintenanceCost>();
}

/// <summary>
/// Engine-independent repair/demolition transaction boundary. It owns
/// reservations, validates ownership, and never mutates a stockpile until all
/// cost entries have been normalized and affordability has been established.
/// </summary>
public sealed class BuildingRepairDemolitionService
{
    private readonly string _buildingId;
    private readonly int _ownerId;
    private readonly int _maxHealth;
    private readonly EconomyLedger _stockpile;
    private readonly BuildingMaintenanceSpec _spec;
    private BuildingMaintenanceOperationState _operation;

    public int CurrentHealth { get; private set; }
    public BuildingLifecycleState Lifecycle { get; private set; }
    public string BuildingId { get { return _buildingId; } }
    public int OwnerId { get { return _ownerId; } }
    public BuildingMaintenanceOperationState Operation { get { return _operation; } }
    public bool IsBusy { get { return _operation != null; } }

    /// <summary>Restores a previously reserved operation without spending twice.</summary>
    public bool RestoreOperation(BuildingMaintenanceOperationState saved)
    {
        if (saved == null || saved.operation == BuildingMaintenanceOperation.None)
        {
            _operation = null;
            return true;
        }
        if (_operation != null || saved.duration <= 0f || saved.elapsed < 0f
            || saved.elapsed > saved.duration || saved.initialHealth < 0
            || saved.initialHealth > _maxHealth
            || (saved.operation != BuildingMaintenanceOperation.Repair
                && saved.operation != BuildingMaintenanceOperation.Demolition))
            return false;
        List<BuildingMaintenanceCost> normalized = NormalizeCost(saved.reservedCost);
        if (normalized == null) return false;
        _operation = new BuildingMaintenanceOperationState {
            operation = saved.operation, elapsed = saved.elapsed,
            duration = saved.duration, initialHealth = saved.initialHealth,
            initialLifecycle = saved.initialLifecycle, reservedCost = normalized
        };
        return true;
    }

    /// <summary>Restores the live state first, then reattaches its reservation.</summary>
    public bool RestoreSnapshot(int currentHealth, BuildingLifecycleState lifecycle,
        BuildingMaintenanceOperationState saved)
    {
        if (currentHealth < 0 || currentHealth > _maxHealth) return false;
        CurrentHealth = currentHealth;
        Lifecycle = lifecycle;
        return RestoreOperation(saved);
    }

    public BuildingRepairDemolitionService(
        string buildingId,
        int ownerId,
        int currentHealth,
        int maxHealth,
        BuildingLifecycleState lifecycle,
        EconomyLedger stockpile,
        BuildingMaintenanceSpec spec)
    {
        _buildingId = buildingId == null ? string.Empty : buildingId.Trim();
        if (ownerId < 0) throw new ArgumentOutOfRangeException(nameof(ownerId));
        if (maxHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maxHealth));
        _ownerId = ownerId;
        _maxHealth = maxHealth;
        CurrentHealth = ClampHealth(currentHealth);
        Lifecycle = lifecycle;
        _stockpile = stockpile ?? throw new ArgumentNullException(nameof(stockpile));
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public BuildingMaintenanceResult TryBeginRepair(int requesterOwnerId)
    {
        BuildingMaintenanceResult access = ValidateAccess(requesterOwnerId);
        if (access != BuildingMaintenanceResult.Accepted) return access;
        if (_operation != null) return BuildingMaintenanceResult.AlreadyInProgress;
        if (Lifecycle != BuildingLifecycleState.Damaged
            && !(Lifecycle == BuildingLifecycleState.Destroyed && _spec.allowDestroyedRepair))
            return BuildingMaintenanceResult.InvalidState;
        if (CurrentHealth >= _maxHealth) return BuildingMaintenanceResult.InvalidState;

        List<BuildingMaintenanceCost> cost;
        BuildingMaintenanceResult prepared = PrepareCost(_spec.repairCost, _spec.repairDuration, out cost);
        if (prepared != BuildingMaintenanceResult.Accepted) return prepared;
        Spend(cost);
        _operation = new BuildingMaintenanceOperationState {
            operation = BuildingMaintenanceOperation.Repair,
            elapsed = 0f,
            duration = _spec.repairDuration,
            initialHealth = CurrentHealth,
            initialLifecycle = Lifecycle,
            reservedCost = cost
        };
        return BuildingMaintenanceResult.Accepted;
    }

    public BuildingMaintenanceResult TryBeginDemolition(int requesterOwnerId)
    {
        BuildingMaintenanceResult access = ValidateAccess(requesterOwnerId);
        if (access != BuildingMaintenanceResult.Accepted) return access;
        if (_operation != null) return BuildingMaintenanceResult.AlreadyInProgress;
        if (Lifecycle == BuildingLifecycleState.Ruined) return BuildingMaintenanceResult.InvalidState;

        if (float.IsNaN(_spec.demolitionRefundRatio)
            || float.IsInfinity(_spec.demolitionRefundRatio)
            || _spec.demolitionRefundRatio < 0f
            || _spec.demolitionRefundRatio > 1f
            || NormalizeCost(_spec.demolitionRefund) == null)
            return BuildingMaintenanceResult.InvalidCost;

        List<BuildingMaintenanceCost> cost;
        BuildingMaintenanceResult prepared = PrepareCost(_spec.demolitionCost, _spec.demolitionDuration, out cost);
        if (prepared != BuildingMaintenanceResult.Accepted) return prepared;
        Spend(cost);
        _operation = new BuildingMaintenanceOperationState {
            operation = BuildingMaintenanceOperation.Demolition,
            elapsed = 0f,
            duration = _spec.demolitionDuration,
            initialHealth = CurrentHealth,
            initialLifecycle = Lifecycle,
            reservedCost = cost
        };
        return BuildingMaintenanceResult.Accepted;
    }

    /// <summary>Advances one operation and returns true only when it completes.</summary>
    public BuildingMaintenanceResult Advance(int requesterOwnerId, float deltaTime, out bool completed)
    {
        completed = false;
        BuildingMaintenanceResult access = ValidateAccess(requesterOwnerId);
        if (access != BuildingMaintenanceResult.Accepted) return access;
        if (_operation == null) return BuildingMaintenanceResult.NothingToCancel;
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            return BuildingMaintenanceResult.InvalidDeltaTime;
        if (deltaTime == 0f) return BuildingMaintenanceResult.Accepted;

        _operation.elapsed = Math.Min(_operation.duration, _operation.elapsed + deltaTime);
        if (_operation.operation == BuildingMaintenanceOperation.Repair)
        {
            float ratio = _operation.duration <= 0f ? 1f : _operation.elapsed / _operation.duration;
            CurrentHealth = ClampHealth(_operation.initialHealth
                + (int)Math.Floor((_maxHealth - _operation.initialHealth) * ratio));
        }
        if (_operation.elapsed + 0.000001f < _operation.duration) return BuildingMaintenanceResult.Accepted;

        if (_operation.operation == BuildingMaintenanceOperation.Repair)
        {
            CurrentHealth = _maxHealth;
            Lifecycle = BuildingLifecycleState.Operational;
        }
        else
        {
            AddCost(ApplyRefundRatio(_spec.demolitionRefund, _spec.demolitionRefundRatio));
            CurrentHealth = 0;
            Lifecycle = BuildingLifecycleState.Ruined;
        }
        _operation = null;
        completed = true;
        return BuildingMaintenanceResult.Accepted;
    }

    /// <summary>Refunds the reservation and restores the exact initial snapshot.</summary>
    public BuildingMaintenanceResult TryCancel(int requesterOwnerId)
    {
        BuildingMaintenanceResult access = ValidateAccess(requesterOwnerId);
        if (access != BuildingMaintenanceResult.Accepted) return access;
        if (_operation == null) return BuildingMaintenanceResult.NothingToCancel;
        AddCost(_operation.reservedCost);
        CurrentHealth = _operation.initialHealth;
        Lifecycle = _operation.initialLifecycle;
        _operation = null;
        return BuildingMaintenanceResult.Accepted;
    }

    private BuildingMaintenanceResult ValidateAccess(int requesterOwnerId)
    {
        if (requesterOwnerId != _ownerId) return BuildingMaintenanceResult.InvalidOwner;
        if (string.IsNullOrEmpty(_buildingId)) return BuildingMaintenanceResult.InvalidBuilding;
        return BuildingMaintenanceResult.Accepted;
    }

    private BuildingMaintenanceResult PrepareCost(
        List<BuildingMaintenanceCost> requested,
        float duration,
        out List<BuildingMaintenanceCost> normalized)
    {
        normalized = NormalizeCost(requested);
        if (normalized == null) return BuildingMaintenanceResult.InvalidCost;
        if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f)
            return BuildingMaintenanceResult.InvalidDuration;
        for (int i = 0; i < normalized.Count; i++)
            if (!_stockpile.CanSpend(normalized[i].resource, normalized[i].amount))
                return BuildingMaintenanceResult.InsufficientResources;
        return BuildingMaintenanceResult.Accepted;
    }

    private void Spend(List<BuildingMaintenanceCost> cost)
    {
        for (int i = 0; i < cost.Count; i++) _stockpile.TrySpend(cost[i].resource, cost[i].amount);
    }

    private void AddCost(List<BuildingMaintenanceCost> cost)
    {
        if (cost == null) return;
        for (int i = 0; i < cost.Count; i++) _stockpile.Add(cost[i].resource, cost[i].amount);
    }

    private static List<BuildingMaintenanceCost> NormalizeCost(List<BuildingMaintenanceCost> requested)
    {
        List<BuildingMaintenanceCost> result = new List<BuildingMaintenanceCost>();
        if (requested == null) return result;
        for (int i = 0; i < requested.Count; i++)
        {
            BuildingMaintenanceCost item = requested[i];
            if (item == null || item.amount < 0
                || !Enum.IsDefined(typeof(EconomyResourceType), item.resource)) return null;
            if (item.amount == 0) continue;
            BuildingMaintenanceCost existing = null;
            for (int j = 0; j < result.Count; j++)
                if (result[j].resource == item.resource) { existing = result[j]; break; }
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

    private static List<BuildingMaintenanceCost> ApplyRefundRatio(
        List<BuildingMaintenanceCost> requested, float ratio)
    {
        List<BuildingMaintenanceCost> normalized = NormalizeCost(requested);
        if (normalized == null) return new List<BuildingMaintenanceCost>();
        ratio = Math.Max(0f, Math.Min(1f, ratio));
        for (int i = 0; i < normalized.Count; i++)
            normalized[i].amount = (int)Math.Floor(normalized[i].amount * ratio);
        return normalized;
    }

    private int ClampHealth(int health) { return Math.Max(0, Math.Min(_maxHealth, health)); }
}
