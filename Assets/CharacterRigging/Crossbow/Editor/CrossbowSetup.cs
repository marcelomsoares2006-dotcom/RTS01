using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EraImperial.Units.Advanced;
using EraImperial.Units.Crossbow;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Configura a Balestra (balista de carreta) no Unity a partir dos FBX gerados por Tools/CharacterRigging/BuildCrossbow.py:
/// importacao Generic, clipes, materiais, Animator Controller (com camada Hit mascarada na raiz), efeitos de particulas,
/// projetil (virote) separado, prefab visual, prefab de destruicao, wrapper jogavel, vinculo ao contrato e registro
/// condicionado no catalogo. Idempotente: pode ser executado quantas vezes for preciso.
/// Batch: -executeMethod CrossbowSetup.BatchBuild
/// </summary>
public static class CrossbowSetup
{
    public const string Root = "Assets/CharacterRigging/Crossbow";
    public const string ModelsDir = Root + "/Models";
    public const string AccessoriesDir = Root + "/Accessories";
    public const string TexturesDir = Root + "/Textures";
    public const string MaterialsDir = Root + "/Materials";
    public const string AnimationDir = Root + "/Animation";
    public const string PrefabsDir = Root + "/Prefabs";
    public const string BaseModel = ModelsDir + "/Crossbow.fbx";
    public const string BoltModel = AccessoriesDir + "/Crossbow_Bolt.fbx";
    public const string VisualPrefabPath = PrefabsDir + "/CrossbowVisual.prefab";
    public const string DeathPrefabPath = PrefabsDir + "/CrossbowDeath.prefab";
    public const string BoltPrefabPath = PrefabsDir + "/CrossbowBolt.prefab";
    public const string ImpactPrefabPath = PrefabsDir + "/CrossbowImpact.prefab";
    public const string MuzzlePrefabPath = PrefabsDir + "/CrossbowMuzzle.prefab";
    public const string WrapperPrefabPath = "Assets/Resources/Prefabs/Units/Characters/Crossbow.prefab";
    public const string ControllerPath = AnimationDir + "/Crossbow.controller";
    public const string DeathControllerPath = AnimationDir + "/CrossbowDeath.controller";
    public const string HitMaskPath = AnimationDir + "/CrossbowHitMask.mask";
    public const string CharacterDataPath = "Assets/Resources/ScriptableObjects/Units/Characters/Crossbow.asset";
    public const string ContractPath = "Assets/Data/AdvancedUnits/Crossbow.asset";
    public const string BoltContractPath = "Assets/Data/AdvancedUnits/Projectiles/CrossbowBolt.asset";
    public const string BuildJson = "Tools/CharacterRigging/Working/Crossbow/crossbow-build.json";
    const string ReportPath = "Docs/CharacterMeshAudit/Crossbow/unity-build-report.txt";

    public const float FireFrame = 6f;                 // quadro de soltura no clipe Fire (30 fps)
    public const float ReleaseDelay = FireFrame / 30f; // 0,2 s
    public const float TeamRingRadius = 1.1f;
    public static readonly string[] ClipNames = { "Idle", "Move", "Reload", "Fire", "Hit", "Destroy" };
    static readonly HashSet<string> LoopClips = new HashSet<string> { "Idle", "Move" };
    static readonly string[] MaterialNames = { "Crossbow_Wood", "Crossbow_DarkWood", "Crossbow_Iron", "Crossbow_Rope", "Crossbow_Feather" };

    static readonly List<string> Notes = new List<string>();

