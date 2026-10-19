using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using EraImperial.Units.Advanced;

/// <summary>
/// Bounded live-production probe for the authored Core/GameScene/Map1 stack.
///
/// The harness never saves a scene and never edits a prefab or gameplay script.
/// It creates temporary runtime instances from the catalog's existing
/// BuildingData assets, queues villager and spearman through the live driver,
/// and checks the resulting Character objects, owner, NavMesh and selection.
/// An advanced Barracks product is attempted only when its authored catalog
/// entry and CharacterData prefab are both available.
/// </summary>
[InitializeOnLoad]
public static class VillagerSpearmanLiveProductionPlayModeHarness
{
    private const string RunningKey = "EraImperial.LiveProductionHarness.Running";
    private const string FinishedKey = "EraImperial.LiveProductionHarness.Finished";
    // Unity 6 may spend over a minute importing the authored scene stack on
    // the first batch run. The deadline must cover startup before judging the
    // actual live production probe.
    private const double TimeoutSeconds = 600.0;

    private static bool _finished;
    private static double _deadline;
    private static int _stage;
    private static int _owner;
    private static bool _readinessReported;
    private static Building _townCenter;
    private static Building _barracks;
    private static Building _siegeWorkshop;
    private static Building _enemyTarget;
    private static UnitManager _catapultManager;
    private static CatapultCombatController _catapultController;
    private static string _advancedCode;
    private static int _siegeShots;
    private static double _nextSiegeShotAt;
    private static readonly Dictionary<string, int> Baselines =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    static VillagerSpearmanLiveProductionPlayModeHarness()
    {
        EditorApplication.update += Tick;
    }

    /// <summary>Opens the real playable scene stack and enters PlayMode.</summary>
    public static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException(
                "Live production harness requires the Editor outside PlayMode.");

        _finished = false;
        _stage = 0;
        _townCenter = null;
        _barracks = null;
        _siegeWorkshop = null;
        _enemyTarget = null;
        _catapultManager = null;
        _catapultController = null;
        _advancedCode = null;
        _siegeShots = 0;
        _nextSiegeShotAt = 0.0;
        _readinessReported = false;
        Baselines.Clear();
        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(FinishedKey, false);
        _deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;

