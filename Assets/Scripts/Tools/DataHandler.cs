using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class DataHandler : MonoBehaviour
{
    public static LegacyEconomyRuntimeAdapter EconomyRuntime { get; set; }
    public static BuildingContentCatalog ProductionContentCatalog { get; set; }
    public static System.Func<int, StrategicPlanningState> ProductionPlanningStateProvider { get; set; }
    public static System.Func<string, BuildingProductionQueue> ProductionQueueProvider { get; set; }

    /// <summary>
    /// Captures a live production queue into the unit save record. The caller
    /// supplies the detached planning state because save code must not infer
    /// era/ownership from a prefab or scene object.
    /// </summary>
    public static BuildingProductionSnapshotResult CaptureProductionQueue(
        GamePlayerUnitData unitData,
        BuildingContentCatalog catalog,
        StrategicPlanningState state,
        BuildingProductionQueue queue)
    {
        if (unitData == null) return BuildingProductionSnapshotResult.InvalidSnapshot;
        BuildingProductionQueueSnapshot snapshot;
        BuildingProductionSnapshotResult result = BuildingProductionSaveRules.Capture(
            catalog, state, queue, unitData.code, out snapshot);
        if (result == BuildingProductionSnapshotResult.Captured)
            unitData.productionQueue = snapshot;
        return result;
    }

    /// <summary>Restores the queue persisted on a unit save record.</summary>
    public static BuildingProductionSnapshotResult RestoreProductionQueue(
        GamePlayerUnitData unitData,
        BuildingContentCatalog catalog,
        StrategicPlanningState state,
        BuildingProductionQueue queue)
    {
        if (unitData == null) return BuildingProductionSnapshotResult.InvalidSnapshot;
        return BuildingProductionSaveRules.Restore(
            catalog, state, queue, unitData.productionQueue);
    }
    public void Start()
    {
        // A era persistida e restaurada por ConstructionReplayRuntime abaixo.
        // O reset cobre apenas uma nova partida sem save e emite Changed para
        // manter materiais e decoracoes sincronizados.
        AdvancedEraState.Reset();
        DeserializeGameData();
    }

    public static void LoadGameData()
    {
        // load building data
        Globals.BUILDING_DATA = Resources.LoadAll<BuildingData>("ScriptableObjects/Units/Buildings") as BuildingData[];
        CharacterData[] characterData = Resources.LoadAll<CharacterData>("ScriptableObjects/Units/Characters") as CharacterData[];
        foreach (CharacterData d in characterData)
            Globals.CHARACTER_DATA[d.code] = d;

        // load game parameters
        string gameUid = CoreDataHandler.instance.GameUID;

        GameParameters[] gameParametersList = Resources.LoadAll<GameParameters>("ScriptableObjects/Parameters");
        foreach (GameParameters parameters in gameParametersList)
        {
            if (parameters is GamePlayersParameters pp)
                pp.LoadFromFile($"Games/{gameUid}/PlayerParameters");
            else
                parameters.LoadFromFile();
        }

        // prepare technology tree
        TechnologyNodeData.LoadTechnologyTree();

        // load game scene data
        GameData.gameUid = gameUid;
        GameData.Load();
    }

    public static void SaveGameData()
    {
        // save game parameters
        GameParameters[] gameParametersList = Resources.LoadAll<GameParameters>("ScriptableObjects/Parameters");
        foreach (GameParameters parameters in gameParametersList)
        {
            if (parameters is GamePlayersParameters)
                parameters.SaveToFile($"Games/{CoreDataHandler.instance.GameUID}/PlayerParameters");
            else
                parameters.SaveToFile();
        }

        // save game scene data
        GameData.gameUid = CoreDataHandler.instance.GameUID;
        GameData.Save(SerializeGameData());
        // save game scene current minimap
        Camera minimapCamera = GameObject.Find("CameraMinimap").GetComponent<Camera>();
        MinimapManager.IS_ENABLED = false;
        MinimapCapture.TakeScreenshot(
            "minimap",
            new Vector2Int(512, 512),
            minimapCamera,
            GameData.GetFolderPath());
        MinimapManager.IS_ENABLED = true;
    }

    public static void DeserializeGameData()
    {
        GameData data = GameData.Instance;
        if (data == null)
        {
            Object.FindAnyObjectByType<CameraManager>().InitializeBounds();
            EventManager.TriggerEvent("LoadedScene");
            return;
        }

        TechnologyNodeData.SetUnlockedNodes(data.unlockedTechnologyNodeCodes);
        ConstructionReplayResult replayRestore = ConstructionReplayRuntime.Restore(
            data.constructionReplayCommands, data.constructionReplayState);
        if (replayRestore != ConstructionReplayResult.Accepted)
            Debug.LogWarning($"Construction replay restore rejected: {replayRestore}.");

        GamePlayersParameters playerParameters = GameManager.instance.gamePlayersParameters;
        Dictionary<string, CharacterManager> restoredCharacters =
            new Dictionary<string, CharacterManager>();
        List<Building> buildingsWithSavedConstructors = new List<Building>();
        List<string[]> savedConstructorIds = new List<string[]>();
        for (int p = 0; p < playerParameters.players.Length; p++)
        {
            GamePlayerData d = data.players[p];

            // restore resources
            for (int i = 0; i < d.resources.Length; i++)
            {
                InGameResource r = Globals.GAME_RESOURCE_KEYS[i];
                Globals.GAME_RESOURCES[p][r].Amount = d.resources[i];
            }

            if (EconomyRuntime != null && d.economy != null)
                EconomyRuntime.RestorePlayer(p, d.economy.Restore());

            // restore units
            foreach (GamePlayerUnitData unit in d.units)
            {
                Unit u;
                string type = unit.unitType;
                List<ResourceValue> production = new List<ResourceValue>();
                for (int i = 0; i < unit.production.Length; i++)
                {
                    InGameResource r = Globals.GAME_RESOURCE_KEYS[i];
                    production.Add(new ResourceValue(r, unit.production[i]));
                }
                if (type == "building")
                {
                    if (unit.construction != null && !BuildingConstructionSaveRules.CanRestoreRuntime(
                        unit.construction, p))
                    {
                        Debug.LogWarning($"Skipping non-runtime building save '{unit.code}'.");
                        continue;
                    }
                    BuildingData bd = Globals.BUILDING_DATA
                        .Where((BuildingData x) => x.code == unit.code)
                        .First();
                    u = new Building(bd, p, production);
                    u.SetPosition(unit.position);
                    u.Place(true);
                    Building restoredBuilding = (Building)u;
                    if (unit.construction != null)
                        restoredBuilding.SetReservedConstructionCosts(unit.construction.reservedCosts);
                    if (EconomyRuntime != null)
                    {
                        EconomyPlayerState economyState;
                        if (EconomyRuntime.TryGetPlayer(p, out economyState))
                        {
                            BuildingMaintenanceRuntimeDriver driver = restoredBuilding.Transform.gameObject
                                .GetComponent<BuildingMaintenanceRuntimeDriver>();
                            if (driver == null) driver = restoredBuilding.Transform.gameObject
                                .AddComponent<BuildingMaintenanceRuntimeDriver>();
                            driver.Bind(restoredBuilding, economyState.stockpile, new BuildingMaintenanceSpec());
                        }
                    }
                    int constructionHealth = unit.construction != null
                        ? unit.construction.constructionHP
                        : unit.constructionHP;
                    // Legacy saves used zero for a completed building before
                    // lifecycle tracking existed. Keep those buildings live.
                    if (constructionHealth <= 0
                        && unit.currentHealth >= bd.healthpoints
                        && unit.buildingLifecycle == (int)BuildingLifecycleState.Blueprint)
                        constructionHealth = bd.healthpoints;
                    restoredBuilding.SetConstructionHP(constructionHealth, true);
                    string[] constructorIds = unit.construction != null
                        ? unit.construction.constructorIds
                        : unit.constructorIds;
                    if (constructorIds != null && constructorIds.Length > 0)
                    {
                        buildingsWithSavedConstructors.Add(restoredBuilding);
                        savedConstructorIds.Add(constructorIds);
                    }
                }
                else
                {
                    CharacterData cd = Globals.CHARACTER_DATA[unit.code];
                    u = new Character(cd, p);
                    CharacterManager restoredCharacter =
                        u.Transform.GetComponent<CharacterManager>();
                    if (restoredCharacter != null && !string.IsNullOrEmpty(unit.uid))
                        restoredCharacters[unit.uid] = restoredCharacter;
                    u.ComputeProduction();
                    if (!Globals.TryWarpToNavMesh(
                        u.Transform.GetComponent<NavMeshAgent>(),
                        unit.position, 8f))
                    {
                        Debug.LogWarning($"Could not restore character {unit.uid} on NavMesh at {unit.position}; keeping fallback position.");
                    }
                }

                u.Uid = unit.uid;
                u.HP = unit.currentHealth;
                u.Level = unit.level - 1;
                u.LevelUp(true);
                u.AttackDamage = unit.attackDamage;
                u.AttackRange = unit.attackRange;
                if (type == "building" && unit.maintenance != null)
                {
                    BuildingMaintenanceRuntimeDriver driver = u.Transform.gameObject
                        .GetComponent<BuildingMaintenanceRuntimeDriver>();
                    if (driver != null)
                        driver.RestoreSnapshot(
                            unit.maintenance.currentHealth,
                            unit.maintenance.lifecycle,
                            unit.maintenance.operationState);
                }
                RestoreRegisteredProductionQueue(unit, p);
            }
        }

        for (int i = 0; i < buildingsWithSavedConstructors.Count; i++)
        {
            Building building = buildingsWithSavedConstructors[i];
            if (building == null || building.IsAlive)
                continue;
            string[] constructorIds = savedConstructorIds[i];
            for (int j = 0; j < constructorIds.Length; j++)
            {
                CharacterManager constructor;
                if (constructorIds[j] == null
                    || !restoredCharacters.TryGetValue(constructorIds[j], out constructor))
                    continue;
                building.RestoreConstructor(constructor);
            }
        }

        Camera.main.transform.position = data.camPosition;
        Object.FindAnyObjectByType<CameraManager>().InitializeBounds();
        EventManager.TriggerEvent("UpdatedResources");
        EventManager.TriggerEvent("LoadedScene");
    }

    public static GameData SerializeGameData()
    {
        List<GamePlayerData> players = new List<GamePlayerData>();
        GamePlayersParameters playerParameters = GameManager.instance.gamePlayersParameters;
        for (int p = 0; p < playerParameters.players.Length; p++)
        {
            GamePlayerData d = new GamePlayerData();

            // get resources
            int[] resources = new int[Globals.GAME_RESOURCE_KEYS.Length];
            for (int i = 0; i < Globals.GAME_RESOURCE_KEYS.Length; i++)
            {
                InGameResource r = Globals.GAME_RESOURCE_KEYS[i];
                resources[i] = Globals.GAME_RESOURCES[p][r].Amount;
            }
            d.resources = resources;

            if (EconomyRuntime != null)
            {
                EconomyPlayerState economyState;
                if (EconomyRuntime.TryGetPlayer(p, out economyState))
                    d.economy = EconomyPlayerSaveData.Capture(economyState);
            }

            // get units
            List<GamePlayerUnitData> units = new List<GamePlayerUnitData>();
            foreach (Unit unit in Unit.UNITS_BY_OWNER[p])
            {
                if (!unit.Transform) continue;

                int[] production = new int[Globals.GAME_RESOURCE_KEYS.Length];
                for (int i = 0; i < Globals.GAME_RESOURCE_KEYS.Length; i++)
                {
                    InGameResource r = Globals.GAME_RESOURCE_KEYS[i];
                    production[i] = unit.Production.ContainsKey(r)
                        ? unit.Production[r]
                        : 0;
                }
                GamePlayerUnitData u = new GamePlayerUnitData()
                {
                    unitType = (unit is Building) ? "building" : "character",
                    uid = unit.Uid,
                    code = unit.Code,
                    position = unit.Transform.position,
                    currentHealth = unit.HP,
                    level = unit.Level,
                    production = production,
                    attackDamage = unit.AttackDamage,
                    attackRange = unit.AttackRange,
                };
                if (u.unitType == "building")
                {
                    Building building = (Building)unit;
                    u.constructionHP = building.ConstructionHP;
                    u.buildingLifecycle = (int)building.LifecycleState;
                    u.constructorIds = building.Constructors == null ? new string[0] : building.Constructors
                        .FindAll(constructor => constructor != null)
                        .ConvertAll(constructor => constructor.Uid).ToArray();
                    BuildingConstructionSaveRules.Capture(
                        building.Code,
                        building.Owner,
                        building.Transform.position,
                        building.ConstructionHP,
                        building.MaxHP,
                        building.LifecycleState,
                        u.constructorIds,
                        building.ReservedConstructionCosts,
                        BuildingContentAssetStage.RuntimeReady,
                        true,
                        out u.construction);
                    CaptureRegisteredProductionQueue(u, p);
                    BuildingMaintenanceRuntimeDriver driver = building.Transform.gameObject
                        .GetComponent<BuildingMaintenanceRuntimeDriver>();
                    if (driver != null)
                        u.maintenance = driver.Capture();
                }
                units.Add(u);
            }
            d.units = units.ToArray();

            players.Add(d);
        }

        GameData data = new GameData();
        data.players = players.ToArray();
        data.camPosition = Camera.main.transform.position;
        data.unlockedTechnologyNodeCodes = TechnologyNodeData.GetUnlockedNodeCodes();
        data.constructionReplayCommands = ConstructionReplayRuntime.ExportCommands();
        data.constructionReplayState = ConstructionReplayRuntime.State;
        return data;
    }

    private static void CaptureRegisteredProductionQueue(GamePlayerUnitData unit, int ownerId)
    {
        if (ProductionContentCatalog == null || ProductionPlanningStateProvider == null
            || ProductionQueueProvider == null || unit == null) return;
        StrategicPlanningState state = ProductionPlanningStateProvider(ownerId);
        BuildingProductionQueue queue = ProductionQueueProvider(unit.uid);
        if (state == null || queue == null) return;
        BuildingProductionSnapshotResult result = CaptureProductionQueue(
            unit, ProductionContentCatalog, state, queue);
        if (result != BuildingProductionSnapshotResult.Captured)
            Debug.LogWarning("Production queue was not captured for " + unit.code + ": " + result);
    }

    private static void RestoreRegisteredProductionQueue(GamePlayerUnitData unit, int ownerId)
    {
        if (ProductionContentCatalog == null || ProductionPlanningStateProvider == null
            || ProductionQueueProvider == null || unit == null || unit.productionQueue == null) return;
        StrategicPlanningState state = ProductionPlanningStateProvider(ownerId);
        BuildingProductionQueue queue = ProductionQueueProvider(unit.uid);
        if (state == null || queue == null) return;
        BuildingProductionSnapshotResult result = RestoreProductionQueue(
            unit, ProductionContentCatalog, state, queue);
        if (result != BuildingProductionSnapshotResult.Restored)
            Debug.LogWarning("Production queue was not restored for " + unit.code + ": " + result);
    }

    public static List<(string, System.DateTime)> GetGamesList()
    {
        string rootPath = Path.Combine(
            Application.persistentDataPath,
            BinarySerializable.DATA_DIRECTORY,
            "Games");
        if (!Directory.Exists(rootPath))
            return null;
        string[] gameDirs = Directory.GetDirectories(rootPath);

        // filter to keep only game folders with a game save
        IEnumerable<string> validGameDirs = gameDirs
            .Where((string d)
                => File.Exists(Path.Combine(d, GameData.DATA_FILE_NAME)));

        // extract the last modification time
        List<(string, System.DateTime)> games = new List<(string, System.DateTime)>();
        foreach (string dir in validGameDirs)
        {
            games.Add((
                dir,
                File.GetLastWriteTime(Path.Combine(dir, GameData.DATA_FILE_NAME))
            ));
        }

        // sort by reverse chronological order (most recent first)
        return games
            .OrderByDescending(((string, System.DateTime) x) => x.Item2)
            .ToList();
    }
}
