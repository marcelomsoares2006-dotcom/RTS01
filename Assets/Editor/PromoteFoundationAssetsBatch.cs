using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Deterministic batch runner for the nine first-playable foundation buildings.
/// It reuses the project's authoring implementation and never overwrites a
/// pre-existing final asset. Intended for Unity -batchmode -executeMethod.
/// </summary>
public static class PromoteFoundationAssetsBatch
{
    private const string PrefabFolder = "Assets/Resources/Prefabs/Units/Buildings";
    private const string DataFolder = "Assets/Resources/ScriptableObjects/Units/Buildings";
    private const string ReportPath = "TestResults/foundation-promotion-batch.txt";

    private static readonly (string code, string name)[] Foundations =
    {
        ("town_center", "Town Center"),
        ("resource_dropoff", "Resource Dropoff"),
        ("lumber_camp", "Lumber Camp"),
        ("mining_camp", "Mining Camp"),
        ("mill", "Mill"),
        ("barracks", "Barracks"),
        ("archery_range", "Archery Range"),
        ("stable", "Stable"),
        ("siege_workshop", "SiegeWorkshop")
    };

    public static void Run()
    {
        Directory.CreateDirectory("TestResults");
        var lines = new List<string>
        {
            "Foundation promotion batch",
            "Started: " + DateTime.UtcNow.ToString("O"),
            "Unity: " + Application.unityVersion
        };

        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureFinalAssetsDoNotExist();
            InvokePrivateAuthoringMethod("CreateDraftAssets");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ValidateDrafts();
            PromoteDrafts();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            InvokePrivateAuthoringMethod("LinkCatalogReferencesAndPromoteStatus");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (var foundation in Foundations)
                lines.Add("PROMOTED " + foundation.code + " -> " + FinalPrefab(foundation.name));
            lines.Add("RESULT: PASS");
            File.WriteAllLines(ReportPath, lines);
            Debug.Log("Foundation promotion batch passed: nine foundations promoted.");
        }
        catch (Exception exception)
        {
            lines.Add("RESULT: FAIL");
            lines.Add(exception.ToString());
            File.WriteAllLines(ReportPath, lines);
            Debug.LogError("Foundation promotion batch failed. See " + ReportPath + "\n" + exception);
            throw;
        }
    }

    public static void FinishExisting()
    {
        InvokePrivateAuthoringMethod("LinkCatalogReferencesAndPromoteStatus");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        File.WriteAllText(ReportPath, "Foundation promotion batch\nRESULT: PASS\nExisting final assets linked and promoted.\n");
    }

    private static void EnsureFinalAssetsDoNotExist()
    {
        var existing = Foundations
            .SelectMany(f => new[] { FinalPrefab(f.name), FinalData(f.name) })
            .Where(File.Exists)
            .ToArray();
        if (existing.Length > 0)
            throw new InvalidOperationException("Refusing to overwrite existing final foundation assets: " + string.Join(", ", existing));
    }

    private static void InvokePrivateAuthoringMethod(string methodName)
    {
        var type = typeof(FoundationBuildingAuthoringWindow);
        var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(type.FullName, methodName);
        var instance = method.IsStatic ? null : ScriptableObject.CreateInstance(type);
        try
        {
            method.Invoke(instance, null);
        }
        finally
        {
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void ValidateDrafts()
    {
        foreach (var foundation in Foundations)
        {
            var prefabPath = DraftPrefab(foundation.name);
            var dataPath = DraftData(foundation.name);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var data = AssetDatabase.LoadAssetAtPath<BuildingData>(dataPath);
            if (prefab == null || data == null)
                throw new InvalidOperationException("Missing draft for " + foundation.code);
            EnsurePhysicsComponents(prefabPath);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!string.Equals(prefab.tag, "Unit", StringComparison.Ordinal) || prefab.layer != 12)
                throw new InvalidOperationException("Draft root layer/tag invalid for " + foundation.code);
            if (prefab.GetComponent<BuildingManager>() == null || prefab.GetComponent<BuildingBT>() == null)
                throw new InvalidOperationException("Draft runtime components missing for " + foundation.code);
            if (prefab.GetComponent<Collider>() == null || prefab.GetComponent<Rigidbody>() == null)
                throw new InvalidOperationException("Draft physics components missing for " + foundation.code);
            if (prefab.GetComponent<UnityEngine.AI.NavMeshObstacle>() == null)
                throw new InvalidOperationException("Draft NavMeshObstacle missing for " + foundation.code);
            if (data.prefab == null || data.constructionMeshes == null || data.constructionMeshes.Length == 0)
                throw new InvalidOperationException("Draft BuildingData references missing for " + foundation.code);
        }
    }

    private static void EnsurePhysicsComponents(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            if (root.GetComponent<Rigidbody>() == null)
            {
                Rigidbody body = root.AddComponent<Rigidbody>();
                if (body != null) { body.isKinematic = true; body.useGravity = false; }
            }
            if (root.GetComponent<NavMeshObstacle>() == null)
            {
                NavMeshObstacle obstacle = root.AddComponent<NavMeshObstacle>();
                if (obstacle != null) obstacle.carving = true;
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void PromoteDrafts()
    {
        foreach (var foundation in Foundations)
        {
            var prefabError = AssetDatabase.MoveAsset(DraftPrefab(foundation.name), FinalPrefab(foundation.name));
            if (!string.IsNullOrEmpty(prefabError))
                throw new InvalidOperationException("Could not promote prefab " + foundation.code + ": " + prefabError);
            var dataError = AssetDatabase.MoveAsset(DraftData(foundation.name), FinalData(foundation.name));
            if (!string.IsNullOrEmpty(dataError))
                throw new InvalidOperationException("Could not promote BuildingData " + foundation.code + ": " + dataError);
        }
    }

    private static string SafeName(string displayName) => displayName.Replace(" ", string.Empty);
    private static string DraftPrefab(string displayName) => PrefabFolder + "/" + SafeName(displayName) + "_DRAFT.prefab";
    private static string DraftData(string displayName) => DataFolder + "/" + SafeName(displayName) + "_DRAFT.asset";
    private static string FinalPrefab(string displayName) => PrefabFolder + "/" + SafeName(displayName) + ".prefab";
    private static string FinalData(string displayName) => DataFolder + "/" + SafeName(displayName) + ".asset";
}
