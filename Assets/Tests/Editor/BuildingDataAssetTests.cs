using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only contract tests for the building catalog. These tests deliberately
/// inspect serialized assets instead of constructing Building/Unit instances,
/// so they do not require a running scene or GameManager singleton.
/// </summary>
public sealed class BuildingDataAssetTests
{
    private const string BuildingFolder = "Assets/Resources/ScriptableObjects/Units/Buildings";

    [Test]
    public void Catalog_HasUniqueNonEmptyCodes()
    {
        BuildingData[] buildings = LoadCatalog();
        Assert.That(buildings, Is.Not.Empty, "The building catalog is empty.");

        string[] invalidNames = buildings
            .Where(data => string.IsNullOrWhiteSpace(data.code))
            .Select(data => AssetDatabase.GetAssetPath(data))
            .ToArray();
        Assert.That(invalidNames, Is.Empty,
            "Every BuildingData asset needs a stable non-empty code. Assets: " + string.Join(", ", invalidNames));

        string[] duplicateCodes = buildings
            .GroupBy(data => data.code)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        Assert.That(duplicateCodes, Is.Empty,
            "Building codes must be unique because save/load resolves buildings by code.");
    }

    [Test]
    public void Catalog_EachBuildingHasRuntimeReferencesAndConstructionStages()
    {
        foreach (BuildingData data in LoadCatalog())
        {
            string path = AssetDatabase.GetAssetPath(data);
            Assert.That(data.healthpoints, Is.GreaterThan(0), $"{path} must have positive healthpoints.");
            Assert.That(data.prefab, Is.Not.Null, $"{path} is missing its prefab.");
            Assert.That(data.prefab.GetComponent<BuildingManager>(), Is.Not.Null,
                $"{path} prefab must contain BuildingManager.");
            Assert.That(data.prefab.GetComponent<BoxCollider>(), Is.Not.Null,
                $"{path} prefab must contain a BoxCollider for placement.");
            Assert.That(data.constructionMeshes, Is.Not.Null.And.Not.Empty,
                $"{path} must provide at least one construction mesh.");
            Assert.That(data.constructionMeshes.All(mesh => mesh != null), Is.True,
                $"{path} contains a missing construction mesh reference.");
        }
    }

    [Test]
    public void Catalog_CodesMatchAssetNamesAndBuildSkillsResolveToCatalog()
    {
        BuildingData[] buildings = LoadCatalog();
        HashSet<string> codes = new HashSet<string>(buildings.Select(data => data.code));

        foreach (BuildingData data in buildings)
        {
            string path = AssetDatabase.GetAssetPath(data);
            Assert.That(data.name, Is.EqualTo(data.unitName),
                $"{path} asset name and unitName should stay aligned for UI/debugging.");
        }

        string[] skillGuids = AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/Resources/ScriptableObjects/Skills" });
        foreach (string guid in skillGuids)
        {
            SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill == null || skill.type != SkillType.INSTANTIATE_BUILDING)
                continue;

            BuildingData referenced = skill.unitReference as BuildingData;
            Assert.That(referenced, Is.Not.Null, $"{skill.name} must reference a BuildingData asset.");
            Assert.That(codes.Contains(referenced.code), Is.True,
                $"{skill.name} references a building outside the loaded catalog.");
        }
    }

    private static BuildingData[] LoadCatalog()
    {
        string[] guids = AssetDatabase.FindAssets("t:BuildingData", new[] { BuildingFolder });
        return guids
            .Select(guid => AssetDatabase.LoadAssetAtPath<BuildingData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(data => data != null)
            .ToArray();
    }
}
