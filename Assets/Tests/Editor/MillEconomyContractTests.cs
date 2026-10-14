using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class MillEconomyContractTests
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
    public void MillRequiresOperationalTownCenterAndKeepsDarkAgeWoodCost()
    {
        BuildingContentCatalog catalog = Catalog();
        StrategicPlanningState state = State();
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 0, code = "mill", lifecycle = BuildingLifecycleState.Operational
        });

        Assert.That(MillEconomyContract.Evaluate(catalog, state),
            Is.EqualTo(MillContractResult.MissingTownCenter));

        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.UnderConstruction
        });
        Assert.That(MillEconomyContract.Evaluate(catalog, state),
            Is.EqualTo(MillContractResult.MissingTownCenter));

        state.buildings[1].lifecycle = BuildingLifecycleState.Operational;
        Assert.That(MillEconomyContract.Evaluate(catalog, state),
            Is.EqualTo(MillContractResult.Accepted));
    }

    [Test]
    public void MillRejectsNonDarkAgeOrNonFoodCatalogContract()
    {
        BuildingContentCatalog catalog = Catalog();
        StrategicPlanningState state = State();
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational
        });
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 0, code = "mill", lifecycle = BuildingLifecycleState.Operational
        });

        catalog.Find("mill").minimumEra = BuildingEra.Feudal;
        Assert.That(MillEconomyContract.Evaluate(catalog, state),
            Is.EqualTo(MillContractResult.InvalidConstructionContract));
        catalog.Find("mill").minimumEra = BuildingEra.Dark;
        catalog.Find("mill").dropoffResources.Add(EconomyResourceType.Wood);
        Assert.That(MillEconomyContract.Evaluate(catalog, state),
            Is.EqualTo(MillContractResult.UnsupportedResource));
    }

    [Test]
    public void MillRoutesFoodOnlyAndNeverMutatesLegacyResources()
    {
        BuildingContentCatalog catalog = Catalog();
        StrategicPlanningState state = State();
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational
        });
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 0, code = "mill", lifecycle = BuildingLifecycleState.Operational
        });

        MillEconomyContract contract;
        MillContractResult contractResult;
        Assert.IsTrue(MillEconomyContract.TryCreate(catalog, state, 0,
            Vector3.zero, 3f, out contract, out contractResult));

        EconomyLedger ledger = new EconomyLedger();
        EconomyCollector food = new EconomyCollector(
            "food-worker", 0, EconomyResourceType.Food, 1f, 10f);
        food.SetCarriedAmount(4.75f);
        ResourceDropoffResult depositResult;
        Assert.IsTrue(contract.TryDeposit(food, new Vector3(2f, 20f, 0f),
            ledger, out depositResult));
        Assert.That(depositResult, Is.EqualTo(ResourceDropoffResult.Accepted));
        Assert.That(ledger.Get(EconomyResourceType.Food), Is.EqualTo(4));
        Assert.That(food.CarriedAmount, Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(ledger.Get(EconomyResourceType.Wood), Is.EqualTo(0));
        Assert.That(EconomyResourceType.Food.TryGetLegacyResource(out _), Is.False);

        EconomyCollector wood = new EconomyCollector(
            "wood-worker", 0, EconomyResourceType.Wood, 1f, 10f);
        wood.SetCarriedAmount(2f);
        Assert.IsFalse(contract.TryDeposit(wood, Vector3.zero, ledger, out depositResult));
        Assert.That(depositResult, Is.EqualTo(ResourceDropoffResult.UnsupportedResource));
        Assert.That(ledger.Get(EconomyResourceType.Food), Is.EqualTo(4));
    }

    [Test]
    public void PlannerOffersMillAfterOperationalTownCenterAndFunds()
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
        Assert.That(intent.targetCode, Is.EqualTo("mill"));
        Assert.That(intent.priority, Is.EqualTo(StrategicPriority.High));
    }

    private BuildingContentCatalog Catalog()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);

        BuildingData millData = ScriptableObject.CreateInstance<BuildingData>();
        millData.code = "mill";
        _created.Add(millData);
        catalog.entries.Add(new BuildingContentSpec
        {
            code = "mill", displayName = "Mill",
            role = BuildingContentRole.ResourceDropoff,
            status = BuildingContentStatus.Playable, minimumEra = BuildingEra.Dark,
            buildingData = millData,
            constructionCost = new List<BuildingContentCost>
            {
                new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 50 }
            },
            requirements = new List<BuildingRequirement>
            {
                new BuildingRequirement { buildingCode = "town_center" }
            },
            dropoffResources = new List<EconomyResourceType> { EconomyResourceType.Food },
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
            ownerId = 0,
            era = BuildingEra.Dark,
            buildings = new List<StrategicBuildingObservation>()
        };
    }
}
