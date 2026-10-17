using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;

public static class CityArchitectureShowcaseBuilder
{
    private const string ScenePath = "Assets/Scenes/CityArchitectureShowcase.unity";

    [MenuItem("Tools/City Architecture/Build Showcase Scene")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var lightGo = new GameObject("Showcase Sun");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Showcase Ground";
        ground.transform.position = new Vector3(6f, -0.02f, 6f);
        ground.transform.localScale = new Vector3(4.5f, 1f, 4.5f);
        Object.DestroyImmediate(ground.GetComponent<Collider>());
        var groundMaterial = new Material(Shader.Find("Standard"));
        groundMaterial.color = new Color(.12f, .14f, .12f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

        var cameraGo = new GameObject("Showcase Camera");
        var camera = cameraGo.AddComponent<Camera>();
        camera.transform.position = new Vector3(24f, 22f, -30f);
        camera.transform.LookAt(new Vector3(9f, 2f, 9f));
        camera.fieldOfView = 42f;
        camera.farClipPlane = 100f;

        string[] pieces = { "WallStraight", "WallCorner", "Gatehouse", "WallTowerConnector" };
        for (int i = 0; i < pieces.Length; i++)
        {
            string path = "Assets/CityArchitecture/Prefabs/" + pieces[i] + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new System.IO.FileNotFoundException("Prefab ausente", path);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = pieces[i] + "_Showcase_" + i;
            instance.transform.position = new Vector3((i % 2) * 12f, 0f, (i / 2) * 12f);
            SelectEra(instance.transform, i);
        }

        // Candidatos visuais reversíveis: não substituem prefabs de produção.
        string[] buildingCandidates = { "House_Medieval", "Barracks_Medieval" };
        var showcaseMaterials = EnsureShowcaseMaterials();
        for (int i = 0; i < buildingCandidates.Length; i++)
        {
            string path = "Assets/CityArchitecture/Prefabs/EraBuildings/" + buildingCandidates[i] + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new System.IO.FileNotFoundException("Prefab candidato ausente", path);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = buildingCandidates[i] + "_Candidate_" + i;
            instance.transform.position = new Vector3(5f + i * 8f, 0f, 17f);
            instance.transform.localScale = Vector3.one * 0.18f;
            ApplyShowcaseMaterials(instance, showcaseMaterials);
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Cena de showcase criada em " + ScenePath);
    }

    public static void Capture()
    {
        if (!File.Exists(ScenePath)) Build();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Camera camera = Object.FindFirstObjectByType<Camera>();
        if (camera == null) throw new System.InvalidOperationException("Câmera showcase ausente.");
        string output = Path.GetFullPath("TestResults/CityArchitectureShowcase.png");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var texture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = texture;
        camera.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        image.Apply();
        File.WriteAllBytes(output, image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(texture);
        Debug.Log("Showcase capturado em " + output);
    }

    public static void BuildAndCapture()
    {
        Build();
        Capture();
    }

    private static Dictionary<string, Material> EnsureShowcaseMaterials()
    {
        var textures = new Dictionary<string, string>
        {
            { "Stone", "Assets/CityArchitecture/Generated/Textures/PBR_Stone.png" },
            { "Wood", "Assets/CityArchitecture/Generated/Textures/PBR_Wood.png" },
            { "Roof", "Assets/CityArchitecture/Generated/Textures/PBR_Roof.png" },
            { "Trim", "Assets/CityArchitecture/Generated/Textures/PBR_Trim.png" }
        };
        var result = new Dictionary<string, Material>();
        foreach (var pair in textures)
        {
            var path = "Assets/CityArchitecture/Materials/Showcase_" + pair.Key + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = "Showcase_" + pair.Key };
                AssetDatabase.CreateAsset(material, path);
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pair.Value);
            material.mainTexture = texture;
            material.SetFloat("_Glossiness", pair.Key == "Trim" ? 0.42f : 0.22f);
            material.SetFloat("_Metallic", pair.Key == "Trim" ? 0.25f : 0f);
            EditorUtility.SetDirty(material);
            result[pair.Key] = material;
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    private static void ApplyShowcaseMaterials(GameObject root, Dictionary<string, Material> materials)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var name = renderer.gameObject.name.ToLowerInvariant();
            var key = name.Contains("roof") ? "Roof" : name.Contains("door") || name.Contains("body") ? "Wood" : name.Contains("banner") ? "Trim" : "Stone";
            renderer.sharedMaterial = materials[key];
        }
    }

    private static void SelectEra(Transform root, int era)
    {
        for (int i = 0; i < root.childCount; i++)
            root.GetChild(i).gameObject.SetActive(i == era);
    }
}
