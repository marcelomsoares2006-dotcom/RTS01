using System;

[Serializable]
public sealed class BuildingMaintenanceRuntimeSnapshot
{
    public string buildingId;
    public int ownerId;
    public int currentHealth;
    public int maxHealth;
    public BuildingLifecycleState lifecycle;
    public BuildingMaintenanceOperation operation;
    public float progress;
    public bool busy;
    public BuildingMaintenanceOperationState operationState;

    public BuildingMaintenanceRuntimeSnapshot(Building building, BuildingRepairDemolitionService service)
    {
        buildingId = building == null ? string.Empty : building.Uid;
        ownerId = building == null ? -1 : building.Owner;
        currentHealth = service == null ? 0 : service.CurrentHealth;
        maxHealth = building == null ? 0 : building.MaxHP;
        lifecycle = service == null ? BuildingLifecycleState.Destroyed : service.Lifecycle;
        operation = service == null || service.Operation == null
            ? BuildingMaintenanceOperation.None : service.Operation.operation;
        busy = service != null && service.IsBusy;
        float duration = service == null || service.Operation == null ? 0f : service.Operation.duration;
        float elapsed = service == null || service.Operation == null ? 0f : service.Operation.elapsed;
        progress = duration <= 0f ? 0f : Clamp01(elapsed / duration);
        if (service != null && service.Operation != null)
        {
            operationState = new BuildingMaintenanceOperationState {
                operation = service.Operation.operation,
                elapsed = service.Operation.elapsed,
                duration = service.Operation.duration,
                initialHealth = service.Operation.initialHealth,
                initialLifecycle = service.Operation.initialLifecycle,
                reservedCost = CopyCosts(service.Operation.reservedCost)
            };
        }
    }

    // Constructor used by the versioned save migration boundary. It keeps
    // deserialization independent from live scene objects and services.
    public BuildingMaintenanceRuntimeSnapshot(
        string savedBuildingId, int savedOwnerId, int savedCurrentHealth, int savedMaxHealth,
        BuildingLifecycleState savedLifecycle, BuildingMaintenanceOperation savedOperation,
        float savedProgress, bool savedBusy, BuildingMaintenanceOperationState savedOperationState)
    {
        buildingId = savedBuildingId;
        ownerId = savedOwnerId;
        currentHealth = savedCurrentHealth;
        maxHealth = savedMaxHealth;
        lifecycle = savedLifecycle;
        operation = savedOperation;
        progress = savedProgress;
        busy = savedBusy;
        operationState = savedOperationState;
    }

    private static float Clamp01(float value)
    {
        if (value <= 0f) return 0f;
        if (value >= 1f) return 1f;
        return value;
    }

    private static System.Collections.Generic.List<BuildingMaintenanceCost> CopyCosts(
        System.Collections.Generic.List<BuildingMaintenanceCost> source)
    {
        var result = new System.Collections.Generic.List<BuildingMaintenanceCost>();
        if (source == null) return result;
        for (int i = 0; i < source.Count; i++)
            if (source[i] != null)
                result.Add(new BuildingMaintenanceCost(source[i].resource, source[i].amount));
        return result;
    }
}

/// <summary>Runtime bridge from deterministic maintenance transactions to a live Building.</summary>
public sealed class BuildingMaintenanceRuntimeBinding
{
    private readonly Building _building;
    private readonly BuildingRepairDemolitionService _service;

    private BuildingMaintenanceRuntimeBinding(Building building, EconomyLedger economy, BuildingMaintenanceSpec spec)
    {
        _building = building;
        _service = new BuildingRepairDemolitionService(
            building.Uid, building.Owner, building.HP, building.MaxHP,
            building.LifecycleState, economy, spec);
    }

    public Building Building { get { return _building; } }
    public BuildingRepairDemolitionService Service { get { return _service; } }
    public BuildingMaintenanceRuntimeSnapshot State
    {
        get { return new BuildingMaintenanceRuntimeSnapshot(_building, _service); }
    }

    public event Action<BuildingMaintenanceOperation> OperationCompleted;
    public event Action<BuildingMaintenanceRuntimeSnapshot> StateChanged;

    public BuildingMaintenanceResult Restore(BuildingMaintenanceOperationState saved)
    {
        if (!_service.RestoreOperation(saved)) return BuildingMaintenanceResult.InvalidState;
        NotifyChanged();
        return BuildingMaintenanceResult.Accepted;
    }

    public BuildingMaintenanceResult RestoreSnapshot(
        int currentHealth, BuildingLifecycleState lifecycle,
        BuildingMaintenanceOperationState saved)
    {
        if (!_service.RestoreSnapshot(currentHealth, lifecycle, saved))
            return BuildingMaintenanceResult.InvalidState;
        SyncBuildingHealth();
        NotifyChanged();
        return BuildingMaintenanceResult.Accepted;
    }

    public void Tick(float deltaTime)
    {
        if (!_service.IsBusy) return;
        bool completed;
        Advance(_building.Owner, deltaTime, out completed);
    }

    public static bool TryCreate(Building building, EconomyLedger economy, BuildingMaintenanceSpec spec,
        out BuildingMaintenanceRuntimeBinding binding, out string reason)
    {
        binding = null;
        reason = string.Empty;
        if (building == null) { reason = "Missing building."; return false; }
        if (economy == null) { reason = "Missing owner economy state."; return false; }
        if (spec == null) { reason = "Missing maintenance specification."; return false; }
        if (string.IsNullOrWhiteSpace(building.Uid))
        {
            reason = "Building has no stable identity.";
            return false;
        }
        binding = new BuildingMaintenanceRuntimeBinding(building, economy, spec);
        return true;
    }

    public BuildingMaintenanceResult TryBeginRepair(int requesterOwnerId)
    {
        BuildingMaintenanceResult result = _service.TryBeginRepair(requesterOwnerId);
        NotifyChanged();
        return result;
    }

    public BuildingMaintenanceResult TryBeginDemolition(int requesterOwnerId)
    {
        BuildingMaintenanceResult result = _service.TryBeginDemolition(requesterOwnerId);
        NotifyChanged();
        return result;
    }

    public BuildingMaintenanceResult Advance(int requesterOwnerId, float deltaTime, out bool completed)
    {
        BuildingMaintenanceOperation operation = _service.Operation == null
            ? BuildingMaintenanceOperation.None : _service.Operation.operation;
        BuildingMaintenanceResult result = _service.Advance(requesterOwnerId, deltaTime, out completed);
        SyncBuildingHealth();
        NotifyChanged();
        if (completed && result == BuildingMaintenanceResult.Accepted)
        {
            Action<BuildingMaintenanceOperation> callback = OperationCompleted;
            if (callback != null) callback(operation);
        }
        return result;
    }

    public BuildingMaintenanceResult TryCancel(int requesterOwnerId)
    {
        BuildingMaintenanceResult result = _service.TryCancel(requesterOwnerId);
        SyncBuildingHealth();
        NotifyChanged();
        return result;
    }

    private void SyncBuildingHealth()
    {
        if (_building == null) return;
        _building.HP = _service.CurrentHealth;
        _building.NotifyHealthChanged();
    }

    private void NotifyChanged()
    {
        Action<BuildingMaintenanceRuntimeSnapshot> callback = StateChanged;
        if (callback != null) callback(State);
    }
}
