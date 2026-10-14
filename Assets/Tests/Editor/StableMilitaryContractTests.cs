using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class StableMilitaryContractTests
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
    public void StableRequiresFeudalRidingTownCenterAndOperationalStable()
    {
        BuildingContentCatalog catalog = CreateCatalog(ReadyStable());
        StrategicPlanningState state = State();
        Assert.That(StableMilitaryContract.Evaluate(catalog, state), Is.EqualTo(StableContractResult.FeudalEraRequired));
        state.era = BuildingEra.Feudal;
        Assert.That(StableMilitaryContract.Evaluate(catalog, state), Is.EqualTo(StableContractResult.MissingRiding));
        state.unlockedTechnologyCodes.Add("riding");
        Assert.That(StableMilitaryContract.Evaluate(catalog, state), Is.EqualTo(StableContractResult.MissingTownCenter));
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational });
        Assert.That(StableMilitaryContract.Evaluate(catalog, state), Is.EqualTo(StableContractResult.StableNotOperational));
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "stable", lifecycle = BuildingLifecycleState.Operational });
        Assert.That(StableMilitaryContract.Evaluate(catalog, state), Is.EqualTo(StableContractResult.Accepted));
    }

    [Test]
    public void InitialOrderPrefersHorsemanAndBuildsStableReplayCommand()
    {
        BuildingContentSpec stable = ReadyStable();
        stable.production.Insert(0, new BuildingContentProduction {
            productCode = "scout", productionTime = 10f, queueLimit = 5,
            cost = new List<BuildingContentCost> { new BuildingContentCost { resource = EconomyResourceType.Food, amount = 20 } }
        });
        BuildingContentCatalog catalog = CreateCatalog(stable);
        StrategicPlanningState state = ReadyState();
        ConstructionReplayCommand command;
        StableContractResult result;
        Assert.That(StableMilitaryContract.TryCreateInitialProductionCommand(catalog, state, 12, 240, string.Empty, out command, out result), Is.True);
        Assert.That(result, Is.EqualTo(StableContractResult.Accepted));
        Assert.That(command.kind, Is.EqualTo(ConstructionReplayCommandKind.ProduceUnit));
        Assert.That(command.targetCode, Is.EqualTo("horseman"));
        Assert.That(command.commandId, Is.EqualTo("stable-initial-0-12-horseman"));
        Assert.That(command.costs[0].resource, Is.EqualTo(EconomyResourceType.Food));
        Assert.That(command.costs[1].resource, Is.EqualTo(EconomyResourceType.Wood));
    }

    [Test]
    public void ReplayAndStrategicPlanningProjectTheSameMountedUnit()
    {
        BuildingContentCatalog catalog = CreateCatalog(ReadyStable());
        StrategicPlanningState state = ReadyState();
        ConstructionReplayCommand command;
        StableContractResult result;
        Assert.That(StableMilitaryContract.TryCreateInitialProductionCommand(catalog, state, 2, 40, string.Empty, out command, out result), Is.True);
        ConstructionReplayLog first = new ConstructionReplayLog();
        ConstructionReplayLog second = new ConstructionReplayLog();
        Assert.That(first.TryAppend(command), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(second.TryAppend(command), Is.EqualTo(ConstructionReplayResult.Accepted));
        ConstructionReplaySnapshot a;
        ConstructionReplaySnapshot b;
        Assert.That(ConstructionReplayExecutor.Replay(first, null, out a), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(ConstructionReplayExecutor.Replay(second, null, out b), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(a.fingerprint, Is.EqualTo(b.fingerprint));
        CollectionAssert.AreEqual(new[] { "horseman" }, a.producedUnits);

        StrategicActionIntent intent;
        Assert.That(ConstructionPlanningRules.TryCreateInitialStableIntent(catalog, state, out intent, out result), Is.True);
        Assert.That(intent.action, Is.EqualTo(StrategicActionKind.ProduceUnit));
        Assert.That(intent.targetCode, Is.EqualTo("horseman"));
        Assert.That(intent.targetEntityId, Is.EqualTo("stable"));
    }

    [Test]
    public void ProductionCatalogAcceptsHorsemanAfterFeudalAndRiding()
    {
        BuildingContentCatalog catalog = CreateCatalog(ReadyStable());
        StrategicPlanningState state = ReadyState();
        BuildingProductionEntry entry;
        BuildingProductionCatalogResult result;
        Assert.That(BuildingProductionCatalogRules.TryCreateLegacyQueueEntry(catalog, "stable", "horseman", state, out entry, out result), Is.True);
        Assert.That(result, Is.EqualTo(BuildingProductionCatalogResult.Available));
        Assert.That(entry.productCode, Is.EqualTo("horseman"));
        Assert.That(entry.cost.Count, Is.EqualTo(2));
    }

    private BuildingContentCatalog CreateCatalog(BuildingContentSpec stable)
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        catalog.entries.Add(stable);
        return catalog;
    }

    private BuildingContentSpec ReadyStable()
    {
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        _created.Add(data);
        return new BuildingContentSpec {
            code = "stable", role = BuildingContentRole.Production, status = BuildingContentStatus.Playable,
            buildingData = data, minimumEra = BuildingEra.Feudal, supportsProduction = true,
            constructionCost = new List<BuildingContentCost> { new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 150 } },
            requirements = new List<BuildingRequirement> {
                new BuildingRequirement { technologyCode = "riding" },
                new BuildingRequirement { buildingCode = "town_center" }
            },
            production = new List<BuildingContentProduction> { new BuildingContentProduction {
                productCode = "horseman", productionTime = 23f, queueLimit = 5, populationCost = 1,
                cost = new List<BuildingContentCost> {
                    new BuildingContentCost { resource = EconomyResourceType.Food, amount = 100 },
                    new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 20 }
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
        state.unlockedTechnologyCodes.Add("riding");
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational });
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "stable", lifecycle = BuildingLifecycleState.Operational });
        return state;
    }
}
