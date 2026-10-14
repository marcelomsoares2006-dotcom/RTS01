using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BarracksMilitaryContractTests
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
    public void BarracksRequiresOperationalTownCenterAndBarracks()
    {
        BuildingContentCatalog catalog = CreateCatalog(ReadyBarracks());
        StrategicPlanningState state = State();
        Assert.That(BarracksMilitaryContract.Evaluate(catalog, state), Is.EqualTo(BarracksContractResult.MissingTownCenter));
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational });
        Assert.That(BarracksMilitaryContract.Evaluate(catalog, state), Is.EqualTo(BarracksContractResult.BarracksNotOperational));
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "barracks", lifecycle = BuildingLifecycleState.Operational });
        Assert.That(BarracksMilitaryContract.Evaluate(catalog, state), Is.EqualTo(BarracksContractResult.Accepted));
    }

    [Test]
    public void InitialOrderPrefersSpearmanAndBuildsStableReplayCommand()
    {
        BuildingContentSpec barracks = ReadyBarracks();
        barracks.production.Insert(0, new BuildingContentProduction {
            productCode = "militia", productionTime = 10f, queueLimit = 5,
            cost = new List<BuildingContentCost> { new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 20 } }
        });
        BuildingContentCatalog catalog = CreateCatalog(barracks);
        StrategicPlanningState state = State();
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational });
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "barracks", lifecycle = BuildingLifecycleState.Operational });
        ConstructionReplayCommand command;
        BarracksContractResult result;
        Assert.That(BarracksMilitaryContract.TryCreateInitialProductionCommand(catalog, state, 12, 240, string.Empty, out command, out result), Is.True);
        Assert.That(result, Is.EqualTo(BarracksContractResult.Accepted));
        Assert.That(command.kind, Is.EqualTo(ConstructionReplayCommandKind.ProduceUnit));
        Assert.That(command.targetCode, Is.EqualTo("spearman"));
        Assert.That(command.commandId, Is.EqualTo("barracks-initial-0-12-spearman"));
        Assert.That(command.costs[0].resource, Is.EqualTo(EconomyResourceType.Food));
    }

    [Test]
    public void ReplayOfInitialOrderIsDeterministic()
    {
        BuildingContentCatalog catalog = CreateCatalog(ReadyBarracks());
        StrategicPlanningState state = State();
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational });
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "barracks", lifecycle = BuildingLifecycleState.Operational });
        ConstructionReplayCommand command;
        BarracksContractResult result;
        Assert.That(BarracksMilitaryContract.TryCreateInitialProductionCommand(catalog, state, 2, 40, string.Empty, out command, out result), Is.True);
        ConstructionReplayLog first = new ConstructionReplayLog();
        ConstructionReplayLog second = new ConstructionReplayLog();
        Assert.That(first.TryAppend(command), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(second.TryAppend(command), Is.EqualTo(ConstructionReplayResult.Accepted));
        ConstructionReplaySnapshot a;
        ConstructionReplaySnapshot b;
        Assert.That(ConstructionReplayExecutor.Replay(first, null, out a), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(ConstructionReplayExecutor.Replay(second, null, out b), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(a.fingerprint, Is.EqualTo(b.fingerprint));
        CollectionAssert.AreEqual(new[] { "spearman" }, a.producedUnits);
    }

    [Test]
    public void StrategicPlanningProjectsTheSameInitialUnit()
    {
        BuildingContentCatalog catalog = CreateCatalog(ReadyBarracks());
        StrategicPlanningState state = State();
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational });
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "barracks", lifecycle = BuildingLifecycleState.Operational });
        StrategicActionIntent intent;
        BarracksContractResult result;
        Assert.That(ConstructionPlanningRules.TryCreateInitialBarracksIntent(catalog, state, out intent, out result), Is.True);
        Assert.That(intent.action, Is.EqualTo(StrategicActionKind.ProduceUnit));
        Assert.That(intent.targetCode, Is.EqualTo("spearman"));
        Assert.That(intent.quantity, Is.EqualTo(1));
    }

    private BuildingContentCatalog CreateCatalog(BuildingContentSpec barracks)
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        catalog.entries.Add(barracks);
        return catalog;
    }

    private BuildingContentSpec ReadyBarracks()
    {
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        _created.Add(data);
        return new BuildingContentSpec {
            code = "barracks", role = BuildingContentRole.Production, status = BuildingContentStatus.Playable,
            buildingData = data, minimumEra = BuildingEra.Dark, supportsProduction = true,
            constructionCost = new List<BuildingContentCost> { new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 150 } },
            requirements = new List<BuildingRequirement> { new BuildingRequirement { buildingCode = "town_center" } },
            production = new List<BuildingContentProduction> { new BuildingContentProduction {
                productCode = "spearman", productionTime = 15f, queueLimit = 5, populationCost = 1,
                cost = new List<BuildingContentCost> {
                    new BuildingContentCost { resource = EconomyResourceType.Food, amount = 80 },
                    new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 30 }
                }
            } }
        };
    }

    private static StrategicPlanningState State()
    {
        return new StrategicPlanningState { ownerId = 0, era = BuildingEra.Dark, population = 3,
            buildings = new List<StrategicBuildingObservation>() };
    }
}
