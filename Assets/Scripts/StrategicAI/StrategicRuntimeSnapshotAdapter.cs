using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Read-only boundary between the live match and strategic AI code.</summary>
public interface IStrategicRuntimeSnapshotSource
{
    StrategicPlanningState Capture(int ownerId);
}

/// <summary>
/// Builds a detached strategic snapshot from the current runtime registries.
/// The adapter never writes to a Unit, Building, resource dictionary, or scene.
/// </summary>
public sealed class StrategicRuntimeSnapshotAdapter : IStrategicRuntimeSnapshotSource
{
    public StrategicPlanningState Capture(int ownerId)
    {
        StrategicPlanningState state = new StrategicPlanningState { ownerId = ownerId };

        if (Globals.GAME_RESOURCES != null && ownerId >= 0 && ownerId < Globals.GAME_RESOURCES.Length)
        {
            Dictionary<InGameResource, GameResource> resources = Globals.GAME_RESOURCES[ownerId];
            if (resources != null)
            {
                foreach (KeyValuePair<InGameResource, GameResource> pair in resources)
                    if (pair.Value != null)
                        state.SetAmount(pair.Key, Mathf.Max(0, pair.Value.Amount));
            }
        }

        if (Unit.UNITS_BY_OWNER == null)
            return state;

        // Include the world observations so the intent gateway can reject commands
        // aimed at another player's entity. Resource values remain owner-scoped.
        foreach (KeyValuePair<int, List<Unit>> ownerUnits in Unit.UNITS_BY_OWNER)
        {
            if (ownerUnits.Value == null)
                continue;
            foreach (Unit unit in ownerUnits.Value)
            {
                if (unit == null || unit.Data == null || unit.Transform == null)
                    continue;

                Vector3 position = unit.Transform.position;
                Building building = unit as Building;
                if (building != null)
                {
                    state.buildings.Add(new StrategicBuildingObservation
                    {
                        entityId = unit.Uid,
                        code = unit.Code ?? string.Empty,
                        ownerId = unit.Owner,
                        position = position,
                        lifecycle = ResolveLifecycle(building)
                    });
                }
                else
                {
                    state.units.Add(new StrategicUnitObservation
                    {
                        entityId = unit.Uid,
                        code = unit.Code ?? string.Empty,
                        ownerId = unit.Owner,
                        position = position,
                        alive = unit.IsAlive,
                        idle = false
                    });
                }
            }
        }

        return state;
    }

    private static BuildingLifecycleState ResolveLifecycle(Building building)
    {
        if (!building.IsFixed)
            return BuildingLifecycleState.Blueprint;
        if (!building.IsAlive)
            return BuildingLifecycleState.UnderConstruction;
        if (building.HP <= 0)
            return BuildingLifecycleState.Destroyed;
        if (building.HP < building.MaxHP)
            return BuildingLifecycleState.Damaged;
        return BuildingLifecycleState.Operational;
    }
}
