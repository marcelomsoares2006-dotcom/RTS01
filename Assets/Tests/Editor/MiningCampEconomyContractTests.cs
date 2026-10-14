using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class MiningCampEconomyContractTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object value in _created)
            if (value != null) Object.DestroyImmediate(value);
        _created.Clear();
    }

    [Test]
    public void MiningCampRequiresOperationalTownCenterAndKeepsDarkAgeWoodCost()
    {
        BuildingContentCatalog catalog = Catalog();
        StrategicPlanningState state = State();
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 0, code = "mining_camp", lifecycle = BuildingLifecycleState.Operational
        });

        Assert.That(MiningCampEconomyContract.Evaluate(catalog, state),
            Is.EqualTo(MiningCampContractResult.MissingTownCenter));

        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.UnderConstruction
        });
        Assert.That(MiningCampEconomyContract.Evaluate(catalog, state),
            Is.EqualTo(MiningCampContractResult.MissingTownCenter));

        state.buildings[1].lifecycle = BuildingLifecycleState.Operational;
        Assert.That(MiningCampEconomyContract.Evaluate(catalog, state),
            Is.EqualTo(MiningCampContractResult.Accepted));
    }

    [Test]
    public void MiningCampRejectsNonDarkAgeOrMissingTownCenterCatalogContract()
    {
        BuildingContentCatalog catalog = Catalog();
        StrategicPlanningState state = State();
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational
        });
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 0, code = "mining_camp", lifecycle = BuildingLifecycleState.Operational
        });

        catalog.Find("mining_camp").minimumEra = BuildingEra.Feudal;
        Assert.That(MiningCampEconomyContract.Evaluate(catalog, state),
            Is.EqualTo(MiningCampContractResult.InvalidConstructionContract));
        catalog.Find("mining_camp").minimumEra = BuildingEra.Dark;
        catalog.Find("mining_camp").requirements.Clear();
        Assert.That(MiningCampEconomyContract.Evaluate(catalog, state),
            Is.EqualTo(MiningCampContractResult.InvalidConstructionContract));
    }

    [Test]
    public void MiningCampRoutesGoldAndStoneOnlyAndPreservesFractionalCarry()
    {
        BuildingContentCatalog catalog = Catalog();
        StrategicPlanningState state = State();
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational
        });
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 0, code = "mining_camp", lifecycle = BuildingLifecycleState.Operational
        });

        MiningCampEconomyContract contract;
        MiningCampContractResult contractResult;
        Assert.IsTrue(MiningCampEconomyContract.TryCreate(catalog, state, 0,
            Vector3.zero, 3f, out contract, out contractResult));

        EconomyLedger ledger = new EconomyLedger();
        EconomyCollector gold = new EconomyCollector(
            "gold-worker", 0, EconomyResourceType.Gold, 1f, 10f);
        gold.SetCarriedAmount(4.75f);
        ResourceDropoffResult depositResult;
        Assert.IsTrue(contract.TryDeposit(gold, new Vector3(2f, 20f, 0f),
            ledger, out depositResult));
        Assert.That(depositResult, Is.EqualTo(ResourceDropoffResult.Accepted));
        Assert.That(ledger.Get(EconomyResourceType.Gold), Is.EqualTo(4));
        Assert.That(gold.CarriedAmount, Is.EqualTo(0.75f).Within(0.0001f));

        EconomyCollector stone = new EconomyCollector(
            "stone-worker", 0, EconomyResourceType.Stone, 1f, 10f);
        stone.SetCarriedAmount(2f);
        Assert.IsTrue(contract.TryDeposit(stone, Vector3.zero, ledger, out depositResult));
        Assert.That(ledger.Get(EconomyResourceType.Stone), Is.EqualTo(2));

        EconomyCollector wood = new EconomyCollector(
            "wood-worker", 0, EconomyResourceType.Wood, 1f, 10f);
        wood.SetCarriedAmount(2f);
        Assert.IsFalse(contract.TryDeposit(wood, Vector3.zero, ledger, out depositResult));
        Assert.That(depositResult, Is.EqualTo(ResourceDropoffResult.UnsupportedResource));
        Assert.That(ledger.Get(EconomyResourceType.Wood), Is.EqualTo(0));
    }

    [Test]
    public void PlannerOffersMiningCampOnlyAfterOperationalTownCenterAndFunds()
    {
        BuildingContentCatalog catalog = Catalog();
        StrategicPlanningState state = State();
        state.SetAmount(InGameResource.Wood, 50);
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.UnderConstruction
        });

        StrategicActionIntent intent;
        string rationale;
        Assert.IsFalse(ConstructionPlanningRules.TryCreateIntent(
            state, catalog, out intent, out rationale));
        Assert.IsNull(intent);

        state.buildings[0].lifecycle = BuildingLifecycleState.Operational;
        Assert.IsTrue(ConstructionPlanningRules.TryCreateIntent(
            state, catalog, out intent, out rationale));
        Assert.That(intent.targetCode, Is.EqualTo("mining_camp"));
        Assert.That(intent.priority, Is.EqualTo(StrategicPriority.High));
    }

    private BuildingContentCatalog Catalog()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        BuildingData miningData = ScriptableObject.CreateInstance<BuildingData>();
        miningData.code = "mining_camp";
        _created.Add(miningData);
        catalog.entries.Add(new BuildingContentSpec
        {
            code = "mining_camp", displayName = "Mining Camp",
            role = BuildingContentRole.ResourceDropoff,
            status = BuildingContentStatus.Playable, minimumEra = BuildingEra.Dark,
            buildingData = miningData,
            constructionCost = new List<BuildingContentCost>
            {
                new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 50 }
            },
            requirements = new List<BuildingRequirement>
            {
                new BuildingRequirement { buildingCode = "town_center" }
            },
            dropoffResources = new List<EconomyResourceType>
            {
                EconomyResourceType.Gold, EconomyResourceType.Stone
            },
            supportsDropoff = true
        });

        BuildingData townCenterData = ScriptableObject.CreateInstance<BuildingData>();
        townCenterData.code = "town_center";
        _created.Add(townCenterData);
        catalog.entries.Add(new BuildingContentSpec
        {
            code = "town_center", displayName = "Town Center",
            role = BuildingContentRole.TownCenter,
            status = BuildingContentStatus.Playable, minimumEra = BuildingEra.Dark,
            buildingData = townCenterData,
            constructionCost = new List<BuildingContentCost>
            {
                new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 400 }
            }
        });
        return catalog;
    }

    private static StrategicPlanningState State()
    {
        return new StrategicPlanningState
        {
            ownerId = 0, era = BuildingEra.Dark,
            buildings = new List<StrategicBuildingObservation>()
        };
    }
}
