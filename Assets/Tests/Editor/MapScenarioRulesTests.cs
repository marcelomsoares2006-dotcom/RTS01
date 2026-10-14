using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public sealed class MapScenarioRulesTests
{
    [Test] public void ValidScenarioRequiresEveryStrategicResourcePerPlayer()
    {
        MapData map = CreateMap();
        Assert.That(MapScenarioRules.Validate(map, out _), Is.EqualTo(MapScenarioValidationResult.Valid));
        map.resourceNodes.RemoveAt(map.resourceNodes.Count - 1);
        Assert.That(MapScenarioRules.Validate(map, out _), Is.EqualTo(MapScenarioValidationResult.InsufficientResourceCoverage));
        Object.DestroyImmediate(map);
    }

    [Test] public void InvalidStartAndNodeIdsAreRejected()
    {
        MapData map = CreateMap();
        map.playerStarts[1].playerId = map.playerStarts[0].playerId;
        Assert.That(MapScenarioRules.Validate(map, out _), Is.EqualTo(MapScenarioValidationResult.DuplicatePlayerStart));
        map.playerStarts[1].playerId = 1;
        map.resourceNodes[0].nodeId = map.resourceNodes[1].nodeId;
        Assert.That(MapScenarioRules.Validate(map, out _), Is.EqualTo(MapScenarioValidationResult.DuplicateResourceNode));
        Object.DestroyImmediate(map);
    }

    [Test] public void CanonicalEraCatalogAdvancesSequentially()
    {
        Assert.That(EraProgressionCatalog.For(BuildingEra.Feudal).targetEra, Is.EqualTo(BuildingEra.Feudal));
        Assert.That(EraProgressionCatalog.For(BuildingEra.Castle).requiredTechnologyCodes, Does.Contain("wheelbarrow"));
        Assert.That(EraProgressionCatalog.For(BuildingEra.Imperial).requiredBuildingCodes, Does.Contain("siege_workshop"));
    }

    private static MapData CreateMap()
    {
        MapData map = ScriptableObject.CreateInstance<MapData>(); map.mapName = "Test"; map.sceneName = "Test"; map.mapSize = 100; map.maxPlayers = 2;
        map.playerStarts = new List<MapPlayerStart> { new MapPlayerStart { playerId = 0, position = new Vector3(10, 0, 10) }, new MapPlayerStart { playerId = 1, position = new Vector3(90, 0, 90) } };
        map.resourceNodes = new List<MapResourceNode> {
            new MapResourceNode { nodeId = "wood-0", resource = InGameResource.Wood, position = new Vector3(20,0,20) },
            new MapResourceNode { nodeId = "wood-1", resource = InGameResource.Wood, position = new Vector3(80,0,80) },
            new MapResourceNode { nodeId = "gold-0", resource = InGameResource.Gold, position = new Vector3(20,0,30) },
            new MapResourceNode { nodeId = "gold-1", resource = InGameResource.Gold, position = new Vector3(80,0,70) },
            new MapResourceNode { nodeId = "stone-0", resource = InGameResource.Stone, position = new Vector3(30,0,20) },
            new MapResourceNode { nodeId = "stone-1", resource = InGameResource.Stone, position = new Vector3(70,0,80) }
        };
        map.startingResources = new List<MapStartingResource> { new MapStartingResource { resource = InGameResource.Wood, amount = 200 } };
        return map;
    }
}
