using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BuildingProductionCatalogRulesTests
{
    private const string CatalogPath = "Assets/Resources/ScriptableObjects/Units/Building Content Catalog.asset";
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object asset in _created)
            if (asset != null) Object.DestroyImmediate(asset);
        _created.Clear();
    }

    [Test]
    public void PlannedBarracksIsBlockedBeforeQueueProjection()
    {
        BuildingContentCatalog catalog = CreateCatalog();
        catalog.entries.Add(Entry("barracks", BuildingEra.Dark, "spearman", EconomyResourceType.Food));
        BuildingProductionEntry queueEntry;
        BuildingProductionCatalogResult result;

        Assert.That(BuildingProductionCatalogRules.TryCreateLegacyQueueEntry(
            catalog, "barracks", "spearman", State(BuildingEra.Dark),
            out queueEntry, out result), Is.False);
        Assert.That(result, Is.EqualTo(BuildingProductionCatalogResult.ContentUnavailable));
    }

    [Test]
    public void MilitaryFoodCostIsReportedInsteadOfCoercedToLegacyResource()
    {
        BuildingContentCatalog catalog = CreateCatalog();
        BuildingContentSpec barracks = Entry("barracks", BuildingEra.Dark, "spearman", EconomyResourceType.Food);
        barracks.status = BuildingContentStatus.Playable;
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        _created.Add(data);
        barracks.buildingData = data;
        catalog.entries.Add(barracks);

        BuildingProductionCatalogResult result = BuildingProductionCatalogRules.Evaluate(
            catalog, "barracks", "spearman", State(BuildingEra.Dark), false);
        Assert.That(result, Is.EqualTo(BuildingProductionCatalogResult.UnsupportedEconomyResource));
    }

    [Test]
    public void LegacyCompatibleProductionProjectsCostTimeQueueAndPopulation()
    {
        BuildingContentCatalog catalog = CreateCatalog();
        BuildingContentSpec barracks = Entry("barracks", BuildingEra.Dark, "spearman", EconomyResourceType.Wood);
        barracks.status = BuildingContentStatus.Playable;
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        _created.Add(data);
        barracks.buildingData = data;
        catalog.entries.Add(barracks);

        BuildingProductionEntry queueEntry;
        BuildingProductionCatalogResult result;
        Assert.That(BuildingProductionCatalogRules.TryCreateLegacyQueueEntry(
            catalog, "barracks", "spearman", State(BuildingEra.Dark),
            out queueEntry, out result), Is.True);
        Assert.That(queueEntry.productCode, Is.EqualTo("spearman"));
        Assert.That(queueEntry.productionTime, Is.EqualTo(15f));
        Assert.That(queueEntry.queueLimit, Is.EqualTo(5));
        Assert.That(queueEntry.populationCost, Is.EqualTo(1));
        Assert.That(queueEntry.cost[0].code, Is.EqualTo(InGameResource.Wood));
    }

    [Test]
    public void PlannedMilitaryRosterStaysBlockedAndExposesFoodCompatibilityGap()
    {
        BuildingContentCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildingContentCatalog>(CatalogPath);
        StrategicPlanningState state = State(BuildingEra.Feudal);
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = state.ownerId, code = "town_center",
            lifecycle = BuildingLifecycleState.Operational
        });

        foreach (string buildingCode in new[] { "barracks", "archery_range", "stable" })
        {
            BuildingProductionCatalogResult blocked = BuildingProductionCatalogRules.Evaluate(
                catalog, buildingCode,
                buildingCode == "barracks" ? "spearman" : buildingCode == "stable" ? "horseman" : "archer",
                state);
            Assert.That(blocked, Is.EqualTo(BuildingProductionCatalogResult.ContentUnavailable), buildingCode);

            BuildingProductionCatalogResult compatibility = BuildingProductionCatalogRules.Evaluate(
                catalog, buildingCode,
                buildingCode == "barracks" ? "spearman" : buildingCode == "stable" ? "horseman" : "archer",
                state, false);
            Assert.That(compatibility, Is.EqualTo(BuildingProductionCatalogResult.UnsupportedEconomyResource), buildingCode);
        }
    }

    private BuildingContentCatalog CreateCatalog()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        return catalog;
    }

    private static BuildingContentSpec Entry(string code, BuildingEra era, string product, EconomyResourceType resource)
    {
        BuildingContentSpec entry = new BuildingContentSpec
        {
            code = code, minimumEra = era, supportsProduction = true,
            production = new List<BuildingContentProduction>
            {
                new BuildingContentProduction
                {
                    productCode = product, productionTime = 15f,
                    queueLimit = 5, populationCost = 1,
                    cost = new List<BuildingContentCost>
                    {
                        new BuildingContentCost { resource = resource, amount = 30 }
                    }
                }
            }
        };
        return entry;
    }

    private static StrategicPlanningState State(BuildingEra era)
    {
        return new StrategicPlanningState
        {
            ownerId = 0, era = era, population = 5,
            buildings = new List<StrategicBuildingObservation>()
        };
    }
}
