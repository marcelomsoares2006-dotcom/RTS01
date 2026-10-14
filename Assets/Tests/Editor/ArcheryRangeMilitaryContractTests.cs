using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class ArcheryRangeMilitaryContractTests
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
    public void ArcheryRangeRequiresFeudalFletchingTownCenterAndOperationalRange()
    {
        BuildingContentCatalog catalog = CreateCatalog(ReadyArcheryRange());
        StrategicPlanningState state = State();
        Assert.That(ArcheryRangeMilitaryContract.Evaluate(catalog, state), Is.EqualTo(ArcheryRangeContractResult.FeudalEraRequired));
        state.era = BuildingEra.Feudal;
        Assert.That(ArcheryRangeMilitaryContract.Evaluate(catalog, state), Is.EqualTo(ArcheryRangeContractResult.MissingFletching));
        state.unlockedTechnologyCodes.Add("fletching");
        Assert.That(ArcheryRangeMilitaryContract.Evaluate(catalog, state), Is.EqualTo(ArcheryRangeContractResult.MissingTownCenter));
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational });
        Assert.That(ArcheryRangeMilitaryContract.Evaluate(catalog, state), Is.EqualTo(ArcheryRangeContractResult.ArcheryRangeNotOperational));
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "archery_range", lifecycle = BuildingLifecycleState.Operational });
        Assert.That(ArcheryRangeMilitaryContract.Evaluate(catalog, state), Is.EqualTo(ArcheryRangeContractResult.Accepted));
    }

    [Test]
    public void InitialOrderPrefersArcherAndBuildsStableReplayCommand()
    {
        BuildingContentCatalog catalog = CreateCatalog(ReadyArcheryRange());
        StrategicPlanningState state = ReadyState();
        ConstructionReplayCommand command;
        ArcheryRangeContractResult result;
        Assert.That(ArcheryRangeMilitaryContract.TryCreateInitialProductionCommand(catalog, state, 12, 240, string.Empty, out command, out result), Is.True);
        Assert.That(result, Is.EqualTo(ArcheryRangeContractResult.Accepted));
        Assert.That(command.kind, Is.EqualTo(ConstructionReplayCommandKind.ProduceUnit));
        Assert.That(command.targetCode, Is.EqualTo("archer"));
        Assert.That(command.commandId, Is.EqualTo("archery-range-initial-0-12-archer"));
        Assert.That(command.costs[0].resource, Is.EqualTo(EconomyResourceType.Food));
        Assert.That(command.costs[1].resource, Is.EqualTo(EconomyResourceType.Wood));
    }

    [Test]
    public void ReplayAndStrategicPlanningProjectTheSameArcher()
    {
        BuildingContentCatalog catalog = CreateCatalog(ReadyArcheryRange());
        StrategicPlanningState state = ReadyState();
        ConstructionReplayCommand command;
        ArcheryRangeContractResult result;
        Assert.That(ArcheryRangeMilitaryContract.TryCreateInitialProductionCommand(catalog, state, 2, 40, string.Empty, out command, out result), Is.True);
        ConstructionReplayLog first = new ConstructionReplayLog();
        ConstructionReplayLog second = new ConstructionReplayLog();
        Assert.That(first.TryAppend(command), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(second.TryAppend(command), Is.EqualTo(ConstructionReplayResult.Accepted));
        ConstructionReplaySnapshot a;
        ConstructionReplaySnapshot b;
        Assert.That(ConstructionReplayExecutor.Replay(first, null, out a), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(ConstructionReplayExecutor.Replay(second, null, out b), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(a.fingerprint, Is.EqualTo(b.fingerprint));
        CollectionAssert.AreEqual(new[] { "archer" }, a.producedUnits);

        StrategicActionIntent intent;
        Assert.That(ConstructionPlanningRules.TryCreateInitialArcheryRangeIntent(catalog, state, out intent, out result), Is.True);
        Assert.That(intent.action, Is.EqualTo(StrategicActionKind.ProduceUnit));
        Assert.That(intent.targetCode, Is.EqualTo("archer"));
        Assert.That(intent.targetEntityId, Is.EqualTo("archery_range"));
    }

    [Test]
    public void ProductionCatalogAcceptsArcherAfterFeudalAndFletching()
    {
        BuildingContentCatalog catalog = CreateCatalog(ReadyArcheryRange());
        StrategicPlanningState state = ReadyState();
        BuildingProductionEntry entry;
        BuildingProductionCatalogResult result;
        Assert.That(BuildingProductionCatalogRules.TryCreateLegacyQueueEntry(catalog, "archery_range", "archer", state, out entry, out result), Is.True);
        Assert.That(result, Is.EqualTo(BuildingProductionCatalogResult.Available));
        Assert.That(entry.productCode, Is.EqualTo("archer"));
        Assert.That(entry.cost.Count, Is.EqualTo(2));
    }

    private BuildingContentCatalog CreateCatalog(BuildingContentSpec archeryRange)
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        catalog.entries.Add(archeryRange);
        return catalog;
    }

    private BuildingContentSpec ReadyArcheryRange()
    {
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        _created.Add(data);
        return new BuildingContentSpec {
            code = "archery_range", role = BuildingContentRole.Production, status = BuildingContentStatus.Playable,
            buildingData = data, minimumEra = BuildingEra.Feudal, supportsProduction = true,
            constructionCost = new List<BuildingContentCost> { new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 150 } },
            requirements = new List<BuildingRequirement> {
                new BuildingRequirement { technologyCode = "fletching" },
                new BuildingRequirement { buildingCode = "town_center" }
            },
            production = new List<BuildingContentProduction> { new BuildingContentProduction {
                productCode = "archer", productionTime = 15f, queueLimit = 5, populationCost = 1,
                cost = new List<BuildingContentCost> {
                    new BuildingContentCost { resource = EconomyResourceType.Food, amount = 30 },
                    new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 50 }
                }
            } }
        };
    }

    private static StrategicPlanningState State()
    {
        return new StrategicPlanningState { ownerId = 0, era = BuildingEra.Dark, population = 3,
            buildings = new List<StrategicBuildingObservation>() };
    }

    private static StrategicPlanningState ReadyState()
    {
        StrategicPlanningState state = State();
        state.era = BuildingEra.Feudal;
        state.unlockedTechnologyCodes.Add("fletching");
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational });
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "archery_range", lifecycle = BuildingLifecycleState.Operational });
        return state;
    }
}
