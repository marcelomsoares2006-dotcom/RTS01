using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
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
        light.color = new Color(1f, 0.86f, 0.70f, 1f);
        light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

        var fillGo = new GameObject("Showcase Fill Light");
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.28f;
        fill.color = new Color(0.46f, 0.62f, 0.86f, 1f);
        fill.transform.rotation = Quaternion.Euler(25f, 145f, 0f);

        var probeGo = new GameObject("Showcase Reflection Probe");
        var probe = probeGo.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.size = new Vector3(34f, 14f, 34f);
        probe.center = new Vector3(10f, 3f, 9f);
        probe.intensity = 0.65f;
        probe.boxProjection = true;

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Showcase Ground";
        ground.transform.position = new Vector3(10f, -0.02f, 10f);
        ground.transform.localScale = new Vector3(5.5f, 1f, 5.5f);
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
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.36f, 0.49f, 0.62f, 1f);

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
        string[] buildingCandidates =
        {
            "House_Medieval", "House_Medieval", "House_Medieval",
            "Barracks_Medieval", "Barracks_Medieval",
            "Market_Classical", "Temple_Imperial", "House_Medieval"
        };
        var candidatePositions = new[]
        {
            new Vector3(3.5f, 0f, 3.5f),
            new Vector3(16.5f, 0f, 3.5f),
            new Vector3(3.5f, 0f, 16.5f),
            new Vector3(16.5f, 0f, 16.5f),
            new Vector3(20.5f, 0f, 9.5f),
            new Vector3(2.5f, 0f, 9.5f),
            new Vector3(10f, 0f, 19.5f),
            new Vector3(18.5f, 0f, 2.5f)
        };
        for (int i = 0; i < buildingCandidates.Length; i++)
        {
            string path = "Assets/CityArchitecture/Prefabs/EraBuildings/" + buildingCandidates[i] + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new System.IO.FileNotFoundException("Prefab candidato ausente", path);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "Showcase_Building_" + buildingCandidates[i] + "_" + i;
            // Os candidatos têm pivôs internos deslocados nos FBX. Esta correção existe
            // apenas na cópia visual da cena: usa os bounds ativos para pousar e centralizar
            // a massa arquitetônica sem tocar no prefab de produção nem no authoring 3D.
            PositionShowcaseObject(instance, candidatePositions[i], 0.32f);
            ApplyShowcaseMaterials(instance, showcaseMaterials);
        }
        AddShowcaseRoads(scene, showcaseMaterials);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        WriteShowcaseMetrics(scene);
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
            PositionShowcaseObject(visual, new Vector3(10f, 0f, 10f), 0.55f);
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
        CaptureView(camera, new Vector3(27f, 24f, -28f), new Vector3(10f, 2f, 9f), "TestResults/CityArchitectureShowcase_Overview.png");
        CaptureView(camera, new Vector3(24f, 15f, -15f), new Vector3(10f, 2f, 9f), "TestResults/CityArchitectureShowcase_Mid.png");
        CaptureView(camera, new Vector3(21f, 14f, -15f), new Vector3(7f, 2f, 7f), "TestResults/CityArchitectureShowcase_Close.png");
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
        AddShowcaseBlock(roads.transform, new Vector3(10f, 0.015f, 10f), new Vector3(23f, 0.03f, 1.45f), materials["Stone"]);
        AddShowcaseBlock(roads.transform, new Vector3(10f, 0.02f, 10f), new Vector3(1.45f, 0.04f, 22f), materials["Stone"]);
        AddShowcaseBlock(roads.transform, new Vector3(5.5f, 0.025f, 10f), new Vector3(7f, 0.05f, 0.9f), materials["Stone"]);
        AddShowcaseBlock(roads.transform, new Vector3(15.5f, 0.025f, 10f), new Vector3(7f, 0.05f, 0.9f), materials["Stone"]);
        var plaza = new GameObject("Showcase_Plaza");
        plaza.transform.SetParent(roads.transform, false);
        AddShowcaseBlock(plaza.transform, new Vector3(10f, 0.045f, 10f), new Vector3(5.6f, 0.05f, 5.6f), materials["Trim"]);
    }

    private static void PositionShowcaseObject(GameObject instance, Vector3 target, float uniformScale)
    {
        instance.transform.position = Vector3.zero;
        instance.transform.localScale = Vector3.one * uniformScale;
        var bounds = CalculateActiveRendererBounds(instance);
        if (!bounds.HasValue)
        {
            instance.transform.position = target;
            return;
        }

        var current = instance.transform.position;
        current += target - new Vector3(bounds.Value.center.x, 0f, bounds.Value.center.z);
        current.y += -bounds.Value.min.y;
        instance.transform.position = current;
    }

    private static Bounds? CalculateActiveRendererBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = default;
        var hasBounds = false;
        foreach (var renderer in renderers)
        {
            if (!renderer.gameObject.activeInHierarchy) continue;
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else bounds.Encapsulate(renderer.bounds);
        }
        return hasBounds ? bounds : (Bounds?)null;
    }

    private static void WriteShowcaseMetrics(Scene scene)
    {
        Directory.CreateDirectory("TestResults");
        var meshes = new HashSet<Mesh>();
        var materials = new HashSet<Material>();
        var roots = scene.GetRootGameObjects();
        var gameObjects = 0;
        var renderers = 0;
        var triangles = 0;
        var buildingCandidates = 0;
        var props = 0;
        Bounds sceneBounds = default;
        var hasSceneBounds = false;

        foreach (var root in roots)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                gameObjects++;
                if (transform.name.StartsWith("Showcase_Building_")) buildingCandidates++;
                if (transform.name == "ShowcaseProp") props++;
            }
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.gameObject.activeInHierarchy) continue;
                renderers++;
                if (!hasSceneBounds)
                {
                    sceneBounds = renderer.bounds;
                    hasSceneBounds = true;
                }
                else sceneBounds.Encapsulate(renderer.bounds);
                foreach (var material in renderer.sharedMaterials)
                    if (material != null) materials.Add(material);
                var meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    meshes.Add(meshFilter.sharedMesh);
                    for (var subMesh = 0; subMesh < meshFilter.sharedMesh.subMeshCount; subMesh++)
                        triangles += (int)meshFilter.sharedMesh.GetIndexCount(subMesh) / 3;
                }
            }
        }

        var lines = new List<string>
        {
            "Era Imperial - CityArchitectureShowcase audit",
            "Escopo: cópia visual da cena; prefabs de produção não são alterados.",
            "Building candidates visuais: " + buildingCandidates,
            "GameObjects na cena: " + gameObjects,
            "Renderers ativos: " + renderers,
            "Meshes distintos: " + meshes.Count,
            "Triângulos ativos estimados: " + triangles,
            "Materiais distintos: " + materials.Count,
            "Props cúbicos showcase: " + props,
            "Bounds da cena: " + (hasSceneBounds ? sceneBounds.ToString() : "n/a"),
            "Gate AoE4 cidade densa: PENDENTE — este showcase é uma prova de composição, não 100+ construções.",
            "Gate AoE4 estabilidade: usa a validação Unity/standalone registrada separadamente."
        };
        File.WriteAllLines("TestResults/CityArchitectureShowcaseAudit.txt", lines);
        Debug.Log("SHOWCASE_METRICS buildings=" + buildingCandidates + " renderers=" + renderers + " triangles=" + triangles);
    }

    private static Dictionary<string, Material> EnsureShowcaseMaterials()
    {
        var textures = new Dictionary<string, string>
        {
            { "Stone", "Assets/CityArchitecture/Generated/Textures/PBR_Stone.png" },
            { "Plaster", "Assets/CityArchitecture/Generated/Textures/PBR_Plaster.png" },
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
            string normalPath = "Assets/CityArchitecture/Generated/Textures/Showcase/Showcase_" + pair.Key + "_Normal.png";
            string metallicGlossPath = "Assets/CityArchitecture/Generated/Textures/Showcase/Showcase_" + pair.Key + "_MetallicGloss.png";
            string aoPath = "Assets/CityArchitecture/Generated/Textures/Showcase/Showcase_" + pair.Key + "_AO.png";
            ConfigureShowcaseMapImporter(normalPath, true);
            ConfigureShowcaseMapImporter(metallicGlossPath, false);
            ConfigureShowcaseMapImporter(aoPath, false);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            var metallicGloss = AssetDatabase.LoadAssetAtPath<Texture2D>(metallicGlossPath);
            var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(aoPath);
            if (normal != null) material.SetTexture("_BumpMap", normal);
            if (metallicGloss != null) material.SetTexture("_MetallicGlossMap", metallicGloss);
            if (ao != null) material.SetTexture("_OcclusionMap", ao);
            material.color = ShowcaseColor(pair.Key);
            material.SetFloat("_Glossiness", pair.Key == "Trim" ? 0.42f : 0.22f);
            material.SetFloat("_Metallic", pair.Key == "Trim" ? 0.25f : 0f);
            material.SetFloat("_BumpScale", pair.Key == "Trim" ? 0.35f : 0.65f);
            material.SetFloat("_OcclusionStrength", 0.72f);
            EditorUtility.SetDirty(material);
            result[pair.Key] = material;
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    private static void ConfigureShowcaseMapImporter(string path, bool normalMap)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        bool changed = false;
        var desiredType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != desiredType)
        {
            importer.textureType = desiredType;
            changed = true;
        }
        if (!normalMap && importer.sRGBTexture)
        {
            importer.sRGBTexture = false;
            changed = true;
        }
        if (changed) importer.SaveAndReimport();
    }

    private static Color ShowcaseColor(string key)
    {
        if (key == "Stone") return new Color(0.68f, 0.62f, 0.52f, 1f);
        if (key == "Plaster") return new Color(0.76f, 0.67f, 0.52f, 1f);
        if (key == "Wood") return new Color(0.48f, 0.25f, 0.12f, 1f);
        if (key == "Roof") return new Color(0.34f, 0.12f, 0.06f, 1f);
        return new Color(0.82f, 0.55f, 0.12f, 1f);
    }

    private static void ApplyShowcaseMaterials(GameObject root, Dictionary<string, Material> materials)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var name = renderer.gameObject.name.ToLowerInvariant();
            var key = name.Contains("roof") ? "Roof" :
                name.Contains("door") ? "Wood" :
                name.Contains("banner") || name.Contains("pediment") || name.Contains("trim") ? "Trim" :
                name.Contains("housebody") || name.Contains("plaster") ? "Plaster" :
                "Stone";
            renderer.sharedMaterial = materials[key];
        }
    }

    private static void SelectEra(Transform root, int era)
    {
        for (int i = 0; i < root.childCount; i++)
            root.GetChild(i).gameObject.SetActive(i == era);
    }
}
