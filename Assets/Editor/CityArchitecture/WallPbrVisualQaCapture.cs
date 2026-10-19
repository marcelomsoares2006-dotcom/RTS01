using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

/// <summary>
/// Disposable, scene-in-memory comparison for wall materials. It never saves a
/// scene, prefab, material, texture, or gameplay asset. Production and candidate
/// instances are rendered side by side so the evidence is directly comparable.
/// </summary>
public static class WallPbrVisualQaCapture
{
    private const string OutputRoot = "TestResults/WallPbrVisualQA-2026-10-19";
    private static readonly string[] Eras = { "Ancient", "Classical", "Medieval", "Imperial" };

    [MenuItem("Tools/City Architecture/Capture Wall PBR QA")]
    public static void Capture()
    {
        Directory.CreateDirectory(OutputRoot);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cleanup = new List<UnityEngine.Object>();

        try
        {
            ConfigureRenderSettings();
            CreateLighting(cleanup);
            CreateGround(cleanup);
            BuildComparison(scene, cleanup);
            var camera = CreateCamera(cleanup);

            CaptureView(camera, new Vector3(11f, 6.4f, -12f), new Vector3(0f, 1.8f, 0.1f), "wall-pbr-comparison-close-ancient.png");
            CaptureView(camera, new Vector3(15f, 8.8f, -19f), new Vector3(0f, 2.0f, 3.7f), "wall-pbr-comparison-mid-ancient-classical.png");
            CaptureView(camera, new Vector3(24f, 13.5f, -34f), new Vector3(0f, 2.2f, 10.5f), "wall-pbr-comparison-far-all-eras.png");
            CaptureView(camera, new Vector3(0f, 15.5f, -28f), new Vector3(0f, 2.0f, 10.5f), "wall-pbr-comparison-grid.png");

            WriteManifest();
            Debug.Log("WALL_PBR_VISUAL_QA_PASS output=" + Path.GetFullPath(OutputRoot));
        }
        finally
        {
            foreach (var item in cleanup)
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    private static void ConfigureRenderSettings()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.30f, 0.38f, 0.50f);
        RenderSettings.ambientEquatorColor = new Color(0.26f, 0.27f, 0.25f);
        RenderSettings.ambientGroundColor = new Color(0.08f, 0.075f, 0.065f);
        RenderSettings.fog = false;
    }

    private static void CreateLighting(List<UnityEngine.Object> cleanup)
    {
        var keyObject = new GameObject("Wall PBR QA Key");
        cleanup.Add(keyObject);
        var key = keyObject.AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 1.25f;
        key.color = new Color(1.0f, 0.88f, 0.72f);
        key.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

        var fillObject = new GameObject("Wall PBR QA Fill");
        cleanup.Add(fillObject);
        var fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.32f;
        fill.color = new Color(0.48f, 0.62f, 0.86f);
        fill.transform.rotation = Quaternion.Euler(25f, 145f, 0f);
    }

