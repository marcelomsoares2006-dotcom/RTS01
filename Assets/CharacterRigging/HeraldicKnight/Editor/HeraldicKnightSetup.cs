using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Etapa 1: configures the Heraldic Knight assets exported by
/// Tools/CharacterRigging/BuildHeraldicKnight.py (Humanoid avatar, clips, material, controller,
/// prefab and an isolated demo scene) and audits the result.
/// Batch: -executeMethod HeraldicKnightSetup.BatchBuildAndAudit
/// </summary>
public static class HeraldicKnightSetup
{
    public const string Root = "Assets/CharacterRigging/HeraldicKnight";
    public const string BaseModel = Root + "/Models/HeraldicKnight.fbx";
    public const string TexturePath = Root + "/Textures/HeraldicKnight_BaseColor.png";
    public const string MaterialPath = Root + "/Materials/HeraldicKnight_Mat.mat";
    public const string GroundMaterialPath = Root + "/Materials/HeraldicKnight_DemoGround.mat";
    public const string MarkerMaterialPath = Root + "/Materials/HeraldicKnight_DemoMarker.mat";
    public const string ControllerPath = Root + "/Animation/HeraldicKnight.controller";
    public const string PrefabPath = Root + "/Prefabs/HeraldicKnight.prefab";
    public const string ScenePath = Root + "/Scenes/HeraldicKnight_Demo.unity";
    public const string AuditPath = "TestResults/heraldic-import-audit.txt";

    // file suffix, clip name, Animator state, loop, expected seconds (from the source glTF)
    public static readonly (string file, string clip, string state, bool loop, float seconds)[] Clips =
    {
        ("Walking", "HK_Walking", "Walking", true, 1.0833f),
        ("Running", "HK_Running", "Running", true, 0.7083f),
        ("Attack", "HK_Attack", "Attack", false, 2.8333f),
        ("Triple_Combo_Attack", "HK_TripleCombo", "TripleCombo", false, 4.375f),
    };

    // Unity human bone -> Meshy/Mixamo bone. headfront (face helper) stays unmapped.
    public static readonly Dictionary<string, string> Mapping = new Dictionary<string, string>
    {
        { "Hips", "mixamorig:Hips" }, { "Spine", "mixamorig:Spine" }, { "Chest", "mixamorig:Spine1" },
        { "UpperChest", "mixamorig:Spine2" }, { "Neck", "mixamorig:Neck" }, { "Head", "mixamorig:Head" },
        { "LeftUpperLeg", "mixamorig:LeftUpLeg" }, { "LeftLowerLeg", "mixamorig:LeftLeg" },
        { "LeftFoot", "mixamorig:LeftFoot" }, { "LeftToes", "mixamorig:LeftToeBase" },
        { "RightUpperLeg", "mixamorig:RightUpLeg" }, { "RightLowerLeg", "mixamorig:RightLeg" },
        { "RightFoot", "mixamorig:RightFoot" }, { "RightToes", "mixamorig:RightToeBase" },
        { "LeftShoulder", "mixamorig:LeftShoulder" }, { "LeftUpperArm", "mixamorig:LeftArm" },
        { "LeftLowerArm", "mixamorig:LeftForeArm" }, { "LeftHand", "mixamorig:LeftHand" },
        { "RightShoulder", "mixamorig:RightShoulder" }, { "RightUpperArm", "mixamorig:RightArm" },
        { "RightLowerArm", "mixamorig:RightForeArm" }, { "RightHand", "mixamorig:RightHand" },
    };

    private static readonly List<string> ImportNotes = new List<string>();

    public static string ClipPath(string file) => $"{Root}/Models/HeraldicKnight@{file}.fbx";

    [MenuItem("Tools/Character Rigging/Heraldic Knight/Build Assets And Audit")]
    public static void BuildAndAudit()
    {
        if (!Build()) return;
        Audit();
    }

    public static void BatchBuildAndAudit()
    {
        int failures;
        try
        {
            if (!Build()) return;
            failures = Audit();
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory("TestResults");
            File.AppendAllText(AuditPath, "EXCEPTION: " + exception + "\n");
            Debug.LogException(exception);
            failures = 1;
        }
        if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    public static bool Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before building the demo.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;
        ImportNotes.Clear();
        foreach (var folder in new[] { "Materials", "Animation", "Prefabs", "Scenes" })
            if (!AssetDatabase.IsValidFolder($"{Root}/{folder}")) AssetDatabase.CreateFolder(Root, folder);
        AssetDatabase.Refresh();

        var texture = ConfigureTexture();
        var material = CreateMaterial(texture);
        var avatar = ConfigureBaseModel(material);
        var clips = ConfigureClips(avatar);
        var controller = CreateController(clips);
        var prefab = CreatePrefab(controller, material);
        CreateDemoScene(prefab);
        AssetDatabase.SaveAssets();
        return true;
    }

