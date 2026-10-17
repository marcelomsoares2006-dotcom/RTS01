using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

/// <summary>
/// Piloto reversível de PBR para um único prefab de produção: House.
/// PrepareAndCapture não salva o prefab; Promote aplica somente este piloto.
/// </summary>
public static class HousePbrPilotAuthoring
{
    private const string PrefabPath = "Assets/Resources/Prefabs/Units/Buildings/House.prefab";
    private const string MaterialFolder = "Assets/Resources/Materials/Building/HousePilotPBR";
    private const string TextureFolder = "Assets/Resources/Textures/Buildings/HousePilotPBR";
    private const string OutputFolder = "TestResults/HousePBRPilot";

    [MenuItem("Tools/City Architecture/House PBR Pilot Prepare and Capture")]
    public static void PrepareAndCapture()
    {
        PrepareMaterials();
        CaptureComparison();
    }

    [MenuItem("Tools/City Architecture/House PBR Pilot Promote")]
    public static void Promote()
    {
        var wood = LoadMaterial("HousePilot_Wood.mat");
        var roof = LoadMaterial("HousePilot_Roof.mat");
        var floor = LoadMaterial("HousePilot_WoodFloor.mat");
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var renderer = root.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.gameObject.name == "Mesh");
            if (renderer == null) throw new InvalidOperationException("House PBR pilot: Mesh renderer não encontrado.");
            var materials = renderer.sharedMaterials;
            if (materials.Length != 4) throw new InvalidOperationException("House PBR pilot: o piloto espera exatamente 4 slots.");
            materials[0] = wood;
            materials[1] = roof;
            materials[3] = floor;
            renderer.sharedMaterials = materials;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        File.AppendAllText(Path.Combine(OutputFolder, "authoring.log"), "HOUSE_PBR_PILOT_PROMOTED=true\n");
        Debug.Log("HOUSE_PBR_PILOT_PROMOTED");
    }

    private static void PrepareMaterials()
    {
        Directory.CreateDirectory(OutputFolder);
        EnsureFolder(MaterialFolder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureTexture("HousePilot_Wood", true, false);
        ConfigureTexture("HousePilot_Roof", true, false);
        ConfigureTexture("HousePilot_Wood_Normal", false, true);
        ConfigureTexture("HousePilot_Wood_MetallicGloss", false, false);
        ConfigureTexture("HousePilot_Wood_AO", false, false);
        ConfigureTexture("HousePilot_Roof_Normal", false, true);
        ConfigureTexture("HousePilot_Roof_MetallicGloss", false, false);
        ConfigureTexture("HousePilot_Roof_AO", false, false);

        CreateOrUpdateMaterial("HousePilot_Wood.mat", "HousePilot_Wood", "HousePilot_Wood", 1.0f, 0.46f, 0.76f, 0.24f);
        CreateOrUpdateMaterial("HousePilot_WoodFloor.mat", "HousePilot_Wood", "HousePilot_Wood", 1.35f, 0.40f, 0.72f, 0.24f);
        CreateOrUpdateMaterial("HousePilot_Roof.mat", "HousePilot_Roof", "HousePilot_Roof", 1.0f, 0.38f, 0.78f, 0.30f);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void CreateOrUpdateMaterial(string fileName, string profile, string textureProfile, float tiling, float bumpScale, float occlusion, float glossiness)
    {
        var path = MaterialFolder + "/" + fileName;
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard")) { name = Path.GetFileNameWithoutExtension(fileName) };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = Shader.Find("Standard");
        material.SetTexture("_MainTex", LoadTexture("HousePilot_" + textureProfile + "_Albedo.png"));
        material.SetTexture("_BumpMap", LoadTexture("HousePilot_" + textureProfile + "_Normal.png"));
        material.SetTexture("_MetallicGlossMap", LoadTexture("HousePilot_" + textureProfile + "_MetallicGloss.png"));
        material.SetTexture("_OcclusionMap", LoadTexture("HousePilot_" + textureProfile + "_AO.png"));
        material.SetTextureScale("_MainTex", Vector2.one * tiling);
        material.SetTextureScale("_BumpMap", Vector2.one * tiling);
        material.SetTextureScale("_MetallicGlossMap", Vector2.one * tiling);
        material.SetTextureScale("_OcclusionMap", Vector2.one * tiling);
        material.SetColor("_Color", Color.white);
        material.SetFloat("_Metallic", 0.0f);
        material.SetFloat("_Glossiness", glossiness);
        material.SetFloat("_BumpScale", bumpScale);
        material.SetFloat("_OcclusionStrength", occlusion);
        material.SetFloat("_GlossMapScale", 1.0f);
        material.SetFloat("_SmoothnessTextureChannel", 0.0f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
    }

    private static void ConfigureTexture(string stem, bool srgb, bool normal)
    {
        var path = TextureFolder + "/" + stem + (stem.EndsWith("_Normal") || stem.EndsWith("_MetallicGloss") || stem.EndsWith("_AO") ? ".png" : "_Albedo.png");
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("House PBR pilot: textura ausente: " + path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = srgb;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    private static void CaptureComparison()
    {
        Directory.CreateDirectory(OutputFolder);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene) as GameObject;
        if (root == null) throw new InvalidOperationException("House PBR pilot: prefab não instanciou.");
        root.transform.position = Vector3.zero;
        var renderer = root.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.gameObject.name == "Mesh");
        if (renderer == null) throw new InvalidOperationException("House PBR pilot: renderer não encontrado.");
        var camera = CreateCamera();
        CreateLighting();
        Render(camera, "baseline.png");
        var materials = renderer.sharedMaterials;
        materials[0] = LoadMaterial("HousePilot_Wood.mat");
        materials[1] = LoadMaterial("HousePilot_Roof.mat");
        materials[3] = LoadMaterial("HousePilot_WoodFloor.mat");
        renderer.sharedMaterials = materials;
        Render(camera, "candidate.png");
        File.WriteAllText(Path.Combine(OutputFolder, "capture.log"),
            "HOUSE_PBR_PILOT_PREVIEW_PASSED\n" +
            "prefab=" + PrefabPath + "\n" +
            "prefab_saved=false\n" +
            "shader=Standard\n" +
            "baseline=baseline.png\n" +
            "candidate=candidate.png\n");
        UnityEngine.Object.DestroyImmediate(camera.gameObject);
        UnityEngine.Object.DestroyImmediate(root);
        SceneManager.UnloadSceneAsync(scene);
        Debug.Log("HOUSE_PBR_PILOT_PREVIEW_PASSED");
    }

    private static Camera CreateCamera()
    {
        var go = new GameObject("HousePbrPilotCamera");
        var camera = go.AddComponent<Camera>();
        camera.fieldOfView = 34.0f;
        camera.aspect = 16.0f / 9.0f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100.0f;
        camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f, 1.0f);
        go.transform.position = new Vector3(8.6f, 6.4f, 8.6f);
        go.transform.LookAt(new Vector3(0.0f, 1.0f, 0.0f));
        return camera;
    }

    private static void CreateLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.31f, 0.37f, 0.48f);
        RenderSettings.ambientEquatorColor = new Color(0.18f, 0.17f, 0.14f);
        RenderSettings.ambientGroundColor = new Color(0.07f, 0.06f, 0.045f);
        var go = new GameObject("HousePbrPilotKeyLight");
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.35f;
        light.color = new Color(1.0f, 0.86f, 0.70f);
        go.transform.rotation = Quaternion.Euler(42.0f, -34.0f, 0.0f);
    }

    private static void Render(Camera camera, string fileName)
    {
        const int width = 1280;
        const int height = 720;
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply(false, false);
        File.WriteAllBytes(Path.Combine(OutputFolder, fileName), image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        camera.targetTexture = null;
        RenderTexture.active = previous;
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
    }

    private static Material LoadMaterial(string fileName)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + fileName);
        if (material == null) throw new InvalidOperationException("House PBR pilot: material ausente: " + fileName);
        return material;
    }

    private static Texture2D LoadTexture(string fileName)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/" + fileName);
        if (texture == null) throw new InvalidOperationException("House PBR pilot: mapa ausente: " + fileName);
        return texture;
    }

    private static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
