using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CityDensePerformanceAudit
{
    private const string ReportPath = "TestResults/CityDensePerformanceAudit.txt";
    private static readonly string[] PrefabPaths =
    {
        "Assets/CityArchitecture/Prefabs/WallStraight.prefab",
        "Assets/CityArchitecture/Prefabs/WallCorner.prefab",
        "Assets/CityArchitecture/Prefabs/Gatehouse.prefab",
        "Assets/CityArchitecture/Prefabs/EraBuildings/House_Medieval.prefab",
        "Assets/CityArchitecture/Prefabs/EraBuildings/Barracks_Medieval.prefab"
    };

    [MenuItem("Tools/City Architecture/Audit Dense City Complexity")]
    public static void Run()
    {
        Directory.CreateDirectory("TestResults");
        var report = new StringBuilder();
        var spawned = new List<GameObject>();
        var renderers = new List<Renderer>();
        var meshes = new HashSet<Mesh>();
        var materialCount = new HashSet<Material>();
        var triangles = 0;
        var colliders = 0;
        var lodGroups = 0;

        report.AppendLine("Era Imperial - Dense City Complexity Audit");
        report.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
        report.AppendLine("Modo: instanciacao temporaria em memoria; nenhuma cena ou prefab foi salvo.");
        report.AppendLine();

        for (var i = 0; i < 100; i++)
        {
            var path = PrefabPaths[i % PrefabPaths.Length];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new FileNotFoundException("Prefab ausente", path);
            var instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = "DenseAudit_" + i;
            instance.transform.position = new Vector3((i % 10) * 8f, 0f, (i / 10) * 8f);
            spawned.Add(instance);
        }

        foreach (var root in spawned)
        {
            renderers.AddRange(root.GetComponentsInChildren<Renderer>(true));
            colliders += root.GetComponentsInChildren<Collider>(true).Length;
            lodGroups += root.GetComponentsInChildren<LODGroup>(true).Length;
        }
        foreach (var renderer in renderers)
        {
            foreach (var material in renderer.sharedMaterials)
                if (material != null) materialCount.Add(material);
            if (renderer is MeshRenderer meshRenderer)
            {
                var filter = meshRenderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    meshes.Add(filter.sharedMesh);
                    triangles += CountTriangles(filter.sharedMesh);
                }
            }
            else if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                meshes.Add(skinned.sharedMesh);
                triangles += CountTriangles(skinned.sharedMesh);
            }
        }

        report.AppendLine("Instancias temporarias: " + spawned.Count);
        report.AppendLine("Renderers: " + renderers.Count);
        report.AppendLine("Meshes distintos: " + meshes.Count);
        report.AppendLine("Triangulos totais: " + triangles);
        report.AppendLine("Materiais distintos: " + materialCount.Count);
        report.AppendLine("Colliders: " + colliders);
        report.AppendLine("LODGroups: " + lodGroups);
        report.AppendLine("Aviso: esta e uma estimativa de complexidade; FPS real exige player standalone com cidade densa.");

        foreach (var root in spawned)
            UnityEngine.Object.DestroyImmediate(root);
        File.WriteAllText(ReportPath, report.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log("Dense city audit concluido: " + ReportPath);
    }

    private static int CountTriangles(Mesh mesh)
    {
        var count = 0;
        for (var i = 0; i < mesh.subMeshCount; i++)
            count += (int)mesh.GetIndexCount(i) / 3;
        return count;
    }
}
