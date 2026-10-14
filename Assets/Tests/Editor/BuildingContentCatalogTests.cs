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

        foreach (string code in new[] { "town_center", "house", "resource_dropoff", "barracks" })
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
        CollectionAssert.AreEquivalent(new[] { "house" }, runtimeCodes,
            "The catalog must not expose planned buildings to runtime construction.");
    }
}