    [MenuItem("Tools/Character Rigging/Crossbow/Build Assets")]
    public static void Build()
    {
        Notes.Clear();
        AssetDatabase.Refresh();
        foreach (string d in new[] { MaterialsDir, AnimationDir, PrefabsDir })
            AdvancedUnitBuilders.EnsureFolder(d);

        ConfigureTextures();
        Dictionary<string, Material> mats = CreateMaterials();
        Avatar avatar = ConfigureBaseModel(mats);
        ConfigureBoltModel(mats);
        Dictionary<string, AnimationClip> clips = ConfigureClips(avatar);

        GameObject muzzle = BuildMuzzlePrefab(mats);
        GameObject impact = BuildImpactPrefab(mats);
        GameObject boltPrefab = BuildBoltPrefab();
        AnimatorController controller = BuildController(clips);
        AnimatorController deathController = BuildDeathController(clips["Destroy"]);
        GameObject visual = BuildVisualPrefab(controller, avatar, muzzle);
        GameObject death = BuildDeathPrefab(deathController, avatar, mats);
        AssetDatabase.SaveAssets();

        CrossbowContract contract = AdvancedUnitBuilders.LoadContract<CrossbowContract>("Crossbow");
        GameObject wrapper = BuildWrapperPrefab(visual, death, contract);
        BindContract(contract, wrapper, clips, muzzle, death, boltPrefab, impact);

        string reason;
        CharacterData data = AdvancedUnitBuilders.RegisterCharacterData(contract, "Crossbow", out reason);
        if (data == null) Notes.Add("CharacterData NAO registrado: " + reason);
        else Notes.Add("CharacterData registrado: " + AssetDatabase.GetAssetPath(data) + " code=" + data.code);
        AssetDatabase.SaveAssets();
        WriteReport(clips, wrapper, data, reason);
        Debug.Log("CROSSBOW_SETUP_OK\n" + string.Join("\n", Notes));
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
            Debug.LogError("CROSSBOW_SETUP_FAILED " + e);
            EditorApplication.Exit(1);
        }
    }

    // ------------------------------------------------------------------------------------------------ helpers
    static T Require<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Asset ausente: " + path);
        return asset;
    }

    public static Vector3 BlenderToUnity(Vector3 b) { return new Vector3(-b.x, b.z, -b.y); }

    static Vector3 ReadSocket(string name)
    {
        string json = File.ReadAllText(BuildJson);
        int i = json.IndexOf("\"" + name + "\"", StringComparison.Ordinal);
        if (i < 0) throw new InvalidOperationException("Socket ausente em " + BuildJson + ": " + name);
        int a = json.IndexOf('[', i), b = json.IndexOf(']', a);
        float[] p = json.Substring(a + 1, b - a - 1).Split(',').Select(s => float.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return BlenderToUnity(new Vector3(p[0], p[1], p[2]));
    }

    // ------------------------------------------------------------------------------------------------ texturas e materiais
    static void ConfigureTextures()
    {
        foreach (string n in new[] { "Crossbow_Wood", "Crossbow_Iron", "Crossbow_Rope" })
        {
            TextureImporter t = (TextureImporter)AssetImporter.GetAtPath(TexturesDir + "/" + n + ".png");
            t.textureType = TextureImporterType.Default;
            t.sRGBTexture = true;
            t.maxTextureSize = 512;
            t.mipmapEnabled = true;
            t.wrapMode = TextureWrapMode.Repeat;
            t.SaveAndReimport();
        }
        foreach (string n in new[] { "Crossbow_Smoke", "Crossbow_Spark" })
        {
            TextureImporter t = (TextureImporter)AssetImporter.GetAtPath(TexturesDir + "/" + n + ".png");
            t.textureType = TextureImporterType.Default;
            t.alphaIsTransparency = true;
            t.maxTextureSize = 128;
            t.wrapMode = TextureWrapMode.Clamp;
            t.SaveAndReimport();
        }
    }

    static Material Standard(string name, Color color, float metallic, float smoothness, Texture albedo)
    {
        string path = MaterialsDir + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = Shader.Find("Standard");
        m.color = color;
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Glossiness", smoothness);
        m.mainTexture = albedo;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Particle(string name, string shader, Color tint, Texture tex)
    {
        string path = MaterialsDir + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader s = Shader.Find(shader);
        if (s == null) throw new InvalidOperationException("Shader nao encontrado: " + shader);
        if (m == null)
        {
            m = new Material(s);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = s;
        m.SetColor("_TintColor", tint);
        m.mainTexture = tex;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Dictionary<string, Material> CreateMaterials()
    {
        Texture2D wood = Require<Texture2D>(TexturesDir + "/Crossbow_Wood.png");
        Texture2D iron = Require<Texture2D>(TexturesDir + "/Crossbow_Iron.png");
        Texture2D rope = Require<Texture2D>(TexturesDir + "/Crossbow_Rope.png");
        Dictionary<string, Material> m = new Dictionary<string, Material>
        {
            ["Crossbow_Wood"] = Standard("Crossbow_Wood", Color.white, 0f, 0.18f, wood),
            ["Crossbow_DarkWood"] = Standard("Crossbow_DarkWood", new Color(0.62f, 0.56f, 0.52f), 0f, 0.15f, wood),
            ["Crossbow_Iron"] = Standard("Crossbow_Iron", Color.white, 0.55f, 0.38f, iron),
            ["Crossbow_Rope"] = Standard("Crossbow_Rope", Color.white, 0f, 0.05f, rope),
            ["Crossbow_Feather"] = Standard("Crossbow_Feather", new Color(0.88f, 0.85f, 0.78f), 0f, 0.1f, null),
        };
        m["Crossbow_SmokeFx"] = Particle("Crossbow_SmokeFx", "Legacy Shaders/Particles/Alpha Blended", new Color(0.78f, 0.74f, 0.66f, 0.55f), Require<Texture2D>(TexturesDir + "/Crossbow_Smoke.png"));
        m["Crossbow_SparkFx"] = Particle("Crossbow_SparkFx", "Legacy Shaders/Particles/Additive", new Color(1f, 0.85f, 0.55f, 0.9f), Require<Texture2D>(TexturesDir + "/Crossbow_Spark.png"));
        string ringPath = MaterialsDir + "/Crossbow_TeamRing.mat";
        Material ring = AssetDatabase.LoadAssetAtPath<Material>(ringPath);
        if (ring == null) { ring = new Material(Shader.Find("Unlit/Color")); AssetDatabase.CreateAsset(ring, ringPath); }
        ring.color = Color.white;
        m["Crossbow_TeamRing"] = ring;
        AssetDatabase.SaveAssets();
        return m;
    }

    // ------------------------------------------------------------------------------------------------ modelo e clipes
    static Avatar ConfigureBaseModel(Dictionary<string, Material> mats)
    {
        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(BaseModel);
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.importAnimation = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        foreach (string n in MaterialNames)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), mats[n]);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects = false;
        importer.SaveAndReimport();
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(BaseModel).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid || avatar.isHuman)
            throw new InvalidOperationException("Avatar generico da Balestra invalido.");
        Notes.Add("Avatar generico valido (Crossbow.fbx).");
        return avatar;
    }

    static void ConfigureBoltModel(Dictionary<string, Material> mats)
    {
        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(BoltModel);
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        foreach (string n in MaterialNames)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), mats[n]);
        importer.SaveAndReimport();
        GameObject root = Require<GameObject>(BoltModel);
        float angle = Quaternion.Angle(root.transform.rotation, Quaternion.identity);
        if (angle > 0.5f) throw new InvalidOperationException($"Crossbow_Bolt.fbx chegou ao Unity girado {angle:F1} graus.");
    }

    static Dictionary<string, AnimationClip> ConfigureClips(Avatar avatar)
    {
        Dictionary<string, AnimationClip> result = new Dictionary<string, AnimationClip>();
        foreach (string name in ClipNames)
        {
            string path = ModelsDir + "/Crossbow@" + name + ".fbx";
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();

            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            if (defaults.Length != 1) throw new InvalidOperationException(path + ": esperado um take, obtido " + defaults.Length);
            ModelImporterClipAnimation clip = defaults[0];
            clip.name = name;
            clip.loopTime = LoopClips.Contains(name);
            clip.loopPose = clip.loopTime;
            AnimationClip temp = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            double duration = temp != null ? temp.length : (clip.lastFrame - clip.firstFrame) / 30.0;
            List<AnimationEvent> events = new List<AnimationEvent>();
            if (name == "Fire")
                events.Add(new AnimationEvent { functionName = "CrossbowRelease", time = (float)(FireFrame / 30.0 / duration) });
            clip.events = events.ToArray();
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();

            AnimationClip asset = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (asset == null) throw new InvalidOperationException(path + ": clipe nao importado");
            result[name] = asset;
            Notes.Add($"Clip {name}: {asset.length:F3}s, loop={asset.isLooping}, eventos={AnimationUtility.GetAnimationEvents(asset).Length}");
        }
        AnimationEvent[] fire = AnimationUtility.GetAnimationEvents(result["Fire"]);
        AnimationEvent release = fire.FirstOrDefault(e => e.functionName == "CrossbowRelease");
        if (release == null || Mathf.Abs(release.time - ReleaseDelay) > 0.05f)
            throw new InvalidOperationException("Evento CrossbowRelease ausente ou fora do instante esperado em Fire.");
        Notes.Add($"Fire: CrossbowRelease em {release.time:F3}s (releaseDelay {ReleaseDelay:F3}s).");
        return result;
    }

    // ------------------------------------------------------------------------------------------------ animator
    static AvatarMask BuildHitMask()
    {
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(HitMaskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, HitMaskPath);
        }
        GameObject fbx = Require<GameObject>(BaseModel);
        Transform rootBone = AdvancedUnitBuilders.FindDeep(fbx.transform, "Root");
        if (rootBone == null) throw new InvalidOperationException("Osso Root ausente em Crossbow.fbx.");
        mask.transformCount = 0;
        mask.AddTransformPath(fbx.transform, true);
        string rootPath = AdvancedUnitBuilders.RelativePath(fbx.transform, rootBone);
        for (int i = 0; i < mask.transformCount; i++)
            mask.SetTransformActive(i, mask.GetTransformPath(i).EndsWith(rootPath, StringComparison.Ordinal) && mask.GetTransformPath(i).Length == rootPath.Length);
        EditorUtility.SetDirty(mask);
        Notes.Add($"Mascara da camada Hit: somente '{rootPath}' ativo.");
        return mask;
    }

    static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
    {
        if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AdvancedUnitBuilders.AddStandardParameters(controller);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimatorState idle = machine.AddState("Idle"); idle.motion = clips["Idle"];
        AnimatorState move = machine.AddState("Move"); move.motion = clips["Move"];
        // um giro de roda por clipe (4 s) = circunferencia de 3,456 m; multiplicador = 4 / 3,456 por m/s
        move.speedParameterActive = true; move.speedParameter = "Speed"; move.speed = 4f / (2f * Mathf.PI * 0.55f);
        AnimatorState fire = machine.AddState("Fire"); fire.motion = clips["Fire"];
        AnimatorState reload = machine.AddState("Reload"); reload.motion = clips["Reload"];
        machine.defaultState = idle;

        AnimatorStateTransition toMove = idle.AddTransition(move);
        toMove.hasExitTime = false; toMove.duration = 0.1f; toMove.AddCondition(AnimatorConditionMode.Greater, 0.25f, "Speed");
        AnimatorStateTransition toIdle = move.AddTransition(idle);
        toIdle.hasExitTime = false; toIdle.duration = 0.1f; toIdle.AddCondition(AnimatorConditionMode.Less, 0.12f, "Speed");
        foreach (AnimatorState from in new[] { idle, move })
        {
            AnimatorStateTransition t = from.AddTransition(fire);
            t.hasExitTime = false; t.duration = 0.04f; t.AddCondition(AnimatorConditionMode.If, 0, "Fire");
        }
        AnimatorStateTransition fr = fire.AddTransition(reload);
        fr.hasExitTime = true; fr.exitTime = 1f; fr.duration = 0f; fr.hasFixedDuration = true;
        AnimatorStateTransition ri = reload.AddTransition(idle);
        ri.hasExitTime = true; ri.exitTime = 1f; ri.duration = 0f; ri.hasFixedDuration = true;

        // camada Hit: so a raiz (tremida do corpo todo) sobre o que a camada base estiver fazendo
        AnimatorStateMachine hitMachine = new AnimatorStateMachine { name = "Hit" };
        AssetDatabase.AddObjectToAsset(hitMachine, controller);
        hitMachine.hideFlags = HideFlags.HideInHierarchy;
        AnimatorState empty = hitMachine.AddState("Empty");
        AnimatorState hit = hitMachine.AddState("Hit"); hit.motion = clips["Hit"];
        hitMachine.defaultState = empty;
        AnimatorStateTransition eh = empty.AddTransition(hit);
        eh.hasExitTime = false; eh.duration = 0.02f; eh.AddCondition(AnimatorConditionMode.If, 0, "Damaged");
        AnimatorStateTransition he = hit.AddTransition(empty);
        he.hasExitTime = true; he.exitTime = 1f; he.duration = 0.05f; he.hasFixedDuration = true;
        controller.AddLayer(new AnimatorControllerLayer
        {
            name = "Hit",
            stateMachine = hitMachine,
            avatarMask = BuildHitMask(),
            blendingMode = AnimatorLayerBlendingMode.Override,
            defaultWeight = 1f
        });
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static AnimatorController BuildDeathController(AnimationClip destroy)
    {
        if (File.Exists(DeathControllerPath)) AssetDatabase.DeleteAsset(DeathControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(DeathControllerPath);
        AnimatorState state = controller.layers[0].stateMachine.AddState("Destroy");
        state.motion = destroy;
        controller.layers[0].stateMachine.defaultState = state;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    // ------------------------------------------------------------------------------------------------ efeitos
    static ParticleSystem NewSystem(Transform parent, string name, Material material, bool playOnAwake)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = playOnAwake;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        return ps;
    }

    static void ConfigureBurst(ParticleSystem ps, int count, float lifeMin, float lifeMax, float speedMin, float speedMax,
        float sizeMin, float sizeMax, Color start, float gravity, float coneAngle, float growth, float duration = 0.6f)
    {
        ParticleSystem.MainModule main = ps.main;
        main.duration = duration;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = start;
        main.gravityModifier = gravity;
        main.maxParticles = count * 2;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = coneAngle;
        shape.radius = 0.05f;
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, growth)));
    }

    static GameObject SavePrefab(GameObject go, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    static GameObject BuildMuzzlePrefab(Dictionary<string, Material> mats)
    {
        GameObject root = new GameObject("CrossbowMuzzle");
        ParticleSystem puff = NewSystem(root.transform, "Puff", mats["Crossbow_SmokeFx"], false);
        ConfigureBurst(puff, 7, 0.5f, 0.9f, 0.6f, 1.6f, 0.22f, 0.4f, new Color(0.85f, 0.8f, 0.7f, 0.5f), -0.02f, 28f, 2.4f);
        puff.transform.localRotation = Quaternion.LookRotation(Vector3.forward);
        ParticleSystem dust = NewSystem(root.transform, "Dust", mats["Crossbow_SmokeFx"], false);
        ConfigureBurst(dust, 4, 0.7f, 1.1f, 0.2f, 0.6f, 0.3f, 0.5f, new Color(0.7f, 0.62f, 0.5f, 0.35f), -0.01f, 70f, 2.8f);
        GameObject prefab = SavePrefab(root, MuzzlePrefabPath);
        Notes.Add("CrossbowMuzzle.prefab criado (fumaca/poeira leve, 2 sistemas).");
        return prefab;
    }

    static GameObject BuildImpactPrefab(Dictionary<string, Material> mats)
    {
        GameObject root = new GameObject("CrossbowImpact");
        ParticleSystem sparks = NewSystem(root.transform, "Sparks", mats["Crossbow_SparkFx"], true);
        ConfigureBurst(sparks, 14, 0.18f, 0.45f, 2.5f, 6f, 0.05f, 0.11f, new Color(1f, 0.85f, 0.55f, 1f), 1.2f, 55f, 0.3f);
        ParticleSystem dust = NewSystem(root.transform, "Dust", mats["Crossbow_SmokeFx"], true);
        ConfigureBurst(dust, 6, 0.6f, 1.0f, 0.4f, 1.2f, 0.25f, 0.45f, new Color(0.72f, 0.64f, 0.52f, 0.5f), -0.03f, 40f, 2.6f);
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleSystem.MainModule main = ps.main;
            main.stopAction = ParticleSystemStopAction.None;
            ps.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        }
        GameObject prefab = SavePrefab(root, ImpactPrefabPath);
        Notes.Add("CrossbowImpact.prefab criado (faiscas + poeira, textura procedural suave).");
        return prefab;
    }

    static GameObject BuildBoltPrefab()
    {
        GameObject root = new GameObject("CrossbowBolt");
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(BoltModel), root.transform);
        model.name = "Bolt";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        GameObject prefab = SavePrefab(root, BoltPrefabPath);
        Notes.Add("CrossbowBolt.prefab criado (pivo na ponta; corpo se estende para tras).");
        return prefab;
    }

    // ------------------------------------------------------------------------------------------------ prefabs visuais
    static void ConfigureLods(GameObject instance)
    {
        SkinnedMeshRenderer[] all = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        List<Renderer>[] lods = { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
        foreach (SkinnedMeshRenderer r in all)
        {
            for (int i = 0; i < 3; i++)
                if (r.name.Contains("_LOD" + i)) lods[i].Add(r);
            r.updateWhenOffscreen = false;
        }
        if (lods.Any(l => l.Count == 0)) throw new InvalidOperationException("Renderers _LOD0/_LOD1/_LOD2 ausentes na Balestra.");
        LODGroup group = instance.GetComponent<LODGroup>();
        if (group == null) group = instance.AddComponent<LODGroup>();
        // Camera ortografica do jogo (zoom 8..26, lodBias 2): altura relativa efetiva ~ 2,7 / orthoSize para este tamanho (2,45 m).
        group.SetLODs(new[]
        {
            new LOD(0.21f, lods[0].ToArray()),   // zoom proximo (ortho < ~13)
            new LOD(0.12f, lods[1].ToArray()),   // zoom padrao ate ~22
            new LOD(0.0f, lods[2].ToArray()),    // zoom longe; nunca some
        });
        group.fadeMode = LODFadeMode.None;
        group.RecalculateBounds();
    }

    static Animator PrepareAnimator(GameObject instance, RuntimeAnimatorController controller, Avatar avatar, AnimatorCullingMode culling)
    {
        Animator animator = instance.GetComponent<Animator>();
        if (animator == null) animator = instance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.cullingMode = culling;
        return animator;
    }

    static GameObject BuildVisualPrefab(AnimatorController controller, Avatar avatar, GameObject muzzlePrefab)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(BaseModel));
        instance.name = "CrossbowVisual";
        PrepareAnimator(instance, controller, avatar, AnimatorCullingMode.CullUpdateTransforms);

        Transform rootBone = AdvancedUnitBuilders.FindDeep(instance.transform, "Root");
        if (rootBone == null) throw new InvalidOperationException("Osso Root ausente.");
        GameObject sockets = new GameObject("Sockets");
        sockets.transform.SetParent(rootBone, false);
        Transform boltSpawn = MakeSocket(sockets.transform, instance.transform, "BoltSpawn", ReadSocket("BoltSpawn"));
        MakeSocket(sockets.transform, instance.transform, "WeaponMount", ReadSocket("WeaponMount"));

        GameObject muzzle = (GameObject)PrefabUtility.InstantiatePrefab(muzzlePrefab, boltSpawn);
        muzzle.name = "MuzzleFx";
        muzzle.transform.localPosition = Vector3.zero;
        muzzle.transform.localRotation = Quaternion.identity;
        CrossbowFireEffects effects = instance.AddComponent<CrossbowFireEffects>();
        effects.muzzle = muzzle.GetComponentInChildren<ParticleSystem>();

        ConfigureLods(instance);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, VisualPrefabPath);
        UnityEngine.Object.DestroyImmediate(instance);
        return prefab;
    }

    static Transform MakeSocket(Transform parent, Transform visualRoot, string name, Vector3 positionInVisual)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = visualRoot.TransformPoint(positionInVisual);
        go.transform.rotation = visualRoot.rotation; // +Z = frente da unidade
        return go.transform;
    }

    static GameObject BuildDeathPrefab(AnimatorController deathController, Avatar avatar, Dictionary<string, Material> mats)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(BaseModel));
        instance.name = "CrossbowDeath";
        PrepareAnimator(instance, deathController, avatar, AnimatorCullingMode.AlwaysAnimate);
        ConfigureLods(instance);
        // poeira da queda: duas nuvens, uma no colapso e outra no impacto da carreta
        ParticleSystem a = NewSystem(instance.transform, "CollapseDust", mats["Crossbow_SmokeFx"], true);
        ConfigureBurst(a, 14, 0.9f, 1.6f, 0.3f, 1.4f, 0.5f, 0.9f, new Color(0.72f, 0.64f, 0.52f, 0.55f), -0.02f, 80f, 3f, 1.5f);
        ParticleSystem.MainModule ma = a.main; ma.startDelay = 0.35f;
        a.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        a.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        ParticleSystem b = NewSystem(instance.transform, "ImpactDust", mats["Crossbow_SmokeFx"], true);
        ConfigureBurst(b, 10, 0.8f, 1.4f, 0.4f, 1.8f, 0.4f, 0.7f, new Color(0.7f, 0.62f, 0.5f, 0.5f), -0.02f, 85f, 3f, 1.5f);
        ParticleSystem.MainModule mb = b.main; mb.startDelay = 0.75f;
        b.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        b.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, DeathPrefabPath);
        UnityEngine.Object.DestroyImmediate(instance);
        return prefab;
    }

    // ------------------------------------------------------------------------------------------------ wrapper e contrato
    static GameObject BuildWrapperPrefab(GameObject visualPrefab, GameObject deathPrefab, CrossbowContract contract)
    {
        GameObject root = AdvancedUnitBuilders.BuildWrapperRoot("Crossbow", visualPrefab, TeamRingRadius, out GameObject visual);
        AdvancedUnitBuilders.ConfigureAgent(root, 3.5f, 8f, 0.5f, 240f);
        UnityEngine.AI.NavMeshAgent agent = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
        agent.radius = 0.9f;
        agent.height = 1.5f;

        // pegada da carreta (largura 1,8 x comprimento 2,4)
        BoxCollider box = root.GetComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.75f, -0.07f);
        box.size = new Vector3(1.8f, 1.5f, 2.45f);

        // circulo de selecao proporcional ao anel de time
        Transform selection = AdvancedUnitBuilders.FindDeep(root.transform, "SelectionCircle");
        if (selection != null)
        {
            float s = TeamRingRadius / 0.5f;
            selection.localScale = new Vector3(s, selection.localScale.y, s);
        }

        Animator animator = visual.GetComponent<Animator>();
        Transform boltSpawn = AdvancedUnitBuilders.FindDeep(visual.transform, "BoltSpawn");
        if (boltSpawn == null) throw new InvalidOperationException("Socket BoltSpawn ausente no visual.");

        AdvancedUnitEraBinding binding = root.AddComponent<AdvancedUnitEraBinding>();
        binding.contract = contract;
        AdvancedUnitAnimatorDriver driver = root.AddComponent<AdvancedUnitAnimatorDriver>();
        driver.animator = animator;
        ProjectileAttackController attack = root.AddComponent<ProjectileAttackController>();
        attack.contract = contract;
        attack.animator = animator;
        attack.projectileSpawn = boltSpawn;
        attack.releaseDelay = ReleaseDelay;
        attack.sourceHeight = 1.05f;
        attack.turnSpeed = 150f; // carreta pesada gira devagar
        AdvancedUnitDeathEffect death = root.AddComponent<AdvancedUnitDeathEffect>();
        death.deathPrefab = deathPrefab;
        death.lifetime = 10f;
        return AdvancedUnitBuilders.SaveWrapper(root, WrapperPrefabPath);
    }

    static void SetReferenceList(SerializedObject so, string path, (string role, string expected)[] items)
    {
        SerializedProperty list = so.FindProperty(path);
        list.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("role").stringValue = items[i].role;
            e.FindPropertyRelative("expectedAssetPath").stringValue = items[i].expected;
            e.FindPropertyRelative("asset").objectReferenceValue = null;
            e.FindPropertyRelative("required").boolValue = true;
        }
    }

    static void BindContract(CrossbowContract contract, GameObject wrapper, Dictionary<string, AnimationClip> clips,
        GameObject muzzle, GameObject death, GameObject boltPrefab, GameObject impact)
    {
        SerializedObject so = new SerializedObject(contract);
        SetReferenceList(so, "visual.meshes", new[] { ("malha-fonte da balestra", BaseModel), ("malha do virote", BoltModel) });
        SetReferenceList(so, "visual.effects", new[] { ("efeito de disparo (fumaca e poeira leve)", MuzzlePrefabPath), ("prefab de destruicao", DeathPrefabPath) });
        SerializedProperty anims = so.FindProperty("visual.animations");
        (string state, string param)[] states = { ("Idle", "Speed"), ("Move", "Speed"), ("Reload", "Fire"), ("Fire", "Fire"), ("Hit", "Damaged"), ("Destroy", "") };
        anims.arraySize = states.Length;
        for (int i = 0; i < states.Length; i++)
        {
            SerializedProperty e = anims.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("state").stringValue = states[i].state;
            e.FindPropertyRelative("animatorParameter").stringValue = states[i].param;
            e.FindPropertyRelative("clip").objectReferenceValue = null;
            e.FindPropertyRelative("required").boolValue = true;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        AdvancedUnitBuilders.BindPrefab(contract, wrapper);
        AdvancedUnitBuilders.BindVisualReference(contract, "meshes", "malha-fonte da balestra", Require<GameObject>(BaseModel));
        AdvancedUnitBuilders.BindVisualReference(contract, "meshes", "malha do virote", Require<GameObject>(BoltModel));
        AdvancedUnitBuilders.BindVisualReference(contract, "effects", "efeito de disparo (fumaca e poeira leve)", muzzle);
        AdvancedUnitBuilders.BindVisualReference(contract, "effects", "prefab de destruicao", death);
        foreach (string state in ClipNames)
            AdvancedUnitBuilders.BindAnimation(contract, state, clips[state]);

        foreach (string socket in new[] { "BoltSpawn", "WeaponMount" })
        {
            Transform t = AdvancedUnitBuilders.FindDeep(wrapper.transform, socket);
            if (t == null) throw new InvalidOperationException("Socket ausente no wrapper: " + socket);
            string path = AdvancedUnitBuilders.RelativePath(wrapper.transform, t);
            AdvancedUnitBuilders.BindSocket(contract, socket, path);
            Notes.Add($"Socket {socket}: {path}");
        }
        AdvancedUnitBuilders.BindProjectile(contract.Projectile, boltPrefab, impact);
        EditorUtility.SetDirty(contract);
        EditorUtility.SetDirty(contract.Projectile);
        Notes.Add("Contrato vinculado: prefab, malhas, efeitos, sockets, animacoes e projetil.");
    }

    static void WriteReport(Dictionary<string, AnimationClip> clips, GameObject wrapper, CharacterData data, string reason)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Crossbow - Unity build report (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")");
        foreach (string n in Notes) sb.AppendLine("- " + n);
        sb.AppendLine("Wrapper: " + AssetDatabase.GetAssetPath(wrapper));
        sb.AppendLine(data != null ? "CharacterData: " + AssetDatabase.GetAssetPath(data) + " code=" + data.code : "CharacterData: nao registrado (" + reason + ")");
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, sb.ToString());
    }
}
