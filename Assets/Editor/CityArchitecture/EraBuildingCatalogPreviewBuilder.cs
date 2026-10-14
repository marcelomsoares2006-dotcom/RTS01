using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EraImperial.Editor.CityArchitecture
{
    /// <summary>
    /// Builds an isolated visual catalog scene for the four era building prefabs.
    /// It never edits production scenes, BuildingData, or the source prefabs.
    /// </summary>
    public static class EraBuildingCatalogPreviewBuilder
    {
        private const string ScenePath = "Assets/Scenes/CityArchitectureBuildingCatalogPreview.unity";
        private const string PrefabFolder = "Assets/CityArchitecture/Prefabs/EraBuildings";
        private static readonly string[] BuildingNames =
        {
            "House_Medieval",
            "Barracks_Medieval",
            "Temple_Imperial",
            "Market_Classical"
        };

        [MenuItem("Tools/City Architecture/Build Building Catalog Preview")]
        public static void BuildCatalogPreview()
        {
            var scene = BuildCatalogPreviewInternal();
            if (scene.IsValid())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        public static Scene BuildCatalogPreviewInternal()
        {
            EnsureSceneDirectory();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "CityArchitectureBuildingCatalogPreview";

            var root = new GameObject("EraBuildingCatalogPreview");
            var catalog = new GameObject("BuildingCatalog");
            catalog.transform.SetParent(root.transform, false);

            var groundMaterial = CreateMaterial("CatalogGround", new Color(0.12f, 0.14f, 0.16f));
            var pedestalMaterial = CreateMaterial("CatalogPedestal", new Color(0.30f, 0.33f, 0.36f));
            var accentMaterial = CreateMaterial("CatalogAccent", new Color(0.58f, 0.40f, 0.16f));

            CreatePrimitive("CatalogGround", PrimitiveType.Plane, new Vector3(0f, -0.06f, 0f), new Vector3(5.6f, 1f, 3.3f), groundMaterial, root.transform);

            var positions = new[]
            {
                new Vector3(-7.0f, 0f, 2.5f),
                new Vector3( 7.0f, 0f, 2.5f),
                new Vector3(-7.0f, 0f,-5.0f),
                new Vector3( 7.0f, 0f,-5.0f)
            };

            var loaded = 0;
            for (var i = 0; i < BuildingNames.Length; i++)
            {
                var name = BuildingNames[i];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");
                if (prefab == null)
                {
                    Debug.LogError($"Building catalog preview missing prefab: {PrefabFolder}/{name}.prefab");
                    continue;
                }

                var slot = new GameObject($"Slot_{name}");
                slot.transform.SetParent(catalog.transform, false);
                slot.transform.position = positions[i];
                CreatePrimitive("Pedestal", PrimitiveType.Cylinder, positions[i] + new Vector3(0f, 0.35f, 0f), new Vector3(3.1f, 0.35f, 3.1f), pedestalMaterial, catalog.transform);
                CreatePrimitive("PedestalAccent", PrimitiveType.Cylinder, positions[i] + new Vector3(0f, 0.54f, 0f), new Vector3(2.6f, 0.06f, 2.6f), accentMaterial, catalog.transform);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = $"{name}_Preview";
                instance.transform.SetParent(slot.transform, true);
                instance.transform.position = positions[i] + new Vector3(0f, 0.62f, 0f);
                loaded++;
            }

            CreateLighting(root.transform);
            CreateCamera(root.transform);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.27f, 0.32f);
            RenderSettings.fog = false;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Era building catalog preview ready: {loaded}/{BuildingNames.Length} prefabs, {ScenePath}");
            return scene;
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return go;
        }

        private static void CreateLighting(Transform parent)
        {
            var lightObject = new GameObject("CatalogKeyLight");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.91f, 0.78f);
            light.shadows = LightShadows.Soft;

            var fillObject = new GameObject("CatalogFillLight");
            fillObject.transform.SetParent(parent, false);
            fillObject.transform.rotation = Quaternion.Euler(24f, 145f, 0f);
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.35f;
            fill.color = new Color(0.66f, 0.78f, 1f);
            fill.shadows = LightShadows.None;
        }

        private static void CreateCamera(Transform parent)
        {
            var cameraObject = new GameObject("CatalogCamera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.position = new Vector3(0f, 18f, -23f);
            cameraObject.transform.rotation = Quaternion.Euler(31f, 0f, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 14.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.085f);
            camera.tag = "MainCamera";
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var shader = Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            material.SetFloat("_Glossiness", 0.28f);
            return material;
        }

        private static void EnsureSceneDirectory()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
        }
    }
}
