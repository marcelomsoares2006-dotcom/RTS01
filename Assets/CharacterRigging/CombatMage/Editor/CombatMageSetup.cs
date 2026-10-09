using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EraImperial.Units.Advanced;
using EraImperial.Units.Mage;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Configura o Mago de combate (combat_mage) no Unity a partir dos FBX gerados por
/// Tools/CharacterRigging/BuildCombatMage.py e BuildCombatMageAccessories.py: importacao Humanoid, clipes,
/// Animator Controller, efeitos magicos, prefabs (visual, morte, wrapper jogavel), vinculo ao contrato
/// (Assets/Data/AdvancedUnits/CombatMage.asset) e registro condicionado no catalogo.
/// Reexecutavel (idempotente). Batch: -executeMethod CombatMageSetup.BatchBuild
/// Menu: Tools > Character Rigging > Combat Mage > Build Assets
/// </summary>
public static class CombatMageSetup
{
    public const string Root = "Assets/CharacterRigging/CombatMage";
    public const string ModelsDir = Root + "/Models";
    public const string AccessoriesDir = Root + "/Accessories";
    public const string TexturesDir = Root + "/Textures";
    public const string MaterialsDir = Root + "/Materials";
    public const string AnimationDir = Root + "/Animation";
    public const string PrefabsDir = Root + "/Prefabs";
    public const string BaseModel = ModelsDir + "/CombatMage.fbx";
    public const string StaffModel = AccessoriesDir + "/CombatMage_Staff.fbx";
    public const string VisualPrefabPath = PrefabsDir + "/CombatMageVisual.prefab";
    public const string DeathPrefabPath = PrefabsDir + "/CombatMageDeath.prefab";
    public const string WrapperPrefabPath = "Assets/Resources/Prefabs/Units/Characters/CombatMage.prefab";
    public const string ControllerPath = AnimationDir + "/CombatMage.controller";
    public const string DeathControllerPath = AnimationDir + "/CombatMageDeath.controller";
    public const string CharacterDataPath = "Assets/Resources/ScriptableObjects/Units/Characters/CombatMage.asset";
    public const string ContractName = "CombatMage";
    public const string ProjectileContractPath = AdvancedUnitBuilders.DataRoot + "/Projectiles/CombatMageBolt.asset";
    public const string ReportPath = "Docs/CharacterMeshAudit/CombatMage/unity-build-report.txt";
    public const string VisualInstanceName = "CombatMageVisual";

    /// <summary>Instante (s) do disparo dentro do clipe Cast; vem do build do Blender (frame 24 de 30 fps) e vira releaseDelay.</summary>
    public const float ReleaseSeconds = 23f / 30f;
    public const float TeamRingRadius = 0.5f;

    // osso humano -> osso do esqueleto Meshy (a coluna sobe como Spine02, Spine01, Spine)
    static readonly Dictionary<string, string> Mapping = new Dictionary<string, string>
    {
        {"Hips","Hips"},{"Spine","Spine02"},{"Chest","Spine01"},{"UpperChest","Spine"},{"Neck","neck"},{"Head","Head"},
        {"LeftShoulder","LeftShoulder"},{"LeftUpperArm","LeftArm"},{"LeftLowerArm","LeftForeArm"},{"LeftHand","LeftHand"},
        {"RightShoulder","RightShoulder"},{"RightUpperArm","RightArm"},{"RightLowerArm","RightForeArm"},{"RightHand","RightHand"},
        {"LeftUpperLeg","LeftUpLeg"},{"LeftLowerLeg","LeftLeg"},{"LeftFoot","LeftFoot"},{"LeftToes","LeftToeBase"},
        {"RightUpperLeg","RightUpLeg"},{"RightLowerLeg","RightLeg"},{"RightFoot","RightFoot"},{"RightToes","RightToeBase"},
    };

    public class ClipInfo
    {
        public string Name; public bool Loop; public (string fn, double seconds)[] Events;
    }

    public static readonly ClipInfo[] Clips =
    {
        new ClipInfo{Name="Idle", Loop=true, Events=new (string,double)[0]},
        new ClipInfo{Name="Walk", Loop=true, Events=new (string,double)[0]},
        new ClipInfo{Name="Run", Loop=true, Events=new (string,double)[0]},
        // frame 24 (30 fps) do Cast: o cajado aponta para o alvo e a esfera sai
        new ClipInfo{Name="Cast", Loop=false, Events=new[]{("CombatMageRelease", (double)ReleaseSeconds)}},
        new ClipInfo{Name="Hit", Loop=false, Events=new (string,double)[0]},
        new ClipInfo{Name="Death", Loop=false, Events=new (string,double)[0]},
    };

