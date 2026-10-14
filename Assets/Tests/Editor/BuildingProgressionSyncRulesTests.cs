using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BuildingProgressionSyncRulesTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown() { foreach (Object asset in _created) if (asset != null) Object.DestroyImmediate(asset); }

    [Test]
    public void RoundTripProgressionSnapshotSynchronizesAndTechnologyDriftIsRejected()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        _created.Add(data);
        catalog.entries.Add(new BuildingContentSpec { code = "barracks", status = BuildingContentStatus.Playable, buildingData = data });
        StrategicPlanningState state = new StrategicPlanningState { ownerId = 0, era = BuildingEra.Feudal,
            unlockedTechnologyCodes = new List<string> { "fletching" } };
        BuildingProductionQueueSnapshot snapshot = new BuildingProductionQueueSnapshot
        {
            buildingCode = "barracks", ownerId = 0, era = BuildingEra.Feudal,
            assetOrigin = BuildingContentAssetStage.RuntimeReady,
            unlockedTechnologyCodes = new List<string> { "fletching" }
        };

        Assert.That(BuildingProgressionSyncRules.Validate(catalog, state, snapshot),
            Is.EqualTo(BuildingProgressionSyncResult.Synchronized));
        state.unlockedTechnologyCodes.Add("riding");
        Assert.That(BuildingProgressionSyncRules.Validate(catalog, state, snapshot),
            Is.EqualTo(BuildingProgressionSyncResult.StateMismatch));
    }

    [Test]
    public void PlannedRuntimeDriftIsRejectedWithoutMutatingPlanningState()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        catalog.entries.Add(new BuildingContentSpec { code = "barracks", status = BuildingContentStatus.Planned });
        StrategicPlanningState state = new StrategicPlanningState { ownerId = 0, era = BuildingEra.Dark,
            unlockedTechnologyCodes = new List<string>() };
        BuildingProductionQueueSnapshot snapshot = new BuildingProductionQueueSnapshot
        {
            buildingCode = "barracks", ownerId = 0, era = BuildingEra.Dark,
            assetOrigin = BuildingContentAssetStage.RuntimeReady
        };
        int originalPopulation = state.population;
        Assert.That(BuildingProgressionSyncRules.Validate(catalog, state, snapshot),
            Is.EqualTo(BuildingProgressionSyncResult.StateMismatch));
        Assert.That(state.population, Is.EqualTo(originalPopulation));
    }

    [Test]
    public void RestoreStateMismatchLeavesQueueAndPlanningResourcesUntouched()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        _created.Add(data);
        catalog.entries.Add(new BuildingContentSpec { code = "barracks", status = BuildingContentStatus.Playable, buildingData = data });
        StrategicPlanningState state = new StrategicPlanningState { ownerId = 0, era = BuildingEra.Feudal,
            unlockedTechnologyCodes = new List<string> { "fletching" }, resources = new List<StrategicResourceSnapshot>
            { new StrategicResourceSnapshot(InGameResource.Wood, 100, 20) } };
        BuildingProductionQueueSnapshot snapshot = new BuildingProductionQueueSnapshot
        {
            buildingCode = "barracks", ownerId = 0, era = BuildingEra.Dark,
            assetOrigin = BuildingContentAssetStage.RuntimeReady,
            unlockedTechnologyCodes = new List<string> { "fletching" }
        };
        BuildingProductionQueue queue = new BuildingProductionQueue();
        Assert.That(BuildingProductionSaveRules.Restore(catalog, state, queue, snapshot),
            Is.EqualTo(BuildingProductionSnapshotResult.StateMismatch));
        Assert.That(queue.Count, Is.EqualTo(0));
        Assert.That(state.GetAmount(InGameResource.Wood), Is.EqualTo(100));
        Assert.That(state.GetAvailableAmount(InGameResource.Wood), Is.EqualTo(80));
    }
}
