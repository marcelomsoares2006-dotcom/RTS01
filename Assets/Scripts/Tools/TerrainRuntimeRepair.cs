using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class TerrainRuntimeRepair : MonoBehaviour
{
    private static readonly string[] DefaultLayerNames =
    {
        "grass",
        "dirt",
        "soil",
        "sand"
    };

    private void Awake()
    {
        RepairAllTerrains();
    }

    private void OnEnable()
    {
        RepairAllTerrains();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RepairAllTerrains();
    }
#endif

    public static void RepairAllTerrains()
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            RepairTerrain(terrain);
        }

        foreach (Terrain terrain in FindObjectsOfType<Terrain>())
        {
            RepairTerrain(terrain);
        }
    }

    private static void RepairTerrain(Terrain terrain)
    {
        if (terrain == null || terrain.terrainData == null)
        {
            return;
        }

        TerrainLayer[] defaultLayers = LoadDefaultLayers();
        if (defaultLayers.Length == 0)
        {
            Debug.LogWarning("TerrainRuntimeRepair could not find default terrain layers in Resources/Terrain/Layers.");
            return;
        }

        TerrainData terrainData = terrain.terrainData;
        if (NeedsLayerRepair(terrainData.terrainLayers, defaultLayers))
        {
            terrainData.terrainLayers = defaultLayers;
            Debug.Log("TerrainRuntimeRepair restored terrain layers.");
        }

        terrain.materialTemplate = null;
        terrain.Flush();

#if UNITY_EDITOR
        EditorUtility.SetDirty(terrain);
        EditorUtility.SetDirty(terrainData);
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        }
#endif
    }

    private static TerrainLayer[] LoadDefaultLayers()
    {
        List<TerrainLayer> layers = new List<TerrainLayer>();
        foreach (string layerName in DefaultLayerNames)
        {
            TerrainLayer layer = Resources.Load<TerrainLayer>($"Terrain/Layers/{layerName}");
            if (layer != null)
            {
                layers.Add(layer);
            }
        }

        return layers.ToArray();
    }

    private static bool NeedsLayerRepair(TerrainLayer[] currentLayers, TerrainLayer[] defaultLayers)
    {
        if (currentLayers == null || currentLayers.Length == 0)
        {
            return true;
        }

        foreach (TerrainLayer layer in currentLayers)
        {
            if (layer == null || string.IsNullOrWhiteSpace(layer.name))
            {
                return true;
            }

            if (layer.name.StartsWith("NewLayer") || layer.name.StartsWith("New Layer"))
            {
                return true;
            }
        }

        return currentLayers.Length < defaultLayers.Length;
    }
}