    private static Texture2D ConfigureTexture()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.None; // unused texels are black, not cut-outs
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
    }

    private static Material LoadOrCreateMaterial(string path, Shader shader)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        return material;
    }

    private static Material CreateMaterial(Texture2D texture)
    {
        // Built-in pipeline Standard (metallic workflow). The Meshy texture already contains painted
        // shading; the glTF emissive (= base colour) and implicit metallic 1.0 are intentionally dropped.
        var material = LoadOrCreateMaterial(MaterialPath, Shader.Find("Standard"));
        material.SetTexture("_MainTex", texture);
        material.SetColor("_Color", Color.white);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", 0.18f);
        material.SetColor("_EmissionColor", Color.black);
        material.DisableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(material);

        var ground = LoadOrCreateMaterial(GroundMaterialPath, Shader.Find("Standard"));
        ground.SetColor("_Color", new Color(0.42f, 0.45f, 0.38f));
        ground.SetFloat("_Glossiness", 0.05f);
        EditorUtility.SetDirty(ground);
        var marker = LoadOrCreateMaterial(MarkerMaterialPath, Shader.Find("Standard"));
        marker.SetColor("_Color", new Color(0.75f, 0.62f, 0.25f));
        marker.SetFloat("_Glossiness", 0.1f);
        EditorUtility.SetDirty(marker);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static Avatar ConfigureBaseModel(Material material)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(BaseModel);
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.importAnimation = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "HeraldicKnight_Mat"), material);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects = false;
        importer.SaveAndReimport();

        // Unity's automatic mapping is kept only if it is exactly the expected table.
        var description = importer.humanDescription;
        var auto = (description.human ?? Array.Empty<HumanBone>()).ToDictionary(b => b.humanName, b => b.boneName);
        bool matches = auto.Count == Mapping.Count && Mapping.All(p => auto.TryGetValue(p.Key, out var bone) && bone == p.Value);
        if (!matches)
        {
            ImportNotes.Add("Automatic humanoid mapping differed; explicit mapping applied. Auto: " +
                string.Join(", ", auto.Select(p => p.Key + "=" + p.Value)));
            description.human = Mapping.Select(p =>
            {
                var bone = new HumanBone { humanName = p.Key, boneName = p.Value };
                bone.limit.useDefaultValues = true;
                return bone;
            }).ToArray();
            importer.humanDescription = description;
            importer.SaveAndReimport();
        }
        else
        {
            ImportNotes.Add("Automatic humanoid mapping matches the expected 22-bone table.");
        }
        return AssetDatabase.LoadAllAssetsAtPath(BaseModel).OfType<Avatar>().FirstOrDefault();
    }

    private static Dictionary<string, AnimationClip> ConfigureClips(Avatar avatar)
    {
        var result = new Dictionary<string, AnimationClip>();
        foreach (var info in Clips)
        {
            var path = ClipPath(info.file);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            importer.SaveAndReimport();

            var defaults = importer.defaultClipAnimations;
            if (defaults.Length != 1) throw new InvalidOperationException($"{path}: expected one take, got {defaults.Length}");
            var clip = defaults[0];
            clip.name = info.clip;
            clip.loopTime = info.loop;
            clip.loopPose = false;
            // Rotation and height stay in the pose. Horizontal root motion is NOT baked into the pose,
            // so it is available for gameplay later and simply discarded while applyRootMotion is off
            // (Triple Combo moves ~1.9 m forward in the source; the demo keeps the knight in place).
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.heightFromFeet = false;
            clip.lockRootPositionXZ = false;
            clip.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
            result[info.state] = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .First(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
        }
        return result;
    }

    private static AnimatorController CreateController(Dictionary<string, AnimationClip> clips)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var machine = controller.layers[0].stateMachine;
        var states = new Dictionary<string, AnimatorState>();
        for (int i = 0; i < Clips.Length; i++)
        {
            var info = Clips[i];
            controller.AddParameter(HeraldicKnightDemo.Triggers[i], AnimatorControllerParameterType.Trigger);
            var state = machine.AddState(info.state, new Vector3(300, 60 + 70 * i, 0));
            state.motion = clips[info.state];
            state.writeDefaultValues = true;
            states[info.state] = state;
            var enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0, HeraldicKnightDemo.Triggers[i]);
            enter.hasExitTime = false;
            enter.hasFixedDuration = true;
            enter.duration = 0.2f;
            enter.canTransitionToSelf = false;
        }
        machine.defaultState = states["Walking"];
        // One-shot attacks: play to the end, then blend back to the locomotion loop.
        foreach (var attack in new[] { "Attack", "TripleCombo" })
        {
            var back = states[attack].AddTransition(states["Walking"]);
            back.hasExitTime = true;
            back.exitTime = 0.94f;
            back.hasFixedDuration = true;
            back.duration = 0.25f;
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static GameObject CreatePrefab(AnimatorController controller, Material material)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(BaseModel);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            instance.name = "HeraldicKnight";
            var animator = instance.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.sharedMaterial = material;
                renderer.quality = SkinQuality.Bone4;
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
            return PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void CreateDemoScene(GameObject prefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.74f);
        RenderSettings.ambientEquatorColor = new Color(0.46f, 0.46f, 0.44f);
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.24f, 0.22f);

        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.05f;
        light.color = new Color(1f, 0.96f, 0.9f);
        light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.55f, 0.63f, 0.72f);
        camera.fieldOfView = 38f;
        camera.transform.position = new Vector3(2.1f, 1.55f, 3.9f);
        camera.transform.LookAt(new Vector3(0f, 0.9f, 0f));
        camera.gameObject.AddComponent<AudioListener>();

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(2f, 1f, 2f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
        // Origin marker: makes any drift of the character away from its spawn point visible.
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "Origin Marker (r=0.6 m)";
        marker.transform.localScale = new Vector3(1.2f, 0.005f, 1.2f);
        marker.transform.position = new Vector3(0f, 0.004f, 0f);
        UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
        marker.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MarkerMaterialPath);

        var knight = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        knight.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        var labelObject = new GameObject("Action Label");
        labelObject.transform.position = new Vector3(0f, 2.15f, 0f);
        labelObject.transform.rotation = Quaternion.LookRotation(labelObject.transform.position - camera.transform.position);
        var label = labelObject.AddComponent<TextMesh>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelObject.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        label.anchor = TextAnchor.MiddleCenter;
        label.characterSize = 0.035f;
        label.fontSize = 64;
        label.color = Color.white;
        label.text = "Walking";

        // The game's DebugConsole is injected into every scene at runtime and requires an EventManager.
        // Provide one here instead of changing game code, so the isolated scene plays without errors.
        new GameObject("EventManager (required by DebugConsole)").AddComponent<EventManager>();

        var director = new GameObject("Demo Director").AddComponent<HeraldicKnightDemo>();
        director.knight = knight.GetComponent<Animator>();
        director.label = label;

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    [MenuItem("Tools/Character Rigging/Heraldic Knight/Audit Imported Assets")]
    public static int Audit()
    {
        var report = new StringBuilder();
        int failures = 0;
        void Check(bool ok, string message)
        {
            if (!ok) failures++;
            report.AppendLine((ok ? "PASS " : "FAIL ") + message);
        }
        report.AppendLine("Heraldic Knight Unity import audit");
        report.AppendLine("Editor: " + Application.unityVersion + "  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine("Render pipeline: " + (GraphicsSettings.defaultRenderPipeline == null ? "Built-in" : GraphicsSettings.defaultRenderPipeline.name));
        foreach (var note in ImportNotes) report.AppendLine("NOTE " + note);

        Check(!EditorUtility.scriptCompilationFailed, "scripts compiled (HeraldicKnightSetup/HeraldicKnightDemo loaded, no compilation failure)");

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        var textureImporter = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        Check(texture != null && texture.width == 2048 && textureImporter.sRGBTexture,
            $"texture {TexturePath}: {texture?.width}x{texture?.height}, sRGB={textureImporter?.sRGBTexture}, format={texture?.format}");

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Check(material != null && material.shader.name == "Standard" && material.shader.isSupported &&
              material.mainTexture == texture && material.GetFloat("_Metallic") == 0f && !material.IsKeywordEnabled("_EMISSION"),
            $"material {MaterialPath}: shader={material?.shader.name}, supported={material?.shader.isSupported}, " +
            $"mainTexture={material?.mainTexture?.name}, metallic={material?.GetFloat("_Metallic")}, " +
            $"smoothness={material?.GetFloat("_Glossiness")}, emission={material?.IsKeywordEnabled("_EMISSION")}");

        var importer = (ModelImporter)AssetImporter.GetAtPath(BaseModel);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(BaseModel).OfType<Avatar>().FirstOrDefault();
        Check(importer.animationType == ModelImporterAnimationType.Human && avatar != null && avatar.isValid && avatar.isHuman,
            $"avatar {avatar?.name}: animationType={importer.animationType}, isValid={avatar?.isValid}, isHuman={avatar?.isHuman}");
        var human = importer.humanDescription.human.ToDictionary(b => b.humanName, b => b.boneName);
        var missingRequired = Enumerable.Range(0, HumanTrait.BoneCount)
            .Where(HumanTrait.RequiredBone).Select(i => HumanTrait.BoneName[i]).Where(n => !human.ContainsKey(n)).ToList();
        bool exact = Mapping.All(p => human.TryGetValue(p.Key, out var bone) && bone == p.Value) && human.Count == Mapping.Count;
        Check(missingRequired.Count == 0 && exact,
            $"humanoid mapping: {human.Count} bones, required missing=[{string.Join(",", missingRequired)}], matches table={exact}; " +
            string.Join(", ", human.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)));

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(BaseModel);
        var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var mesh = renderers.FirstOrDefault()?.sharedMesh;
        int triangles = mesh == null ? 0 : mesh.triangles.Length / 3;
        Check(renderers.Length == 1 && mesh != null && triangles == 19609 && renderers[0].bones.Length == 23 &&
              renderers[0].sharedMaterial == material,
            $"model {BaseModel}: skinnedRenderers={renderers.Length}, vertices={mesh?.vertexCount}, triangles={triangles}, " +
            $"bones={renderers.FirstOrDefault()?.bones.Length}, meshLocalBoundsSize={mesh?.bounds.size.ToString("F3")} m, " +
            $"material(remap)={renderers.FirstOrDefault()?.sharedMaterial?.name}, subMeshes={mesh?.subMeshCount}");
        if (mesh != null)
        {
            var weights = mesh.GetAllBoneWeights();
            var perVertex = mesh.GetBonesPerVertex();
            int maxInfluence = perVertex.Length == 0 ? 0 : perVertex.Max();
            Check(maxInfluence <= 4 && perVertex.All(c => c > 0),
                $"skin weights: maxInfluences={maxInfluence}, verticesWithoutWeights={perVertex.Count(c => c == 0)}, totalWeights={weights.Length}");
        }

        foreach (var info in Clips)
        {
            var path = ClipPath(info.file);
            var clipImporter = (ModelImporter)AssetImporter.GetAtPath(path);
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == info.clip);
            bool ok = clip != null && clip.isHumanMotion && Mathf.Abs(clip.length - info.seconds) < 0.05f &&
                      clip.isLooping == info.loop && clipImporter.animationType == ModelImporterAnimationType.Human &&
                      clipImporter.sourceAvatar == avatar;
            Check(ok, $"clip {info.clip} ({path}): length={clip?.length:F4}s expected {info.seconds:F4}s, " +
                $"humanMotion={clip?.isHumanMotion}, loop={clip?.isLooping}, hasRootCurves={clip?.hasGenericRootTransform}, " +
                $"avgSpeed={clip?.averageSpeed}, sourceAvatar={clipImporter.sourceAvatar?.name}");
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var states = controller == null ? new List<ChildAnimatorState>() : controller.layers[0].stateMachine.states.ToList();
        Check(controller != null && states.Count == 4 && states.All(s => s.state.motion != null) &&
              controller.layers[0].stateMachine.defaultState.name == "Walking",
            $"controller {ControllerPath}: states=[{string.Join(", ", states.Select(s => s.state.name + ":" + s.state.motion?.name))}], " +
            $"parameters=[{string.Join(", ", controller?.parameters.Select(p => p.name) ?? Array.Empty<string>())}]");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var prefabAnimator = prefab?.GetComponent<Animator>();
        Check(prefabAnimator != null && prefabAnimator.avatar == avatar && prefabAnimator.runtimeAnimatorController == controller &&
              !prefabAnimator.applyRootMotion && prefab.GetComponentInChildren<SkinnedMeshRenderer>()?.sharedMaterial == material,
            $"prefab {PrefabPath}: animator={prefabAnimator != null}, avatar={prefabAnimator?.avatar?.name}, " +
            $"controller={prefabAnimator?.runtimeAnimatorController?.name}, applyRootMotion={prefabAnimator?.applyRootMotion}");
        Check(File.Exists(ScenePath), $"demo scene {ScenePath} exists (not added to Build Settings)");

        report.AppendLine(failures == 0 ? "UNITY_IMPORT_AUDIT_PASSED" : $"UNITY_IMPORT_AUDIT_FAILED: {failures}");
        Directory.CreateDirectory("TestResults");
        File.WriteAllText(AuditPath, report.ToString());
        Debug.Log(report.ToString());
        return failures;
    }
}
