using System;
using System.IO;
using EraImperial.Units.Advanced;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Isolated renders of completed House and Tower without gameplay/save side effects.</summary>
public static class BuildingEraVisualCheck
{
    // Batch: -quit -executeMethod BuildingEraVisualCheck.Run
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        const int width = 1024;
        const int height = 768;
        const string folder = "TestResults/BuildingEraVisual";
        Directory.CreateDirectory(folder);

        for (int i = 0; i < 2; i++)
        {
            string name = i == 0 ? "House" : "Tower";
            BuildingData data = Resources.Load<BuildingData>("ScriptableObjects/Units/Buildings/" + name);
            GameObject prefab = Resources.Load<GameObject>("Prefabs/Units/Buildings/" + name);
            if (data == null || prefab == null || data.constructionMeshes == null || data.constructionMeshes.Length == 0)
                throw new InvalidOperationException("Missing completed building mesh: " + name);

            GameObject root = new GameObject(name);
            root.transform.position = new Vector3(i == 0 ? -4f : 4f, 0f, 0f);
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = data.constructionMeshes[data.constructionMeshes.Length - 1];
            visual.AddComponent<MeshRenderer>().sharedMaterials = prefab.GetComponent<BuildingManager>().meshRenderer.sharedMaterials;
            var details = root.AddComponent<BuildingEraDecoration>();
            details.Initialize(name.ToLowerInvariant(), visual.transform, visual.GetComponent<MeshFilter>().sharedMesh);
            details.SetFinished(true);
        }

        GameObject cameraObject = new GameObject("Building review camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 6.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.72f, 0.79f, 0.84f);
        camera.transform.position = new Vector3(0f, 7f, -17f);
        camera.transform.LookAt(new Vector3(0f, 2f, 0f));

        GameObject lightObject = new GameObject("Building review sun");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
        RenderSettings.ambientLight = new Color(0.55f, 0.56f, 0.55f);

        RenderTexture target = new RenderTexture(width, height, 24);
        Texture2D pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        camera.targetTexture = target;
        try
        {
            foreach (ImperialEra era in ImperialEras.Ordered)
            {
                AdvancedEraState.Set(era);
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(folder, era + ".png"), pixels.EncodeToPNG());
            }
            Debug.Log("[BuildingEraVisualCheck] Four era images saved to " + folder);
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(target);
            AdvancedEraState.Reset();
        }
    }
}