    static readonly List<string> Notes = new List<string>();

    [MenuItem("Tools/Character Rigging/Combat Mage/Build Assets")]
    public static void Build()
    {
        Notes.Clear();
        AssetDatabase.Refresh();
        foreach (var d in new[] { MaterialsDir, AnimationDir, PrefabsDir })
            AdvancedUnitBuilders.EnsureFolder(d);

        ConfigureTextures();
        var mats = CreateMaterials();
        var avatar = ConfigureBaseModel(mats["CombatMage_Mat"]);
        var clips = ConfigureClips(avatar);
        ConfigureStaffImporter(mats);
        var fx = CombatMageEffectsBuilder.BuildAll();
        var controller = BuildController(clips);
        var deathController = BuildDeathController(clips["Death"]);
        var visual = BuildVisualPrefab(controller, avatar, clips["Idle"], fx.cast);
        var death = BuildDeathPrefab(visual, deathController);

        var contract = AdvancedUnitBuilders.LoadContract<CombatMageContract>(ContractName);
        var wrapper = BuildWrapperPrefab(visual, death, contract, out string castPointPath, out string staffMountPath);
        BindContract(contract, wrapper, clips, fx, castPointPath, staffMountPath);

        AssetDatabase.SaveAssets();
        string reason;
        var data = AdvancedUnitBuilders.RegisterCharacterData(contract, "CombatMage", out reason);
        Notes.Add(data != null ? "CharacterData registrado: " + CharacterDataPath : "CharacterData NAO registrado: " + reason);
        AssetDatabase.SaveAssets();
        WriteReport(wrapper, data, reason);
        Debug.Log("COMBATMAGE_SETUP_OK\n" + string.Join("\n", Notes));
        if (data == null) throw new InvalidOperationException("RegisterCharacterData devolveu null: " + reason);
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
            Debug.LogError("COMBATMAGE_SETUP_FAILED " + e);
            EditorApplication.Exit(1);
        }
    }

    // ------------------------------------------------------------------------------------------------ helpers
    static T Require<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Asset ausente: " + path);
        return asset;
    }

    public static AnimationClip LoadClip(string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(ModelsDir + "/CombatMage@" + name + ".fbx")
            .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    static void ConfigureTextures()
    {
        var normal = (TextureImporter)AssetImporter.GetAtPath(TexturesDir + "/CombatMage_Normal.png");
        normal.textureType = TextureImporterType.NormalMap;
        normal.maxTextureSize = 2048;
        normal.SaveAndReimport();
        var color = (TextureImporter)AssetImporter.GetAtPath(TexturesDir + "/CombatMage_BaseColor.png");
        color.textureType = TextureImporterType.Default;
        color.sRGBTexture = true;
        color.maxTextureSize = 2048;
        color.SaveAndReimport();
    }

    static Material MakeStandard(string name, Color color, float metallic, float smoothness, Texture albedo = null, Texture normal = null)
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

    static Dictionary<string, Material> CreateMaterials()
    {
        var baseColor = Require<Texture2D>(TexturesDir + "/CombatMage_BaseColor.png");
        var normal = Require<Texture2D>(TexturesDir + "/CombatMage_Normal.png");
        var m = new Dictionary<string, Material>
        {
            ["CombatMage_Mat"] = MakeStandard("CombatMage_Mat", Color.white, 0f, 0.15f, baseColor, normal),
            ["CombatMage_StaffWood"] = MakeStandard("CombatMage_StaffWood", new Color(0.20f, 0.11f, 0.06f), 0f, 0.35f),
            ["CombatMage_StaffLeather"] = MakeStandard("CombatMage_StaffLeather", new Color(0.30f, 0.17f, 0.09f), 0f, 0.2f),
            ["CombatMage_StaffBronze"] = MakeStandard("CombatMage_StaffBronze", new Color(0.62f, 0.42f, 0.18f), 0.85f, 0.55f),
            ["CombatMage_Orb"] = CombatMageEffectsBuilder.EnsureEmissive("CombatMage_Orb", new Color(0.25f, 0.5f, 0.95f), new Color(0.30f, 0.60f, 1f) * 1.25f),
            ["CombatMage_TeamRing"] = MakeUnlit("CombatMage_TeamRing", Color.white),
        };
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
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "CombatMage_Mat"), material);
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
            throw new InvalidOperationException("Avatar humanoide do CombatMage invalido.");
        return avatar;
    }

    static Dictionary<string, AnimationClip> ConfigureClips(Avatar avatar)
    {
        var result = new Dictionary<string, AnimationClip>();
        foreach (var info in Clips)
        {
            string path = ModelsDir + "/CombatMage@" + info.Name + ".fbx";
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
            // eventos do importador usam tempo normalizado (0..1) do clipe
            var temp = LoadClip(info.Name);
            double duration = temp != null ? temp.length : (clip.lastFrame - clip.firstFrame) / 30.0;
            clip.events = info.Events.Select(e => new AnimationEvent
            {
                functionName = e.fn,
                time = (float)(e.seconds / duration),
            }).ToArray();
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();

            var asset = LoadClip(info.Name);
            if (asset == null) throw new InvalidOperationException(path + ": clipe nao importado");
            result[info.Name] = asset;

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

    static void ConfigureStaffImporter(Dictionary<string, Material> mats)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(StaffModel);
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
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(StaffModel);
        float angle = Quaternion.Angle(root.transform.rotation, Quaternion.identity);
        if (angle > 0.5f) throw new InvalidOperationException($"CombatMage_Staff: o FBX chega ao Unity girado {angle:F1} graus; exporte com bake_space_transform.");
        Notes.Add($"CombatMage_Staff: rotacao de importacao identidade ({angle:F2} graus)");
    }

    // ------------------------------------------------------------------------------------------- animator
    static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
    {
        if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        // Speed, Fire, Damaged, Heal (triggers) e Running/Attacking/Hit (escritos pelo behavior tree, sem transicoes)
        AdvancedUnitBuilders.AddStandardParameters(controller);

        var machine = controller.layers[0].stateMachine;
        var tree = new BlendTree { name = "Locomotion", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(clips["Idle"], 0f);
        tree.AddChild(clips["Walk"], 1.4f);
        tree.AddChild(clips["Run"], 4.2f);

        var locomotion = machine.AddState("Locomotion");
        locomotion.motion = tree;
        machine.defaultState = locomotion;
        var cast = machine.AddState("Cast"); cast.motion = clips["Cast"];
        var hit = machine.AddState("Hit"); hit.motion = clips["Hit"];

        var toCast = locomotion.AddTransition(cast);
        toCast.hasExitTime = false; toCast.duration = 0.08f; toCast.hasFixedDuration = true; toCast.AddCondition(AnimatorConditionMode.If, 0, "Fire");
        var castBack = cast.AddTransition(locomotion);
        castBack.hasExitTime = true; castBack.exitTime = 0.93f; castBack.duration = 0.2f; castBack.hasFixedDuration = true;

        var toHit = locomotion.AddTransition(hit);
        toHit.hasExitTime = false; toHit.duration = 0.08f; toHit.hasFixedDuration = true; toHit.AddCondition(AnimatorConditionMode.If, 0, "Damaged");
        var hitBack = hit.AddTransition(locomotion);
        hitBack.hasExitTime = true; hitBack.exitTime = 0.85f; hitBack.duration = 0.15f; hitBack.hasFixedDuration = true;
        // dano durante a conjuracao nao interrompe o clipe (o disparo ja esta agendado); o trigger e consumido ao voltar
        var castToHit = cast.AddTransition(hit);
        castToHit.hasExitTime = true; castToHit.exitTime = 0.9f; castToHit.duration = 0.1f; castToHit.hasFixedDuration = true;
        castToHit.AddCondition(AnimatorConditionMode.If, 0, "Damaged");
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static AnimatorController BuildDeathController(AnimationClip death)
    {
        if (File.Exists(DeathControllerPath)) AssetDatabase.DeleteAsset(DeathControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(DeathControllerPath);
        var machine = controller.layers[0].stateMachine;
        var state = machine.AddState("Death");
        state.motion = death;
        machine.defaultState = state;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    // --------------------------------------------------------------------------------------------- prefabs
    /// <summary>Pose do cajado relativa a mao direita: vertical (orbe para cima), punho 7 cm abaixo da cabeca do osso da mao, no frame 0 do Idle.</summary>
    static void GetStaffLocalPose(GameObject fbx, AnimationClip idle, out Vector3 localPos, out Quaternion localRot)
    {
        var temp = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        try
        {
            var animator = temp.GetComponent<Animator>();
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            idle.SampleAnimation(temp, 0f);
            Vector3 grip = hand.position + Vector3.down * 0.07f;
            Quaternion upright = temp.transform.rotation;
            localPos = hand.InverseTransformPoint(grip);
            localRot = Quaternion.Inverse(hand.rotation) * upright;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(temp);
        }
    }

    static GameObject BuildVisualPrefab(AnimatorController controller, Avatar avatar, AnimationClip idle, GameObject castEffectPrefab)
    {
        var fbx = Require<GameObject>(BaseModel);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        instance.name = VisualInstanceName;
        var animator = instance.GetComponent<Animator>();
        if (animator == null) animator = instance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (rightHand == null) throw new InvalidOperationException("Osso RightHand ausente no CombatMage.");

        // --- cajado: socket StaffMount na mao direita, cajado separado como filho dele
        GetStaffLocalPose(fbx, idle, out Vector3 mountPos, out Quaternion mountRot);
        var mount = new GameObject("StaffMount");
        mount.transform.SetParent(rightHand, false);
        mount.transform.localPosition = mountPos;
        mount.transform.localRotation = mountRot;
        var staff = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(StaffModel), mount.transform);
        staff.name = "Staff";
        staff.transform.localPosition = Vector3.zero;
        staff.transform.localRotation = Quaternion.identity;
        var castPoint = AdvancedUnitBuilders.FindDeep(staff.transform, "CastPoint");
        var orb = AdvancedUnitBuilders.FindDeep(staff.transform, "Orb");
        if (castPoint == null || orb == null) throw new InvalidOperationException("Cajado sem CastPoint/Orb.");

        // --- efeito de conjuracao no orbe
        var cast = (GameObject)PrefabUtility.InstantiatePrefab(castEffectPrefab, castPoint);
        cast.name = "CastEffect";
        cast.transform.localPosition = Vector3.zero;
        cast.transform.localRotation = Quaternion.identity;

        var effects = instance.AddComponent<CombatMageStaffEffects>();
        effects.animator = animator;
        effects.orbRenderer = orb.GetComponent<Renderer>();
        effects.chargeEffects = new[]
        {
            cast.transform.Find("Charge").GetComponent<ParticleSystem>(),
            cast.transform.Find("Core").GetComponent<ParticleSystem>(),
        };
        effects.releaseBurst = cast.transform.Find("ReleaseBurst").GetComponent<ParticleSystem>();

        ConfigureLods(instance);
        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, VisualPrefabPath);
        UnityEngine.Object.DestroyImmediate(instance);
        Notes.Add($"Cajado: StaffMount pos local {mountPos:F3}, rot local {mountRot.eulerAngles:F1} (mao direita)");
        return prefab;
    }

    static GameObject BuildDeathPrefab(GameObject visualPrefab, AnimatorController deathController)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab);
        instance.name = "CombatMageDeath";
        var animator = instance.GetComponent<Animator>();
        animator.runtimeAnimatorController = deathController;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (instance.GetComponent<CombatMageDeathGrounding>() == null) instance.AddComponent<CombatMageDeathGrounding>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, DeathPrefabPath);
        UnityEngine.Object.DestroyImmediate(instance);
        return prefab;
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
        // Camera do jogo ortografica (zoom 8..26, padrao 18) e nivel Ultra com lodBias 2; altura relativa efetiva ~ 2.2 / orthoSize
        // (calibrado no Arqueiro, mesma escala de humanoide ~1,8 m): 0.275 em 8, 0.122 em 18, 0.085 em 26.
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

    static GameObject BuildWrapperPrefab(GameObject visualPrefab, GameObject deathPrefab, CombatMageContract contract,
        out string castPointPath, out string staffMountPath)
    {
        var root = AdvancedUnitBuilders.BuildWrapperRoot("CombatMage", visualPrefab, TeamRingRadius, out var visual);
        visual.name = VisualInstanceName;
        var animator = visual.GetComponent<Animator>();

        // pegada humanoide: caixa 0,8 x 1,9 x 0,8 m e agente com raio 0,4 (o Soldier usa 1 x 2 x 1 e raio 0,5)
        var box = root.GetComponent<BoxCollider>();
        box.size = new Vector3(0.8f, 1.9f, 0.8f);
        box.center = new Vector3(0f, 0.95f, 0f);
        var agent = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
        agent.radius = 0.4f;
        agent.height = 1.9f;
        AdvancedUnitBuilders.ConfigureAgent(root, 4.2f, 8f, 0.5f);

        var castPoint = AdvancedUnitBuilders.FindDeep(visual.transform, "CastPoint");
        var staffMount = AdvancedUnitBuilders.FindDeep(visual.transform, "StaffMount");
        castPointPath = AdvancedUnitBuilders.RelativePath(root.transform, castPoint);
        staffMountPath = AdvancedUnitBuilders.RelativePath(root.transform, staffMount);

        var era = root.AddComponent<AdvancedUnitEraBinding>();
        era.contract = contract;

        var driver = root.AddComponent<AdvancedUnitAnimatorDriver>();
        driver.animator = animator;

        var attack = root.AddComponent<ProjectileAttackController>();
        attack.contract = contract;
        attack.animator = animator;
        attack.projectileSpawn = castPoint;
        attack.releaseDelay = ReleaseSeconds;
        attack.sourceHeight = 1.5f;

        var deathEffect = root.AddComponent<AdvancedUnitDeathEffect>();
        deathEffect.deathPrefab = deathPrefab;
        deathEffect.lifetime = 6f;

        return AdvancedUnitBuilders.SaveWrapper(root, WrapperPrefabPath);
    }

    // ------------------------------------------------------------------------------------- contract linking
    static void EnsureMeshReference(AdvancedUnitContract contract, string role, string expectedPath)
    {
        var so = new SerializedObject(contract);
        var list = so.FindProperty("visual.meshes");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("role").stringValue == role) return;
        list.InsertArrayElementAtIndex(list.arraySize);
        var element = list.GetArrayElementAtIndex(list.arraySize - 1);
        element.FindPropertyRelative("role").stringValue = role;
        element.FindPropertyRelative("expectedAssetPath").stringValue = expectedPath;
        element.FindPropertyRelative("asset").objectReferenceValue = null;
        element.FindPropertyRelative("required").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetAnimationRequirements(AdvancedUnitContract contract, (string state, string parameter)[] items)
    {
        var so = new SerializedObject(contract);
        var list = so.FindProperty("visual.animations");
        list.ClearArray();
        for (int i = 0; i < items.Length; i++)
        {
            list.InsertArrayElementAtIndex(i);
            var element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("state").stringValue = items[i].state;
            element.FindPropertyRelative("animatorParameter").stringValue = items[i].parameter;
            element.FindPropertyRelative("clip").objectReferenceValue = null;
            element.FindPropertyRelative("required").boolValue = true;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BindContract(CombatMageContract contract, GameObject wrapper, Dictionary<string, AnimationClip> clips,
        (GameObject bolt, GameObject impact, GameObject cast) fx, string castPointPath, string staffMountPath)
    {
        AdvancedUnitBuilders.BindPrefab(contract, wrapper);

        // malhas: corpo (FBX com rig e LODs) e cajado separado
        EnsureMeshReference(contract, "cajado", StaffModel);
        AdvancedUnitBuilders.BindVisualReference(contract, "meshes", "malha-fonte do corpo", Require<GameObject>(BaseModel));
        AdvancedUnitBuilders.BindVisualReference(contract, "meshes", "cajado", Require<GameObject>(StaffModel));
        // efeitos
        AdvancedUnitBuilders.BindVisualReference(contract, "effects", "efeito de conjuração", fx.cast);
        AdvancedUnitBuilders.BindVisualReference(contract, "effects", "efeito de impacto", fx.impact);
        // sockets (caminho real a partir da raiz do wrapper)
        AdvancedUnitBuilders.BindSocket(contract, "CastPoint", castPointPath);
        AdvancedUnitBuilders.BindSocket(contract, "StaffMount", staffMountPath);
        // animacoes: estado -> parametro do Animator
        SetAnimationRequirements(contract, new[]
        {
            ("Idle", "Speed"), ("Walk", "Speed"), ("Run", "Speed"),
            ("Cast", "Fire"), ("Hit", "Damaged"), ("Death", "AdvancedUnitDeathEffect"),
        });
        foreach (var name in new[] { "Idle", "Walk", "Run", "Cast", "Hit", "Death" })
            AdvancedUnitBuilders.BindAnimation(contract, name, clips[name]);

        // projetil separado
        var projectile = AssetDatabase.LoadAssetAtPath<ProjectileContract>(ProjectileContractPath);
        if (projectile == null) throw new InvalidOperationException("Contrato do projetil ausente: " + ProjectileContractPath);
        AdvancedUnitBuilders.BindProjectile(projectile, fx.bolt, fx.impact);
        EditorUtility.SetDirty(contract);
        EditorUtility.SetDirty(projectile);
        AssetDatabase.SaveAssets();
    }

    static void WriteReport(GameObject wrapper, CharacterData data, string reason)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CombatMage - Unity build report (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")");
        foreach (var n in Notes) sb.AppendLine("- " + n);
        sb.AppendLine("Wrapper: " + AssetDatabase.GetAssetPath(wrapper));
        sb.AppendLine(data != null ? "CharacterData: " + AssetDatabase.GetAssetPath(data) + " code=" + data.code : "CharacterData: NAO registrado (" + reason + ")");
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, sb.ToString());
    }
}
