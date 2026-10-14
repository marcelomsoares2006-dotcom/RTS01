using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class DataHandlerProductionQueueTests
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
    public void GamePlayerUnitDataCapturesAndRestoresQueueSnapshot()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        _created.Add(data);
        catalog.entries.Add(new BuildingContentSpec
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
        });
        StrategicPlanningState state = new StrategicPlanningState
        {
            ownerId = 0, era = BuildingEra.Dark, population = 0,
            buildings = new List<StrategicBuildingObservation>()
        };
        EconomyLedger ledger = new EconomyLedger();
        ledger.Set(EconomyResourceType.Wood, 100);
        PopulationLedger population = new PopulationLedger(10, 0);
        BuildingProductionQueue queue = new BuildingProductionQueue();
        Assert.That(queue.TryEnqueue(new BuildingProductionEntry
        {
            productCode = "spearman", productionTime = 15f, queueLimit = 5,
            populationCost = 1,
            cost = new List<ResourceValue> { new ResourceValue(InGameResource.Wood, 30) }
        }, ledger, population), Is.EqualTo(BuildingProductionQueueResult.Accepted));

        GamePlayerUnitData save = new GamePlayerUnitData { code = "barracks" };
        Assert.That(DataHandler.CaptureProductionQueue(save, catalog, state, queue),
            Is.EqualTo(BuildingProductionSnapshotResult.Captured));
        Assert.That(save.productionQueue, Is.Not.Null);

        BuildingProductionQueue restored = new BuildingProductionQueue();
        Assert.That(DataHandler.RestoreProductionQueue(save, catalog, state, restored),
            Is.EqualTo(BuildingProductionSnapshotResult.Restored));
        Assert.That(restored.Count, Is.EqualTo(1));
        Assert.That(restored.Peek.productCode, Is.EqualTo("spearman"));
    }

    [Test]
    public void DataHandlerRejectsTamperedSavedCostBeforeMutatingQueue()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        _created.Add(data);
        catalog.entries.Add(new BuildingContentSpec
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
        });
        GamePlayerUnitData save = new GamePlayerUnitData
        {
            code = "barracks",
            productionQueue = new BuildingProductionQueueSnapshot
            {
                buildingCode = "barracks", ownerId = 0, era = BuildingEra.Dark,
                orders = new List<BuildingProductionOrderSnapshot>
                {
                    new BuildingProductionOrderSnapshot
                    {
                        productCode = "spearman", duration = 15f,
                        elapsed = 2f, populationCost = 99,
                        cost = new List<ResourceValue>
                        {
                            new ResourceValue(InGameResource.Wood, 999)
                        }
                    }
                }
            }
        };
        StrategicPlanningState state = new StrategicPlanningState
        {
            ownerId = 0, era = BuildingEra.Dark, population = 0,
            buildings = new List<StrategicBuildingObservation>()
        };
        BuildingProductionQueue restored = new BuildingProductionQueue();
        Assert.That(DataHandler.RestoreProductionQueue(save, catalog, state, restored),
            Is.EqualTo(BuildingProductionSnapshotResult.InvalidSnapshot));
        Assert.That(restored.Count, Is.EqualTo(0));
    }
}
