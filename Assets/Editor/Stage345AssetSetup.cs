using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Imports the prepared animal and armour FBXs into isolated review prefabs.
/// Blender PoseCheck is a rig diagnostic, not a gameplay animation.</summary>
[InitializeOnLoad]
public static class Stage345AssetSetup
{
    const string Root = "Assets/CharacterRigging";
    static readonly (string stage, string name, bool human)[] Characters =
    {
        ("Stage3", "Horse", false), ("Stage3", "RedDeer", false),
        ("Stage5", "ArmoredKnight", true), ("Stage5", "DarkKnight", true)
    };
    public const string ScenePath = Root + "/Stage345Review.unity";
    const string PreviousSceneKey = "Stage345.PreviousPlayModeStartScene";

    static Stage345AssetSetup()
    {
        EditorSceneManager.sceneOpened += (scene, mode) => SelectReviewStartScene(scene.path);
        EditorApplication.delayCall += () => SelectReviewStartScene(SceneManager.GetActiveScene().path);
    }

    static void SelectReviewStartScene(string path)
    {
        if (path == ScenePath)
        {
            var current = EditorSceneManager.playModeStartScene;
            if (current != null && AssetDatabase.GetAssetPath(current) != ScenePath)
                SessionState.SetString(PreviousSceneKey, AssetDatabase.GetAssetPath(current));
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }
        else if (EditorSceneManager.playModeStartScene != null &&
                 AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == ScenePath)
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                SessionState.GetString(PreviousSceneKey, "Assets/Scenes/Core.unity"));
        }
    }

    static void Require(bool condition, string detail)
    {
        if (!condition) throw new InvalidOperationException(detail);
    }

    [MenuItem("Tools/Character Rigging/Stages 3-5/Build Review Assets")]
    public static void Build()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var prepared = Characters.Select(c => Import(c.stage, c.name, c.human)).ToArray();
        var existing = new[]
        {
            Root + "/HeraldicKnight/Prefabs/HeraldicKnight.prefab",
            Root + "/Stage2/Farmhand/Prefabs/Farmhand.prefab",
            Root + "/Stage2/Spearman/Prefabs/Spearman.prefab"
        }.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();
        foreach (var missing in existing.Where(p => p == null))
            Debug.LogWarning("Stage 3-5 review is not joint-complete: an earlier-stage prefab is missing.");
        var prefabs = existing.Concat(prepared).Where(p => p != null).ToArray();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("EventManager").AddComponent<EventManager>();
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Review Ground"; ground.transform.localScale = Vector3.one * 1.1f;
        for (int i = 0; i < prefabs.Length; i++)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i]);
            obj.transform.position = new Vector3((i % 3 - 1) * 2.6f, 0, (1 - i / 3) * 2.6f);
        }
        var camera = new GameObject("Main Camera").AddComponent<Camera>();camera.tag = "MainCamera";
        camera.transform.position = new Vector3(8, 8, 11);
        camera.transform.LookAt(new Vector3(0, 1, 0));
        camera.orthographic = true;camera.orthographicSize = 5.6f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.12f, .16f, .2f);
        var light = new GameObject("Key Light").AddComponent<Light>();light.type = LightType.Directional;
        light.intensity = 1.25f;light.transform.rotation = Quaternion.Euler(48, -35, 0);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.6f, .6f, .63f);
        EditorSceneManager.SaveScene(scene, ScenePath);
        SelectReviewStartScene(ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Stage 3/5 review scene built: " + ScenePath + ". Inspect before approving art or runtime.");
    }

    static GameObject Import(string stage, string name, bool human)
    {
        string folder = $"{Root}/{stage}/{name}";
        string modelPath = $"{folder}/Models/{name}.fbx";
        Require(File.Exists(modelPath), "Missing " + modelPath);
        string materialFolder = folder + "/Materials";
        string prefabFolder = folder + "/Prefabs";
        string animationFolder = folder + "/Animations";
        Directory.CreateDirectory(materialFolder);Directory.CreateDirectory(prefabFolder);
        Directory.CreateDirectory(animationFolder);AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        Require(importer != null, "No model importer for " + modelPath);
        importer.animationType = human ? ModelImporterAnimationType.Human : ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;importer.optimizeGameObjects = false;
        importer.SaveAndReimport();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
        Require(avatar != null && avatar.isValid && (!human || avatar.isHuman), name + " invalid Avatar.");
        var clip = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__"));
        Require(clip != null && clip.length > 1, name + " missing PoseCheck.");
        string texturePath = $"{folder}/Textures/{name}_BaseColor.png";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        Require(texture != null, "Missing " + texturePath);
        string materialPath = $"{materialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        Require(material.shader != null, name + " missing Standard shader.");
        material.mainTexture = texture;material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", .2f);material.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name + "_Material"), material);
        importer.SaveAndReimport();
        avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().First();
        clip = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
            .First(c => !c.name.StartsWith("__"));
        string controllerPath = $"{animationFolder}/{name}.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine;
        var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "PoseCheck")
            ?? machine.AddState("PoseCheck");
        state.motion = clip;machine.defaultState = state;EditorUtility.SetDirty(controller);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        Require(model != null, "Missing model " + modelPath);
        var obj = UnityEngine.Object.Instantiate(model);obj.name = name;
        var animator = obj.GetComponent<Animator>();
        Require(animator != null, name + " missing Animator.");
        animator.avatar = avatar;animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        var skin = obj.GetComponentInChildren<SkinnedMeshRenderer>();
        Require(skin != null && skin.bones.Length >= (human ? 20 : 15), name + " missing skin/bones.");
        foreach (var renderer in obj.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
        string prefabPath = $"{prefabFolder}/{name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(obj, prefabPath);
        int boneCount = skin.bones.Length;
        UnityEngine.Object.DestroyImmediate(obj);
        Require(prefab != null, "Could not save " + prefabPath);
        Debug.Log($"Imported {name}: Avatar valid, human={avatar.isHuman}, bones={boneCount}, clip={clip.length:F3}s");
        return prefab;
    }

    [MenuItem("Tools/Character Rigging/Stages 3-5/Build Mounted Fit Prototype")]
    public static void BuildMountedFitPrototype()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var horsePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            Root + "/Stage3/Horse/Prefabs/Horse.prefab");
        var riderMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            Root + "/Stage5/ArmoredKnight/Materials/ArmoredKnight.mat");
        Require(horsePrefab != null && riderMaterial != null, "Build review assets first.");
        string mountedPath = Root + "/Stage5/Mounted/ArmoredKnight_MountedIdle.fbx";
        var importer = AssetImporter.GetAtPath(mountedPath) as ModelImporter;
        Require(importer != null, "Missing mounted hold FBX.");
        // Keep the authored bone rotations. Humanoid retargeting changed the seated
        // pose into a wide, floating split in the Unity preview.
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.SaveAndReimport();
        var mountedClip = AssetDatabase.LoadAllAssetsAtPath(mountedPath)
            .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__"));
        Require(mountedClip != null && mountedClip.length > .5f, "Missing mounted hold clip.");
        var mountedAvatar = AssetDatabase.LoadAllAssetsAtPath(mountedPath)
            .OfType<Avatar>().FirstOrDefault();
        Require(mountedAvatar != null && mountedAvatar.isValid && !mountedAvatar.isHuman,
            "Mounted hold FBX has no valid Generic Avatar.");
        var mountedModel = AssetDatabase.LoadAssetAtPath<GameObject>(mountedPath);
        Require(mountedModel != null,"Mounted model did not import.");
        string controllerPath = Root + "/Stage5/Mounted/MountedIdle.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine;
        var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "MountedIdle")
            ?? machine.AddState("MountedIdle");
        state.motion = mountedClip;machine.defaultState = state;EditorUtility.SetDirty(controller);
        var horse = UnityEngine.Object.Instantiate(horsePrefab);horse.name = "MountedFitPrototype";
        try
        {
            var spine = horse.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Spine");
            Require(spine != null, "Horse Spine bone missing.");
            var rider = UnityEngine.Object.Instantiate(mountedModel);
            rider.name = "ArmoredKnight_Rider";
            rider.transform.SetParent(spine, true);
            rider.transform.rotation = horse.transform.rotation;
            var horseAnimator = horse.GetComponent<Animator>();
            var riderAnimator = rider.GetComponent<Animator>();
            Require(horseAnimator != null && riderAnimator != null, "Mounted rigs missing Animators.");
            foreach (var skin in rider.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.sharedMaterial = riderMaterial;
            riderAnimator.avatar = mountedAvatar;
            mountedClip.SampleAnimation(rider,0);
            var hips = rider.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "mixamorig:Hips");
            Require(hips != null,"Mounted rider hips missing.");
            var seat = spine.position + horse.transform.TransformDirection(new Vector3(0,.06f,.15f));
            rider.transform.position += seat - hips.position;
            horseAnimator.enabled = false;
            riderAnimator.runtimeAnimatorController = controller;
            riderAnimator.applyRootMotion = false;
            riderAnimator.enabled = true;
            string path = Root + "/Stage5/MountedFitPrototype.prefab";
            PrefabUtility.SaveAsPrefabAsset(horse, path);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("EventManager").AddComponent<EventManager>();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Mounted Review Ground";ground.transform.localScale = Vector3.one * .5f;
            var mounted = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(path));
            mounted.transform.position = Vector3.zero;
            var camera = new GameObject("Main Camera").AddComponent<Camera>();camera.tag = "MainCamera";
            camera.transform.position = new Vector3(3.2f, 2.6f, 4.5f);
            camera.transform.LookAt(new Vector3(0, 1.1f, 0));
            camera.orthographic = true;camera.orthographicSize = 1.8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .16f, .20f);
            var light = new GameObject("Key Light").AddComponent<Light>();
            light.type = LightType.Directional;light.intensity = 1.25f;
            light.transform.rotation = Quaternion.Euler(45, -35, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.6f, .6f, .63f);
            EditorSceneManager.SaveScene(scene, Root + "/Stage5/MountedReview.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("Mounted hold prototype and review scene saved: " + path);
        }
        finally { UnityEngine.Object.DestroyImmediate(horse); }
    }

    [MenuItem("Tools/Character Rigging/Stages 3-5/Configure Mounted Sockets")]
    public static void ConfigureMountedSockets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode first.");
        const string path = Root + "/Stage5/MountedFitPrototype.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Require(prefab != null, "Mounted prototype prefab is missing.");
        var root = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        try
        {
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            var spine = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Spine");
            var rider = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "ArmoredKnight_Rider");
            Require(spine != null && rider != null, "Mounted prototype lacks Spine or rider.");
            var sockets = root.GetComponent<MountedSockets>() ?? root.AddComponent<MountedSockets>();
            var saddle = FindOrCreate("SaddleRoot", spine);
            saddle.localPosition = new Vector3(0f, .06f, .15f);
            saddle.localRotation = Quaternion.identity;
            var riderRoot = FindOrCreate("RiderRoot", saddle);
            rider.SetParent(riderRoot, true);
            sockets.saddleRoot = saddle;
            sockets.riderRoot = riderRoot;
            sockets.leftFootTarget = FindOrCreate("LeftStirrup", saddle);
            sockets.rightFootTarget = FindOrCreate("RightStirrup", saddle);
            sockets.leftHandTarget = FindOrCreate("LeftHandTarget", riderRoot);
            sockets.rightHandTarget = FindOrCreate("RightHandTarget", riderRoot);
            sockets.weaponMount = FindOrCreate("WeaponMount", riderRoot);
            sockets.leftFootTarget.localPosition = new Vector3(-.18f, -.18f, .12f);
            sockets.rightFootTarget.localPosition = new Vector3(.18f, -.18f, .12f);
            sockets.leftHandTarget.localPosition = new Vector3(-.22f, .38f, .28f);
            sockets.rightHandTarget.localPosition = new Vector3(.22f, .38f, .28f);
            sockets.weaponMount.localPosition = new Vector3(.35f, .45f, -.08f);
            var sync = root.GetComponent<MountedAnimatorSync>() ?? root.AddComponent<MountedAnimatorSync>();
            sync.horseAnimator = root.GetComponent<Animator>();
            sync.riderAnimator = rider.GetComponent<Animator>();
            var ik = rider.gameObject.GetComponent<MountedIKController>() ?? rider.gameObject.AddComponent<MountedIKController>();
            ik.riderAnimator = rider.GetComponent<Animator>();
            ik.sockets = sockets;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
            Debug.Log("Mounted sockets configured: " + path);
        }
        finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
    }

    static Transform FindOrCreate(string name, Transform parent)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing;
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        return obj.transform;
    }
}
