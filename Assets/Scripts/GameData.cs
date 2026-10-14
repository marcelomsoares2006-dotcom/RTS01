using System.Runtime.Serialization;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GamePlayerUnitData : BinarySerializable
{
    public string unitType;
    public string code;
    public string uid;
    public Vector3 position;
    public int currentHealth;
    public int level;
    public int[] production; // sorted by GAME_RESOURCES_KEYS
    public int attackDamage;
    public float attackRange;

    // building-specific
    public int constructionHP;
    public int buildingLifecycle;
    public string[] constructorIds;
    /// <summary>Forward-compatible construction state; null means legacy save.</summary>
    public BuildingConstructionSaveSnapshot construction;
    public BuildingMaintenanceRuntimeSnapshot maintenance;
    /// <summary>Forward-compatible catalog-backed production queue snapshot.</summary>
    public BuildingProductionQueueSnapshot productionQueue;

    public GamePlayerUnitData() { }

    protected GamePlayerUnitData(SerializationInfo info, StreamingContext context)
    {
        BinarySerializable.Deserialize(this, info, context);
    }
}
[System.Serializable]
public class GamePlayerData
{
    public GamePlayerUnitData[] units;
    public int[] resources; // sorted by GAME_RESOURCES_KEYS
    public EconomyPlayerSaveData economy;
}

[System.Serializable]
public sealed class EconomyPlayerSaveData
{
    public EconomyResourceSaveData[] stockpile;
    public int population;
    public int populationCapacity;
    public EconomyCollectorSaveData[] collectors;

    public static EconomyPlayerSaveData Capture(EconomyPlayerState state)
    {
        if (state == null) return null;
        return new EconomyPlayerSaveData
        {
            stockpile = state.stockpile != null && state.stockpile.entries != null ? state.stockpile.entries.FindAll(e => e != null)
                .ConvertAll(e => new EconomyResourceSaveData((int)e.resource, e.amount)).ToArray() : new EconomyResourceSaveData[0],
            population = state.population != null ? state.population.current : 0,
            populationCapacity = state.population != null ? state.population.capacity : 0,
            collectors = state.collectors != null ? state.collectors.FindAll(c => c != null)
                .ConvertAll(EconomyCollectorSaveData.Capture).ToArray() : new EconomyCollectorSaveData[0]
        };
    }

    public EconomyPlayerState Restore()
    {
        EconomyPlayerState state = new EconomyPlayerState();
        state.stockpile.entries = new List<EconomyLedger.Entry>();
        if (stockpile != null) foreach (EconomyResourceSaveData e in stockpile)
            if (e != null) state.stockpile.entries.Add(new EconomyLedger.Entry((EconomyResourceType)e.resource, e.amount));
        state.stockpile.RebuildLookup();
        state.population = new PopulationLedger(populationCapacity, population);
        state.collectors = new List<EconomyCollector>();
        if (collectors != null) foreach (EconomyCollectorSaveData c in collectors)
            if (c != null) state.collectors.Add(c.Restore());
        return state;
    }
}

[System.Serializable]
public sealed class EconomyResourceSaveData
{
    public int resource;
    public int amount;
    public EconomyResourceSaveData() { }
    public EconomyResourceSaveData(int resource, int amount) { this.resource = resource; this.amount = amount < 0 ? 0 : amount; }
}

[System.Serializable]
public sealed class EconomyCollectorSaveData
{
    public string collectorId;
    public int ownerId;
    public int resource;
    public float ratePerSecond;
    public float carryCapacity;
    public float carried;

    public static EconomyCollectorSaveData Capture(EconomyCollector c) { return new EconomyCollectorSaveData { collectorId = c.collectorId, ownerId = c.ownerId, resource = (int)c.resource, ratePerSecond = c.ratePerSecond, carryCapacity = c.carryCapacity, carried = c.CarriedAmount }; }
    public EconomyCollector Restore() { EconomyCollector c = new EconomyCollector(collectorId, ownerId, (EconomyResourceType)resource, ratePerSecond, carryCapacity); c.SetCarriedAmount(carried); return c; }
}

[System.Serializable]
public class GameData : BinarySerializable
{
    public static string gameUid;
    private static GameData _instance;
    public static GameData Instance => _instance;

    public static string DATA_FILE_NAME = "GameData.data";

    public GamePlayerData[] players;
    public Vector3 camPosition;
    public string[] unlockedTechnologyNodeCodes;

    public static string GetFolderPath()
        => System.IO.Path.Combine(
            Application.persistentDataPath,
            DATA_DIRECTORY,
            "Games",
            gameUid);

    private static string _GetFilePath()
        => System.IO.Path.Combine(GetFolderPath(), DATA_FILE_NAME);

    public GameData() { }

    protected GameData(SerializationInfo info, StreamingContext context)
    {
        BinarySerializable.Deserialize(this, info, context);
    }

    public static GameData Load()
    {
        // A new match deliberately has no save yet; this is not a load failure.
        string path = _GetFilePath();
        _instance = System.IO.File.Exists(path) ? (GameData)BinarySerializable.Load(path) : null;
        return _instance;
    }

    public static void Save(GameData instance)
    {
        BinarySerializable.Save(_GetFilePath(), instance);
    }

}
