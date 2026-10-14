using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BuildingProductionCommandRulesTests
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
    public void PlannedOrEraLockedBuildingsCannotReserveQueueState()
    {
        BuildingContentCatalog catalog = Catalog();
        catalog.entries.Add(Entry("barracks", BuildingEra.Dark, BuildingContentStatus.Planned));
        EconomyLedger stockpile = new EconomyLedger();
        stockpile.Set(EconomyResourceType.Wood, 100);
        PopulationLedger population = new PopulationLedger(10, 0);
        BuildingProductionQueue queue = new BuildingProductionQueue();
        BuildingProductionQueueResult queueResult;

        Assert.That(BuildingProductionCommandRules.TryEnqueue(catalog, State(BuildingEra.Dark), queue,
            "barracks", "spearman", stockpile, population, out queueResult),
            Is.EqualTo(BuildingProductionCommandResult.ContentUnavailable));
        Assert.That(queue.Count, Is.EqualTo(0));
        Assert.That(stockpile.Get(EconomyResourceType.Wood), Is.EqualTo(100));

        BuildingContentSpec stable = Entry("stable", BuildingEra.Feudal, BuildingContentStatus.Playable);
        stable.buildingData = ReadyData();
        catalog.entries.Add(stable);
        Assert.That(BuildingProductionCommandRules.TryEnqueue(catalog, State(BuildingEra.Dark), queue,
            "stable", "horseman", stockpile, population, out queueResult),
            Is.EqualTo(BuildingProductionCommandResult.EraLocked));
        Assert.That(queue.Count, Is.EqualTo(0));
    }

    [Test]
    public void CancelRefundsCostAndPopulationOnlyThroughValidOperationalState()
    {
        BuildingContentCatalog catalog = Catalog();
        BuildingContentSpec barracks = Entry("barracks", BuildingEra.Dark, BuildingContentStatus.Playable);
        barracks.buildingData = ReadyData();
        catalog.entries.Add(barracks);
        StrategicPlanningState state = State(BuildingEra.Dark);
        EconomyLedger stockpile = new EconomyLedger();
        stockpile.Set(EconomyResourceType.Wood, 100);
        PopulationLedger population = new PopulationLedger(10, 0);
        BuildingProductionQueue queue = new BuildingProductionQueue();
        BuildingProductionQueueResult queueResult;
        Assert.That(BuildingProductionCommandRules.TryEnqueue(catalog, state, queue,
            "barracks", "spearman", stockpile, population, out queueResult),
            Is.EqualTo(BuildingProductionCommandResult.Accepted));
        Assert.That(stockpile.Get(EconomyResourceType.Wood), Is.EqualTo(70));
        Assert.That(population.current, Is.EqualTo(1));

        BuildingProductionOrder cancelled;
        Assert.That(BuildingProductionCommandRules.TryCancel(catalog, state, queue,
            "barracks", 0, stockpile, population, out cancelled),
            Is.EqualTo(BuildingProductionCommandResult.Accepted));
        Assert.That(cancelled.productCode, Is.EqualTo("spearman"));
        Assert.That(stockpile.Get(EconomyResourceType.Wood), Is.EqualTo(100));
        Assert.That(population.current, Is.EqualTo(0));
        Assert.That(queue.Count, Is.EqualTo(0));

        state.era = BuildingEra.Feudal;
        Assert.That(BuildingProductionCommandRules.TryCancel(catalog, state, queue,
            "barracks", 0, stockpile, population, out cancelled),
            Is.EqualTo(BuildingProductionCommandResult.QueueRejected));
    }

    private BuildingContentCatalog Catalog()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        return catalog;
    }

    private BuildingContentSpec Entry(string code, BuildingEra era, BuildingContentStatus status)
    {
        return new BuildingContentSpec
        {
            code = code, minimumEra = era, status = status,
            supportsProduction = true,
            production = new List<BuildingContentProduction>
            {
                new BuildingContentProduction
                {
                    productCode = code == "stable" ? "horseman" : "spearman",
                    productionTime = 15f, queueLimit = 5, populationCost = 1,
                    cost = new List<BuildingContentCost>
                    {
                        new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 30 }
                    }
                }
            }
        };
    }

    private BuildingData ReadyData()
    {
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        _created.Add(data);
        return data;
    }

    private static StrategicPlanningState State(BuildingEra era)
    {
        return new StrategicPlanningState
        {
            ownerId = 0, era = era, population = 0,
            buildings = new List<StrategicBuildingObservation>()
        };
    }
}