    private static void CreateGround(List<UnityEngine.Object> cleanup)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Wall PBR QA Ground";
        ground.transform.position = new Vector3(0f, -0.02f, 10.5f);
        ground.transform.localScale = new Vector3(5.0f, 1f, 3.1f);
        var renderer = ground.GetComponent<Renderer>();
        renderer.sharedMaterial = CreateRuntimeMaterial("Wall PBR QA Ground", new Color(0.105f, 0.12f, 0.105f), 0.05f);
        cleanup.Add(renderer.sharedMaterial);
        cleanup.Add(ground);
        UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
    }

    private static void BuildComparison(Scene scene, List<UnityEngine.Object> cleanup)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityArchitecture/Prefabs/WallStraight.prefab");
        if (prefab == null) throw new FileNotFoundException("WallStraight.prefab ausente.");

        for (int row = 0; row < Eras.Length; row++)
        {
            string era = Eras[row];
            var production = InstantiateWall(prefab, scene, era, false, row, cleanup);
            var candidate = InstantiateWall(prefab, scene, era, true, row, cleanup);
            AddLabel("PRODUCTION", new Vector3(-3.6f, 4.75f + row * 0.05f, row * 7f), Color.white, cleanup);
            AddLabel("PBR CANDIDATE", new Vector3(3.6f, 4.75f + row * 0.05f, row * 7f), Color.white, cleanup);
            AddLabel(era.ToUpperInvariant(), new Vector3(0f, 0.03f, row * 7f - 0.55f), new Color(0.88f, 0.86f, 0.78f), cleanup);
        }
    }

    private static GameObject InstantiateWall(GameObject prefab, Scene scene, string era, bool candidate, int row, List<UnityEngine.Object> cleanup)
    {
        var wall = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        SceneManager.MoveGameObjectToScene(wall, scene);
        wall.name = (candidate ? "Candidate_" : "Production_") + era;
        wall.transform.position = new Vector3(candidate ? 3.6f : -3.6f, 0f, row * 7f);

        var eraRoot = wall.transform.Find(era);
        if (eraRoot == null) throw new InvalidOperationException("Era ausente no prefab: " + era);
        foreach (Transform child in wall.transform)
            child.gameObject.SetActive(child == eraRoot);

        string materialPath = candidate
            ? "Assets/CityArchitecture/Materials/Candidates/Wall_" + era + "_Candidate.mat"
            : "Assets/CityArchitecture/Materials/Wall_" + era + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null) throw new FileNotFoundException("Material ausente: " + materialPath);
        foreach (var renderer in eraRoot.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
        }
        cleanup.Add(wall);
        return wall;
    }

    private static Camera CreateCamera(List<UnityEngine.Object> cleanup)
    {
        var cameraObject = new GameObject("Wall PBR QA Camera");
        cleanup.Add(cameraObject);
        var camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 43f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 120f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.20f, 0.28f, 0.38f);
        return camera;
    }

    private static void CaptureView(Camera camera, Vector3 position, Vector3 target, string filename)
    {
        camera.transform.position = position;
        camera.transform.LookAt(target);
        const int width = 1920;
        const int height = 1080;
        var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(OutputRoot, filename), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void AddLabel(string text, Vector3 position, Color color, List<UnityEngine.Object> cleanup)
    {
        var label = new GameObject("Label_" + text);
        label.transform.position = position;
        label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var mesh = label.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.fontSize = 28;
        mesh.characterSize = 0.055f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = color;
        cleanup.Add(label);
    }

    private static Material CreateRuntimeMaterial(string name, Color color, float smoothness)
    {
        var material = new Material(Shader.Find("Standard"));
        material.name = name;
        material.color = color;
        material.SetFloat("_Glossiness", smoothness);
        return material;
    }

    private static void WriteManifest()
    {
        var manifest = "{\n" +
            "  \"status\": \"PASS_RUNTIME_CANDIDATE_COMPARISON_ONLY\",\n" +
            "  \"output\": \"TestResults/WallPbrVisualQA-2026-10-19\",\n" +
            "  \"renderer\": \"Unity 6000 built-in Standard, 1920x1080, scene-in-memory\",\n" +
            "  \"views\": [\"close-ancient\", \"mid-ancient-classical\", \"far-all-eras\", \"grid\"],\n" +
            "  \"rows\": [\"Ancient\", \"Classical\", \"Medieval\", \"Imperial\"],\n" +
            "  \"columns\": [\"production\", \"candidate\"],\n" +
            "  \"productionWrites\": false,\n" +
            "  \"candidatePromotion\": false,\n" +
            "  \"runtimeSeamsOrShaderErrors\": \"inspect PNGs and Unity log before any promotion\"\n" +
            "}\n";
        File.WriteAllText(Path.Combine(OutputRoot, "manifest.json"), manifest);
    }
}
