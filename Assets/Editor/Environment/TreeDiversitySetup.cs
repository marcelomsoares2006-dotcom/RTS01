using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>One-time, guarded migration from one Type7 palm shape to the four existing Type7 shapes.</summary>
public static class TreeDiversitySetup
{
    private static readonly string[] Maps = { "Map1", "Map2" };
    private static readonly int[] ExpectedCounts = { 5916, 19030 };

    [MenuItem("Tools/Era Imperial/Diversify existing trees (backup first)")]
    public static void Run()
    {
        var prefabs = Enumerable.Range(1, 4)
            .Select(i => Resources.Load<GameObject>("Prefabs/Doodads/Trees/Tree Type7 0" + i))
            .ToArray();
        if (prefabs.Any(p => p == null))
            throw new InvalidOperationException("All four Type7 tree prefabs must be present.");

        TerrainData[] data = new TerrainData[Maps.Length];
        TreeInstance[][] originals = new TreeInstance[Maps.Length][];
        string[] paths = new string[Maps.Length];
        bool[] needsChange = new bool[Maps.Length];
        const string backupFolder = "TestResults/TreeTerrainBeforeDiversity";
        Directory.CreateDirectory(backupFolder);
        for (int m = 0; m < Maps.Length; m++)
        {
            paths[m] = "Assets/Scenes/Maps/Terrain-" + Maps[m] + ".asset";
            data[m] = AssetDatabase.LoadAssetAtPath<TerrainData>(paths[m]);
            if (data[m] == null || data[m].treeInstanceCount != ExpectedCounts[m])
                throw new InvalidOperationException(Maps[m] + " does not match the expected tree count.");
            TreePrototype[] oldPrototypes = data[m].treePrototypes;
            if (oldPrototypes.Length == 4 && oldPrototypes.Select(p => p.prefab).SequenceEqual(prefabs))
            {
                Debug.Log("[TreeDiversitySetup] " + Maps[m] + " already diversified; leaving it unchanged.");
                continue;
            }
            if (oldPrototypes.Length != 1 || oldPrototypes[0].prefab != prefabs[0])
                throw new InvalidOperationException(Maps[m] + " is not a known baseline; no migration performed.");
            needsChange[m] = true;
            originals[m] = data[m].treeInstances;
            if (originals[m].Any(t => t.prototypeIndex != 0))
                throw new InvalidOperationException(Maps[m] + " has unexpected prototype indices.");
        }

        // Back up both originals before changing either asset. Never replace an existing backup.
        for (int m = 0; m < Maps.Length; m++)
        {
            if (!needsChange[m]) continue;
            string backup = Path.Combine(backupFolder, "Terrain-" + Maps[m] + ".asset");
            if (!File.Exists(backup)) File.Copy(paths[m], backup);
        }

        for (int m = 0; m < Maps.Length; m++)
        {
            if (!needsChange[m]) continue;
            TreePrototype source = data[m].treePrototypes[0];
            data[m].treePrototypes = prefabs.Select(prefab => new TreePrototype
            {
                prefab = prefab,
                bendFactor = source.bendFactor,
                navMeshLod = source.navMeshLod
            }).ToArray();
            data[m].RefreshPrototypes();
            TreeInstance[] varied = (TreeInstance[])originals[m].Clone();
            int[] distribution = new int[prefabs.Length];
            for (int i = 0; i < varied.Length; i++)
            {
                TreeInstance tree = varied[i];
                tree.prototypeIndex = SelectVariant(tree.position);
                distribution[tree.prototypeIndex]++;
                varied[i] = tree;
            }
            data[m].SetTreeInstances(varied, false);
            VerifyPreserved(originals[m], data[m].treeInstances, Maps[m]);
            EditorUtility.SetDirty(data[m]);
            Debug.Log("[TreeDiversitySetup] " + Maps[m] + " distribution: " + string.Join(", ", distribution));
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[TreeDiversitySetup] PASSED; all positions/scales/colors preserved; backups in " + backupFolder);
    }

    private static int SelectVariant(Vector3 position)
    {
        unchecked
        {
            uint x = (uint)Mathf.RoundToInt(position.x * 1000000f);
            uint z = (uint)Mathf.RoundToInt(position.z * 1000000f);
            uint hash = (x * 73856093u) ^ (z * 19349663u);
            hash ^= hash >> 16;
            return (int)(hash % 4u);
        }
    }

    private static void VerifyPreserved(TreeInstance[] before, TreeInstance[] after, string name)
    {
        if (before.Length != after.Length) throw new InvalidOperationException(name + " tree count changed.");
        for (int i = 0; i < before.Length; i++)
        {
            TreeInstance a = before[i];
            TreeInstance b = after[i];
            if (a.position != b.position || a.widthScale != b.widthScale || a.heightScale != b.heightScale ||
                a.rotation != b.rotation || !a.color.Equals(b.color) || !a.lightmapColor.Equals(b.lightmapColor))
                throw new InvalidOperationException(name + " tree " + i + " changed outside the species index.");
        }
    }
}
