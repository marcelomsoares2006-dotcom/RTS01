using System;
using UnityEditor;
using UnityEngine;

public static class CharacterCatalogPrefabRepair
{
    private static readonly (string dataPath, string prefabPath)[] Entries =
    {
        ("Assets/Resources/ScriptableObjects/Units/Characters/Worker.asset",
            "Assets/Resources/Prefabs/Units/Characters/Worker.prefab"),
        ("Assets/Resources/ScriptableObjects/Units/Characters/Spearman.asset",
            "Assets/Resources/Prefabs/Units/Characters/Spearman.prefab"),
        ("Assets/Resources/ScriptableObjects/Units/Characters/HeraldicKnight.asset",
            "Assets/Resources/Prefabs/Units/Characters/HeraldicKnight.prefab"),
        ("Assets/Resources/ScriptableObjects/Units/Characters/DarkKnight/DarkKnight.asset",
            "Assets/Resources/Prefabs/Units/Characters/DarkKnight/DarkKnight.prefab")
    };

    [MenuItem("Tools/Character Rigging/Repair Character Catalog Prefabs")]
    public static void Run()
    {
        foreach (var entry in Entries)
        {
            CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(entry.dataPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath);
            if (data == null || prefab == null)
                throw new InvalidOperationException($"Cannot load CharacterData or GameObject: {entry.dataPath}, {entry.prefabPath}");
            if (prefab.GetComponent<CharacterManager>() == null ||
                prefab.GetComponent<UnityEngine.AI.NavMeshAgent>() == null)
                throw new InvalidOperationException($"Playable components are missing: {entry.prefabPath}");

            var serialized = new SerializedObject(data);
            SerializedProperty prefabProperty = serialized.FindProperty("prefab");
            if (prefabProperty == null)
                throw new InvalidOperationException($"CharacterData.prefab is missing: {entry.dataPath}");
            prefabProperty.objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            Debug.Log($"Assigned {entry.dataPath} -> {entry.prefabPath}");
        }

        AssetDatabase.SaveAssets();
        foreach (var entry in Entries)
        {
            AssetDatabase.ImportAsset(entry.dataPath, ImportAssetOptions.ForceUpdate);
            CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(entry.dataPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath);
            if (data.prefab != prefab)
                throw new InvalidOperationException($"Saved prefab reference is invalid: {entry.dataPath}");
            Debug.Log($"Verified {entry.dataPath}: {data.prefab.name}");
        }
    }
}
