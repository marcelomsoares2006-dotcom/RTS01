$ErrorActionPreference = "Stop"

$root = Get-Location
$toolsDir = Join-Path $root "Assets\Scripts\Tools"
$scriptPath = Join-Path $toolsDir "TerrainRuntimeRepairBootstrap.cs"
$metaPath = Join-Path $toolsDir "TerrainRuntimeRepairBootstrap.cs.meta"
New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null

@'
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class TerrainRuntimeRepairBootstrap
{
    private static readonly string[] DefaultLayerNames = { "grass", "dirt", "soil", "sand" };

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void EditorInitialize()
    {
        EditorApplication.delayCall += RepairAllTerrains;
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RuntimeInitialize()
    {
        RepairAllTerrains();
    }

    private static void RepairAllTerrains()
    {
        TerrainLayer[] defaults = LoadDefaultLayers();
        if (defaults.Length == 0)
        {
            Debug.LogWarning("Terrain repair: nenhuma TerrainLayer foi encontrada.");
            return;
        }

        foreach (Terrain terrain in Object.FindObjectsOfType<Terrain>())
        {
            if (terrain == null || terrain.terrainData == null)
                continue;

            TerrainData data = terrain.terrainData;
            if (NeedsRepair(data.terrainLayers, defaults))
            {
                data.terrainLayers = defaults;
                Debug.Log("Terrain repair: camadas do terreno restauradas.");
#if UNITY_EDITOR
                EditorUtility.SetDirty(data);
                EditorUtility.SetDirty(terrain);
#endif
            }

            terrain.materialTemplate = null;
            terrain.Flush();
        }
    }

    private static TerrainLayer[] LoadDefaultLayers()
    {
        List<TerrainLayer> result = new List<TerrainLayer>();
        foreach (string name in DefaultLayerNames)
        {
            TerrainLayer layer = Resources.Load<TerrainLayer>("Terrain/Layers/" + name);
            if (layer != null)
                result.Add(layer);
        }
        return result.ToArray();
    }

    private static bool NeedsRepair(TerrainLayer[] current, TerrainLayer[] defaults)
    {
        if (current == null || current.Length == 0 || current.Length < defaults.Length)
            return true;

        foreach (TerrainLayer layer in current)
        {
            if (layer == null || string.IsNullOrEmpty(layer.name) ||
                layer.name.StartsWith("NewLayer") || layer.name.StartsWith("New Layer"))
                return true;
        }
        return false;
    }
}
'@ | Set-Content -Path $scriptPath -Encoding UTF8

@'
fileFormatVersion: 2
guid: 1bbd492c28ef4cf6bd8a5d26eebd7c81
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
'@ | Set-Content -Path $metaPath -Encoding UTF8

Write-Host "Correção v2 instalada em Assets\Scripts\Tools. Nenhuma cena foi alterada."
Write-Host "Abra o Unity, aguarde a compilação e pressione Play em Core ou Map1."
