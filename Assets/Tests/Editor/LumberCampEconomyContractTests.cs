using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class LumberCampEconomyContractTests
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
    public void LumberCampRequiresOperationalTownCenterBeforeRoutingWood()
    {
        BuildingContentCatalog catalog = Catalog();
        StrategicPlanningState state = State();
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 0, code = "lumber_camp", lifecycle = BuildingLifecycleState.Operational
        });

        Assert.That(LumberCampEconomyContract.Evaluate(catalog, state),
            Is.EqualTo(LumberCampContractResult.MissingTownCenter));

        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.UnderConstruction
        });
        Assert.That(LumberCampEconomyContract.Evaluate(catalog, state),
            Is.EqualTo(LumberCampContractResult.MissingTownCenter));

        state.buildings[1].lifecycle = BuildingLifecycleState.Operational;
        Assert.That(LumberCampEconomyContract.Evaluate(catalog, state),
            Is.EqualTo(LumberCampContractResult.Accepted));
    }

    [Test]
    public void LumberCampCollectorRoutesOnlyWoodAndPreservesFractionalCarry()
    {
        BuildingContentCatalog catalog = Catalog();
        StrategicPlanningState state = State();
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 0, code = "town_center", lifecycle = BuildingLifecycleState.Operational
        });
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = 0, code = "lumber_camp", lifecycle = BuildingLifecycleState.Operational
        });

        LumberCampEconomyContract contract;
        LumberCampContractResult contractResult;
        Assert.IsTrue(LumberCampEconomyContract.TryCreate(catalog, state, 0,
            Vector3.zero, 3f, out contract, out contractResult));

        EconomyCollector collector = new EconomyCollector(
            "wood-worker", 0, EconomyResourceType.Wood, 1f, 10f);
        collector.SetCarriedAmount(4.75f);
        EconomyLedger ledger = new EconomyLedger();
        ResourceDropoffResult depositResult;
        Assert.IsTrue(contract.TryDeposit(collector, new Vector3(2f, 20f, 0f),
            ledger, out depositResult));
        Assert.That(depositResult, Is.EqualTo(ResourceDropoffResult.Accepted));
        Assert.That(ledger.Get(EconomyResourceType.Wood), Is.EqualTo(4));
        Assert.That(collector.CarriedAmount, Is.EqualTo(0.75f).Within(0.0001f));

        collector.resource = EconomyResourceType.Gold;
        collector.SetCarriedAmount(2f);
        Assert.IsFalse(contract.TryDeposit(collector, Vector3.zero, ledger, out depositResult));
        Assert.That(depositResult, Is.EqualTo(ResourceDropoffResult.UnsupportedResource));
        Assert.That(ledger.Get(EconomyResourceType.Gold), Is.EqualTo(0));
    }

    [Test]
    public void PlannerDoesNotOfferLumberCampBeforeOperationalTownCenter()
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
    }

    private BuildingContentCatalog Catalog()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);

        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        data.code = "lumber_camp";
        _created.Add(data);
        catalog.entries.Add(new BuildingContentSpec
        {
            code = "lumber_camp",
            displayName = "Lumber Camp",
            role = BuildingContentRole.ResourceDropoff,
            status = BuildingContentStatus.Playable,
            buildingData = data,
            constructionCost = new List<BuildingContentCost>
            {
                new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 50 }
            },
            requirements = new List<BuildingRequirement>
            {
                new BuildingRequirement { buildingCode = "town_center" }
            },
            dropoffResources = new List<EconomyResourceType> { EconomyResourceType.Wood },
            supportsDropoff = true
        });

        BuildingData townCenterData = ScriptableObject.CreateInstance<BuildingData>();
        townCenterData.code = "town_center";
        _created.Add(townCenterData);
        catalog.entries.Add(new BuildingContentSpec
        {
            code = "town_center",
            displayName = "Town Center",
            role = BuildingContentRole.TownCenter,
            status = BuildingContentStatus.Playable,
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
