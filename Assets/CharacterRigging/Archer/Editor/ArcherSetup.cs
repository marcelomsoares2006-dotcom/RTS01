using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Configura o Archer no Unity a partir dos FBX gerados por Tools/CharacterRigging/BuildArcher.py e
/// BuildArcherAccessories.py: importacao Humanoid, clipes, Animator Controller, prefabs (visual, projetil, unidade),
/// CharacterData e perfil de ataque. Reexecutavel: recria os assets gerados sem mudar os GUIDs existentes.
/// Menu: Tools > Character Rigging > Archer > Build Assets. Batch: -executeMethod ArcherSetup.BatchBuild
/// </summary>
public static class ArcherSetup
{
    public const string Root = "Assets/CharacterRigging/Archer";
    const string ModelsDir = Root + "/Models";
    const string AccessoriesDir = Root + "/Accessories";
    const string TexturesDir = Root + "/Textures";
    const string MaterialsDir = Root + "/Materials";
    const string AnimationDir = Root + "/Animation";
    const string PrefabsDir = Root + "/Prefabs";
    const string DataDir = Root + "/Data";
    public const string BaseModel = ModelsDir + "/Archer.fbx";
    public const string VisualPrefabPath = PrefabsDir + "/ArcherVisual.prefab";
    public const string ProjectilePrefabPath = PrefabsDir + "/ArcherArrowProjectile.prefab";
    public const string WrapperPrefabPath = "Assets/Resources/Prefabs/Units/Characters/Archer.prefab";
    public const string ControllerPath = AnimationDir + "/Archer.controller";
    public const string CharacterDataPath = "Assets/Resources/ScriptableObjects/Units/Characters/Archer.asset";
    public const string ProfilePath = DataDir + "/ArcherRangedAttackProfile.asset";
    const string SoldierPrefabPath = "Assets/Resources/Prefabs/Units/Characters/Soldier.prefab";
    const string ReportPath = "Docs/CharacterMeshAudit/Archer/unity-build-report.txt";

    // human bone -> skeleton bone of the Meshy rig (the rig names the spine bottom-up as Spine02, Spine01, Spine)
    static readonly Dictionary<string, string> Mapping = new Dictionary<string, string>
    {
        {"Hips","Hips"},{"Spine","Spine02"},{"Chest","Spine01"},{"UpperChest","Spine"},{"Neck","neck"},{"Head","Head"},
        {"LeftShoulder","LeftShoulder"},{"LeftUpperArm","LeftArm"},{"LeftLowerArm","LeftForeArm"},{"LeftHand","LeftHand"},
        {"RightShoulder","RightShoulder"},{"RightUpperArm","RightArm"},{"RightLowerArm","RightForeArm"},{"RightHand","RightHand"},
        {"LeftUpperLeg","LeftUpLeg"},{"LeftLowerLeg","LeftLeg"},{"LeftFoot","LeftFoot"},{"LeftToes","LeftToeBase"},
        {"RightUpperLeg","RightUpLeg"},{"RightLowerLeg","RightLeg"},{"RightFoot","RightFoot"},{"RightToes","RightToeBase"},
    };

    // clip name, loop, events (time in seconds of the source clip; converted to what the importer expects)
    class ClipInfo
    {
        public string Name; public bool Loop; public (string fn, double seconds)[] Events;
    }
    static readonly ClipInfo[] Clips =
    {
        new ClipInfo{Name="Idle", Loop=true, Events=new (string,double)[0]},
        new ClipInfo{Name="Walk", Loop=true, Events=new (string,double)[0]},
        new ClipInfo{Name="Run", Loop=true, Events=new (string,double)[0]},
        new ClipInfo{Name="Aim", Loop=true, Events=new (string,double)[0]},
        // source frames 1..97 @30fps: right hand reaches the quiver at ~f13, nocks the arrow at ~f43
        new ClipInfo{Name="DrawString", Loop=false, Events=new[]{("ArcherArrowGrab",12/30.0),("ArcherArrowNock",42/30.0)}},
        // source frames 109..151: string released at ~f121, motion settles by the end
        new ClipInfo{Name="Fire", Loop=false, Events=new[]{("ArcherArrowRelease",12/30.0),("ArcherAttackEnd",1.30)}},
    };

    static readonly List<string> Notes = new List<string>();

