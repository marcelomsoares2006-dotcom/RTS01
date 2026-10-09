using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Preenche UnitManager.meshRenderer dos prefabs jogaveis de personagem quando a referencia nested ficou nula
/// (ex.: HeraldicKnight: Unit.SetOwnerMaterial lancava UnassignedReferenceException ao criar a unidade).
/// So altera prefabs com a referencia vazia e que tenham exatamente um SkinnedMeshRenderer.
/// </summary>
public static class CharacterManagerRendererRepair
{
    private static readonly string[] Prefabs =
    {
        "Assets/Resources/Prefabs/Units/Characters/Spearman.prefab",
        "Assets/Resources/Prefabs/Units/Characters/HeraldicKnight.prefab",
        "Assets/Resources/Prefabs/Units/Characters/DarkKnight/DarkKnight.prefab"
    };

    [MenuItem("Tools/Character Rigging/Repair Character Mesh Renderers")]
    public static void Run()
    {
        foreach (string path in Prefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                UnitManager manager = root.GetComponent<UnitManager>();
                if (manager == null) throw new InvalidOperationException("UnitManager ausente: " + path);
                if (manager.meshRenderer != null)
                {
                    Debug.Log($"OK {path}: meshRenderer = {manager.meshRenderer.name}");
                    continue;
                }

                SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skinned.Length != 1)
                    throw new InvalidOperationException($"{path}: esperado 1 SkinnedMeshRenderer, encontrado {skinned.Length}");

                manager.meshRenderer = skinned[0];
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"Reparado {path}: meshRenderer -> {skinned[0].name}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        AssetDatabase.SaveAssets();
    }

    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
