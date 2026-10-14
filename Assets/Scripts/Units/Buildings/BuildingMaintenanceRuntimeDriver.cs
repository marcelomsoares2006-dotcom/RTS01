using UnityEngine;

/// <summary>
/// Scene lifecycle bridge. A bound maintenance transaction advances from the
/// normal Unity Update loop and remains available to save/load adapters.
/// </summary>
public sealed class BuildingMaintenanceRuntimeDriver : MonoBehaviour
{
    private BuildingMaintenanceRuntimeBinding _binding;
    public BuildingMaintenanceRuntimeBinding Binding { get { return _binding; } }

    public bool Bind(Building building, EconomyLedger economy, BuildingMaintenanceSpec spec)
    {
        string reason;
        if (!BuildingMaintenanceRuntimeBinding.TryCreate(building, economy, spec, out _binding, out reason))
            return false;
        return true;
    }

    public BuildingMaintenanceResult Restore(BuildingMaintenanceOperationState state)
    {
        return _binding == null ? BuildingMaintenanceResult.InvalidState : _binding.Restore(state);
    }

    public BuildingMaintenanceRuntimeSnapshot Capture()
    {
        return _binding == null ? null : _binding.State;
    }

    private void Update()
    {
        if (_binding != null) _binding.Tick(Time.deltaTime);
    }
}
