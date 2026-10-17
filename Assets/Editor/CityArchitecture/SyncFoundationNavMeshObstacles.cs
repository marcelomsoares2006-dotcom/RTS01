using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class SyncFoundationNavMeshObstacles
{
    private static readonly string[] Prefabs =
    {
        "TownCenter", "ResourceDropoff", "LumberCamp", "MiningCamp", "Mill",
        "Barracks", "ArcheryRange", "Stable", "SiegeWorkshop"
    };

    [MenuItem("Tools/City Architecture/Sync Foundation NavMesh Obstacles")]
    public static void Run()
    {
        var changed = 0;
        foreach (var name in Prefabs)
        {
            var path = "Assets/Resources/Prefabs/Units/Buildings/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) throw new InvalidOperationException("Prefab ausente: " + path);
            try
            {
                var placement = root.GetComponent<BoxCollider>();
                var obstacle = root.GetComponent<NavMeshObstacle>();
                if (placement == null || obstacle == null)
                    throw new InvalidOperationException("Collider/obstacle ausente: " + path);

                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = placement.center;
                obstacle.size = placement.size;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("FOUNDATION_NAVMESH_OBSTACLES_SYNCED=" + changed);
    }
}
