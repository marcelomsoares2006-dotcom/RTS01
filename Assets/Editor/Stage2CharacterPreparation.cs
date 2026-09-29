using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using EraImperial.CharacterPreparation;

[InitializeOnLoad]
public static class Stage2CharacterPreparation
{
    public const string Root = "Assets/CharacterRigging/Stage2";
    public const string ScenePath = Root + "/Scenes/Stage2RigDemo.unity";
    const string Key = "Stage2.CharacterCheck.";
    static readonly string[] Names = { "Farmhand", "Spearman" };
    static readonly string ReportDir = "Docs/CharacterMeshAudit/Stage2";
    static double nextTick;
    static Vector3[][] samples;
    static int tick;

    static Stage2CharacterPreparation()
    {
        EditorApplication.update += ValidateTick;
        Application.logMessageReceived += OnLog;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.playModeStateChanged += change =>
        {
            if (change == PlayModeStateChange.ExitingEditMode && SceneManager.GetActiveScene().path == ScenePath)
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        };
        EditorApplication.delayCall += () => OnSceneOpened(SceneManager.GetActiveScene(), OpenSceneMode.Single);
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path == ScenePath)
        {
            var existing = EditorSceneManager.playModeStartScene;
            if (existing != null && AssetDatabase.GetAssetPath(existing) != ScenePath)
                SessionState.SetString(Key + "PreviousScene", AssetDatabase.GetAssetPath(existing));
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }
        else if (EditorSceneManager.playModeStartScene != null && AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == ScenePath)
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "PreviousScene", "Assets/Scenes/Core.unity"));
    }

    [MenuItem("Tools/Character Rigging/Stage 2/Open Rig Demo")]
    public static void OpenDemo()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
    }

    static void Require(bool ok, string detail)
    {
        if (!ok) throw new InvalidOperationException(detail);
    }

    static void EnsureFolder(string path)
    {
        Directory.CreateDirectory(path);
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Character Rigging/Stage 2/Build Assets and Demo")]
    public static void BuildFromMenu() { Build(); }

    static bool Build()
    {
        Require(!EditorApplication.isPlaying, "Exit Play Mode before building the demo.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        Directory.CreateDirectory(ReportDir);
        AssetDatabase.Refresh();
        var report = new StringBuilder("Stage 2 import audit — Unity " + Application.unityVersion + "\n");
        foreach (string name in Names)
        {
            string dir = Root + "/" + name;
            EnsureFolder(dir + "/Materials"); EnsureFolder(dir + "/Prefabs"); EnsureFolder(dir + "/Animations");
            string path = dir + "/Models/" + name + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Require(importer != null, "Missing FBX: " + path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;
            importer.isReadable = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            var clips = importer.defaultClipAnimations;
            Require(clips.Length == 1, name + ": expected one diagnostic clip.");
            clips[0].name = "PoseCheck"; clips[0].loopTime = true;
            clips[0].lockRootRotation = true; clips[0].lockRootHeightY = true; clips[0].lockRootPositionXZ = true;
            importer.clipAnimations = clips; importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            Require(avatar != null && avatar.isValid && avatar.isHuman, name + ": invalid Humanoid Avatar.");
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
            Require(clip.length > 1 && clip.isLooping, name + ": missing looping PoseCheck.");

            string materialPath = dir + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, materialPath); }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/Textures/" + name + "_BaseColor.png");
            Require(material.mainTexture != null, name + ": missing colour texture.");
            material.SetFloat("_Metallic", 0f); material.SetFloat("_Glossiness", .25f);
            string normalPath = dir + "/Textures/" + name + "_Normal.png";
            var normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (normalImporter != null)
            {
                normalImporter.textureType = TextureImporterType.NormalMap; normalImporter.SaveAndReimport();
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath)); material.EnableKeyword("_NORMALMAP");
            }
            material.DisableKeyword("_EMISSION"); EditorUtility.SetDirty(material);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name + "_Material"), material);
            importer.SaveAndReimport();
            clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
            avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().First();
            string controllerPath = dir + "/Animations/" + name + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "PoseCheck") ?? machine.AddState("PoseCheck");
            state.motion = clip; machine.defaultState = state; EditorUtility.SetDirty(controller);
            var obj = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path)); obj.name = name;
            var animator = obj.GetComponent<Animator>(); animator.avatar = avatar; animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
            var skinned = obj.GetComponentsInChildren<SkinnedMeshRenderer>();
            Require(skinned.Length == 1 && skinned[0].bones.Length >= 20, name + ": missing skin or bones.");
            foreach (var w in skinned[0].sharedMesh.boneWeights)
                Require(Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1f) < .01f, name + ": invalid weights.");
            if (name == "Spearman")
            {
                var transforms = obj.GetComponentsInChildren<Transform>();
                Require(transforms.Any(t => t.name == "Spearman_Spear" && t.parent.name.Contains("RightHand")), "Spear is not attached to right hand.");
                Require(transforms.Any(t => t.name == "Spearman_Shield" && t.parent.name.Contains("LeftHand")), "Shield is not attached to left hand.");
            }
            PrefabUtility.SaveAsPrefabAsset(obj, dir + "/Prefabs/" + name + ".prefab");
            report.AppendLine($"PASS {name}: Humanoid valid, bones={skinned[0].bones.Length}, vertices={skinned[0].sharedMesh.vertexCount}, PoseCheck={clip.length:F3}s, texture={material.mainTexture.name}");
            UnityEngine.Object.DestroyImmediate(obj);
        }
        EnsureFolder(Root + "/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // The project injects DebugConsole into every scene; it subscribes through EventManager.
        new GameObject("EventManager").AddComponent<EventManager>();
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.name = "Preview Ground"; floor.transform.localScale = Vector3.one * .6f;
        var groundMat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Scenes/Ground.mat");
        if (groundMat == null) { groundMat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(groundMat, Root + "/Scenes/Ground.mat"); }
        groundMat.color = new Color(.22f, .27f, .22f); groundMat.SetFloat("_Glossiness", .1f); EditorUtility.SetDirty(groundMat);
        floor.GetComponent<Renderer>().sharedMaterial = groundMat;
        var actors = Names.Select((name, i) =>
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + name + "/Prefabs/" + name + ".prefab"));
            obj.transform.position = new Vector3(i == 0 ? -.95f : .95f, .025f, 0); return obj.GetComponent<Animator>();
        }).ToArray();
        var driver = new GameObject("Stage 2 Demo").AddComponent<Stage2RigDemo>(); driver.characters = actors;
        var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.transform.position = new Vector3(3f, 2.8f, 6.2f); camera.transform.LookAt(new Vector3(0, 1.12f, 0));
        camera.orthographic = true; camera.orthographicSize = 1.65f; camera.backgroundColor = new Color(.08f, .1f, .13f); camera.clearFlags = CameraClearFlags.SolidColor;
        var sun = new GameObject("Key Light").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.2f; sun.transform.rotation = Quaternion.Euler(45, -30, 0); sun.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.55f, .55f, .6f);
        EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
        File.WriteAllText(ReportDir + "/unity-import.txt", report.ToString()); Debug.Log(report.ToString());
        return true;
    }

    [MenuItem("Tools/Character Rigging/Stage 2/Build and Validate")]
    public static void RunValidation()
    {
        tick = 0; samples = null; nextTick = 0;
        if (!Build()) return;
        File.WriteAllText(ReportDir + "/unity-playmode.txt", "Unity " + Application.unityVersion + "\n");
        SessionState.SetBool(Key + "Active", true); SessionState.SetInt(Key + "Errors", 0);
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 150);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        EditorApplication.EnterPlaymode();
    }

    static void OnLog(string message, string stack, LogType type)
    {
        if (SessionState.GetBool(Key + "Active", false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
        {
            SessionState.SetInt(Key + "Errors", SessionState.GetInt(Key + "Errors", 0) + 1);
            File.AppendAllText(ReportDir + "/unity-playmode.txt", "ERROR " + message + "\n" + stack + "\n");
        }
    }

    static Vector3[] Bake(SkinnedMeshRenderer renderer)
    {
        var mesh = new Mesh(); renderer.BakeMesh(mesh); var vertices = mesh.vertices; UnityEngine.Object.DestroyImmediate(mesh); return vertices;
    }

    static void Capture(string file)
    {
        var camera = Camera.main; var previous = camera.targetTexture; var previousActive = RenderTexture.active;
        var rt = new RenderTexture(1280, 800, 24); var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); tex.Apply();
            var pixels = tex.GetPixels32(); int pink = pixels.Count(p => p.r > 220 && p.b > 220 && p.g < 60);
            Require(pink < pixels.Length * .005, "Unsupported pink material in screenshot.");
            File.WriteAllBytes(ReportDir + "/" + file, tex.EncodeToPNG());
        }
        finally { camera.targetTexture = previous; RenderTexture.active = previousActive; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); }
    }

    static void ValidateTick()
    {
        if (!SessionState.GetBool(Key + "Active", false)) return;
        try
        {
            Require(EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + "Deadline", 0), "PlayMode validation timeout.");
            Require(SessionState.GetInt(Key + "Errors", 0) == 0, "Runtime error logged.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < nextTick) return;
            var driver = UnityEngine.Object.FindAnyObjectByType<Stage2RigDemo>(); if (driver == null) return;
            var renderers = driver.characters.Select(a => a.GetComponentInChildren<SkinnedMeshRenderer>()).ToArray();
            if (tick == 0)
            {
                foreach (var a in driver.characters) { a.Play("PoseCheck", 0, 0); a.Update(0); a.speed = 0; }
                samples = renderers.Select(Bake).ToArray(); Capture("unity-rest.png");
                tick++; nextTick = EditorApplication.timeSinceStartup + 1; return;
            }
            foreach (var a in driver.characters) { a.Play("PoseCheck", 0, 1f / 3f); a.Update(0); }
            for (int i = 0; i < renderers.Length; i++)
            {
                var current = Bake(renderers[i]); float delta = current.Zip(samples[i], (a, b) => Vector3.Distance(a, b)).Max();
                Require(delta > .015f && delta < 2f, Names[i] + ": invalid deformation " + delta);
                Require(current.All(v => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)), "Invalid vertex positions.");
                Require(renderers[i].sharedMaterial.shader.isSupported, "Shader unsupported.");
                File.AppendAllText(ReportDir + "/unity-playmode.txt", $"PASS {Names[i]}: deformation={delta:F4}m; Humanoid={driver.characters[i].isHuman}; root={driver.characters[i].transform.position}\n");
            }
            Capture("unity-bend.png"); File.AppendAllText(ReportDir + "/unity-playmode.txt", "STAGE2_PLAYMODE_PASSED: 0 runtime errors.\n");
            Finish(0);
        }
        catch (Exception ex) { File.AppendAllText(ReportDir + "/unity-playmode.txt", "FAIL " + ex + "\n"); Finish(1); }
    }

    static void Finish(int code)
    {
        SessionState.SetBool(Key + "Active", false);
        if (Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.ExitPlaymode();
    }
}
