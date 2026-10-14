using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BuildingProductionSaveContractsTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object asset in _created)
            if (asset != null) Object.DestroyImmediate(asset);
        _created.Clear();
    }

    [Test]
    public void QueueSnapshotPreservesMilitaryOrderProgressCostAndPopulation()
    {
        BuildingContentCatalog catalog = Catalog();
        BuildingContentSpec barracks = ReadyBarracks();
        catalog.entries.Add(barracks);
        StrategicPlanningState state = State();
        BuildingProductionQueue queue = new BuildingProductionQueue(5);
        BuildingProductionEntry entry = new BuildingProductionEntry
        {
            productCode = "spearman", productionTime = 15f, queueLimit = 5,
            populationCost = 1,
            cost = new List<ResourceValue> { new ResourceValue(InGameResource.Wood, 30) }
        };
        EconomyLedger ledger = new EconomyLedger();
        ledger.Set(EconomyResourceType.Wood, 100);
        PopulationLedger population = new PopulationLedger(10, 0);
        Assert.That(queue.TryEnqueue(entry, ledger, population), Is.EqualTo(BuildingProductionQueueResult.Accepted));
        queue.Advance(4f);

        BuildingProductionQueueSnapshot snapshot;
        Assert.That(BuildingProductionSaveRules.Capture(catalog, state, queue, "barracks", out snapshot),
            Is.EqualTo(BuildingProductionSnapshotResult.Captured));
        Assert.That(snapshot.orders[0].elapsed, Is.EqualTo(4f).Within(0.001f));
        Assert.That(snapshot.orders[0].cost[0].amount, Is.EqualTo(30));
        Assert.That(snapshot.orders[0].populationCost, Is.EqualTo(1));

        BuildingProductionQueue restored = new BuildingProductionQueue(5);
        Assert.That(BuildingProductionSaveRules.Restore(catalog, state, restored, snapshot),
            Is.EqualTo(BuildingProductionSnapshotResult.Restored));
        Assert.That(restored.Peek.productCode, Is.EqualTo("spearman"));
        Assert.That(restored.Peek.elapsed, Is.EqualTo(4f).Within(0.001f));
        Assert.That(restored.Peek.cost[0].amount, Is.EqualTo(30));
    }

    [Test]
    public void PlannedBarracksCannotCaptureOrRestoreRuntimeQueue()
    {
        BuildingContentCatalog catalog = Catalog();
        catalog.entries.Add(new BuildingContentSpec { code = "barracks", status = BuildingContentStatus.Planned });
        BuildingProductionQueueSnapshot snapshot;
        Assert.That(BuildingProductionSaveRules.Capture(catalog, State(),
            new BuildingProductionQueue(), "barracks", out snapshot),
            Is.EqualTo(BuildingProductionSnapshotResult.ContentUnavailable));
    }

    private BuildingContentCatalog Catalog()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        return catalog;
    }

    private BuildingContentSpec ReadyBarracks()
    {
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        _created.Add(data);
        return new BuildingContentSpec
        {
            code = "barracks", status = BuildingContentStatus.Playable,
            buildingData = data, minimumEra = BuildingEra.Dark,
            supportsProduction = true,
            production = new List<BuildingContentProduction>
            {
                new BuildingContentProduction
                {
                    productCode = "spearman", productionTime = 15f,
                    queueLimit = 5, populationCost = 1,
                    cost = new List<BuildingContentCost>
                    {
                        new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 30 }
                    }
                }
            }
        };
    }

    private static StrategicPlanningState State()
    {
        return new StrategicPlanningState
        {
            ownerId = 0, era = BuildingEra.Dark, population = 0,
            buildings = new List<StrategicBuildingObservation>()
        };
    }
}
