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
    private const int DensityVolumeCount = 100;
    private const int DensityGridSide = 10;
    private const float DensityGridSpacing = 7f;
    private static readonly Vector3 UrbanCenter = new Vector3(10f, 0f, 10f);

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
        probe.size = new Vector3(92f, 18f, 92f);
        probe.center = new Vector3(10f, 3f, 10f);
        probe.intensity = 0.65f;
        probe.boxProjection = true;

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Showcase Ground";
        ground.transform.position = new Vector3(10f, -0.02f, 10f);
        ground.transform.localScale = new Vector3(10.5f, 1f, 10.5f);
        Object.DestroyImmediate(ground.GetComponent<Collider>());
        var groundMaterial = new Material(Shader.Find("Standard"));
        groundMaterial.color = new Color(.12f, .14f, .12f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

        var cameraGo = new GameObject("Showcase Camera");
        var camera = cameraGo.AddComponent<Camera>();
        camera.transform.position = new Vector3(76f, 70f, -82f);
        camera.transform.LookAt(new Vector3(10f, 3f, 10f));
        camera.fieldOfView = 45f;
        camera.farClipPlane = 180f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.36f, 0.49f, 0.62f, 1f);

        var visualSources = new Dictionary<string, GameObject>();
        try
        {
            string[] pieces = { "WallStraight", "WallCorner", "Gatehouse", "WallTowerConnector" };
            for (int i = 0; i < pieces.Length; i++)
            {
                string path = "Assets/CityArchitecture/Prefabs/" + pieces[i] + ".prefab";
                GameObject instance = InstantiateVisualOnly(path, scene, visualSources);
                instance.name = pieces[i] + "_Showcase_" + i;
                instance.transform.position = new Vector3((i % 2) * 52f - 16f, 0f, (i / 2) * 52f - 16f);
                SelectEra(instance.transform, i);
            }

            // Tudo abaixo é uma cópia visual reversível. Nenhum prefab de produção é salvo.
            var showcaseMaterials = EnsureShowcaseMaterials();
            AddTownCenterVisualCandidate(scene, showcaseMaterials);
            AddCityDensityVolumes(scene, showcaseMaterials, visualSources);
            AddShowcaseRoads(scene, showcaseMaterials);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            foreach (var source in visualSources.Values)
                if (source != null) PrefabUtility.UnloadPrefabContents(source);
        }

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
            // O prefab é somente a referência visual e de escala. Não instanciamos seu
            // Mesh nem aplicamos overrides: o candidato abaixo é uma cópia procedural
            // autocontida na cena showcase, sem scripts, BuildingData ou collider de jogo.
            var sourceMesh = source.transform.Find("Mesh");
            if (sourceMesh == null) throw new System.InvalidOperationException("Mesh do Town Center ausente.");

            var visual = new GameObject("TownCenter_VisualCandidate");
            SceneManager.MoveGameObjectToScene(visual, scene);
            visual.transform.position = new Vector3(10f, 0f, 10f);

            // Massa contínua: plinto + nave escalonada + keep central. As peças se
            // sobrepõem de forma controlada para lerem como uma construção única.
            var shell = new GameObject("TownCenter_IntegratedShell");
            shell.transform.SetParent(visual.transform, false);
            AddShowcaseBlock(shell.transform, new Vector3(0f, 0.30f, 0f), new Vector3(7.8f, 0.60f, 6.8f), materials["Stone"], "TownCenter_Plinto");
            AddShowcaseBlock(shell.transform, new Vector3(0f, 1.72f, 0f), new Vector3(6.9f, 2.45f, 5.9f), materials["Plaster"], "TownCenter_LowerShell");
            AddShowcaseBlock(shell.transform, new Vector3(0f, 3.74f, 0f), new Vector3(5.9f, 1.65f, 5.0f), materials["Stone"], "TownCenter_UpperShell");
            AddShowcaseBlock(shell.transform, new Vector3(0f, 5.38f, 0f), new Vector3(4.25f, 1.65f, 3.65f), materials["Plaster"], "TownCenter_Keep");

            // Alas e torres são ligadas ao corpo por blocos largos, evitando a leitura
            // de módulos soltos que existia na cópia anterior.
            AddShowcaseBlock(shell.transform, new Vector3(-3.05f, 2.55f, 0f), new Vector3(1.35f, 3.90f, 4.75f), materials["Stone"], "TownCenter_LeftWing");
            AddShowcaseBlock(shell.transform, new Vector3(3.05f, 2.55f, 0f), new Vector3(1.35f, 3.90f, 4.75f), materials["Stone"], "TownCenter_RightWing");
            AddShowcaseBlock(shell.transform, new Vector3(0f, 2.28f, -2.82f), new Vector3(5.05f, 3.45f, 1.05f), materials["Plaster"], "TownCenter_FrontBridge");

            // Cobertura com cumeeira forte e volumes baixos nas torres para fechar a
            // silhueta superior, sem recorrer aos LODs desalinhados de produção.
            AddShowcaseGableRoof(shell.transform, new Vector3(0f, 6.32f, 0f), 6.55f, 4.95f, 6.32f, 7.38f, materials["Roof"], "TownCenter_MainRoof");
            AddShowcaseGableRoof(shell.transform, new Vector3(0f, 7.72f, 0f), 4.70f, 3.90f, 7.72f, 8.56f, materials["Roof"], "TownCenter_KeepRoof");
            AddShowcaseBlock(shell.transform, new Vector3(-3.05f, 4.72f, 0f), new Vector3(1.70f, 0.40f, 4.95f), materials["Trim"], "TownCenter_LeftCrown");
            AddShowcaseBlock(shell.transform, new Vector3(3.05f, 4.72f, 0f), new Vector3(1.70f, 0.40f, 4.95f), materials["Trim"], "TownCenter_RightCrown");

            // Entrada frontal, vãos e faixa de acabamento dão escala e reforçam o eixo.
            var facade = new GameObject("TownCenter_FacadeDetails");
            facade.transform.SetParent(visual.transform, false);
            AddShowcaseBlock(facade.transform, new Vector3(0f, 1.32f, -3.20f), new Vector3(2.05f, 1.70f, 0.28f), materials["Roof"], "TownCenter_EntranceShadow");
            AddShowcaseBlock(facade.transform, new Vector3(-1.20f, 1.18f, -3.24f), new Vector3(0.38f, 2.10f, 0.48f), materials["Trim"], "TownCenter_EntryColumn_L");
            AddShowcaseBlock(facade.transform, new Vector3(1.20f, 1.18f, -3.24f), new Vector3(0.38f, 2.10f, 0.48f), materials["Trim"], "TownCenter_EntryColumn_R");
            AddShowcaseBlock(facade.transform, new Vector3(0f, 2.30f, -3.24f), new Vector3(2.80f, 0.36f, 0.52f), materials["Trim"], "TownCenter_EntryLintel");
            AddShowcaseBlock(facade.transform, new Vector3(0f, 4.10f, -2.56f), new Vector3(0.72f, 1.10f, 0.12f), materials["Roof"], "TownCenter_CentralWindow");
            for (int i = -2; i <= 2; i++)
            {
                AddShowcaseBlock(facade.transform, new Vector3(i * 1.05f, 3.10f, -3.02f), new Vector3(0.34f, 0.58f, 0.10f), materials["Roof"], "TownCenter_Window_Front_" + i);
                AddShowcaseBlock(facade.transform, new Vector3(i * 1.05f, 3.10f, 3.02f), new Vector3(0.34f, 0.58f, 0.10f), materials["Roof"], "TownCenter_Window_Back_" + i);
            }

            // Torres laterais e eixo vertical são parte do mesmo envelope arquitetônico.
            var towers = new GameObject("TownCenter_TowersAndBanner");
            towers.transform.SetParent(visual.transform, false);
            foreach (var x in new[] { -3.05f, 3.05f })
            {
                AddShowcaseCylinder(towers.transform, new Vector3(x, 5.55f, 0f), 1.12f, 2.10f, materials["Stone"], "TownCenter_Tower_" + x);
                AddShowcaseBlock(towers.transform, new Vector3(x, 6.72f, 0f), new Vector3(1.40f, 0.28f, 1.40f), materials["Trim"], "TownCenter_TowerCap_" + x);
            }
            AddShowcaseBlock(towers.transform, new Vector3(0f, 9.55f, 0f), new Vector3(0.16f, 3.0f, 0.16f), materials["Wood"], "TownCenter_Flagpole");
            AddShowcaseBlock(towers.transform, new Vector3(0.66f, 10.20f, 0f), new Vector3(1.25f, 0.72f, 0.06f), materials["Trim"], "TownCenter_Flag");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(source);
        }
    }

    private static void AddShowcaseBlock(Transform parent, Vector3 localPosition, Vector3 localScale, Material material, string objectName = "ShowcaseProp")
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objectName;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = localPosition;
        block.transform.localScale = localScale;
        block.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(block.GetComponent<Collider>());
    }

    private static void AddShowcaseCylinder(Transform parent, Vector3 localPosition, float diameter, float height, Material material, string objectName)
    {
        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = objectName;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.localPosition = localPosition;
        cylinder.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
        cylinder.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(cylinder.GetComponent<Collider>());
    }

    private static void AddShowcaseGableRoof(Transform parent, Vector3 localPosition, float width, float depth, float eaveY, float ridgeY, Material material, string objectName)
    {
        var roof = new GameObject(objectName);
        roof.transform.SetParent(parent, false);
        roof.transform.localPosition = localPosition;
        var mesh = new Mesh { name = objectName + "Mesh" };
        float halfWidth = width * 0.5f;
        float halfDepth = depth * 0.5f;
        mesh.vertices = new[]
        {
            new Vector3(-halfWidth, eaveY - localPosition.y, -halfDepth),
            new Vector3(halfWidth, eaveY - localPosition.y, -halfDepth),
            new Vector3(0f, ridgeY - localPosition.y, -halfDepth),
            new Vector3(-halfWidth, eaveY - localPosition.y, halfDepth),
            new Vector3(halfWidth, eaveY - localPosition.y, halfDepth),
            new Vector3(0f, ridgeY - localPosition.y, halfDepth)
        };
        mesh.triangles = new[]
        {
            0, 2, 1, 3, 4, 5,
            0, 3, 5, 0, 5, 2,
            1, 2, 5, 1, 5, 4,
            0, 1, 4, 0, 4, 3
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        roof.AddComponent<MeshFilter>().sharedMesh = mesh;
        roof.AddComponent<MeshRenderer>().sharedMaterial = material;
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
        CaptureView(camera, new Vector3(76f, 70f, -82f), UrbanCenter + new Vector3(0f, 3f, 0f), "TestResults/CityArchitectureShowcase_Overview.png");
        CaptureView(camera, new Vector3(54f, 34f, -42f), UrbanCenter + new Vector3(0f, 2f, 0f), "TestResults/CityArchitectureShowcase_Mid.png");
        CaptureView(camera, new Vector3(31f, 19f, -24f), UrbanCenter + new Vector3(-3f, 2f, -3f), "TestResults/CityArchitectureShowcase_Close.png");
        CaptureView(camera, new Vector3(18f, 12f, -16f), UrbanCenter + new Vector3(0f, 3.2f, 0f), "TestResults/TownCenter_Showcase_D3D11.png");
        Debug.Log("SHOWCASE_MULTI_VIEW_CAPTURED=4");
    }

    private static void AddCityDensityVolumes(Scene scene, Dictionary<string, Material> materials, Dictionary<string, GameObject> visualSources)
    {
        var densityRoot = new GameObject("CityDensity_VisualVolumes");
        SceneManager.MoveGameObjectToScene(densityRoot, scene);

        string[] archetypes =
        {
            "House_Medieval",
            "Barracks_Medieval",
            "Market_Classical",
            "Temple_Imperial"
        };

        for (int i = 0; i < DensityVolumeCount; i++)
        {
            int column = i % DensityGridSide;
            int row = i / DensityGridSide;
            float offset = (DensityGridSide - 1) * 0.5f;
            var target = UrbanCenter + new Vector3(
                (column - offset) * DensityGridSpacing,
                0f,
                (row - offset) * DensityGridSpacing);

            string archetype = archetypes[i % archetypes.Length];
            string path = "Assets/CityArchitecture/Prefabs/EraBuildings/" + archetype + ".prefab";
            GameObject instance = InstantiateVisualOnly(path, scene, visualSources);
            instance.name = "CityDensity_VisualVolume_" + i.ToString("000") + "_" + archetype;
            instance.transform.SetParent(densityRoot.transform, true);
            instance.transform.rotation = Quaternion.Euler(0f, (i % 4) * 90f, 0f);
            // Bounds e pouso são calculados somente na cópia visual da cena.
            PositionShowcaseObject(instance, target, 0.28f);
            ApplyShowcaseMaterials(instance, materials);
        }

        Debug.Log("CITY_DENSITY_VISUAL_VOLUMES=" + DensityVolumeCount);
    }

    private static GameObject InstantiateVisualOnly(string prefabPath, Scene scene, Dictionary<string, GameObject> visualSources)
    {
        GameObject source;
        if (!visualSources.TryGetValue(prefabPath, out source))
        {
            source = PrefabUtility.LoadPrefabContents(prefabPath);
            if (source == null) throw new FileNotFoundException("Prefab visual ausente", prefabPath);
            // O conteúdo isolado fica inativo antes do clone: nenhum Awake/OnEnable de
            // gameplay deve ser executado durante a montagem do showcase.
            source.SetActive(false);
            visualSources.Add(prefabPath, source);
        }

        var instance = (GameObject)Object.Instantiate(source);
        SceneManager.MoveGameObjectToScene(instance, scene);
        StripToVisualComponents(instance);
        instance.SetActive(true);
        return instance;
    }

    private static void StripToVisualComponents(GameObject root)
    {
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component is Transform || component is Renderer ||
                component is MeshFilter || component is LODGroup)
                continue;
            Object.DestroyImmediate(component);
        }
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
        AddShowcaseBlock(roads.transform, new Vector3(10f, 0.015f, 10f), new Vector3(78f, 0.03f, 1.45f), materials["Stone"]);
        AddShowcaseBlock(roads.transform, new Vector3(10f, 0.02f, 10f), new Vector3(1.45f, 0.04f, 78f), materials["Stone"]);
        for (int i = 0; i < 4; i++)
        {
            float axis = -4f + i * 14f;
            AddShowcaseBlock(roads.transform, new Vector3(axis, 0.018f, 10f), new Vector3(1.1f, 0.036f, 78f), materials["Stone"]);
            AddShowcaseBlock(roads.transform, new Vector3(10f, 0.018f, axis), new Vector3(78f, 0.036f, 1.1f), materials["Stone"]);
        }
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
        var densityVolumes = 0;
        var props = 0;
        var colliders = 0;
        var monoBehaviours = 0;
        Bounds sceneBounds = default;
        var hasSceneBounds = false;

        foreach (var root in roots)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                gameObjects++;
                if (transform.name.StartsWith("CityDensity_VisualVolume_")) densityVolumes++;
                if (transform.name == "ShowcaseProp") props++;
            }
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var isDensityVolume = transform.name.StartsWith("CityDensity_VisualVolume_");
                if (!isDensityVolume) continue;
                colliders += transform.GetComponentsInChildren<Collider>(true).Length;
                monoBehaviours += transform.GetComponentsInChildren<MonoBehaviour>(true).Length;
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
                else if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                {
                    meshes.Add(skinned.sharedMesh);
                    for (var subMesh = 0; subMesh < skinned.sharedMesh.subMeshCount; subMesh++)
                        triangles += (int)skinned.sharedMesh.GetIndexCount(subMesh) / 3;
                }
            }
        }

        bool visualOnly = densityVolumes == DensityVolumeCount && colliders == 0 && monoBehaviours == 0;

        var lines = new List<string>
        {
            "Era Imperial - CityArchitectureShowcase audit",
            "Escopo: cópia visual da cena; prefabs de produção não são alterados.",
            "CityDensity target volumes: " + DensityVolumeCount,
            "CityDensity visual volumes: " + densityVolumes,
            "CityDensity gate visual-only: " + (visualOnly ? "PASS" : "FAIL"),
            "Instanciamento: clones visuais da cena sem scripts/colisores/vínculo de prefab.",
            "Prefabs de produção salvos por este builder: 0",
            "GameObjects na cena: " + gameObjects,
            "Renderers ativos: " + renderers,
            "Meshes distintos: " + meshes.Count,
            "Triângulos ativos estimados: " + triangles,
            "Materiais distintos: " + materials.Count,
            "Props cúbicos showcase: " + props,
            "Colliders ativos: " + colliders,
            "MonoBehaviours ativos: " + monoBehaviours,
            "Bounds da cena: " + (hasSceneBounds ? sceneBounds.ToString() : "n/a"),
            "Gate AoE4 cidade densa: PENDENTE — 100 volumes são composição/instanciamento, não prova AAA.",
            "Gate AoE4 estabilidade: usa a validação Unity/standalone registrada separadamente."
        };
        File.WriteAllLines("TestResults/CityArchitectureShowcaseAudit.txt", lines);
        Debug.Log("SHOWCASE_METRICS volumes=" + densityVolumes + " visualOnly=" + visualOnly + " renderers=" + renderers + " triangles=" + triangles);
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
