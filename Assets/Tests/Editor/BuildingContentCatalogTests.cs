using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BuildingContentCatalogTests
{
    private const string CatalogPath = "Assets/Resources/ScriptableObjects/Units/Building Content Catalog.asset";

    [Test]
    public void CatalogHasStableUniqueCodesAndRequiredFoundationEntries()
    {
        BuildingContentCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildingContentCatalog>(CatalogPath);
        Assert.That(catalog, Is.Not.Null, "The building content catalog asset is missing.");
        Assert.That(catalog.entries, Is.Not.Null.And.Not.Empty);

        string[] invalid = catalog.entries.Where(entry => entry == null || string.IsNullOrWhiteSpace(entry.code))
            .Select(entry => entry == null ? "<null>" : entry.displayName).ToArray();
        Assert.That(invalid, Is.Empty, "Every content entry needs a stable code.");

        string[] duplicates = catalog.entries.GroupBy(entry => entry.code.ToLowerInvariant())
            .Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        Assert.That(duplicates, Is.Empty, "Content codes must be unique: " + string.Join(", ", duplicates));

        foreach (string code in new[]
        {
            "town_center", "house", "resource_dropoff", "barracks", "archery_range",
            "stable", "siege_workshop", "mining_camp", "lumber_camp", "mill"
        })
            Assert.That(catalog.Find(code), Is.Not.Null, "Missing foundation content contract: " + code);
    }

    [Test]
    public void PlannedEntriesDoNotClaimRuntimeReadinessOrBrokenObjectReferences()
    {
        BuildingContentCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildingContentCatalog>(CatalogPath);
        foreach (BuildingContentSpec entry in catalog.entries)
        {
            if (entry.status < BuildingContentStatus.Playable)
                Assert.That(entry.IsRuntimeReady, Is.False, entry.code + " is planned/prototype but claims runtime readiness.");

            if (entry.status >= BuildingContentStatus.Playable)
            {
                Assert.That(entry.buildingData, Is.Not.Null, entry.code + " needs BuildingData before becoming playable.");
                Assert.That(entry.buildingData.prefab, Is.Not.Null, entry.code + " needs a prefab before becoming playable.");
            }
        }
    }

    [Test]
    public void ExistingContentPathsResolveAndPlannedPathsRemainExplicitlyAbsent()
    {
        BuildingContentCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildingContentCatalog>(CatalogPath);
        foreach (BuildingContentSpec entry in catalog.entries)
        {
            Assert.That(string.IsNullOrWhiteSpace(entry.prefabPath), Is.False, entry.code + " needs a documented prefab path.");
            Assert.That(string.IsNullOrWhiteSpace(entry.modelPath), Is.False, entry.code + " needs a documented model path.");

            if (entry.status >= BuildingContentStatus.Playable)
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath), Is.Not.Null,
                    entry.code + " prefab cannot be loaded by Unity.");
            }
        }
    }

    [Test]
    public void RuntimeReadyEntriesAreOnlyExistingPlayableContent()
    {
        BuildingContentCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildingContentCatalog>(CatalogPath);
        List<string> runtimeCodes = catalog.RuntimeReadyEntries().Select(entry => entry.code).ToList();
        CollectionAssert.AreEquivalent(new[] {
            "town_center", "house", "resource_dropoff", "barracks", "archery_range",
            "stable", "siege_workshop", "mining_camp", "lumber_camp", "mill", "tower"
        }, runtimeCodes,
            "The catalog must expose only authored playable buildings to runtime construction.");
    }

    [Test]
    public void FoundationContractsDefineTownCenterAndDropoffGameplay()
    {
        BuildingContentCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildingContentCatalog>(CatalogPath);
        BuildingContentSpec townCenter = catalog.Find("town_center");
        BuildingContentSpec dropoff = catalog.Find("resource_dropoff");

        Assert.That(townCenter, Is.Not.Null);
        Assert.That(townCenter.constructionCost.Any(
            cost => cost != null && cost.resource == EconomyResourceType.Wood && cost.amount == 400), Is.True);
        Assert.That(townCenter.dropoffResources, Does.Contain(EconomyResourceType.Food));
        Assert.That(townCenter.dropoffResources, Does.Contain(EconomyResourceType.Wood));
        Assert.That(townCenter.dropoffResources, Does.Contain(EconomyResourceType.Gold));
        Assert.That(townCenter.dropoffResources, Does.Contain(EconomyResourceType.Stone));
        Assert.That(townCenter.production.Any(
            production => production != null && production.productCode == "villager"
                && production.productionTime > 0f
                && production.queueLimit > 0
                && production.populationCost == 1
                && production.cost.Any(cost => cost != null
                    && cost.resource == EconomyResourceType.Food && cost.amount == 50)), Is.True);
        Assert.That(townCenter.garrisonCapacity, Is.GreaterThan(0));
        Assert.That(townCenter.populationCapacity, Is.GreaterThan(0));

        Assert.That(dropoff, Is.Not.Null);
        Assert.That(dropoff.constructionCost.Any(
            cost => cost != null && cost.resource == EconomyResourceType.Wood && cost.amount == 50), Is.True);
        Assert.That(dropoff.supportsDropoff, Is.True);
        Assert.That(dropoff.requirements.Any(
            requirement => requirement != null && requirement.buildingCode == "town_center"), Is.True);
        Assert.That(dropoff.dropoffResources, Does.Contain(EconomyResourceType.Food));
        Assert.That(dropoff.dropoffResources, Does.Contain(EconomyResourceType.Wood));
        Assert.That(dropoff.dropoffResources, Does.Contain(EconomyResourceType.Gold));
        Assert.That(dropoff.dropoffResources, Does.Contain(EconomyResourceType.Stone));
        Assert.That(dropoff.dropoffResources.Distinct().Count(), Is.EqualTo(4));
    }

    [Test]
    public void FoundationGameplayContractsAreInternallyValid()
    {
        BuildingContentCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildingContentCatalog>(CatalogPath);
        string[] errors = catalog.ValidateGameplayContracts().ToArray();
        Assert.That(errors, Is.Empty, string.Join("; ", errors));
    }

    [Test]
    public void EraProgressionContractsKeepEconomyAndMilitaryAnchoredToTownCenter()
    {
        BuildingContentCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildingContentCatalog>(CatalogPath);
        string[] errors = BuildingContentProgressionRules.ValidateEraContracts(catalog).ToArray();
        Assert.That(errors, Is.Empty, string.Join("; ", errors));

        Assert.That(catalog.Find("archery_range").minimumEra, Is.EqualTo(BuildingEra.Feudal));
        Assert.That(catalog.Find("stable").minimumEra, Is.EqualTo(BuildingEra.Feudal));
        Assert.That(catalog.Find("siege_workshop").minimumEra, Is.EqualTo(BuildingEra.Castle));
        Assert.That(catalog.Find("barracks").minimumEra, Is.EqualTo(BuildingEra.Dark));
    }

    [Test]
    public void AssetOriginSeparatesIntermediateTownCenterFromRuntimeBuildings()
    {
        BuildingContentCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildingContentCatalog>(CatalogPath);
        Assert.That(catalog.Find("town_center").AssetStage,
            Is.EqualTo(BuildingContentAssetStage.RuntimeReady));
        Assert.That(catalog.Find("resource_dropoff").AssetStage,
            Is.EqualTo(BuildingContentAssetStage.RuntimeReady));
        Assert.That(catalog.Find("house").AssetStage,
            Is.EqualTo(BuildingContentAssetStage.RuntimeReady));
        Assert.That(catalog.Find("tower").AssetStage,
            Is.EqualTo(BuildingContentAssetStage.RuntimeReady));
    }

    [Test]
    public void EssentialRosterDefinesEraCostProductionAndPrerequisiteContracts()
    {
        BuildingContentCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildingContentCatalog>(CatalogPath);

        AssertConstruction(catalog.Find("town_center"), BuildingEra.Dark, EconomyResourceType.Wood, 400);
        AssertConstruction(catalog.Find("resource_dropoff"), BuildingEra.Dark, EconomyResourceType.Wood, 50);
        AssertConstruction(catalog.Find("mining_camp"), BuildingEra.Dark, EconomyResourceType.Wood, 50);
        AssertConstruction(catalog.Find("lumber_camp"), BuildingEra.Dark, EconomyResourceType.Wood, 50);
        AssertConstruction(catalog.Find("mill"), BuildingEra.Dark, EconomyResourceType.Wood, 50);
        AssertConstruction(catalog.Find("barracks"), BuildingEra.Dark, EconomyResourceType.Wood, 150);
        AssertConstruction(catalog.Find("archery_range"), BuildingEra.Feudal, EconomyResourceType.Wood, 150);
        AssertConstruction(catalog.Find("stable"), BuildingEra.Feudal, EconomyResourceType.Wood, 150);
        AssertConstruction(catalog.Find("siege_workshop"), BuildingEra.Castle, EconomyResourceType.Wood, 300);
        AssertConstruction(catalog.Find("tower"), BuildingEra.Dark, EconomyResourceType.Wood, 80);

        AssertProduction(catalog.Find("town_center"), "villager");
        AssertProduction(catalog.Find("barracks"), "spearman");
        AssertProduction(catalog.Find("archery_range"), "archer");
        AssertProduction(catalog.Find("stable"), "horseman");
        AssertProduction(catalog.Find("siege_workshop"), "battering_ram");

        AssertProduction(catalog.Find("barracks"), "combat_mage");
        AssertProduction(catalog.Find("barracks"), "paladin_healer");
        AssertProduction(catalog.Find("archery_range"), "crossbow");
        AssertProduction(catalog.Find("siege_workshop"), "catapult");

        string[,] advancedAssets = {
            { "combat_mage", "CombatMage" },
            { "paladin_healer", "PaladinHealer" },
            { "catapult", "Catapult" },
            { "crossbow", "Crossbow" }
        };
        for (int i = 0; i < advancedAssets.GetLength(0); i++)
        {
            CharacterData data = Resources.Load<CharacterData>(
                "ScriptableObjects/Units/Characters/" + advancedAssets[i, 1]);
            string code = advancedAssets[i, 0];
            Assert.That(data, Is.Not.Null, code + " must have a runtime CharacterData asset.");
            Assert.That(data.prefab, Is.Not.Null, code + " CharacterData must reference its playable prefab.");
            Assert.That(string.Equals(data.code, code, System.StringComparison.OrdinalIgnoreCase), Is.True,
                code + " CharacterData code must match the production product code.");
        }

        foreach (string code in new[]
        {
            "resource_dropoff", "mining_camp", "lumber_camp", "mill", "barracks",
            "archery_range", "stable", "siege_workshop"
        })
        {
            BuildingContentSpec entry = catalog.Find(code);
            Assert.That(entry.requirements.Any(requirement => requirement != null
                && requirement.buildingCode == "town_center"), Is.True,
                code + " must require a Town Center.");
        }
    }

    [Test]
    public void PromotedEconomyAndBarracksContractsRemainExplicit()
    {
        BuildingContentCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildingContentCatalog>(CatalogPath);

        AssertDropoff(catalog.Find("mining_camp"), EconomyResourceType.Gold, EconomyResourceType.Stone);
        AssertDropoff(catalog.Find("lumber_camp"), EconomyResourceType.Wood);
        AssertDropoff(catalog.Find("mill"), EconomyResourceType.Food);

        BuildingContentSpec barracks = catalog.Find("barracks");
        Assert.That(barracks.role, Is.EqualTo(BuildingContentRole.Production));
        Assert.That(barracks.minimumEra, Is.EqualTo(BuildingEra.Dark));
        Assert.That(barracks.supportsProduction, Is.True);
        Assert.That(barracks.IsRuntimeReady, Is.True,
            "Barracks has an authored prefab and should be runtime-ready.");
        Assert.That(barracks.requirements.Any(requirement => requirement != null
            && requirement.buildingCode == "town_center"), Is.True);
        Assert.That(barracks.constructionCost.Any(cost => cost != null
            && cost.resource == EconomyResourceType.Wood && cost.amount == 150), Is.True);
        AssertProduction(barracks, "spearman");
    }

    private static void AssertConstruction(
        BuildingContentSpec entry, BuildingEra era, EconomyResourceType resource, int amount)
    {
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry.minimumEra, Is.EqualTo(era), entry.code + " has the wrong minimum era.");
        Assert.That(entry.constructionCost.Any(cost => cost != null
            && cost.resource == resource && cost.amount == amount), Is.True,
            entry.code + " has the wrong construction cost.");
    }

    private static void AssertProduction(BuildingContentSpec entry, string productCode)
    {
        BuildingContentProduction production = entry.production
            .FirstOrDefault(item => item != null && item.productCode == productCode);
        Assert.That(production, Is.Not.Null, entry.code + " must produce " + productCode + ".");
        Assert.That(production.productionTime, Is.GreaterThan(0f));
        Assert.That(production.queueLimit, Is.GreaterThan(0));
        Assert.That(production.cost, Is.Not.Null.And.Not.Empty);
        Assert.That(production.cost.All(cost => cost != null && cost.amount > 0), Is.True);
    }

    private static void AssertDropoff(BuildingContentSpec entry, params EconomyResourceType[] resources)
    {
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry.role, Is.EqualTo(BuildingContentRole.ResourceDropoff));
        Assert.That(entry.minimumEra, Is.EqualTo(BuildingEra.Dark));
        Assert.That(entry.supportsDropoff, Is.True);
        Assert.That(entry.IsRuntimeReady, Is.True,
            entry.code + " has an authored prefab and should be runtime-ready.");
        Assert.That(entry.requirements.Any(requirement => requirement != null
            && requirement.buildingCode == "town_center"), Is.True,
            entry.code + " must require Town Center.");
        CollectionAssert.AreEquivalent(resources, entry.dropoffResources, entry.code + " has the wrong accepted resources.");
    }
}
