using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Read-only audit for the four EraBuildings prefabs.
/// It never saves, reimports, or modifies the audited prefabs.
/// </summary>
public static class BuildingMeshAudit
{
    private const string PrefabFolder = "Assets/CityArchitecture/Prefabs/EraBuildings";
    private const string ReportPath = "TestResults/BuildingMeshAudit.txt";

    private static readonly string[] ExpectedPrefabs =
    {
        "House_Medieval.prefab",
        "Barracks_Medieval.prefab",
        "Temple_Imperial.prefab",
        "Market_Classical.prefab"
    };

    [MenuItem("Tools/City Architecture/Audit Era Building Meshes")]
    public static void Run()
    {
        Directory.CreateDirectory("TestResults");

        var report = new StringBuilder();
        var failures = new List<string>();
        var prefabCount = 0;
        var meshCount = 0;
        var triangleCount = 0;

        report.AppendLine("Era Imperial - Building Mesh Audit");
        report.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
        report.AppendLine("Escopo: quatro prefabs em " + PrefabFolder);
        report.AppendLine("Modo: somente leitura; nenhum prefab foi alterado.");
        report.AppendLine();

        foreach (var fileName in ExpectedPrefabs)
        {
            var path = PrefabFolder + "/" + fileName;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                AddFailure(failures, report, path + ": prefab não encontrado");
                continue;
            }

            prefabCount++;
            var meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
            var skinned = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var meshes = new List<Mesh>();

            foreach (var filter in meshFilters)
                if (filter.sharedMesh != null) meshes.Add(filter.sharedMesh);
            foreach (var renderer in skinned)
                if (renderer.sharedMesh != null) meshes.Add(renderer.sharedMesh);

            report.AppendLine("PREFAB: " + path);
            report.AppendLine("  MeshFilters: " + meshFilters.Length + "; SkinnedMeshRenderers: " + skinned.Length);
            report.AppendLine("  Renderers: " + renderers.Length + "; meshes: " + meshes.Count);

            if (meshes.Count == 0)
                AddFailure(failures, report, "  ERRO: nenhum mesh encontrado");

            foreach (var mesh in meshes.Distinct())
            {
                meshCount++;
                var triangles = CountTriangles(mesh);
                triangleCount += triangles;
                report.AppendLine("  MESH: " + mesh.name + " | vertices=" + mesh.vertexCount + " | triangles=" + triangles);

                if (mesh.vertexCount == 0)
                    AddFailure(failures, report, "    ERRO: mesh sem vértices: " + mesh.name);
                AuditUv(mesh, 0, "UV0", path, report, failures);
                AuditUv(mesh, 1, "UV2", path, report, failures);
            }

            AuditMaterials(path, renderers, report, failures);
            report.AppendLine();
        }

        report.AppendLine("RESUMO");
        report.AppendLine("  Prefabs encontrados: " + prefabCount + "/" + ExpectedPrefabs.Length);
        report.AppendLine("  Meshes auditados: " + meshCount);
        report.AppendLine("  Triângulos totais: " + triangleCount);
        report.AppendLine("  Falhas: " + failures.Count);
        report.AppendLine(failures.Count == 0 ? "RESULTADO: PASS" : "RESULTADO: FAIL");

        File.WriteAllText(ReportPath, report.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log("Building mesh audit concluído: " + ReportPath + " | " + (failures.Count == 0 ? "PASS" : "FAIL"));
    }

    private static void AuditUv(Mesh mesh, int channel, string label, string prefabPath, StringBuilder report, List<string> failures)
    {
        var values = new List<Vector2>();
        mesh.GetUVs(channel, values);
        var expected = mesh.vertexCount;
        report.AppendLine("    " + label +  ": " + values.Count + "/" + expected + " coordenadas");

        if (values.Count != expected)
        {
            AddFailure(failures, report, "    ERRO: " + label + " incompleta em " + mesh.name + " (" + prefabPath + ")");
            return;
        }

        for (var i = 0; i < values.Count; i++)
        {
            var uv = values[i];
            if (float.IsNaN(uv.x) || float.IsNaN(uv.y) || float.IsInfinity(uv.x) || float.IsInfinity(uv.y))
            {
                AddFailure(failures, report, "    ERRO: " + label + " não finita em " + mesh.name + " índice " + i);
                return;
            }
            if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f)
            {
                AddFailure(failures, report, "    ERRO: " + label + " fora de 0-1 em " + mesh.name + " índice " + i + " valor=" + uv);
                return;
            }
        }
    }

    private static void AuditMaterials(string prefabPath, Renderer[] renderers, StringBuilder report, List<string> failures)
    {
        var materialSlots = 0;
        var missingMaterials = 0;
        var texturedMaterials = 0;

        foreach (var renderer in renderers)
        {
            foreach (var material in renderer.sharedMaterials)
            {
                materialSlots++;
                if (material == null)
                {
                    missingMaterials++;
                    continue;
                }

                var textureCount = 0;
                foreach (var property in material.GetTexturePropertyNames())
                    if (material.GetTexture(property) != null) textureCount++;
                if (textureCount > 0) texturedMaterials++;
            }
        }

        report.AppendLine("  MATERIAIS: slots=" + materialSlots + "; sem material=" + missingMaterials + "; com texturas=" + texturedMaterials);
        if (materialSlots == 0)
            AddFailure(failures, report, "  ERRO: nenhum material encontrado em " + prefabPath);
        if (missingMaterials > 0)
            AddFailure(failures, report, "  ERRO: slots de material nulos em " + prefabPath);
    }

    private static int CountTriangles(Mesh mesh)
    {
        var count = 0;
        for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            count += (int)mesh.GetIndexCount(subMesh) / 3;
        return count;
    }

    private static void AddFailure(List<string> failures, StringBuilder report, string message)
    {
        failures.Add(message);
        report.AppendLine(message);
    }
}
