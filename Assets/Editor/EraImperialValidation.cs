using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EraImperial.Compatibility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Explicit, repeatable migration and integration checks; never runs on editor startup.</summary>
public static class EraImperialValidation
{
    private const string ReportDirectory = "TestResults";
    private static readonly List<string> Report = new List<string>();

    private static void Record(string message)
    {
        Report.Add(message);
        Debug.Log("[EraImperial validation] " + message);
    }

    [MenuItem("Tools/Era Imperial/Migrate and Audit Unity 6")]
    public static void MigrateAndAudit()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(ReportDirectory);
        Report.Clear();
        Record("Unity " + Application.unityVersion + "; GPU: " + SystemInfo.graphicsDeviceName);
        Shader shader = Shader.Find("Nature/Terrain/Standard");
        if (shader == null || !shader.isSupported)
            throw new InvalidOperationException("Built-in terrain shader unavailable on the current graphics device.");
        const string materialPath = "Assets/Resources/Terrain/EraImperialTerrain.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "EraImperialTerrain" };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else if (material.shader == null || !material.shader.isSupported)
        {
            material.shader = shader;
            EditorUtility.SetDirty(material);
        }

        TerrainLayer[] defaults = new[] { "grass", "dirt", "soil", "sand" }
            .Select(name => Resources.Load<TerrainLayer>("Terrain/Layers/" + name)).ToArray();
        if (defaults.Any(layer => layer == null || layer.diffuseTexture == null))
            throw new InvalidOperationException("Default terrain layer or diffuse texture is missing.");
        GameObject treeFallback = Resources.Load<GameObject>("Prefabs/Doodads/Trees/Tree Type7 01");

        // Repair assets BEFORE loading scenes: Terrain otherwise logs invalid prototypes on activation.
        foreach (string guid in AssetDatabase.FindAssets("t:TerrainData", new[] { "Assets/Scenes/Maps" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            TerrainLayer[] layers = data.terrainLayers;
            TreePrototype[] prototypes = data.treePrototypes;
            int count = data.treeInstanceCount;
            // Earlier startup repairs erased Map1's trees. Recover only this lost channel;
            // keep the current heightmap, textures and all other edits intact.
            const string recoveryPath = "Assets/Editor/MigrationSource/OriginalMap1Terrain.asset";
            if (path.EndsWith("/Terrain-Map1.asset") && count == 0 && prototypes.Length == 0)
            {
                TerrainData original = AssetDatabase.LoadAssetAtPath<TerrainData>(recoveryPath);
                if (original != null && original.treeInstanceCount > 0)
                {
                    data.treePrototypes = TerrainCompatibility.FillMissingTreePrefabs(original.treePrototypes, treeFallback);
                    data.SetTreeInstances(original.treeInstances, false);
                    prototypes = data.treePrototypes;
                    count = data.treeInstanceCount;
                    Record($"Recovered {count} original Map1 tree positions; preserved current terrain painting and heights.");
                }
            }
            Record($"{path}: {layers.Length} layers, {prototypes.Length} prototypes, {count} tree instances.");
            if (layers.Length == 0 || layers.Any(layer => layer == null))
            {
                TerrainLayer[] repaired = TerrainCompatibility.FillMissingLayers(layers, defaults);
                if (repaired.Any(layer => layer == null))
                    throw new InvalidOperationException("Missing terrain layer has no matching default: " + path);
                data.terrainLayers = repaired;
                Record("Repaired missing layer slots without reordering the palette: " + path);
            }
            if (prototypes.Any(prototype => prototype == null || prototype.prefab == null))
            {
                data.treePrototypes = TerrainCompatibility.FillMissingTreePrefabs(prototypes, treeFallback);
                Record("Replaced only missing tree prefab references with Tree Type7 01: " + path);
                if (data.treeInstanceCount != count)
                    throw new InvalidOperationException("Tree instance count changed during repair.");
            }
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();

        // Only delete our temporary imported recovery copy, never the backup or original terrain.
        const string importedRecovery = "Assets/Editor/MigrationSource/OriginalMap1Terrain.asset";
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(importedRecovery) != null)
            AssetDatabase.DeleteAsset(importedRecovery);

        int missingScripts = 0;
        var badMaterials = new HashSet<string>();
        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes.Where(entry => entry.enabled))
        {
            Scene scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    foreach (Material used in renderer.sharedMaterials)
                        if (used != null && (used.shader == null || !used.shader.isSupported || ShaderUtil.ShaderHasError(used.shader)))
                            badMaterials.Add(entry.path + ": " + AssetDatabase.GetAssetPath(used));
                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    terrain.materialTemplate = TerrainCompatibility.SelectMaterial(terrain.materialTemplate, material);
                    NavMeshSurface surface = terrain.GetComponent<NavMeshSurface>();
                    if (surface == null)
                        throw new InvalidOperationException("Map terrain has no NavMeshSurface: " + entry.path);
                    if ((Globals.TERRAIN_LAYER_MASK & (1 << terrain.gameObject.layer)) == 0 ||
                        (surface.layerMask.value & (1 << terrain.gameObject.layer)) == 0)
                        throw new InvalidOperationException("Terrain layer is excluded by navigation or mouse picking: " + entry.path);
                    NavMeshData previous = surface.navMeshData;
                    surface.BuildNavMesh();
                    NavMeshData generated = surface.navMeshData;
                    if (generated == null || NavMesh.CalculateTriangulation().vertices.Length == 0)
                        throw new InvalidOperationException("Baked navigation mesh is empty: " + entry.path);
                    if (previous != null && AssetDatabase.Contains(previous))
                    {
                        surface.RemoveData();
                        EditorUtility.CopySerialized(generated, previous);
                        surface.navMeshData = previous;
                        surface.AddData();
                        EditorUtility.SetDirty(previous);
                        UnityEngine.Object.DestroyImmediate(generated);
                    }
                    else
                    {
                        string navPath = Path.ChangeExtension(entry.path, null) + "/NavMesh-Terrain.asset";
                        Directory.CreateDirectory(Path.GetDirectoryName(navPath));
                        AssetDatabase.CreateAsset(generated, navPath);
                    }
                    AssetDatabase.SaveAssets();
                    Record($"{entry.path}: rebaked {NavMesh.CalculateTriangulation().vertices.Length} navigation vertices.");
                    Record($"{entry.path}: terrain material={terrain.materialTemplate.shader.name}; NavMeshSurface reference resolved.");
                }
            }
            if (entry.path.Contains("/Maps/")) EditorSceneManager.SaveScene(scene);
        }
        Record("Missing scripts in build scenes: " + missingScripts);
        foreach (string bad in badMaterials) Record("Unsupported material: " + bad);
        Record("Unsupported scene materials: " + badMaterials.Count);
        EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
        File.WriteAllLines(Path.Combine(ReportDirectory, "migration-audit.txt"), Report);
        if (missingScripts != 0 || badMaterials.Count != 0)
            throw new InvalidOperationException("Scene audit failed; see TestResults/migration-audit.txt.");
        Record("MIGRATION_AUDIT_PASSED");
        File.WriteAllLines(Path.Combine(ReportDirectory, "migration-audit.txt"), Report);
    }

    public static void BuildWindows()
    {
        Directory.CreateDirectory("Builds/Windows");
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = "Builds/Windows/EraImperial.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        File.WriteAllText(Path.Combine(ReportDirectory, "windows-build.txt"),
            $"Result: {result.summary.result}\nErrors: {result.summary.totalErrors}\nWarnings: {result.summary.totalWarnings}\n");
        if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new InvalidOperationException("Windows build failed.");
    }
}
