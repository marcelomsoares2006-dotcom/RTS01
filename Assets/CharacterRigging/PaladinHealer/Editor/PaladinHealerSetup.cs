using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EraImperial.Units.Advanced;
using EraImperial.Units.Paladin;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Configura o Paladino curador (paladin_healer) no Unity a partir dos FBX gerados por
/// Tools/CharacterRigging/BuildPaladinHealer.py e BuildPaladinHealerAccessories.py: importacao Humanoid, clipes,
/// Animator Controller (locomocao + ataque + reacao a dano + camada de cura so do tronco), prefab visual com espada e
/// escudo nos soquetes, prefab de morte, efeito de cura, wrapper jogavel, vinculo ao contrato e registro condicionado
/// no catalogo. Reexecutavel: recria os assets gerados mantendo o GUID do wrapper, do efeito e dos prefabs.
/// Menu: Tools > Character Rigging > Paladin Healer > Build Assets. Batch: -executeMethod PaladinHealerSetup.BatchBuild
/// </summary>
public static class PaladinHealerSetup
{
    public const string Root = "Assets/CharacterRigging/PaladinHealer";
    public const string ModelsDir = Root + "/Models";
    public const string AccessoriesDir = Root + "/Accessories";
    public const string TexturesDir = Root + "/Textures";
    public const string MaterialsDir = Root + "/Materials";
    public const string AnimationDir = Root + "/Animation";
    public const string PrefabsDir = Root + "/Prefabs";
    public const string BaseModel = ModelsDir + "/PaladinHealer.fbx";
    public const string VisualPrefabPath = PrefabsDir + "/PaladinHealerVisual.prefab";
    public const string DeathPrefabPath = PrefabsDir + "/PaladinHealerDeath.prefab";
    public const string EffectPrefabPath = "Assets/Resources/Prefabs/Effects/PaladinHealAura.prefab";
    public const string WrapperPrefabPath = "Assets/Resources/Prefabs/Units/Characters/PaladinHealer.prefab";
    public const string ControllerPath = AnimationDir + "/PaladinHealer.controller";
    public const string DeathControllerPath = AnimationDir + "/PaladinHealerDeath.controller";
    public const string MaskPath = AnimationDir + "/PaladinHealer_UpperBody.mask";
    public const string QuadPath = PrefabsDir + "/PaladinHealAura_Quad.asset";
    public const string CharacterDataPath = "Assets/Resources/ScriptableObjects/Units/Characters/PaladinHealer.asset";
    public const string MountsPath = "Docs/CharacterMeshAudit/PaladinHealer/mounts.json";
    const string ReportPath = "Docs/CharacterMeshAudit/PaladinHealer/unity-build-report.txt";

    public const float RingRadius = 0.55f;
    public const float AgentSpeed = 4f;
    public const float WalkSpeed = 1.5f, RunSpeed = 4f;
    public const float HealAuraHeight = 1.2f;   // altura do peito (HealAuraCenter)

    public static readonly string[] ClipNames = { "Idle", "Walk", "Run", "Attack", "Heal", "Hit", "Death" };

    // osso humano -> osso do esqueleto Meshy (o rig nomeia a coluna de baixo para cima: Spine02, Spine01, Spine)
    static readonly Dictionary<string, string> Mapping = new Dictionary<string, string>
    {
        {"Hips","Hips"},{"Spine","Spine02"},{"Chest","Spine01"},{"UpperChest","Spine"},{"Neck","neck"},{"Head","Head"},
        {"LeftShoulder","LeftShoulder"},{"LeftUpperArm","LeftArm"},{"LeftLowerArm","LeftForeArm"},{"LeftHand","LeftHand"},
        {"RightShoulder","RightShoulder"},{"RightUpperArm","RightArm"},{"RightLowerArm","RightForeArm"},{"RightHand","RightHand"},
        {"LeftUpperLeg","LeftUpLeg"},{"LeftLowerLeg","LeftLeg"},{"LeftFoot","LeftFoot"},{"LeftToes","LeftToeBase"},
        {"RightUpperLeg","RightUpLeg"},{"RightLowerLeg","RightLeg"},{"RightFoot","RightFoot"},{"RightToes","RightToeBase"},
    };

