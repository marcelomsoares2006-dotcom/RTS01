$ErrorActionPreference = "Stop"

$root = Get-Location
$toolsDir = Join-Path $root "Assets\Scripts\Tools"
$scriptPath = Join-Path $toolsDir "TerrainRuntimeRepair.cs"
$metaPath = Join-Path $toolsDir "TerrainRuntimeRepair.cs.meta"

New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null

@'
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
'@ | Set-Content -Path $scriptPath -Encoding UTF8

@'
fileFormatVersion: 2
guid: 3869c351fe1d4db2ac0c69769937d6ec
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

function Patch-Scene {
    param(
        [string]$RelativePath,
        [string]$ExistingComponentId,
        [string]$RepairComponentId,
        [string]$TerrainGameObjectId,
        [string]$NavMeshGuid,
        [string]$NextObjectId
    )

    $path = Join-Path $root $RelativePath
    if (!(Test-Path $path)) {
        throw "Scene file not found: $RelativePath"
    }

    Copy-Item -Path $path -Destination "$path.bak-terrainfix" -Force
    $text = Get-Content -Path $path -Raw

    if ($text.Contains("guid: 3869c351fe1d4db2ac0c69769937d6ec")) {
        Write-Host "$RelativePath already has TerrainRuntimeRepair."
        return
    }

    $componentNeedle = "  - component: {fileID: $ExistingComponentId}`n"
    $componentPatch = "  - component: {fileID: $ExistingComponentId}`n  - component: {fileID: $RepairComponentId}`n"

    if (!$text.Contains($componentNeedle)) {
        throw "Could not find component list in $RelativePath"
    }

    $text = $text.Replace($componentNeedle, $componentPatch)

    $navNeedle = "  m_NavMeshData: {fileID: 23800000, guid: $NavMeshGuid, type: 2}`n--- !u!1 &$NextObjectId"
    $repairBlock = @"
  m_NavMeshData: {fileID: 23800000, guid: $NavMeshGuid, type: 2}
--- !u!114 &$RepairComponentId
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $TerrainGameObjectId}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3869c351fe1d4db2ac0c69769937d6ec, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
--- !u!1 &$NextObjectId
"@

    if (!$text.Contains($navNeedle)) {
        throw "Could not find NavMesh block in $RelativePath"
    }

    $text = $text.Replace($navNeedle, $repairBlock)
    Set-Content -Path $path -Value $text -Encoding UTF8 -NoNewline
    Write-Host "Patched $RelativePath"
}

Patch-Scene `
    -RelativePath "Assets\Scenes\Maps\Map1.unity" `
    -ExistingComponentId "860364782" `
    -RepairComponentId "860364783" `
    -TerrainGameObjectId "860364778" `
    -NavMeshGuid "bfe080d46ebe541c89707860354a9b67" `
    -NextObjectId "1112833755"

Patch-Scene `
    -RelativePath "Assets\Scenes\Maps\Map2.unity" `
    -ExistingComponentId "885986373" `
    -RepairComponentId "885986374" `
    -TerrainGameObjectId "885986369" `
    -NavMeshGuid "a798b35b37f104014829772965cc8144" `
    -NextObjectId "980064863"

Write-Host ""
Write-Host "Terrain fix applied. Reopen Unity or wait for recompilation, then press Play again."
