using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Renders the four existing tree meshes with the four era atlases for visual QA.</summary>
public static class EraTreeVisualCheck
{
    // Read-only synthetic terrain check: -quit -executeMethod EraTreeVisualCheck.RunTerrainBillboard
    public static void RunTerrainBillboard()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject prefab = Resources.Load<GameObject>("Prefabs/Doodads/Trees/Tree Type7 01");
        if (prefab == null) throw new InvalidOperationException("Missing map tree prefab.");
        TerrainData data = new TerrainData();
        data.heightmapResolution = 33;
        data.size = new Vector3(128f, 4f, 128f);
        data.treePrototypes = new[] { new TreePrototype { prefab = prefab, bendFactor = 0f } };
        data.SetTreeInstances(new[]
        {
            new TreeInstance { position = new Vector3(0.36f, 0f, 0.25f), prototypeIndex = 0,
                widthScale = 1f, heightScale = 1f, color = Color.white, lightmapColor = Color.white },
            new TreeInstance { position = new Vector3(0.64f, 0f, 0.82f), prototypeIndex = 0,
                widthScale = 1f, heightScale = 1f, color = Color.white, lightmapColor = Color.white }
        }, true);
        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        Terrain terrain = terrainObject.GetComponent<Terrain>();
        terrain.treeDistance = 320f;
        terrain.treeBillboardDistance = 50f;
        terrain.treeMaximumFullLODCount = 50;
        terrain.drawTreesAndFoliage = true;

        GameObject cameraObject = new GameObject("Billboard review camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 55f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.72f, 0.79f, 0.84f);
        camera.transform.position = new Vector3(64f, 70f, -35f);
        camera.transform.LookAt(new Vector3(64f, 0f, 64f));
        GameObject lightObject = new GameObject("Billboard review sun");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        lightObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
        RenderSettings.ambientLight = new Color(0.55f, 0.56f, 0.55f);
        Shader.SetGlobalFloat("_EITreeEra", 1f);

        const int width = 1024;
        const int height = 768;
        RenderTexture target = new RenderTexture(width, height, 24);
        Texture2D pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        camera.targetTexture = target;
        try
        {
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            Directory.CreateDirectory("TestResults/EraTreeVisual");
            File.WriteAllBytes("TestResults/EraTreeVisual/TerrainBillboard.png", pixels.EncodeToPNG());
            Debug.Log("[EraTreeVisualCheck] Synthetic near/far terrain screenshot saved.");
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(data);
            Shader.SetGlobalFloat("_EITreeEra", 0f);
        }
    }

    // Batch: -quit -executeMethod EraTreeVisualCheck.Run
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        const int width = 1024;
        const int height = 768;
        const string folder = "TestResults/EraTreeVisual";
        Directory.CreateDirectory(folder);

        GameObject cameraObject = new GameObject("Era tree review camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 9f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.72f, 0.79f, 0.84f);
        camera.transform.position = new Vector3(0f, 8f, -22f);
        camera.transform.LookAt(new Vector3(0f, 2.5f, 0f));

        GameObject lightObject = new GameObject("Era tree review sun");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
        RenderSettings.ambientLight = new Color(0.55f, 0.56f, 0.55f);

        for (int i = 0; i < 4; i++)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/Doodads/Trees/Tree Type7 0" + (i + 1));
            if (prefab == null) throw new InvalidOperationException("Missing tree prefab " + (i + 1));
            GameObject tree = UnityEngine.Object.Instantiate(prefab);
            tree.transform.position = new Vector3((i - 1.5f) * 5f, 0f, 0f);
        }

        RenderTexture target = new RenderTexture(width, height, 24);
        Texture2D pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        camera.targetTexture = target;
        try
        {
            string[] eras = { "Ancient", "Classical", "Medieval", "Imperial" };
            for (int i = 0; i < eras.Length; i++)
            {
                Shader.SetGlobalFloat("_EITreeEra", i);
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(folder, eras[i] + ".png"), pixels.EncodeToPNG());
            }
            Debug.Log("[EraTreeVisualCheck] Four era images saved to " + folder);
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(target);
            Shader.SetGlobalFloat("_EITreeEra", 0f);
        }
    }
}