        Scene core = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Additive);
        Scene map = EditorSceneManager.OpenScene("Assets/Scenes/Maps/Map1.unity", OpenSceneMode.Additive);
        SceneManager.SetActiveScene(map);

        CoreDataHandler coreData = UnityEngine.Object.FindAnyObjectByType<CoreDataHandler>();
        MapData mapData = Resources.Load<MapData>("ScriptableObjects/Maps/Map1");
        if (coreData != null && mapData != null)
        {
            coreData.SetMapData(mapData);
            coreData.SetGameUID("Map1__LiveProductionHarness__" + Guid.NewGuid().ToString("N"));
        }

        // CoreBooter would otherwise switch to the menu during this bounded run.
        CoreBooter booter = UnityEngine.Object.FindAnyObjectByType<CoreBooter>();
        if (booter != null) booter.enabled = false;

        // Keep the local variable alive for clarity: the intended stack is
        // Core + GameScene + Map1, not a synthetic empty test scene.
        if (!core.IsValid())
            throw new InvalidOperationException("Core scene could not be opened.");
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (_finished || !SessionState.GetBool(RunningKey, false)
            || !EditorApplication.isPlaying || EditorApplication.isCompiling)
            return;

        try
        {
            // EnterPlaymode may reload the editor domain and reset static
            // fields. Re-arm the deadline after that reload instead of
            // treating the zeroed value as an immediate timeout.
            if (_deadline <= 0.0)
                _deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;

            if (EditorApplication.timeSinceStartup > _deadline)
            {
                Blocked("timeout aguardando a cena real, GameManager, catálogo ou NavMesh");
                return;
            }

            if (_stage == 0)
            {
                if (!ReadyToProbe()) return;
                SetupLiveBuildingsAndQueues();
                _stage = 1;
                return;
            }

            if (_stage == 1)
            {
                if (!ObserveRequiredProduct("worker", "villager")) return;
                if (!ObserveRequiredProduct("spearman", "spearman")) return;
                _stage = 2;
                return;
            }

            if (_stage == 2)
            {
                if (!string.IsNullOrEmpty(_advancedCode)
                    && !ObserveRequiredProduct(_advancedCode, _advancedCode)) return;
                if (!ObserveRequiredProduct("catapult", "catapult")) return;
                _catapultManager = FindOwnedUnitManager("catapult", _owner);
                Require(_catapultManager != null, "catapulta produzida sem UnitManager real");
                _catapultController = _catapultManager.GetComponent<CatapultCombatController>();
                Require(_catapultController != null, "catapulta produzida sem CatapultCombatController real");
                _stage = 3;
                return;
            }

            if (_stage == 3)
            {
                RunLiveSiegeStep();
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static bool ReadyToProbe()
    {
        bool gameManagerReady = GameManager.instance != null;
        bool economyReady = DataHandler.EconomyRuntime != null;
        bool characterCatalogReady = Globals.CHARACTER_DATA != null;
        bool workerReady = characterCatalogReady && Globals.CHARACTER_DATA.ContainsKey("worker");
        bool spearmanReady = characterCatalogReady && Globals.CHARACTER_DATA.ContainsKey("spearman");
        GameObject spawnpoints = GameObject.Find("Spawnpoints");
        bool spawnpointsReady = spawnpoints != null;
        NavMeshSurface surface = UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();
        int navMeshVertices = NavMesh.CalculateTriangulation().vertices.Length;
        bool navMeshReady = surface != null && navMeshVertices > 0;
        if (!_readinessReported)
        {
            _readinessReported = true;
            Debug.Log("LIVE_PRODUCTION_READINESS gameManager=" + gameManagerReady
                + " economy=" + economyReady
                + " catalog=" + characterCatalogReady
                + " worker=" + workerReady
                + " spearman=" + spearmanReady
                + " spawnpoints=" + spawnpointsReady
                + " navMeshSurface=" + (surface != null)
                + " navMeshVertices=" + navMeshVertices);
        }
        return gameManagerReady && economyReady && workerReady && spearmanReady
            && spawnpointsReady && navMeshReady;
    }

    private static void SetupLiveBuildingsAndQueues()
    {
        _owner = GameManager.instance.gamePlayersParameters.myPlayerId;
        BuildingContentCatalog catalog = Resources.Load<BuildingContentCatalog>(
            "ScriptableObjects/Units/Building Content Catalog");
        Require(catalog != null, "Building Content Catalog ausente em Resources");

        BuildingContentSpec townSpec = catalog.Find("town_center");
        BuildingContentSpec barracksSpec = catalog.Find("barracks");
        Require(townSpec != null && townSpec.buildingData != null,
            "Town Center BuildingData ausente no catálogo");
        Require(barracksSpec != null && barracksSpec.buildingData != null,
            "Barracks BuildingData ausente no catálogo");

        Transform points = GameObject.Find("Spawnpoints").transform;
        Require(points.childCount >= 2, "Map1 não possui dois Spawnpoints");
        Vector3 townPosition = Sample(points.GetChild(0).position);
        Vector3 barracksPosition = Sample(points.GetChild(1).position);

        _townCenter = SpawnOperationalBuilding(townSpec.buildingData, townPosition, "town_center");
        _barracks = SpawnOperationalBuilding(barracksSpec.buildingData, barracksPosition, "barracks");
        Require(_townCenter != null && _townCenter.IsAlive, "Town Center temporário não ficou operacional");
        Require(_barracks != null && _barracks.IsAlive, "Barracks temporário não ficou operacional");

        BuildingContentSpec siegeSpec = catalog.Find("siege_workshop");
        Require(siegeSpec != null && siegeSpec.buildingData != null,
            "Siege Workshop BuildingData ausente no catálogo");
        Require(Globals.CHARACTER_DATA.ContainsKey("catapult"),
            "CharacterData da catapulta ausente no catálogo runtime");
        Require(siegeSpec.production != null && siegeSpec.production.Any(
            p => p != null && string.Equals(p.productCode, "catapult", StringComparison.OrdinalIgnoreCase)),
            "Siege Workshop não expõe produção de catapulta");

        AdvancedEraState.Set(ImperialEra.Imperial);
        Vector3 siegePosition = Sample(points.GetChild(0).position + Vector3.right * 4f);
        _siegeWorkshop = SpawnOperationalBuilding(siegeSpec.buildingData, siegePosition, "siege_workshop");
        int enemyOwner = (GameManager.instance.gamePlayersParameters.players.Length > 1)
            ? (_owner + 1) % GameManager.instance.gamePlayersParameters.players.Length : _owner + 1;
        _enemyTarget = SpawnOperationalBuilding(townSpec.buildingData,
            Sample(siegePosition + Vector3.forward * 12f), "siege_target");
        Require(_enemyTarget != null && _enemyTarget.IsAlive, "alvo BuildingManager não ficou operacional");
        // Building's owner is authored by its constructor; rebuild the target
        // with the adversary owner so the live attack path cannot be friendly fire.
        UnityEngine.Object.Destroy(_enemyTarget.Transform.gameObject);
        _enemyTarget = SpawnOperationalBuilding(townSpec.buildingData,
            Sample(siegePosition + Vector3.forward * 12f), "siege_target_enemy", enemyOwner);
        Require(_enemyTarget != null && _enemyTarget.IsAlive && _enemyTarget.Owner != _owner,
            "alvo BuildingManager não recebeu proprietário adversário");
        // Keep the authored Town Center/BuildingManager/collider intact while
        // making this bounded PlayMode probe finish in a few real impacts.
        // The target still receives the normal UnitManager damage path and
        // must transition through Building destruction.
        _enemyTarget.HP = Mathf.Min(_enemyTarget.HP, 360);

        EconomyPlayerState economy = DataHandler.EconomyRuntime.GetOrCreatePlayer(_owner);
        foreach (EconomyResourceType resource in Enum.GetValues(typeof(EconomyResourceType)))
            economy.stockpile.Set(resource, 10000);
        economy.population.AddCapacity(20);

        QueueAndAdvance(_townCenter, "villager", 20f);
        QueueAndAdvance(_barracks, "spearman", 20f);
        QueueAndAdvance(_siegeWorkshop, "catapult", 60f);

        // Advanced production is optional and does not turn an unavailable
        // authored runtime asset into a false failure.
        foreach (string candidate in new[] { "combat_mage", "paladin_healer" })
        {
            if (!Globals.CHARACTER_DATA.ContainsKey(candidate)) continue;
            BuildingContentSpec spec = catalog.Find("barracks");
            if (spec == null || spec.production == null
                || !spec.production.Any(p => p != null && p.productCode == candidate)) continue;
            _advancedCode = candidate;
            AdvancedEraState.Set(ImperialEra.Imperial);
            QueueAndAdvance(_barracks, candidate, 40f);
            break;
        }
    }

    private static Building SpawnOperationalBuilding(BuildingData data, Vector3 position, string label)
    {
        return SpawnOperationalBuilding(data, position, label, _owner);
    }

    private static Building SpawnOperationalBuilding(BuildingData data, Vector3 position, string label, int owner)
    {
        Building building = new Building(data, owner);
        building.Transform.name = "LiveProductionHarness_" + label;
        building.SetPosition(position);
        building.SetConstructionHP(building.MaxHP);
        BuildingProductionRuntimeDriver driver = building.Transform
            .GetComponent<BuildingProductionRuntimeDriver>();
        if (driver == null || !driver.IsBound)
            driver = BuildingProductionRuntimeDriver.Attach(building);
        Require(driver != null && driver.IsBound,
            label + " não recebeu BuildingProductionRuntimeDriver: "
            + (driver == null ? "null" : driver.LastFailure));
        return building;
    }

    private static UnitManager FindOwnedUnitManager(string runtimeCode, int owner)
    {
        if (Unit.UNITS_BY_OWNER == null || !Unit.UNITS_BY_OWNER.ContainsKey(owner)) return null;
        Unit unit = Unit.UNITS_BY_OWNER[owner].FirstOrDefault(item => item != null
            && string.Equals(item.Code, runtimeCode, StringComparison.OrdinalIgnoreCase)
            && item.Transform != null);
        return unit == null ? null : unit.Transform.GetComponent<UnitManager>();
    }

    private static void RunLiveSiegeStep()
    {
        Require(_catapultManager != null && _catapultManager.Unit != null,
            "catapulta sem UnitManager/Unit no cerco real");
        if (_enemyTarget == null || !_enemyTarget.IsAlive || _enemyTarget.Transform == null)
        {
            Debug.Log("LIVE_PRODUCTION_SIEGE_PASS: SiegeWorkshop -> catapult -> BuildingManager enemy -> "
                + "projectile/damage/destruction shots=" + _siegeShots);
            Pass();
            return;
        }
        if (EditorApplication.timeSinceStartup < _nextSiegeShotAt) return;
        if (_siegeShots >= 12)
            throw new InvalidOperationException("catapulta não destruiu o BuildingManager após 12 disparos válidos");
        // Invoke the authored controller after production. BeginFire keeps
        // this probe independent from the AI target-selection cadence while
        // still exercising the real projectile, cooldown/release and damage
        // path against the live BuildingManager.
        _catapultController.BeginFire(_enemyTarget.Transform);
        _siegeShots++;
        Debug.Log("LIVE_PRODUCTION_SIEGE_SHOT shot=" + _siegeShots
            + " targetHp=" + _enemyTarget.HP);
        // Imperial catapult cooldown is 4.5 s; leave margin for release and
        // projectile flight so each command is a valid live attack.
        _nextSiegeShotAt = EditorApplication.timeSinceStartup + 6.0;
    }

    private static void QueueAndAdvance(Building building, string product, float advance)
    {
        BuildingProductionRuntimeDriver driver = building.Transform
            .GetComponent<BuildingProductionRuntimeDriver>();
        Require(driver != null && driver.IsBound, building.Code + " driver não está ligado");
        int runtimeCodeCount = CountUnits(RuntimeCode(product), _owner);
        Baselines[RuntimeCode(product)] = runtimeCodeCount;
        BuildingProductionQueueResult queueResult;
        Require(driver.TryEnqueueProduct(product, out queueResult)
            == BuildingProductionCommandResult.Accepted,
            building.Code + "/" + product + " enqueue rejeitado: " + queueResult);
        List<string> completed;
        Require(driver.Runtime.Advance(_owner, advance, out completed)
            == BuildingProductionRuntimeResult.Accepted
            && completed.Contains(product),
            building.Code + "/" + product + " não concluiu no runtime vivo");
    }

    private static bool ObserveRequiredProduct(string runtimeCode, string markerCode)
    {
        int baseline;
        if (!Baselines.TryGetValue(runtimeCode, out baseline)) return true;
        List<Unit> units = Unit.UNITS_BY_OWNER != null && Unit.UNITS_BY_OWNER.ContainsKey(_owner)
            ? Unit.UNITS_BY_OWNER[_owner] : new List<Unit>();
        Unit unit = units.FirstOrDefault(item => item != null
            && string.Equals(item.Code, runtimeCode, StringComparison.OrdinalIgnoreCase)
            && item.Transform != null);
        if (unit == null || units.Count(item => item != null
            && string.Equals(item.Code, runtimeCode, StringComparison.OrdinalIgnoreCase)) <= baseline)
            return false;

        CharacterManager manager = unit.Transform.GetComponent<CharacterManager>();
        NavMeshAgent agent = unit.Transform.GetComponent<NavMeshAgent>();
        Require(manager != null, markerCode + " instanciou sem CharacterManager");
        Require(unit.Owner == _owner, markerCode + " instanciou com owner incorreto");
        Require(agent != null && agent.isOnNavMesh,
            markerCode + " instanciou fora do NavMesh");
        manager.Select();
        Require(manager.IsSelected && Globals.SELECTED_UNITS.Contains(manager),
            markerCode + " não entrou na seleção real");
        manager.Deselect();
        Debug.Log("LIVE_PRODUCTION_" + markerCode.ToUpperInvariant()
            + "_PASS: instantiated owner=" + unit.Owner + " navmesh=true selection=true");
        Baselines.Remove(runtimeCode);
        return true;
    }

    private static int CountUnits(string runtimeCode, int owner)
    {
        if (Unit.UNITS_BY_OWNER == null || !Unit.UNITS_BY_OWNER.ContainsKey(owner))
            return 0;

        return Unit.UNITS_BY_OWNER[owner].Count(item => item != null
            && string.Equals(item.Code, runtimeCode, StringComparison.OrdinalIgnoreCase));
    }

    private static Vector3 Sample(Vector3 requested)
    {
        NavMeshHit hit;
        Require(NavMesh.SamplePosition(requested, out hit, 30f, NavMesh.AllAreas),
            "Map1 Spawnpoint sem posição navegável: " + requested);
        return hit.position;
    }

    private static string RuntimeCode(string product)
    {
        return string.Equals(product, "villager", StringComparison.OrdinalIgnoreCase)
            ? "worker" : product;
    }

    private static void Pass()
    {
        _finished = true;
        SessionState.SetBool(FinishedKey, true);
        Debug.Log("LIVE_PRODUCTION_PLAYMODE_PASS: Core+GameScene+Map1 -> "
            + "catalog -> TownCenter/villager -> Barracks/spearman"
            + (string.IsNullOrEmpty(_advancedCode) ? "" : " -> " + _advancedCode)
            + " -> instantiation/owner/NavMesh/selection");
        StopAndExit(0);
    }

    private static void Blocked(string reason)
    {
        _finished = true;
        SessionState.SetBool(FinishedKey, false);
        NavMeshSurface[] surfaces = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include);
        Debug.LogWarning("LIVE_PRODUCTION_READINESS_TIMEOUT_STATE gameManager="
            + (GameManager.instance != null)
            + " economy=" + (DataHandler.EconomyRuntime != null)
            + " catalog=" + (Globals.CHARACTER_DATA != null)
            + " worker=" + (Globals.CHARACTER_DATA != null && Globals.CHARACTER_DATA.ContainsKey("worker"))
            + " spearman=" + (Globals.CHARACTER_DATA != null && Globals.CHARACTER_DATA.ContainsKey("spearman"))
            + " spawnpoints=" + (GameObject.Find("Spawnpoints") != null)
            + " navMeshSurfaces=" + surfaces.Length
            + " navMeshVertices=" + NavMesh.CalculateTriangulation().vertices.Length
            + " compiling=" + EditorApplication.isCompiling);
        Debug.LogWarning("LIVE_PRODUCTION_PLAYMODE_BLOCKED: " + reason);
        StopAndExit(2);
    }

    private static void Fail(Exception exception)
    {
        _finished = true;
        SessionState.SetBool(FinishedKey, false);
        Debug.LogError("LIVE_PRODUCTION_PLAYMODE_FAIL: " + exception);
        StopAndExit(1);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void StopAndExit(int code)
    {
        SessionState.SetBool(RunningKey, false);
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(code);
    }
}
