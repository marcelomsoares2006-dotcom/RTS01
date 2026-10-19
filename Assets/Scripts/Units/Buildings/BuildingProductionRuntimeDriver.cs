using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using EraImperial.Units.Advanced;

/// <summary>
/// Connects the deterministic production queue to one live operational building.
/// The queue and progression rules remain independent; this component is the
/// scene boundary that advances the queue and materializes completed characters.
/// </summary>
[RequireComponent(typeof(BuildingManager))]
public sealed class BuildingProductionRuntimeDriver : MonoBehaviour
{
    private Building _building;
    private BuildingContentCatalog _catalog;
    private BuildingProductionRuntimeAdapter _runtime;
    private EconomyPlayerState _economy;
    private readonly List<string> _completedProducts = new List<string>(1);

    public bool IsBound { get { return _runtime != null; } }
    public string LastFailure { get; private set; }
    public BuildingProductionRuntimeAdapter Runtime { get { return _runtime; } }

    /// <summary>Attaches and binds the driver exactly once to an operational building.</summary>
    public static BuildingProductionRuntimeDriver Attach(Building building)
    {
        if (building == null || building.Transform == null)
            return null;

        BuildingProductionRuntimeDriver driver =
            building.Transform.GetComponent<BuildingProductionRuntimeDriver>();
        if (driver == null)
            driver = building.Transform.gameObject.AddComponent<BuildingProductionRuntimeDriver>();
        driver.Bind(building);
        return driver;
    }

    public void Bind(Building building)
    {
        _building = building;
        _catalog = Resources.Load<BuildingContentCatalog>(
            "ScriptableObjects/Units/Building Content Catalog");
        LastFailure = string.Empty;
        _runtime = null;
        _economy = null;

        if (_building == null || !_building.IsAlive)
        {
            LastFailure = "Building is not operational.";
            return;
        }
        if (_catalog == null)
        {
            LastFailure = "Building content catalog was not found.";
            return;
        }
        if (DataHandler.EconomyRuntime == null)
        {
            LastFailure = "Economy runtime is not initialized.";
            return;
        }

        _economy = DataHandler.EconomyRuntime.GetOrCreatePlayer(_building.Owner);
        _runtime = BuildingProductionRuntimeAdapter.ForBuilding(
            _building,
            _economy.stockpile,
            _economy.population,
            5,
            OnProductCompleted);
    }

    /// <summary>Enqueues only products allowed by the live progression snapshot.</summary>
    public BuildingProductionCommandResult TryEnqueueProduct(
        string productCode,
        out BuildingProductionQueueResult queueResult)
    {
        queueResult = BuildingProductionQueueResult.MissingEntry;
        if (!IsBound || _building == null || !_building.IsAlive)
            return BuildingProductionCommandResult.QueueRejected;

        StrategicPlanningState state = BuildPlanningState();
        return BuildingProductionCommandRules.TryEnqueue(
            _catalog,
            state,
            _runtime.Queue,
            _building.Code,
            productCode,
            _economy.stockpile,
            _economy.population,
            out queueResult);
    }

    private void Update()
    {
        if (!IsBound || _building == null || !_building.IsAlive
            || GameManager.instance == null || GameManager.instance.gameIsPaused)
            return;

        _runtime.Advance(_building.Owner, Time.deltaTime, _completedProducts);
    }

    private StrategicPlanningState BuildPlanningState()
    {
        StrategicPlanningState state = new StrategicPlanningState
        {
            ownerId = _building.Owner,
            era = ToBuildingEra(AdvancedEraState.Current),
            population = _economy.population.current,
            populationCap = _economy.population.capacity,
            unlockedTechnologyCodes = new List<string>()
        };

        foreach (EconomyResourceType resource in Enum.GetValues(typeof(EconomyResourceType)))
            state.SetAmount(resource.TryGetLegacyResource(out InGameResource legacy)
                ? legacy : InGameResource.Food, _economy.stockpile.Get(resource));

        if (TechnologyNodeData.TECH_TREE_NODES != null)
            state.unlockedTechnologyCodes.AddRange(TechnologyNodeData.GetUnlockedNodeCodes());

        List<Unit> units;
        if (Unit.UNITS_BY_OWNER != null
            && Unit.UNITS_BY_OWNER.TryGetValue(_building.Owner, out units)
            && units != null)
        {
            for (int i = 0; i < units.Count; i++)
            {
                Building candidate = units[i] as Building;
                if (candidate == null || !candidate.IsAlive) continue;
                state.buildings.Add(new StrategicBuildingObservation
                {
                    entityId = candidate.Uid,
                    code = candidate.Code,
                    lifecycle = BuildingLifecycleState.Operational,
                    position = candidate.Transform.position,
                    ownerId = candidate.Owner
                });
            }
        }
        return state;
    }

    private void OnProductCompleted(string productCode)
    {
        // The legacy catalog calls the first worker "villager" while the
        // CharacterData asset uses the stable runtime code "worker". Keep
        // the authored economy contract and resolve the runtime alias here.
        string characterCode = productCode;
        if ((Globals.CHARACTER_DATA == null || !Globals.CHARACTER_DATA.ContainsKey(characterCode))
            && string.Equals(productCode, "villager", StringComparison.OrdinalIgnoreCase))
            characterCode = "worker";
        CharacterData data;
        if (Globals.CHARACTER_DATA == null
            || !Globals.CHARACTER_DATA.TryGetValue(characterCode, out data)
            || data == null || data.prefab == null)
        {
            LastFailure = "Completed product has no CharacterData: " + characterCode;
            Debug.LogWarning(LastFailure);
            return;
        }

        Character character = new Character(data, _building.Owner);
        Vector3 requested = _building.Transform.position
            + _building.Transform.forward * 3f
            + Vector3.up * 0.25f;
        CharacterManager manager = character.Transform.GetComponent<CharacterManager>();
        NavMeshAgent agent = manager == null ? null : manager.agent;
        if (agent == null || !Globals.TryWarpToNavMesh(agent, requested, 8f))
            character.SetPosition(requested);
    }

    private static BuildingEra ToBuildingEra(ImperialEra era)
    {
        switch (era)
        {
            case ImperialEra.Classical: return BuildingEra.Feudal;
            case ImperialEra.Medieval: return BuildingEra.Castle;
            case ImperialEra.Imperial: return BuildingEra.Imperial;
            default: return BuildingEra.Dark;
        }
    }
}
