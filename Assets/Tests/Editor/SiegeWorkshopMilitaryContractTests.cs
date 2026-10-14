using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class SiegeWorkshopMilitaryContractTests
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
    public void SiegeWorkshopUsesCatalogCastleEraAndTownCenterGate()
    {
        BuildingContentCatalog catalog = Catalog(ReadyWorkshop(BuildingEra.Castle));
        StrategicPlanningState state = State(BuildingEra.Feudal);
        Assert.That(SiegeWorkshopMilitaryContract.Evaluate(catalog, state), Is.EqualTo(SiegeWorkshopContractResult.EraRequirement));
        state.era = BuildingEra.Castle;
        Assert.That(SiegeWorkshopMilitaryContract.Evaluate(catalog, state), Is.EqualTo(SiegeWorkshopContractResult.MissingTownCenter));
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational });
        Assert.That(SiegeWorkshopMilitaryContract.Evaluate(catalog, state), Is.EqualTo(SiegeWorkshopContractResult.SiegeWorkshopNotOperational));
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "siege_workshop", lifecycle = BuildingLifecycleState.Operational });
        Assert.That(SiegeWorkshopMilitaryContract.Evaluate(catalog, state), Is.EqualTo(SiegeWorkshopContractResult.Accepted));
    }

    [Test]
    public void InitialOrderPrefersBatteringRamAndReplaysDeterministically()
    {
        BuildingContentSpec workshop = ReadyWorkshop(BuildingEra.Feudal);
        workshop.production.Insert(0, new BuildingContentProduction { productCode = "scout", productionTime = 4f, queueLimit = 2,
            cost = new List<BuildingContentCost> { new BuildingContentCost { resource = EconomyResourceType.Food, amount = 20 } } });
        BuildingContentCatalog catalog = Catalog(workshop);
        StrategicPlanningState state = ReadyState(BuildingEra.Feudal);
        ConstructionReplayCommand command;
        SiegeWorkshopContractResult result;
        Assert.That(SiegeWorkshopMilitaryContract.TryCreateInitialProductionCommand(catalog, state, 12, 240, string.Empty, out command, out result), Is.True);
        Assert.That(command.targetCode, Is.EqualTo("battering_ram"));
        Assert.That(command.commandId, Is.EqualTo("siege-workshop-initial-0-12-battering_ram"));
        ConstructionReplayLog first = new ConstructionReplayLog();
        ConstructionReplayLog second = new ConstructionReplayLog();
        Assert.That(first.TryAppend(command), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(second.TryAppend(command), Is.EqualTo(ConstructionReplayResult.Accepted));
        ConstructionReplaySnapshot a; ConstructionReplaySnapshot b;
        Assert.That(ConstructionReplayExecutor.Replay(first, null, out a), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(ConstructionReplayExecutor.Replay(second, null, out b), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(a.fingerprint, Is.EqualTo(b.fingerprint));
        CollectionAssert.AreEqual(new[] { "battering_ram" }, a.producedUnits);
    }

    [Test]
    public void InvalidMinimumEraIsRejectedEvenWhenStateIsImperial()
    {
        BuildingContentCatalog catalog = Catalog(ReadyWorkshop(BuildingEra.Imperial));
        StrategicPlanningState state = ReadyState(BuildingEra.Imperial);
        Assert.That(SiegeWorkshopMilitaryContract.Evaluate(catalog, state), Is.EqualTo(SiegeWorkshopContractResult.InvalidConstructionContract));
    }

    [Test]
    public void QueueAdapterAcceptsTheSelectedSiegeProductAndCosts()
    {
        BuildingContentCatalog catalog = Catalog(ReadyWorkshop(BuildingEra.Feudal));
        StrategicPlanningState state = ReadyState(BuildingEra.Feudal);
        BuildingProductionEntry entry;
        BuildingProductionCatalogResult result;
        Assert.That(BuildingProductionCatalogRules.TryCreateLegacyQueueEntry(
            catalog, "siege_workshop", "battering_ram", state, out entry, out result), Is.True);
        Assert.That(result, Is.EqualTo(BuildingProductionCatalogResult.Available));
        Assert.That(entry.productionTime, Is.EqualTo(35f));
        Assert.That(entry.cost.Count, Is.EqualTo(2));
    }

    private BuildingContentCatalog Catalog(BuildingContentSpec spec)
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        catalog.entries.Add(spec); _created.Add(catalog); return catalog;
    }

    private BuildingContentSpec ReadyWorkshop(BuildingEra era)
    {
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>(); _created.Add(data);
        return new BuildingContentSpec { code = "siege_workshop", role = BuildingContentRole.Production,
            status = BuildingContentStatus.Playable, buildingData = data, minimumEra = era, supportsProduction = true,
            constructionCost = new List<BuildingContentCost> { new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 300 } },
            requirements = new List<BuildingRequirement> { new BuildingRequirement { buildingCode = "town_center" } },
            production = new List<BuildingContentProduction> { new BuildingContentProduction { productCode = "battering_ram", productionTime = 35f, queueLimit = 3, populationCost = 3,
                cost = new List<BuildingContentCost> { new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 160 }, new BuildingContentCost { resource = EconomyResourceType.Gold, amount = 40 } } } } };
    }

    private static StrategicPlanningState State(BuildingEra era)
    {
        return new StrategicPlanningState { ownerId = 0, era = era, population = 3, buildings = new List<StrategicBuildingObservation>() };
    }

    private static StrategicPlanningState ReadyState(BuildingEra era)
    {
        StrategicPlanningState state = State(era);
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational });
        state.buildings.Add(new StrategicBuildingObservation { ownerId = 0, code = "siege_workshop", lifecycle = BuildingLifecycleState.Operational });
        return state;
    }
}