    class ClipInfo { public string Name; public bool Loop; public bool BakeXZ; }
    static readonly ClipInfo[] Clips =
    {
        new ClipInfo{Name="Idle", Loop=true}, new ClipInfo{Name="Walk", Loop=true}, new ClipInfo{Name="Run", Loop=true},
        new ClipInfo{Name="Attack"}, new ClipInfo{Name="Heal"}, new ClipInfo{Name="Hit"}, new ClipInfo{Name="Death", BakeXZ=true},
    };

    [Serializable] class MountEntry { public string bone; public float[] worldPosition; public float[] rot9; }
    [Serializable] class MountFile { public MountEntry sword; public MountEntry shield; }

    static readonly List<string> Notes = new List<string>();

    [MenuItem("Tools/Character Rigging/Paladin Healer/Build Assets")]
    public static void Build()
    {
        Notes.Clear();
        AssetDatabase.Refresh();
        foreach (var d in new[] { MaterialsDir, AnimationDir, PrefabsDir, "Assets/Resources/Prefabs/Effects" }) EnsureFolder(d);

        ConfigureTextures();
        var mats = CreateMaterials();
        var avatar = ConfigureBaseModel(mats["PaladinHealer_Mat"]);
        var clips = ConfigureClips(avatar);
        ConfigureAccessoryImporters(mats);
        var mask = CreateUpperBodyMask();
        var controller = BuildController(clips, mask);
        var deathController = BuildDeathController(clips["Death"]);
        var visual = BuildVisualPrefab(controller, avatar);
        var death = BuildDeathPrefab(visual, deathController);
        var effect = BuildEffectPrefab(mats);
        var contract = AdvancedUnitBuilders.LoadContract<PaladinHealerContract>("PaladinHealer");
        var wrapper = BuildWrapperPrefab(visual, death, effect, contract);
        LinkContract(contract, wrapper, effect, death, clips);
        var data = RegisterCatalog(contract);
        AssetDatabase.SaveAssets();
        WriteReport(clips, wrapper, data, contract);
        Debug.Log("PALADINHEALER_SETUP_OK\n" + string.Join("\n", Notes));
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
            Debug.LogError("PALADINHEALER_SETUP_FAILED " + e);
            EditorApplication.Exit(1);
        }
    }

    // ------------------------------------------------------------------------------------------------ helpers
    static void EnsureFolder(string path) { AdvancedUnitBuilders.EnsureFolder(path); }

    static T Require<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Asset ausente: " + path);
        return asset;
    }

    static Vector3 BlenderToUnity(Vector3 b) { return new Vector3(-b.x, b.z, -b.y); }

    // rotacao do Blender (colunas = eixos do objeto no espaco do personagem) -> Unity: R' = C R C^T, C = [[-1,0,0],[0,0,1],[0,-1,0]]
    static Quaternion BlenderRotationToUnity(float[] r)
    {
        Func<int, int, float> m = (row, col) => r[row * 3 + col];
        float[,] c = { { -1, 0, 0 }, { 0, 0, 1 }, { 0, -1, 0 } };
        float[,] rb = new float[3, 3];
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) rb[i, j] = m(i, j);
        float[,] tmp = new float[3, 3], res = new float[3, 3];
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) for (int k = 0; k < 3; k++) tmp[i, j] += c[i, k] * rb[k, j];
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) for (int k = 0; k < 3; k++) res[i, j] += tmp[i, k] * c[j, k];
        Vector3 up = new Vector3(res[0, 1], res[1, 1], res[2, 1]);
        Vector3 forward = new Vector3(res[0, 2], res[1, 2], res[2, 2]);
        return Quaternion.LookRotation(forward, up);
    }

    static void ConfigureTextures()
    {
        var normal = (TextureImporter)AssetImporter.GetAtPath(TexturesDir + "/PaladinHealer_Normal.png");
        normal.textureType = TextureImporterType.NormalMap;
        normal.maxTextureSize = 2048;
        normal.SaveAndReimport();
        var color = (TextureImporter)AssetImporter.GetAtPath(TexturesDir + "/PaladinHealer_BaseColor.png");
        color.textureType = TextureImporterType.Default;
        color.sRGBTexture = true;
        color.maxTextureSize = 2048;
        color.SaveAndReimport();
        var metal = (TextureImporter)AssetImporter.GetAtPath(TexturesDir + "/PaladinHealer_MetallicSmooth.png");
        metal.textureType = TextureImporterType.Default;
        metal.sRGBTexture = false;
        metal.alphaSource = TextureImporterAlphaSource.FromInput;
        metal.alphaIsTransparency = false;
        metal.maxTextureSize = 2048;
        metal.SaveAndReimport();
        foreach (var n in new[] { "PaladinHealAura_Ring", "PaladinHealAura_Disc", "PaladinHealAura_Mote" })
        {
            var fx = (TextureImporter)AssetImporter.GetAtPath(TexturesDir + "/" + n + ".png");
            fx.textureType = TextureImporterType.Default;
            fx.sRGBTexture = true;
            fx.alphaSource = TextureImporterAlphaSource.FromInput;
            fx.alphaIsTransparency = true;
            fx.mipmapEnabled = true;
            fx.wrapMode = TextureWrapMode.Clamp;
            fx.maxTextureSize = 512;
            fx.SaveAndReimport();
        }
    }

    static Material MakeMaterial(string name, string shaderName, Action<Material> configure)
    {
        string path = MaterialsDir + "/" + name + ".mat";
        var shader = Shader.Find(shaderName);
        if (shader == null) throw new InvalidOperationException("Shader nao encontrado: " + shaderName);
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        configure(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material Standard(string name, Color color, float metallic, float smoothness)
    {
        return MakeMaterial(name, "Standard", m =>
        {
            m.color = color;
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", smoothness);
        });
    }

    static Material Additive(string name, string texture, Color tint)
    {
        return MakeMaterial(name, "Legacy Shaders/Particles/Additive", m =>
        {
            m.mainTexture = Require<Texture2D>(TexturesDir + "/" + texture + ".png");
            m.SetColor("_TintColor", tint);
        });
    }

    static Dictionary<string, Material> CreateMaterials()
    {
        var baseColor = Require<Texture2D>(TexturesDir + "/PaladinHealer_BaseColor.png");
        var normal = Require<Texture2D>(TexturesDir + "/PaladinHealer_Normal.png");
        var metal = Require<Texture2D>(TexturesDir + "/PaladinHealer_MetallicSmooth.png");
        var m = new Dictionary<string, Material>();
        m["PaladinHealer_Mat"] = MakeMaterial("PaladinHealer_Mat", "Standard", mat =>
        {
            mat.color = Color.white;
            mat.mainTexture = baseColor;
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", metal);
            mat.SetFloat("_Metallic", 1f);
            mat.SetFloat("_GlossMapScale", 1f);
            mat.SetFloat("_SmoothnessTextureChannel", 0f);
            mat.EnableKeyword("_METALLICGLOSSMAP");
        });
        m["PaladinHealer_Steel"] = Standard("PaladinHealer_Steel", new Color(0.66f, 0.68f, 0.72f), 0.6f, 0.55f);
        m["PaladinHealer_Gold"] = Standard("PaladinHealer_Gold", new Color(0.90f, 0.70f, 0.24f), 0.65f, 0.6f);
        m["PaladinHealer_Leather"] = Standard("PaladinHealer_Leather", new Color(0.22f, 0.12f, 0.06f), 0f, 0.2f);
        m["PaladinHealer_Wood"] = Standard("PaladinHealer_Wood", new Color(0.40f, 0.25f, 0.12f), 0f, 0.3f);
        m["PaladinHealer_Linen"] = Standard("PaladinHealer_Linen", new Color(0.90f, 0.88f, 0.82f), 0f, 0.15f);
        m["PaladinHealAura_Ring"] = Additive("PaladinHealAura_Ring", "PaladinHealAura_Ring", new Color(1f, 0.82f, 0.40f, 0.62f));
        m["PaladinHealAura_RingTrail"] = Additive("PaladinHealAura_RingTrail", "PaladinHealAura_Ring", new Color(1f, 0.90f, 0.60f, 0.38f));
        m["PaladinHealAura_Disc"] = Additive("PaladinHealAura_Disc", "PaladinHealAura_Disc", new Color(1f, 0.82f, 0.40f, 0.42f));
        m["PaladinHealAura_Mote"] = Additive("PaladinHealAura_Mote", "PaladinHealAura_Mote", new Color(1f, 0.9f, 0.6f, 0.9f));
        AssetDatabase.SaveAssets();
        return m;
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
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "PaladinHealer_Mat"), material);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects = false;
        importer.SaveAndReimport();

        var description = importer.humanDescription;
        var auto = (description.human ?? Array.Empty<HumanBone>()).ToDictionary(b => b.humanName, b => b.boneName);
        bool matches = auto.Count == Mapping.Count && Mapping.All(p => auto.TryGetValue(p.Key, out var bone) && bone == p.Value);
        if (!matches)
        {
            Notes.Add("Mapeamento humanoide automatico diferiu; aplicado mapeamento explicito. Auto: " + string.Join(", ", auto.Select(p => p.Key + "=" + p.Value)));
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
        if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("Avatar humanoide do PaladinHealer invalido.");
        return avatar;
    }

    static Dictionary<string, AnimationClip> ConfigureClips(Avatar avatar)
    {
        var result = new Dictionary<string, AnimationClip>();
        foreach (var info in Clips)
        {
            string path = ModelsDir + "/PaladinHealer@" + info.Name + ".fbx";
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
            clip.lockRootPositionXZ = info.BakeXZ;
            clip.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();

            var asset = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (asset == null) throw new InvalidOperationException(path + ": clipe nao importado");
            result[info.Name] = asset;
            Notes.Add($"Clip {info.Name}: {asset.length:F3}s, loop={asset.isLooping}, humanoide={asset.isHumanMotion}");
        }
        return result;
    }

    static void ConfigureAccessoryImporters(Dictionary<string, Material> mats)
    {
        foreach (var file in new[] { "PaladinHealer_Sword", "PaladinHealer_Shield" })
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
            foreach (var m in mats.Where(p => !p.Key.StartsWith("PaladinHealAura") && p.Key != "PaladinHealer_Mat"))
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.Key), m.Value);
            importer.SaveAndReimport();
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            float angle = Quaternion.Angle(root.transform.rotation, Quaternion.identity);
            if (angle > 0.5f) throw new InvalidOperationException($"{file}: o FBX chega ao Unity girado {angle:F1} graus.");
            Notes.Add($"{file}: rotacao de importacao identidade ({angle:F2} graus)");
        }
    }

    // ------------------------------------------------------------------------------------------- animator
    static AvatarMask CreateUpperBodyMask()
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, MaskPath);
        }
        foreach (AvatarMaskBodyPart part in Enum.GetValues(typeof(AvatarMaskBodyPart)))
        {
            if (part == AvatarMaskBodyPart.LastBodyPart) continue;
            bool on = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head || part == AvatarMaskBodyPart.LeftArm ||
                      part == AvatarMaskBodyPart.RightArm || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
            mask.SetHumanoidBodyPartActive(part, on);
        }
        EditorUtility.SetDirty(mask);
        return mask;
    }

    static AnimatorController BuildController(Dictionary<string, AnimationClip> clips, AvatarMask mask)
    {
        if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AdvancedUnitBuilders.AddStandardParameters(controller);   // Running, Attacking, Hit, Speed, Fire, Damaged, Heal

        var machine = controller.layers[0].stateMachine;
        var tree = new BlendTree { name = "Locomotion", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(clips["Idle"], 0f);
        tree.AddChild(clips["Walk"], WalkSpeed);
        tree.AddChild(clips["Run"], RunSpeed);
        var locomotion = machine.AddState("Locomotion");
        locomotion.motion = tree;
        machine.defaultState = locomotion;

        // Hit = gatilho do ataque corpo a corpo escrito pelo behavior tree (TaskAttack)
        var attack = machine.AddState("Attack"); attack.motion = clips["Attack"]; attack.speed = 1.4f;
        var toAttack = locomotion.AddTransition(attack);
        toAttack.hasExitTime = false; toAttack.duration = 0.10f; toAttack.AddCondition(AnimatorConditionMode.If, 0, "Hit");
        var attackBack = attack.AddTransition(locomotion);
        attackBack.hasExitTime = true; attackBack.exitTime = 0.9f; attackBack.duration = 0.15f; attackBack.hasFixedDuration = true;

        // Damaged = reacao a dano (interrompe qualquer estado do corpo)
        var react = machine.AddState("HitReact"); react.motion = clips["Hit"]; react.speed = 1.25f;
        var anyToReact = machine.AddAnyStateTransition(react);
        anyToReact.hasExitTime = false; anyToReact.duration = 0.06f; anyToReact.canTransitionToSelf = false;
        anyToReact.AddCondition(AnimatorConditionMode.If, 0, "Damaged");
        var reactBack = react.AddTransition(locomotion);
        reactBack.hasExitTime = true; reactBack.exitTime = 0.85f; reactBack.duration = 0.15f; reactBack.hasFixedDuration = true;

        // camada de cura: so tronco/bracos/cabeca (as pernas continuam na locomocao)
        controller.AddLayer("Heal");
        var layers = controller.layers;
        var healMachine = layers[1].stateMachine;
        var empty = healMachine.AddState("Empty");
        healMachine.defaultState = empty;
        var heal = healMachine.AddState("Heal"); heal.motion = clips["Heal"]; heal.speed = 1.15f;
        var toHeal = empty.AddTransition(heal);
        toHeal.hasExitTime = false; toHeal.duration = 0.08f; toHeal.AddCondition(AnimatorConditionMode.If, 0, "Heal");
        var healBack = heal.AddTransition(empty);
        healBack.hasExitTime = true; healBack.exitTime = 0.92f; healBack.duration = 0.15f; healBack.hasFixedDuration = true;
        layers[1].avatarMask = mask;
        layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
        layers[1].defaultWeight = 1f;
        controller.layers = layers;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static AnimatorController BuildDeathController(AnimationClip death)
    {
        if (File.Exists(DeathControllerPath)) AssetDatabase.DeleteAsset(DeathControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(DeathControllerPath);
        var state = controller.layers[0].stateMachine.AddState("Death");
        state.motion = death;
        controller.layers[0].stateMachine.defaultState = state;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    // --------------------------------------------------------------------------------------------- prefabs
    static GameObject BuildVisualPrefab(AnimatorController controller, Avatar avatar)
    {
        var fbx = Require<GameObject>(BaseModel);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        instance.name = "PaladinHealerVisual";
        var animator = instance.GetComponent<Animator>();
        if (animator == null) animator = instance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        var leftForeArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        if (rightHand == null || leftForeArm == null) throw new InvalidOperationException("Ossos humanoides ausentes no PaladinHealer.");

        var mounts = JsonUtility.FromJson<MountFile>(File.ReadAllText(MountsPath));
        var weaponMount = MakeMount("WeaponMount", instance.transform, rightHand, mounts.sword);
        var shieldMount = MakeMount("ShieldMount", instance.transform, leftForeArm, mounts.shield);

        var sword = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(AccessoriesDir + "/PaladinHealer_Sword.fbx"), weaponMount);
        sword.name = "Sword";
        sword.transform.localPosition = Vector3.zero; sword.transform.localRotation = Quaternion.identity;
        var shield = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(AccessoriesDir + "/PaladinHealer_Shield.fbx"), shieldMount);
        shield.name = "Shield";
        shield.transform.localPosition = Vector3.zero; shield.transform.localRotation = Quaternion.identity;

        var center = new GameObject("HealAuraCenter").transform;
        center.SetParent(instance.transform, false);
        center.localPosition = new Vector3(0f, HealAuraHeight, 0f);

        ConfigureLods(instance);
        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, VisualPrefabPath);
        UnityEngine.Object.DestroyImmediate(instance);
        return prefab;
    }

    static Transform MakeMount(string name, Transform visualRoot, Transform bone, MountEntry entry)
    {
        var go = new GameObject(name);
        Vector3 posB = new Vector3(entry.worldPosition[0], entry.worldPosition[1], entry.worldPosition[2]);
        go.transform.position = visualRoot.TransformPoint(BlenderToUnity(posB));
        go.transform.rotation = visualRoot.rotation * BlenderRotationToUnity(entry.rot9);
        go.transform.SetParent(bone, true);
        return go.transform;
    }

    static void ConfigureLods(GameObject instance)
    {
        var lodGroup = instance.GetComponentInChildren<LODGroup>(true);
        var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.Contains("_LOD")).OrderBy(r => r.name).ToArray();
        if (renderers.Length != 3) throw new InvalidOperationException("Esperado 3 SkinnedMeshRenderer _LOD0/1/2, encontrado " + renderers.Length);
        if (lodGroup == null)
        {
            lodGroup = instance.AddComponent<LODGroup>();
            Notes.Add("O importador nao criou LODGroup; criado manualmente.");
        }
        // mesma calibracao do Archer: camera ortografica (zoom 8..26) e lodBias 2 -> LOD0 ate ~13, LOD1 ate ~22, LOD2 alem
        lodGroup.SetLODs(new[]
        {
            new LOD(0.17f, new Renderer[] { renderers[0] }),
            new LOD(0.10f, new Renderer[] { renderers[1] }),
            new LOD(0.0f,  new Renderer[] { renderers[2] }),
        });
        lodGroup.fadeMode = LODFadeMode.None;
        lodGroup.RecalculateBounds();
        foreach (var r in renderers) r.updateWhenOffscreen = false;
    }

    static GameObject BuildDeathPrefab(GameObject visual, AnimatorController deathController)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(visual);
        instance.name = "PaladinHealerDeath";
        var animator = instance.GetComponent<Animator>();
        animator.runtimeAnimatorController = deathController;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, DeathPrefabPath);
        UnityEngine.Object.DestroyImmediate(instance);
        return prefab;
    }

    public const string RingMeshPath = PrefabsDir + "/PaladinHealer_TeamRing.asset";

    static Mesh CreateTeamRingMesh()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RingMeshPath);
        if (mesh == null) { mesh = new Mesh { name = "PaladinHealer_TeamRing" }; AssetDatabase.CreateAsset(mesh, RingMeshPath); }
        const int segments = 48;
        float outer = RingRadius, inner = RingRadius * 0.9f;
        var vertices = new Vector3[segments * 2];
        var triangles = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            vertices[i * 2] = d * outer;
            vertices[i * 2 + 1] = d * inner;
            int n = (i + 1) % segments, t = i * 6;
            triangles[t] = i * 2; triangles[t + 1] = i * 2 + 1; triangles[t + 2] = n * 2;
            triangles[t + 3] = i * 2 + 1; triangles[t + 4] = n * 2 + 1; triangles[t + 5] = n * 2;
        }
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static Mesh CreateQuad()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(QuadPath);
        if (mesh == null) { mesh = new Mesh { name = "PaladinHealAura_Quad" }; AssetDatabase.CreateAsset(mesh, QuadPath); }
        mesh.Clear();
        // quadrado de 1 x 1 no plano XZ (diametro unitario), face para cima
        mesh.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static Renderer MakeDisc(string name, Transform parent, Mesh quad, Material material, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, height, 0f);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    static GameObject BuildEffectPrefab(Dictionary<string, Material> mats)
    {
        var quad = CreateQuad();
        var root = new GameObject("PaladinHealAura");   // diametro 1 em XZ; o PaladinHealBehaviour aplica escala (2r, 1, 2r)
        float ground = -HealAuraHeight + 0.04f;           // a origem do efeito e o HealAuraCenter (peito): os aneis ficam no chao
        var disc = MakeDisc("Disc", root.transform, quad, mats["PaladinHealAura_Disc"], ground);
        var trail = MakeDisc("RingTrail", root.transform, quad, mats["PaladinHealAura_RingTrail"], ground + 0.01f);
        var ring = MakeDisc("Ring", root.transform, quad, mats["PaladinHealAura_Ring"], ground + 0.02f);

        var motes = new GameObject("Motes");
        motes.transform.SetParent(root.transform, false);
        motes.transform.localPosition = new Vector3(0f, ground, 0f);
        motes.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = motes.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 1.2f; main.loop = false; main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.28f);
        main.startColor = new Color(1f, 0.9f, 0.6f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;   // a escala (2r,1,2r) alarga so a area de emissao, nao o tamanho das particulas
        main.gravityModifier = 0f;
        main.maxParticles = 200;
        main.stopAction = ParticleSystemStopAction.None;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 60), new ParticleSystem.Burst(0.25f, 30) });
        var shape = ps.shape;
        shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.5f; shape.radiusThickness = 1f;
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);   // as tres curvas precisam estar no mesmo modo (dois valores)
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.75f), 0f), new GradientColorKey(new Color(1f, 0.8f, 0.35f), 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true; colorOverLifetime.color = gradient;
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.3f)));
        var psr = motes.GetComponent<ParticleSystemRenderer>();
        psr.sharedMaterial = mats["PaladinHealAura_Mote"];
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psr.receiveShadows = false;

        var effect = root.AddComponent<PaladinHealAuraEffect>();
        effect.ring = ring; effect.ringTrail = trail; effect.disc = disc;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, EffectPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject BuildWrapperPrefab(GameObject visualPrefab, GameObject deathPrefab, GameObject effectPrefab, PaladinHealerContract contract)
    {
        var root = AdvancedUnitBuilders.BuildWrapperRoot("PaladinHealer", visualPrefab, RingRadius, out GameObject visual);
        var animator = visual.GetComponent<Animator>();

        // o anel compartilhado (AdvancedUnitBuilders.GetOrCreateRingMesh) tem as normais para baixo e some visto de cima: usa-se malha propria com normais para cima
        var ring = root.transform.Find("Mesh/TeamRing");
        ring.GetComponent<MeshFilter>().sharedMesh = CreateTeamRingMesh();

        var collider = root.GetComponent<BoxCollider>();
        collider.center = new Vector3(0f, 1f, 0f);
        collider.size = new Vector3(0.9f, 2f, 0.9f);
        AdvancedUnitBuilders.ConfigureAgent(root, AgentSpeed, 8f, root.GetComponent<NavMeshAgent>().stoppingDistance, 180f);

        var binding = root.AddComponent<AdvancedUnitEraBinding>();
        binding.contract = contract;
        var driver = root.AddComponent<AdvancedUnitAnimatorDriver>();
        driver.animator = animator;
        // golpe corpo a corpo com o cooldown da era (o BT apenas sonda a cada AttackPollSeconds)
        var melee = root.AddComponent<PaladinMeleeAttackController>();
        melee.animator = animator;
        var heal = root.AddComponent<PaladinHealBehaviour>();
        heal.contract = contract;
        heal.animator = animator;
        heal.effectOrigin = AdvancedUnitBuilders.FindDeep(visual.transform, "HealAuraCenter");
        heal.healEffectPrefab = effectPrefab;
        var deathEffect = root.AddComponent<AdvancedUnitDeathEffect>();
        deathEffect.deathPrefab = deathPrefab;

        return AdvancedUnitBuilders.SaveWrapper(root, WrapperPrefabPath);
    }

    // -------------------------------------------------------------------------------------------- contract
    static void SetReferences(AdvancedUnitContract contract, string listName, (string role, string path, UnityEngine.Object asset)[] items)
    {
        var so = new SerializedObject(contract);
        var list = so.FindProperty("visual." + listName);
        list.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
        {
            var e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("role").stringValue = items[i].role;
            e.FindPropertyRelative("expectedAssetPath").stringValue = items[i].path;
            e.FindPropertyRelative("asset").objectReferenceValue = items[i].asset;
            e.FindPropertyRelative("required").boolValue = true;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(contract);
    }

    static void LinkContract(PaladinHealerContract contract, GameObject wrapper, GameObject effect, GameObject death, Dictionary<string, AnimationClip> clips)
    {
        var body = Require<GameObject>(BaseModel);
        var sword = Require<GameObject>(AccessoriesDir + "/PaladinHealer_Sword.fbx");
        var shield = Require<GameObject>(AccessoriesDir + "/PaladinHealer_Shield.fbx");
        SetReferences(contract, "meshes", new[]
        {
            ("malha-fonte do corpo", BaseModel, (UnityEngine.Object)body),
            ("espada", AccessoriesDir + "/PaladinHealer_Sword.fbx", (UnityEngine.Object)sword),
            ("escudo", AccessoriesDir + "/PaladinHealer_Shield.fbx", (UnityEngine.Object)shield),
        });
        SetReferences(contract, "effects", new[]
        {
            ("efeito de aura de cura", EffectPrefabPath, (UnityEngine.Object)effect),
            ("prefab de morte", DeathPrefabPath, (UnityEngine.Object)death),
        });

        // estados -> parametro do Animator que os dispara (Hit = gatilho do ataque do behavior tree; Death vem do prefab de morte)
        var so = new SerializedObject(contract);
        var animations = so.FindProperty("visual.animations");
        var spec = new (string state, string parameter)[]
        {
            ("Idle", "Speed"), ("Walk", "Speed"), ("Run", "Speed"), ("Attack", "Hit"), ("Heal", "Heal"), ("Hit", "Damaged"), ("Death", "AdvancedUnitDeathEffect"),
        };
        animations.arraySize = spec.Length;
        for (int i = 0; i < spec.Length; i++)
        {
            var e = animations.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("state").stringValue = spec[i].state;
            e.FindPropertyRelative("animatorParameter").stringValue = spec[i].parameter;
            e.FindPropertyRelative("clip").objectReferenceValue = clips[spec[i].state];
            e.FindPropertyRelative("required").boolValue = true;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        AdvancedUnitBuilders.BindPrefab(contract, wrapper);
        foreach (var name in new[] { "WeaponMount", "ShieldMount", "HealAuraCenter" })
        {
            var node = AdvancedUnitBuilders.FindDeep(wrapper.transform, name);
            if (node == null) throw new InvalidOperationException("Socket ausente no wrapper: " + name);
            AdvancedUnitBuilders.BindSocket(contract, name, AdvancedUnitBuilders.RelativePath(wrapper.transform, node));
        }
        // vinculos explicitos tambem pelos auxiliares compartilhados (atualizam o caminho esperado)
        AdvancedUnitBuilders.BindVisualReference(contract, "meshes", "malha-fonte do corpo", body);
        AdvancedUnitBuilders.BindVisualReference(contract, "effects", "efeito de aura de cura", effect);
        foreach (var kv in clips) AdvancedUnitBuilders.BindAnimation(contract, kv.Key, kv.Value);
        EditorUtility.SetDirty(contract);
        AssetDatabase.SaveAssets();
    }

    static CharacterData RegisterCatalog(PaladinHealerContract contract)
    {
        var data = AdvancedUnitBuilders.RegisterCharacterData(contract, "PaladinHealer", out string reason);
        if (data == null) throw new InvalidOperationException("CharacterData do PaladinHealer nao registrado: " + reason);
        Notes.Add("CharacterData registrado: " + AssetDatabase.GetAssetPath(data) + " code=" + data.code);
        return data;
    }

    static void WriteReport(Dictionary<string, AnimationClip> clips, GameObject wrapper, CharacterData data, PaladinHealerContract contract)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PaladinHealer - Unity build report (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")");
        foreach (var n in Notes) sb.AppendLine("- " + n);
        sb.AppendLine("Wrapper: " + AssetDatabase.GetAssetPath(wrapper));
        sb.AppendLine("CharacterData: " + AssetDatabase.GetAssetPath(data) + " code=" + data.code);
        sb.AppendLine("Contrato: valido=" + contract.IsGameplayValid + " visualPronto=" + contract.IsVisualReady);
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, sb.ToString());
    }
}
