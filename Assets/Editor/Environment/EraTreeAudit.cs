using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class EraTreeAudit
{
    // Read-only batch audit: -executeMethod EraTreeAudit.Run
    public static void Run()
    {
        const string materialPath = "Assets/BrokenVector/LowPolyTreePack/Materials/Normal.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null || material.shader == null ||
            material.shader.name != "EraImperial/Environment/EraTree" ||
            !material.shader.isSupported || ShaderUtil.ShaderHasError(material.shader))
            throw new InvalidOperationException("Era tree shader is missing, unsupported or has a compile error.");

        foreach (string name in new[] { "Ancient", "Classical", "Medieval", "Imperial" })
        {
            if (material.GetTexture("_EITree" + name + "Tex") == null)
                throw new InvalidOperationException("Missing tree atlas for " + name);
        }
        if (material.mainTexture == null)
            throw new InvalidOperationException("The tree fallback atlas is missing.");
        foreach (string name in new[] { "Cold", "Dry", "Fall" })
        {
            Material alternate = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/BrokenVector/LowPolyTreePack/Materials/" + name + ".mat");
            if (alternate == null || alternate.shader == null || alternate.mainTexture == null)
                throw new InvalidOperationException("The original " + name + " material is not usable.");
        }

        foreach (var map in new[] { ("Map1", 5916), ("Map2", 19030) })
        {
            string path = "Assets/Scenes/Maps/Terrain-" + map.Item1 + ".asset";
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data == null || data.treeInstanceCount != map.Item2)
                throw new InvalidOperationException(map.Item1 + " tree count differs from baseline.");
            TreePrototype[] prototypes = data.treePrototypes;
            if (prototypes.Length != 4 || prototypes.Any(p => p == null || p.prefab == null))
                throw new InvalidOperationException(map.Item1 + " must have four valid tree prototypes.");
            for (int i = 0; i < prototypes.Length; i++)
            {
                GameObject expected = Resources.Load<GameObject>("Prefabs/Doodads/Trees/Tree Type7 0" + (i + 1));
                if (prototypes[i].prefab != expected)
                    throw new InvalidOperationException(map.Item1 + " tree prototype " + i + " is not the expected Type7 shape.");
            }
            if (data.treeInstances.Any(i => i.prototypeIndex < 0 || i.prototypeIndex >= prototypes.Length))
                throw new InvalidOperationException(map.Item1 + " has an invalid prototype index.");
            int[] distribution = new int[prototypes.Length];
            foreach (TreeInstance tree in data.treeInstances) distribution[tree.prototypeIndex]++;
            if (distribution.Any(count => count == 0))
                throw new InvalidOperationException(map.Item1 + " has an unused tree variant.");
            Debug.Log("[EraTreeAudit] " + map.Item1 + ": " + map.Item2 + " instances, " +
                      prototypes.Length + " valid prototype slots; distribution " + string.Join(", ", distribution));
        }
        Debug.Log("[EraTreeAudit] PASSED; read-only; no TerrainData or scene saved.");
    }
}
