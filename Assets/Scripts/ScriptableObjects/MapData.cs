using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Map", menuName = "Scriptable Objects/Map")]
public class MapData : ScriptableObject
{

    public string mapName;
    public float mapSize;
    public int maxPlayers;

    public string sceneName;

    [Header("Scenario layout")]
    public List<MapPlayerStart> playerStarts = new List<MapPlayerStart>();
    public List<MapResourceNode> resourceNodes = new List<MapResourceNode>();
    public List<MapStartingResource> startingResources = new List<MapStartingResource>();

    [Min(1)] public int minimumResourceNodesPerPlayer = 3;

    public string GetMapSizeType()
    {
        if (mapSize <= 200) return "Small";
        if (mapSize <= 500) return "Medium";
        if (mapSize <= 1200) return "Large";
        return "Immense";
    }
}

[Serializable]
public sealed class MapPlayerStart
{
    [Min(0)] public int playerId;
    public Vector3 position;
    [Min(0f)] public float safeRadius = 12f;
}

[Serializable]
public sealed class MapResourceNode
{
    public string nodeId;
    public InGameResource resource;
    public Vector3 position;
    [Min(1)] public int amount = 500;
    [Min(0f)] public float gatherRadius = 2f;
    [Min(0f)] public float contestedRadius = 10f;
}

[Serializable]
public sealed class MapStartingResource
{
    public InGameResource resource;
    [Min(0)] public int amount;
}
