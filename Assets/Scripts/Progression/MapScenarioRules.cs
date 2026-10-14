using System;
using System.Collections.Generic;
using UnityEngine;

public enum MapScenarioValidationResult
{
    Valid,
    MissingMap,
    InvalidIdentity,
    InvalidDimensions,
    InvalidPlayerCount,
    MissingPlayerStarts,
    DuplicatePlayerStart,
    PlayerStartOutsideMap,
    InvalidResourceNode,
    DuplicateResourceNode,
    InsufficientResourceCoverage,
    InvalidStartingResource
}

/// <summary>
/// Editor-free validation for playable map metadata. Scene objects can be
/// checked against this contract before Unity authoring and runtime wiring.
/// Coordinates use the same X/Z world-space rectangle [0,mapSize].
/// </summary>
public static class MapScenarioRules
{
    public static MapScenarioValidationResult Validate(MapData map, out string detail)
    {
        detail = string.Empty;
        if (map == null) return Fail(MapScenarioValidationResult.MissingMap, "MapData is required.", out detail);
        if (string.IsNullOrWhiteSpace(map.mapName) || string.IsNullOrWhiteSpace(map.sceneName))
            return Fail(MapScenarioValidationResult.InvalidIdentity, "Map name and scene name are required.", out detail);
        if (map.mapSize <= 0f || float.IsNaN(map.mapSize) || float.IsInfinity(map.mapSize))
            return Fail(MapScenarioValidationResult.InvalidDimensions, "Map size must be finite and positive.", out detail);
        if (map.maxPlayers < 2 || map.maxPlayers > 8)
            return Fail(MapScenarioValidationResult.InvalidPlayerCount, "Playable scenarios support 2 to 8 players.", out detail);

        if (map.playerStarts == null || map.playerStarts.Count != map.maxPlayers)
            return Fail(MapScenarioValidationResult.MissingPlayerStarts, "Player start count must equal maxPlayers.", out detail);

        HashSet<int> players = new HashSet<int>();
        for (int i = 0; i < map.playerStarts.Count; i++)
        {
            MapPlayerStart start = map.playerStarts[i];
            if (start == null || !players.Add(start.playerId))
                return Fail(MapScenarioValidationResult.DuplicatePlayerStart, "Player start ids must be unique.", out detail);
            if (!Inside(start.position, map.mapSize) || start.safeRadius <= 0f)
                return Fail(MapScenarioValidationResult.PlayerStartOutsideMap, "Player starts must be inside the map with a positive safe radius.", out detail);
        }

        if (map.resourceNodes == null || map.resourceNodes.Count == 0)
            return Fail(MapScenarioValidationResult.InsufficientResourceCoverage, "At least one resource node is required.", out detail);
        HashSet<string> nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<InGameResource, int> counts = new Dictionary<InGameResource, int>();
        for (int i = 0; i < map.resourceNodes.Count; i++)
        {
            MapResourceNode node = map.resourceNodes[i];
            if (node == null || string.IsNullOrWhiteSpace(node.nodeId) || node.amount <= 0 || node.gatherRadius <= 0f || !Inside(node.position, map.mapSize))
                return Fail(MapScenarioValidationResult.InvalidResourceNode, "Resource nodes require unique ids, positive amounts/radii and an in-bounds position.", out detail);
            if (!nodeIds.Add(node.nodeId.Trim()))
                return Fail(MapScenarioValidationResult.DuplicateResourceNode, "Resource node ids must be unique.", out detail);
            int count; counts.TryGetValue(node.resource, out count); counts[node.resource] = count + 1;
        }
        foreach (InGameResource resource in new[] { InGameResource.Wood, InGameResource.Gold, InGameResource.Stone })
        {
            int count; counts.TryGetValue(resource, out count);
            if (count < map.maxPlayers)
                return Fail(MapScenarioValidationResult.InsufficientResourceCoverage, "Each strategic resource needs at least one node per player.", out detail);
        }
        if (map.startingResources == null || map.startingResources.Count == 0)
            return Fail(MapScenarioValidationResult.InvalidStartingResource, "Starting resource definitions are required.", out detail);
        for (int i = 0; i < map.startingResources.Count; i++)
            if (map.startingResources[i] == null || map.startingResources[i].amount < 0)
                return Fail(MapScenarioValidationResult.InvalidStartingResource, "Starting resource amounts cannot be negative.", out detail);
        detail = "Map metadata, starts and resource coverage are valid.";
        return MapScenarioValidationResult.Valid;
    }

    public static bool IsPlayable(MapData map) { string detail; return Validate(map, out detail) == MapScenarioValidationResult.Valid; }

    private static bool Inside(Vector3 position, float size)
    {
        return position.x >= 0f && position.x <= size && position.z >= 0f && position.z <= size
            && !float.IsNaN(position.x) && !float.IsNaN(position.z)
            && !float.IsInfinity(position.x) && !float.IsInfinity(position.z);
    }

    private static MapScenarioValidationResult Fail(MapScenarioValidationResult result, string message, out string detail)
    { detail = message; return result; }
}

/// <summary>Canonical era transition targets used by scenario setup and AI.</summary>
public static class EraProgressionCatalog
{
    public static EraTransitionRequirement For(BuildingEra targetEra)
    {
        EraTransitionRequirement requirement = new EraTransitionRequirement(targetEra);
        switch (targetEra)
        {
            case BuildingEra.Feudal:
                requirement.minimumPopulation = 6; requirement.landmarkCode = "town_center";
                requirement.requiredBuildingCodes.Add("resource_dropoff");
                requirement.cost.Add(new ProgressionResourceRequirement(EconomyResourceType.Food, 400));
                break;
            case BuildingEra.Castle:
                requirement.minimumPopulation = 12; requirement.landmarkCode = "town_center";
                requirement.requiredBuildingCodes.Add("barracks"); requirement.requiredTechnologyCodes.Add("wheelbarrow");
                requirement.cost.Add(new ProgressionResourceRequirement(EconomyResourceType.Food, 800));
                requirement.cost.Add(new ProgressionResourceRequirement(EconomyResourceType.Gold, 200));
                break;
            case BuildingEra.Imperial:
                requirement.minimumPopulation = 20; requirement.landmarkCode = "town_center";
                requirement.requiredBuildingCodes.Add("siege_workshop"); requirement.requiredTechnologyCodes.Add("masonry");
                requirement.cost.Add(new ProgressionResourceRequirement(EconomyResourceType.Food, 1200));
                requirement.cost.Add(new ProgressionResourceRequirement(EconomyResourceType.Gold, 500));
                break;
            default: throw new ArgumentOutOfRangeException(nameof(targetEra), "A canonical transition must target Feudal, Castle or Imperial.");
        }
        return requirement;
    }
}
