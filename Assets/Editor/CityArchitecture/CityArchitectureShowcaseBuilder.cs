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
        var showcaseMaterials = EnsureShowcaseMaterials();
        AddTownCenterVisualCandidate(scene, showcaseMaterials);
        string[] buildingCandidates = { "House_Medieval", "Barracks_Medieval", "Market_Classical", "Temple_Imperial" };
        var candidatePositions = new[]
        {
            new Vector3(4f, 0f, 17f),
            new Vector3(12f, 0f, 17f),
            new Vector3(17f, 0f, 6f),
            new Vector3(19f, 0f, 15f)
        };
        for (int i = 0; i < buildingCandidates.Length; i++)
        {
            string path = "Assets/CityArchitecture/Prefabs/EraBuildings/" + buildingCandidates[i] + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new System.IO.FileNotFoundException("Prefab candidato ausente", path);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = buildingCandidates[i] + "_Candidate_" + i;
            instance.transform.position = candidatePositions[i];
            // Os candidatos têm pivôs internos deslocados nos FBX; manter a escala de showcase
            // evita que partes flutuem e dominem o quadro. A normalização de pivô fica para authoring 3D.
            instance.transform.localScale = Vector3.one * 0.18f;
            ApplyShowcaseMaterials(instance, showcaseMaterials);
        }
        AddShowcaseRoads(scene, showcaseMaterials);

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

    private static void AddTownCenterVisualCandidate(Scene scene, Dictionary<string, Material> materials)
    {
        const string path = "Assets/Resources/Prefabs/Units/Buildings/TownCenter.prefab";
        var source = PrefabUtility.LoadPrefabContents(path);
        if (source == null) throw new System.IO.FileNotFoundException("Prefab Town Center ausente", path);
        try
        {
            var mesh = source.transform.Find("Mesh");
            if (mesh == null) throw new System.InvalidOperationException("Mesh do Town Center ausente.");
            var visual = (GameObject)Object.Instantiate(mesh.gameObject);
            SceneManager.MoveGameObjectToScene(visual, scene);
            visual.name = "TownCenter_VisualCandidate";
            visual.transform.position = new Vector3(7f, 0f, 7f);
            visual.transform.localScale = Vector3.one * 0.55f;
            ApplyShowcaseMaterials(visual, materials);

            var props = new GameObject("TownCenter_ShowcaseProps");
            props.transform.position = visual.transform.position;
            AddShowcaseBlock(props.transform, new Vector3(-2.5f, 0.45f, 1.9f), new Vector3(0.9f, 0.9f, 0.9f), materials["Wood"]);
            AddShowcaseBlock(props.transform, new Vector3(2.3f, 0.35f, -1.8f), new Vector3(1.2f, 0.7f, 0.8f), materials["Trim"]);
            AddShowcaseBlock(props.transform, new Vector3(0f, 1.8f, 2.5f), new Vector3(0.12f, 3.6f, 0.12f), materials["Wood"]);
            AddShowcaseBlock(props.transform, new Vector3(0f, 2.9f, 2.5f), new Vector3(0.9f, 0.55f, 0.05f), materials["Trim"]);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(source);
        }
    }

    private static void AddShowcaseBlock(Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "ShowcaseProp";
        block.transform.SetParent(parent, false);
        block.transform.localPosition = localPosition;
        block.transform.localScale = localScale;
        block.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(block.GetComponent<Collider>());
    }

    public static void BuildAndCapture()
    {
        Build();
        Capture();
    }

    public static void BuildAndCaptureViews()
    {
        Build();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var camera = Object.FindFirstObjectByType<Camera>();
        if (camera == null) throw new System.InvalidOperationException("Câmera showcase ausente.");
        CaptureView(camera, new Vector3(31f, 30f, -36f), new Vector3(10f, 2f, 9f), "TestResults/CityArchitectureShowcase_Overview.png");
        CaptureView(camera, new Vector3(22f, 16f, -20f), new Vector3(10f, 2f, 9f), "TestResults/CityArchitectureShowcase_Mid.png");
        CaptureView(camera, new Vector3(15f, 10f, -4f), new Vector3(7f, 2f, 7f), "TestResults/CityArchitectureShowcase_Close.png");
        Debug.Log("SHOWCASE_MULTI_VIEW_CAPTURED=3");
    }

    private static void CaptureView(Camera camera, Vector3 position, Vector3 target, string relativeOutput)
    {
        camera.transform.position = position;
        camera.transform.LookAt(target);
        string output = Path.GetFullPath(relativeOutput);
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
        Debug.Log("Showcase view capturada em " + output);
    }

    private static void AddShowcaseRoads(Scene scene, Dictionary<string, Material> materials)
    {
        var roads = new GameObject("Showcase_Roads");
        SceneManager.MoveGameObjectToScene(roads, scene);
        AddShowcaseBlock(roads.transform, new Vector3(10f, 0.015f, 9f), new Vector3(22f, 0.03f, 1.4f), materials["Stone"]);
        AddShowcaseBlock(roads.transform, new Vector3(10f, 0.02f, 9f), new Vector3(1.4f, 0.04f, 20f), materials["Stone"]);
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
            material.color = ShowcaseColor(pair.Key);
            material.SetFloat("_Glossiness", pair.Key == "Trim" ? 0.42f : 0.22f);
            material.SetFloat("_Metallic", pair.Key == "Trim" ? 0.25f : 0f);
            EditorUtility.SetDirty(material);
            result[pair.Key] = material;
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    private static Color ShowcaseColor(string key)
    {
        if (key == "Stone") return new Color(0.68f, 0.62f, 0.52f, 1f);
        if (key == "Wood") return new Color(0.48f, 0.25f, 0.12f, 1f);
        if (key == "Roof") return new Color(0.34f, 0.12f, 0.06f, 1f);
        return new Color(0.82f, 0.55f, 0.12f, 1f);
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
