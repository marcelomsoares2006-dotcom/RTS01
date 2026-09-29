using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Isolated import smoke test for Blender-generated character prototypes.</summary>
public static class CharacterRiggingImportAudit
{
    private const string Folder = "Assets/CharacterRigging/Prototypes";

    [MenuItem("Tools/Character Rigging/Run Import Audit")]
    public static void Run()
    {
        var report = new StringBuilder();
        report.AppendLine("Era Imperial character FBX import audit");
        report.AppendLine("Editor: " + Application.unityVersion);
        var failures = 0;

        foreach (var species in new[] { "Human", "Horse", "Wolf" })
        {
            var path = $"{Folder}/{species}_SkinnedPrototype.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                report.AppendLine($"FAIL {species}: ModelImporter not found ({path})");
                failures++;
                continue;
            }

            importer.importAnimation = species == "Human";
            importer.animationType = species == "Human"
                ? ModelImporterAnimationType.Human
                : ModelImporterAnimationType.Generic;
            importer.optimizeGameObjects = false;
            importer.SaveAndReimport();

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var renderers = root == null ? Array.Empty<SkinnedMeshRenderer>() : root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null && root != null) avatar = root.GetComponent<Animator>()?.avatar;
            var mesh = renderers.FirstOrDefault()?.sharedMesh;
            var validAvatar = avatar != null && avatar.isValid;
            var humanAvatar = validAvatar && avatar.isHuman;
            var passed = renderers.Length > 0 && mesh != null &&
                         (species != "Human" || humanAvatar) &&
                         (species == "Human" || renderers[0].bones.Length > 0);
            if (!passed) failures++;

            report.AppendLine($"{(passed ? "PASS" : "FAIL")} {species}: type={importer.animationType}, " +
                $"avatarValid={validAvatar}, avatarHuman={humanAvatar}, " +
                $"skinnedRenderers={renderers.Length}, vertices={(mesh == null ? 0 : mesh.vertexCount)}, " +
                $"bones={(renderers.FirstOrDefault()?.bones.Length ?? 0)}, " +
                $"materials={(renderers.FirstOrDefault()?.sharedMaterials.Length ?? 0)}");
        }

        var destination = "Docs/CHARACTER_RIGGING_IMPORT_AUDIT.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.WriteAllText(destination, report.ToString());
        Debug.Log(report.ToString());
        if (failures > 0) throw new Exception($"Character rig import audit failed for {failures} prototype(s). See {destination}");
    }
}
