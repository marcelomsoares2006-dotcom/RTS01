using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using NUnit.Framework;

public class CharacterCatalogCompatibilityTests
{
    private static readonly string[] RequiredCodes =
    {
        "worker",
        "soldier",
        "spearman",
        "heraldic_knight",
        "dark_knight"
    };

    [Test]
    public void ResourcesCatalog_ContainsRequiredCharactersWithCompatiblePrefabs()
    {
        var assetsByCode = new Dictionary<string, Object>();
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            SerializedObject serialized = new SerializedObject(asset);
            string code = serialized.FindProperty("code").stringValue;
            assetsByCode[code] = asset;
        }

        foreach (string code in RequiredCodes)
        {
            Assert.That(assetsByCode.ContainsKey(code), Is.True, $"CharacterData ausente: {code}");

            SerializedObject serialized = new SerializedObject(assetsByCode[code]);
            Object prefab = serialized.FindProperty("prefab").objectReferenceValue;
            Assert.That(prefab, Is.Not.Null, $"Prefab ausente para CharacterData: {code}");

            GameObject root = prefab as GameObject;
            Assert.That(root, Is.Not.Null, $"Referência não é GameObject para: {code}");
            Assert.That(root.GetComponent("CharacterManager"), Is.Not.Null, $"CharacterManager ausente: {code}");
            Assert.That(root.GetComponent<UnityEngine.AI.NavMeshAgent>(), Is.Not.Null, $"NavMeshAgent ausente: {code}");
        }
    }
}