    [MenuItem("Tools/Character Rigging/Archer/Build Assets")]
    public static void Build()
    {
        Notes.Clear();
        AssetDatabase.Refresh();
        foreach (var d in new[] { MaterialsDir, AnimationDir, PrefabsDir, DataDir })
            EnsureFolder(d);

        ConfigureTextures();
        var mats = CreateMaterials();
        var avatar = ConfigureBaseModel(mats["Archer_Mat"]);
        var clips = ConfigureClips(avatar);
        ConfigureAccessoryImporters(mats);
        var controller = BuildController(clips);
        var profile = CreateProfile();
        var projectile = BuildProjectilePrefab();
        var visual = BuildVisualPrefab(controller, avatar, clips["Aim"], projectile, profile, mats);
        var wrapper = BuildWrapperPrefab(visual);
        var data = CreateCharacterData(wrapper);
        AssetDatabase.SaveAssets();
        WriteReport(clips, wrapper, data);
        Debug.Log("ARCHER_SETUP_OK\n" + string.Join("\n", Notes));
    }

    public static void BatchBuild()
    {
        try
        {
            Build();
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("ARCHER_SETUP_FAILED " + e);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>Gera TestResults/pkg/Archer_EraImperial.unitypackage com tudo do Archer (sem dependencias do jogo).</summary>
    public static void ExportPackageBatch()
    {
        try
        {
            Directory.CreateDirectory("TestResults/pkg");
            var paths = new[]
            {
                Root,
                WrapperPrefabPath,
                CharacterDataPath,
                "Assets/Scripts/Units/Archer",
                "Assets/Scripts/Units/UnitManager.cs", // recebe o gancho IAttackHandler
            };
            AssetDatabase.ExportPackage(paths, "TestResults/pkg/Archer_EraImperial.unitypackage", ExportPackageOptions.Recurse);
            Debug.Log("ARCHER_PACKAGE_OK");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("ARCHER_PACKAGE_FAILED " + e);
            EditorApplication.Exit(1);
        }
    }

    // ------------------------------------------------------------------------------------------------ helpers
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static T Require<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Asset ausente: " + path);
        return asset;
    }

    static void ConfigureTextures()
    {
        var normal = (TextureImporter)AssetImporter.GetAtPath(TexturesDir + "/Archer_Normal.png");
        normal.textureType = TextureImporterType.NormalMap;
        normal.maxTextureSize = 2048;
        normal.SaveAndReimport();
        var color = (TextureImporter)AssetImporter.GetAtPath(TexturesDir + "/Archer_BaseColor.png");
        color.textureType = TextureImporterType.Default;
        color.sRGBTexture = true;
        color.maxTextureSize = 2048;
        color.SaveAndReimport();
    }

    static Material MakeMaterial(string name, Color color, float metallic, float smoothness, Texture albedo = null, Texture normal = null)
    {
        string path = MaterialsDir + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = Shader.Find("Standard");
        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        material.mainTexture = albedo;
        if (normal != null)
        {
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    static Dictionary<string, Material> CreateMaterials()
    {
        var baseColor = Require<Texture2D>(TexturesDir + "/Archer_BaseColor.png");
        var normal = Require<Texture2D>(TexturesDir + "/Archer_Normal.png");
        var m = new Dictionary<string, Material>
        {
            ["Archer_Mat"] = MakeMaterial("Archer_Mat", Color.white, 0f, 0.18f, baseColor, normal),
            ["Archer_Wood"] = MakeMaterial("Archer_Wood", new Color(0.36f, 0.20f, 0.09f), 0f, 0.35f),
            ["Archer_Leather"] = MakeMaterial("Archer_Leather", new Color(0.20f, 0.11f, 0.06f), 0f, 0.2f),
            ["Archer_AshShaft"] = MakeMaterial("Archer_AshShaft", new Color(0.62f, 0.45f, 0.25f), 0f, 0.3f),
            ["Archer_Steel"] = MakeMaterial("Archer_Steel", new Color(0.55f, 0.56f, 0.58f), 0.85f, 0.55f),
            ["Archer_Feather"] = MakeMaterial("Archer_Feather", new Color(0.88f, 0.85f, 0.78f), 0f, 0.1f),
        };
        // string and team ring use simple unlit colours
        m["Archer_String"] = MakeUnlit("Archer_String", new Color(0.85f, 0.78f, 0.62f));
        m["Archer_TeamRing"] = MakeUnlit("Archer_TeamRing", Color.white);
        AssetDatabase.SaveAssets();
        return m;
    }

    static Material MakeUnlit(string name, Color color)
    {
        string path = MaterialsDir + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Unlit/Color"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = Shader.Find("Unlit/Color");
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    static Avatar ConfigureBaseModel(Material material)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(BaseModel);
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.importAnimation = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Archer_Mat"), material);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects = false;
        importer.SaveAndReimport();

        var description = importer.humanDescription;
        var auto = (description.human ?? Array.Empty<HumanBone>()).ToDictionary(b => b.humanName, b => b.boneName);
        bool matches = auto.Count == Mapping.Count && Mapping.All(p => auto.TryGetValue(p.Key, out var bone) && bone == p.Value);
        if (!matches)
        {
            Notes.Add("Mapeamento humanoide automatico diferiu; aplicado mapeamento explicito. Auto: " +
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
        else Notes.Add("Mapeamento humanoide automatico coincide com a tabela esperada (22 ossos).");

        var avatar = AssetDatabase.LoadAllAssetsAtPath(BaseModel).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
            throw new InvalidOperationException("Avatar humanoide do Archer invalido.");
        return avatar;
    }

    static Dictionary<string, AnimationClip> ConfigureClips(Avatar avatar)
    {
        var result = new Dictionary<string, AnimationClip>();
        foreach (var info in Clips)
        {
            string path = ModelsDir + "/Archer@" + info.Name + ".fbx";
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
            if (defaults.Length != 1) throw new InvalidOperationException(path + ": esperado um take, obtido " + defaults.Length);
            var clip = defaults[0];
            clip.name = info.Name;
            clip.loopTime = info.Loop;
            clip.loopPose = info.Loop;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.heightFromFeet = false;
            clip.lockRootPositionXZ = false;
            clip.keepOriginalPositionXZ = true;
            // importer events use normalized time (0..1) of the clip
            double duration = DurationSeconds(path, clip);
            clip.events = info.Events.Select(e => new AnimationEvent
            {
                functionName = e.fn,
                time = (float)(e.seconds / duration),
            }).ToArray();
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();

            var asset = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (asset == null) throw new InvalidOperationException(path + ": clipe nao importado");
            result[info.Name] = asset;

            // verify the event times in seconds
            var events = AnimationUtility.GetAnimationEvents(asset);
            foreach (var expected in info.Events)
            {
                var ev = events.FirstOrDefault(x => x.functionName == expected.fn);
                if (ev == null) throw new InvalidOperationException(info.Name + ": evento ausente " + expected.fn);
                Notes.Add($"Clip {info.Name}: {expected.fn} em {ev.time:F3}s (esperado {expected.seconds:F3}s), duracao {asset.length:F3}s");
                if (Mathf.Abs(ev.time - (float)expected.seconds) > 0.08f)
                    throw new InvalidOperationException($"{info.Name}.{expected.fn}: tempo {ev.time} difere do esperado {expected.seconds}");
            }
            Notes.Add($"Clip {info.Name}: {asset.length:F3}s, loop={asset.isLooping}, humanoid={asset.isHumanMotion}");
        }
        return result;
    }

    // duration in seconds of the (single) default clip: frames / frame rate of the source take
    static double DurationSeconds(string path, ModelImporterClipAnimation clip)
    {
        var temp = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (temp != null) return temp.length;
        return (clip.lastFrame - clip.firstFrame) / 30.0;
    }

    static void ConfigureAccessoryImporters(Dictionary<string, Material> mats)
    {
        foreach (var file in new[] { "Archer_Bow", "Archer_Quiver", "Archer_Arrow" })
        {
            string path = AccessoriesDir + "/" + file + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var m in mats)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.Key), m.Value);
            importer.SaveAndReimport();
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            float angle = Quaternion.Angle(root.transform.rotation, Quaternion.identity);
            if (angle > 0.5f) throw new InvalidOperationException($"{file}: o FBX chega ao Unity girado {angle:F1} graus; exporte com bake_space_transform.");
            Notes.Add($"{file}: rotacao de importacao identidade ({angle:F2} graus)");
        }
    }

    // ------------------------------------------------------------------------------------------- animator
    static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
    {
        if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        // parameters the existing behaviour tree also writes (kept so it never logs a missing parameter)
        controller.AddParameter("Running", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attacking", AnimatorControllerParameterType.Bool);

        var machine = controller.layers[0].stateMachine;
        var tree = new BlendTree { name = "Locomotion", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(clips["Idle"], 0f);
        tree.AddChild(clips["Walk"], 1.4f);
        tree.AddChild(clips["Run"], 4.2f);

        var locomotion = machine.AddState("Locomotion");
        locomotion.motion = tree;
        machine.defaultState = locomotion;
        var draw = machine.AddState("DrawString"); draw.motion = clips["DrawString"]; draw.speed = 2.4f;
        var aim = machine.AddState("Aim"); aim.motion = clips["Aim"];
        var fire = machine.AddState("Fire"); fire.motion = clips["Fire"]; fire.speed = 1.5f;

        var toDraw = locomotion.AddTransition(draw);
        toDraw.hasExitTime = false; toDraw.duration = 0.12f; toDraw.AddCondition(AnimatorConditionMode.If, 0, "Hit");
        var toAim = draw.AddTransition(aim);
        toAim.hasExitTime = true; toAim.exitTime = 0.97f; toAim.duration = 0.12f; toAim.hasFixedDuration = true;
        var toFire = aim.AddTransition(fire);
        toFire.hasExitTime = true; toFire.exitTime = 0.12f; toFire.duration = 0.1f; toFire.hasFixedDuration = true;
        var toLoco = fire.AddTransition(locomotion);
        toLoco.hasExitTime = true; toLoco.exitTime = 0.95f; toLoco.duration = 0.2f; toLoco.hasFixedDuration = true;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    // --------------------------------------------------------------------------------------------- prefabs
    static RangedAttackProfile CreateProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<RangedAttackProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<RangedAttackProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        profile.projectileSpeed = 22f;
        profile.maximumRange = 12f;
        profile.attackCooldown = 3f;
        profile.damage = 14;
        EditorUtility.SetDirty(profile);
        return profile;
    }

    static GameObject BuildProjectilePrefab()
    {
        var arrow = Require<GameObject>(AccessoriesDir + "/Archer_Arrow.fbx");
        var root = new GameObject("ArcherArrowProjectile");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(arrow, root.transform);
        model.name = "Arrow";
        // the model's pivot is the nock; centre the arrow on the flight path so it does not trail behind the aim point
        model.transform.localPosition = new Vector3(0f, 0f, -0.36f);
        root.AddComponent<ArcherProjectile>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static Vector3 BlenderToUnity(Vector3 b) { return new Vector3(-b.x, b.z, -b.y); }

    static GameObject BuildVisualPrefab(AnimatorController controller, Avatar avatar, AnimationClip aimClip,
        GameObject projectilePrefab, RangedAttackProfile profile, Dictionary<string, Material> mats)
    {
        var fbx = Require<GameObject>(BaseModel);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        instance.name = "ArcherVisual";
        var animator = instance.GetComponent<Animator>();
        if (animator == null) animator = instance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        var spine = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        if (spine == null || leftHand == null) throw new InvalidOperationException("Ossos humanoides ausentes no Archer.");

        // --- bow: grip pose measured with the Aim clip sampled on a throw-away instance
        GetBowLocalPose(fbx, avatar, aimClip, out Vector3 bowLocalPos, out Quaternion bowLocalRot);
        var bow = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(AccessoriesDir + "/Archer_Bow.fbx"));
        bow.name = "Bow";
        bow.transform.SetParent(leftHand, false);
        bow.transform.localPosition = bowLocalPos;
        bow.transform.localRotation = bowLocalRot;

        var tipTop = FindDeep(bow.transform, "Bow_TipTop");
        var tipBottom = FindDeep(bow.transform, "Bow_TipBottom");
        if (tipTop == null || tipBottom == null) throw new InvalidOperationException("Pontas do arco ausentes (Bow_TipTop/Bow_TipBottom).");
        var stringObject = new GameObject("BowString");
        stringObject.transform.SetParent(bow.transform, false);
        var line = stringObject.AddComponent<LineRenderer>();
        line.sharedMaterial = mats["Archer_String"];
        line.widthMultiplier = 0.005f;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.positionCount = 3;
        line.useWorldSpace = true;
        var bowString = stringObject.AddComponent<ArcherBowString>();
        bowString.tipTop = tipTop;
        bowString.tipBottom = tipBottom;

        // --- quiver: placed in character space at the bind pose (origin measured by BuildArcherAccessories.py)
        var quiver = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(AccessoriesDir + "/Archer_Quiver.fbx"));
        quiver.name = "Quiver";
        Vector3 origin = QuiverOriginUnity();
        quiver.transform.position = instance.transform.TransformPoint(origin);
        quiver.transform.rotation = instance.transform.rotation;
        quiver.transform.SetParent(spine, true);

        // --- arrow held by the drawing hand (positioned by ArcherCombatController in LateUpdate)
        var held = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(AccessoriesDir + "/Archer_Arrow.fbx"), instance.transform);
        held.name = "HeldArrow";
        held.SetActive(false);

        instance.AddComponent<ArcherAnimationEvents>();
        ConfigureLods(instance);

        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, VisualPrefabPath);
        UnityEngine.Object.DestroyImmediate(instance);
        Notes.Add($"Arco: pos local {bowLocalPos:F3}, rot local {bowLocalRot.eulerAngles:F1} (mao esquerda)");
        return prefab;
    }

    static Vector3 QuiverOriginUnity()
    {
        string json = File.ReadAllText("Docs/CharacterMeshAudit/Archer/accessories.json");
        int i = json.IndexOf("quiverOriginCharacterSpaceBlender", StringComparison.Ordinal);
        int a = json.IndexOf('[', i), b = json.IndexOf(']', a);
        var parts = json.Substring(a + 1, b - a - 1).Split(',').Select(s => float.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return BlenderToUnity(new Vector3(parts[0], parts[1], parts[2]));
    }

    // bow grip pose relative to the left hand bone, from the Aim clip: bow upright, facing the character's forward
    static void GetBowLocalPose(GameObject fbx, Avatar avatar, AnimationClip aim, out Vector3 localPos, out Quaternion localRot)
    {
        var temp = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        try
        {
            var animator = temp.GetComponent<Animator>();
            var hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var forearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Vector3 bindHand = hand.position;
            aim.SampleAnimation(temp, 0f);
            if ((hand.position - bindHand).magnitude < 0.15f)
                throw new InvalidOperationException("AnimationClip.SampleAnimation nao moveu a mao esquerda (humanoide nao amostrado).");
            Vector3 dir = (hand.position - forearm.position).normalized;
            Vector3 grip = hand.position + dir * 0.07f;
            Quaternion bowWorld = temp.transform.rotation;
            localPos = hand.InverseTransformPoint(grip);
            localRot = Quaternion.Inverse(hand.rotation) * bowWorld;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(temp);
        }
    }

    static void ConfigureLods(GameObject instance)
    {
        var lodGroup = instance.GetComponentInChildren<LODGroup>(true);
        var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Where(r => r.name.Contains("_LOD")).OrderBy(r => r.name).ToArray();
        if (renderers.Length != 3) throw new InvalidOperationException("Esperado 3 SkinnedMeshRenderer _LOD0/1/2, encontrado " + renderers.Length);
        if (lodGroup == null)
        {
            lodGroup = instance.AddComponent<LODGroup>();
            Notes.Add("O importador nao criou LODGroup; criado manualmente.");
        }
        // The game camera is orthographic (zoom clamp 8..26, default 18) and the Ultra quality level uses lodBias 2. Measured in
        // Play Mode (ArcherPlayModeCheck): effective screen-relative height ~= 2.2 / orthoSize -> 0.275 at size 8, 0.122 at 18, 0.085 at 26.
        lodGroup.SetLODs(new[]
        {
            new LOD(0.17f, new Renderer[] { renderers[0] }),   // closest zooms (ortho size < ~13)
            new LOD(0.10f, new Renderer[] { renderers[1] }),   // default zoom (up to ~22)
            new LOD(0.0f,  new Renderer[] { renderers[2] }),   // far zoom; never culled
        });
        lodGroup.fadeMode = LODFadeMode.None;
        lodGroup.RecalculateBounds();
        foreach (var r in renderers) r.updateWhenOffscreen = false;
    }

    static Mesh CreateRingMesh()
    {
        string path = Root + "/Models/TeamRing.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
        const int segments = 40; float inner = 0.42f, outer = 0.52f;
        var vertices = new Vector3[segments * 2];
        var triangles = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            vertices[i * 2] = d * inner;
            vertices[i * 2 + 1] = d * outer;
            int n = (i + 1) % segments;
            int t = i * 6;
            triangles[t] = i * 2; triangles[t + 1] = n * 2 + 1; triangles[t + 2] = i * 2 + 1;
            triangles[t + 3] = i * 2; triangles[t + 4] = n * 2; triangles[t + 5] = n * 2 + 1;
        }
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static GameObject BuildWrapperPrefab(GameObject visualPrefab)
    {
        var soldier = Require<GameObject>(SoldierPrefabPath);
        var root = (GameObject)PrefabUtility.InstantiatePrefab(soldier);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        root.name = "Archer";

        var mesh = root.transform.Find("Mesh");
        var icon = mesh.Find("MinimapIcon");
        Vector3 iconPosition = icon.position; Quaternion iconRotation = icon.rotation; Vector3 iconScale = icon.lossyScale;
        UnityEngine.Object.DestroyImmediate(mesh.GetComponent<MeshRenderer>());
        UnityEngine.Object.DestroyImmediate(mesh.GetComponent<MeshFilter>());
        mesh.localPosition = Vector3.zero; mesh.localRotation = Quaternion.identity; mesh.localScale = Vector3.one;
        icon.position = iconPosition; icon.rotation = iconRotation; icon.localScale = iconScale;

        var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, mesh);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        var ring = new GameObject("TeamRing");
        ring.transform.SetParent(mesh, false);
        ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        ring.AddComponent<MeshFilter>().sharedMesh = CreateRingMesh();
        var ringRenderer = ring.AddComponent<MeshRenderer>();
        ringRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/Archer_TeamRing.mat");
        ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ringRenderer.receiveShadows = false;

        var manager = root.GetComponent("CharacterManager") as MonoBehaviour;
        if (manager == null) throw new InvalidOperationException("Soldier.prefab sem CharacterManager.");
        var serialized = new SerializedObject(manager);
        serialized.FindProperty("meshRenderer").objectReferenceValue = ringRenderer;
        serialized.FindProperty("animator").objectReferenceValue = visual.GetComponent<Animator>();
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var agent = root.GetComponent<NavMeshAgent>();
        agent.speed = 4.5f;
        agent.acceleration = 10f;
        agent.stoppingDistance = 0.5f;

        var combat = root.AddComponent<ArcherCombatController>();
        var visualAnimator = visual.GetComponent<Animator>();
        combat.animator = visualAnimator;
        combat.bow = FindDeep(visual.transform, "Bow");
        combat.heldArrow = FindDeep(visual.transform, "HeldArrow");
        combat.bowString = visual.GetComponentInChildren<ArcherBowString>(true);
        combat.arrowProjectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
        combat.profile = AssetDatabase.LoadAssetAtPath<RangedAttackProfile>(ProfilePath);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, WrapperPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    static CharacterData CreateCharacterData(GameObject wrapper)
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(CharacterDataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<CharacterData>();
            AssetDatabase.CreateAsset(data, CharacterDataPath);
        }
        data.code = "archer";
        data.unitName = "Archer";
        data.description = "Arqueiro medieval: ataca a distancia com arco longo.";
        data.healthpoints = 40;
        data.prefab = wrapper;
        data.cost = new List<ResourceValue> { new ResourceValue(InGameResource.Gold, 60), new ResourceValue(InGameResource.Wood, 30) };
        data.canProduce = new InGameResource[0];
        data.skills = new List<SkillData>();
        data.fieldOfView = 18f;
        data.attackRange = 12f;
        data.attackDamage = 14;
        data.attackRate = 3f;
        data.buildRange = 0f;
        data.buildPower = 0;
        data.buildRate = 0f;
        EditorUtility.SetDirty(data);

        string contractPath = Root + "/Data/ArcherUnitContract.asset";
        var contract = AssetDatabase.LoadAssetAtPath<ArcherUnitContract>(contractPath);
        if (contract == null)
        {
            contract = ScriptableObject.CreateInstance<ArcherUnitContract>();
            AssetDatabase.CreateAsset(contract, contractPath);
        }
        var so = new SerializedObject(contract);
        so.FindProperty("sourceMeshProvided").boolValue = true;
        so.FindProperty("sourceLicenseConfirmed").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(contract);
        return data;
    }

    static void WriteReport(Dictionary<string, AnimationClip> clips, GameObject wrapper, CharacterData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Archer - Unity build report (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")");
        foreach (var n in Notes) sb.AppendLine("- " + n);
        sb.AppendLine("Wrapper: " + AssetDatabase.GetAssetPath(wrapper));
        sb.AppendLine("CharacterData: " + AssetDatabase.GetAssetPath(data) + " code=" + data.code);
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, sb.ToString());
    }
}
