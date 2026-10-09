using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EraImperial.Units.Advanced;
using EraImperial.Units.Siege;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Configura a Catapulta no Unity a partir dos FBX gerados por Tools/CharacterRigging/BuildCatapult.py e BuildCatapultProps.py:
/// importacao Generic, clipes (Idle, Move, Reload, Fire, Hit, Destroy), materiais, Animator Controller, prefabs (visual, projetil,
/// efeitos, morte, wrapper jogavel), vinculo ao contrato Assets/Data/AdvancedUnits/Catapult.asset e registro condicionado no catalogo.
/// Reexecutavel (idempotente): recria os assets gerados mantendo os GUIDs dos que ja existem.
/// Menu: Tools > Character Rigging > Catapult > Build Assets. Batch: -executeMethod CatapultSetup.BatchBuild
/// </summary>
public static class CatapultSetup
{
    public const string Root = "Assets/CharacterRigging/Catapult";
    public const string ModelsDir = Root + "/Models";
    public const string AccessoriesDir = Root + "/Accessories";
    public const string TexturesDir = Root + "/Textures";
    public const string MaterialsDir = Root + "/Materials";
    public const string AnimationDir = Root + "/Animation";
    public const string PrefabsDir = Root + "/Prefabs";
    public const string DataDir = Root + "/Data";

    public const string BaseModel = ModelsDir + "/Catapult.fbx";
    public const string BoulderModel = AccessoriesDir + "/CatapultBoulder.fbx";
    public const string DebrisModel = AccessoriesDir + "/CatapultDebris.fbx";
    public const string VisualPrefabPath = PrefabsDir + "/CatapultVisual.prefab";
    public const string BoulderPrefabPath = PrefabsDir + "/CatapultBoulder.prefab";
    public const string FirePrefabPath = PrefabsDir + "/CatapultFire.prefab";
    public const string ImpactPrefabPath = PrefabsDir + "/CatapultImpact.prefab";
    public const string CollapseDustPrefabPath = PrefabsDir + "/CatapultCollapseDust.prefab";
    public const string DeathPrefabPath = PrefabsDir + "/CatapultDestroyed.prefab";
    public const string WrapperPrefabPath = "Assets/Resources/Prefabs/Units/Characters/Catapult.prefab";
    public const string ControllerPath = AnimationDir + "/Catapult.controller";
    public const string DeathControllerPath = AnimationDir + "/CatapultDeath.controller";
    public const string ContractPath = "Assets/Data/AdvancedUnits/Catapult.asset";
    public const string CharacterDataPath = "Assets/Resources/ScriptableObjects/Units/Characters/Catapult.asset";
    public const string ReportPath = "Docs/CharacterMeshAudit/Catapult/unity-build-report.txt";
    public const string BuildReportJson = "Docs/CharacterMeshAudit/Catapult/build-report.json";

    public static readonly string[] ClipNames = { "Idle", "Move", "Reload", "Fire", "Hit", "Destroy" };
    public const float RingRadius = 1.5f;
    public const float AgentSpeed = 2.5f;
    public const float WheelCircumference = 2.8903f; // 2 * pi * 0.46 m (BuildCatapult.py)

    [Serializable]
    private class BuildReport
    {
        public float release_seconds = 0.4f;
        public float[] projectile_spawn_blender = new float[3];
    }

    static readonly List<string> Notes = new List<string>();

    [MenuItem("Tools/Character Rigging/Catapult/Build Assets")]
    public static void Build()
    {
        Notes.Clear();
        AssetDatabase.Refresh();
        foreach (string d in new[] { MaterialsDir, AnimationDir, PrefabsDir, DataDir, "Assets/Resources/Prefabs/Units/Characters" })
            AdvancedUnitBuilders.EnsureFolder(d);

        ConfigureTextures();
        Dictionary<string, Material> mats = CreateMaterials();
        Avatar avatar = ConfigureBaseModel(mats);
        BuildReport report = LoadBuildReport();
        Dictionary<string, AnimationClip> clips = ConfigureClips(avatar, report.release_seconds);
        ConfigureProp(BoulderModel, mats);
        ConfigureProp(DebrisModel, mats);

        AnimatorController controller = BuildController(clips);
        AnimatorController deathController = BuildDeathController(clips["Destroy"]);
        GameObject boulder = BuildBoulderPrefab(mats);
        GameObject fire = BuildFirePrefab(mats);
        GameObject impact = BuildImpactPrefab(mats);
        GameObject dust = BuildCollapseDustPrefab(mats);
        GameObject visual = BuildVisualPrefab(controller, avatar, fire);
        GameObject death = BuildDeathPrefab(deathController, avatar, dust);
        GameObject wrapper = BuildWrapperPrefab(visual, death, report.release_seconds);
        BindContract(wrapper, boulder, fire, impact, clips, out CharacterData data, out string registerReason);

        AssetDatabase.SaveAssets();
        WriteReport(clips, wrapper, data, registerReason);
        Debug.Log("CATAPULT_SETUP_OK\n" + string.Join("\n", Notes));
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
            Debug.LogError("CATAPULT_SETUP_FAILED " + e);
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

    static BuildReport LoadBuildReport()
    {
        string path = BuildReportJson;
        if (!File.Exists(path)) throw new InvalidOperationException("build-report.json ausente; rode Tools/CharacterRigging/Build-Catapult.ps1");
        BuildReport report = JsonUtility.FromJson<BuildReport>(File.ReadAllText(path));
        Notes.Add($"Instante de disparo (Blender): {report.release_seconds:F3}s");
        return report;
    }

    static void ConfigureTextures()
    {
        foreach (string name in new[] { "Catapult_Wood", "Catapult_Iron", "Catapult_Rope", "Catapult_Stone" })
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(TexturesDir + "/" + name + ".png");
            if (importer == null) throw new InvalidOperationException("Textura ausente: " + name);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
        }
        foreach (string name in new[] { "Fx_Smoke", "Fx_Flash", "Fx_Spark" })
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(TexturesDir + "/" + name + ".png");
            if (importer == null) throw new InvalidOperationException("Textura ausente: " + name);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 256;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }

    static Material MakeStandard(string name, Texture albedo, float metallic, float smoothness)
    {
        string path = MaterialsDir + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("Shader Standard nao encontrado.");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.color = Color.white;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        material.mainTexture = albedo;
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material MakeParticle(string name, string shaderName, Texture texture, Color tint)
    {
        string path = MaterialsDir + "/" + name + ".mat";
        Shader shader = Shader.Find(shaderName);
        if (shader == null) throw new InvalidOperationException("Shader de particula nao encontrado: " + shaderName);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.mainTexture = texture;
        if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", tint);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Dictionary<string, Material> CreateMaterials()
    {
        Dictionary<string, Material> m = new Dictionary<string, Material>
        {
            ["Catapult_Wood"] = MakeStandard("Catapult_Wood", Require<Texture2D>(TexturesDir + "/Catapult_Wood.png"), 0f, 0.12f),
            ["Catapult_Iron"] = MakeStandard("Catapult_Iron", Require<Texture2D>(TexturesDir + "/Catapult_Iron.png"), 0.55f, 0.38f),
            ["Catapult_Rope"] = MakeStandard("Catapult_Rope", Require<Texture2D>(TexturesDir + "/Catapult_Rope.png"), 0f, 0.05f),
            ["Catapult_Stone"] = MakeStandard("Catapult_Stone", Require<Texture2D>(TexturesDir + "/Catapult_Stone.png"), 0f, 0.1f),
        };
        Texture smoke = Require<Texture2D>(TexturesDir + "/Fx_Smoke.png");
        Texture flash = Require<Texture2D>(TexturesDir + "/Fx_Flash.png");
        Texture spark = Require<Texture2D>(TexturesDir + "/Fx_Spark.png");
        m["Fx_Smoke"] = MakeParticle("Fx_Smoke", "Legacy Shaders/Particles/Alpha Blended", smoke, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        m["Fx_Flash"] = MakeParticle("Fx_Flash", "Legacy Shaders/Particles/Additive", flash, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        m["Fx_Spark"] = MakeParticle("Fx_Spark", "Legacy Shaders/Particles/Additive", spark, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        AssetDatabase.SaveAssets();
        return m;
    }

    static void ApplyMaterialRemaps(ModelImporter importer, Dictionary<string, Material> mats)
    {
        foreach (KeyValuePair<string, Material> pair in mats.Where(p => p.Key.StartsWith("Catapult_")))
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
    }

    static Avatar ConfigureBaseModel(Dictionary<string, Material> mats)
    {
        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(BaseModel);
        if (importer == null) throw new InvalidOperationException("Modelo ausente: " + BaseModel);
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.importAnimation = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        ApplyMaterialRemaps(importer, mats);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects = false;
        importer.SaveAndReimport();
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(BaseModel).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid || avatar.isHuman)
            throw new InvalidOperationException("Avatar Generic da Catapult invalido.");
        return avatar;
    }

    static void ConfigureProp(string path, Dictionary<string, Material> mats)
    {
        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new InvalidOperationException("Modelo ausente: " + path);
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        ApplyMaterialRemaps(importer, mats);
        importer.SaveAndReimport();
    }

    static Dictionary<string, AnimationClip> ConfigureClips(Avatar avatar, float releaseSeconds)
    {
        Dictionary<string, AnimationClip> result = new Dictionary<string, AnimationClip>();
        foreach (string name in ClipNames)
        {
            string path = ModelsDir + "/Catapult@" + name + ".fbx";
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Clipe ausente: " + path);
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
            bool loop = name == "Idle" || name == "Move";
            clip.loopTime = loop;
            clip.loopPose = loop;
            AnimationClip temp = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            double duration = temp != null ? temp.length : (clip.lastFrame - clip.firstFrame) / 30.0;
            if (name == "Fire")
            {
                // eventos do importador usam tempo normalizado (0..1)
                clip.events = new[] { new AnimationEvent { functionName = "CatapultRelease", time = (float)(releaseSeconds / duration) } };
            }
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();

            AnimationClip asset = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (asset == null) throw new InvalidOperationException(path + ": clipe nao importado");
            result[name] = asset;
            if (name == "Fire")
            {
                AnimationEvent ev = AnimationUtility.GetAnimationEvents(asset).FirstOrDefault(e => e.functionName == "CatapultRelease");
                if (ev == null) throw new InvalidOperationException("Fire: evento CatapultRelease ausente");
                Notes.Add($"Fire: evento CatapultRelease em {ev.time:F3}s (esperado {releaseSeconds:F3}s), duracao {asset.length:F3}s");
                if (Mathf.Abs(ev.time - releaseSeconds) > 0.05f) throw new InvalidOperationException("Fire: tempo do evento difere do instante de disparo");
            }
            Notes.Add($"Clip {name}: {asset.length:F3}s loop={asset.isLooping} legacy={asset.legacy} humanoid={asset.isHumanMotion}");
        }
        return result;
    }

    // --------------------------------------------------------------------------------------------- animator
    static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
    {
        if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AdvancedUnitBuilders.AddStandardParameters(controller);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimatorState idle = machine.AddState("Idle"); idle.motion = clips["Idle"];
        AnimatorState move = machine.AddState("Move"); move.motion = clips["Move"];
        // um ciclo do clipe Move = uma volta da roda a AgentSpeed; a velocidade do estado acompanha o parametro Speed
        move.speedParameterActive = true; move.speedParameter = "Speed";
        move.speed = clips["Move"].length / (WheelCircumference / AgentSpeed) / AgentSpeed;
        AnimatorState fire = machine.AddState("Fire"); fire.motion = clips["Fire"];
        AnimatorState reload = machine.AddState("Reload"); reload.motion = clips["Reload"];
        AnimatorState hit = machine.AddState("Hit"); hit.motion = clips["Hit"];
        machine.defaultState = idle;

        AnimatorStateTransition T(AnimatorState from, AnimatorState to, bool exit, float exitTime, float duration)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = exit; t.exitTime = exitTime; t.duration = duration; t.hasFixedDuration = true;
            t.canTransitionToSelf = false;
            return t;
        }

        T(idle, move, false, 0f, 0.15f).AddCondition(AnimatorConditionMode.Greater, 0.15f, "Speed");
        T(move, idle, false, 0f, 0.20f).AddCondition(AnimatorConditionMode.Less, 0.10f, "Speed");
        foreach (AnimatorState from in new[] { idle, move, hit })
            T(from, fire, false, 0f, 0f).AddCondition(AnimatorConditionMode.If, 0, "Fire");
        T(fire, reload, true, 1.0f, 0f);
        T(reload, idle, true, 1.0f, 0.15f);
        foreach (AnimatorState from in new[] { idle, move })
            T(from, hit, false, 0f, 0.05f).AddCondition(AnimatorConditionMode.If, 0, "Damaged");
        T(hit, idle, true, 0.95f, 0.10f);
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

    // ---------------------------------------------------------------------------------------- particle helpers
    static ParticleSystem NewSystem(Transform parent, string name, Material material, float duration, bool loop = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.duration = duration;
        main.loop = loop;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 128;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    static Gradient AlphaGradient(Color color, params float[] alphas)
    {
        Gradient g = new Gradient();
        GradientAlphaKey[] keys = new GradientAlphaKey[alphas.Length];
        for (int i = 0; i < alphas.Length; i++) keys[i] = new GradientAlphaKey(alphas[i], alphas.Length == 1 ? 0f : i / (float)(alphas.Length - 1));
        g.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color * 0.8f, 1f) }, keys);
        return g;
    }

    static GameObject SavePrefab(GameObject root, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    // ----------------------------------------------------------------------------------------------- prefabs
    static GameObject BuildBoulderPrefab(Dictionary<string, Material> mats)
    {
        GameObject root = new GameObject("CatapultBoulder");
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(BoulderModel), root.transform);
        model.name = "Stone";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mats["Catapult_Stone"];
        TrailRenderer trail = root.AddComponent<TrailRenderer>();
        trail.sharedMaterial = mats["Fx_Smoke"];
        trail.time = 0.45f;
        trail.minVertexDistance = 0.25f;
        trail.startWidth = 0.30f;
        trail.endWidth = 0f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(0.85f, 0.82f, 0.76f), 0f), new GradientColorKey(new Color(0.7f, 0.68f, 0.64f), 1f) },
                  new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        return SavePrefab(root, BoulderPrefabPath);
    }

    static GameObject BuildFirePrefab(Dictionary<string, Material> mats)
    {
        GameObject root = new GameObject("CatapultFire");
        ParticleSystem dust = NewSystem(root.transform, "Dust", mats["Fx_Smoke"], 1.6f);
        ParticleSystem.MainModule m = dust.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.6f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new Color(0.78f, 0.70f, 0.58f, 0.85f);
        m.gravityModifier = -0.04f;
        dust.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });
        ParticleSystem.ShapeModule s = dust.shape;
        s.shapeType = ParticleSystemShapeType.Cone; s.angle = 40f; s.radius = 0.25f;
        s.rotation = new Vector3(-90f, 0f, 0f);
        ParticleSystem.SizeOverLifetimeModule sol = dust.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(1f, 1.5f)));
        ParticleSystem.ColorOverLifetimeModule col = dust.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(AlphaGradient(Color.white, 0f, 0.9f, 0.5f, 0f));
        ParticleSystem.LimitVelocityOverLifetimeModule lim = dust.limitVelocityOverLifetime;
        lim.enabled = true; lim.dampen = 0.2f; lim.limit = 0.5f;

        ParticleSystem flash = NewSystem(root.transform, "Flash", mats["Fx_Flash"], 0.3f);
        ParticleSystem.MainModule fm = flash.main;
        fm.startLifetime = 0.14f; fm.startSpeed = 0f; fm.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
        fm.startColor = new Color(1f, 0.9f, 0.7f, 0.6f);
        flash.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        ParticleSystem.SizeOverLifetimeModule fs = flash.sizeOverLifetime;
        fs.enabled = true;
        fs.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.4f)));
        ParticleSystem.ColorOverLifetimeModule fc = flash.colorOverLifetime;
        fc.enabled = true;
        fc.color = new ParticleSystem.MinMaxGradient(AlphaGradient(Color.white, 1f, 0.6f, 0f));
        return SavePrefab(root, FirePrefabPath);
    }

    static GameObject BuildImpactPrefab(Dictionary<string, Material> mats)
    {
        GameObject root = new GameObject("CatapultImpact");
        // flash curto
        ParticleSystem flash = NewSystem(root.transform, "Flash", mats["Fx_Flash"], 0.4f);
        ParticleSystem.MainModule fm = flash.main;
        fm.startLifetime = 0.22f; fm.startSpeed = 0f; fm.startSize = new ParticleSystem.MinMaxCurve(4.2f, 5.0f);
        fm.startColor = new Color(1f, 0.85f, 0.55f, 0.9f);
        flash.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        ParticleSystem.ShapeModule fsh = flash.shape; fsh.enabled = false;
        ParticleSystem.SizeOverLifetimeModule fs = flash.sizeOverLifetime;
        fs.enabled = true;
        fs.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.8f)));
        ParticleSystem.ColorOverLifetimeModule fc = flash.colorOverLifetime;
        fc.enabled = true;
        fc.color = new ParticleSystem.MinMaxGradient(AlphaGradient(Color.white, 1f, 0.6f, 0f));
        flash.transform.localPosition = new Vector3(0f, 0.6f, 0f);

        // nuvem de poeira
        ParticleSystem dust = NewSystem(root.transform, "Dust", mats["Fx_Smoke"], 2.2f);
        ParticleSystem.MainModule dm = dust.main;
        dm.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.0f);
        dm.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 4.5f);
        dm.startSize = new ParticleSystem.MinMaxCurve(2.6f, 4.4f);
        dm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        dm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.86f, 0.78f, 0.64f, 1f), new Color(0.74f, 0.70f, 0.64f, 1f));
        dm.gravityModifier = -0.03f;
        dust.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
        ParticleSystem.ShapeModule ds = dust.shape;
        ds.shapeType = ParticleSystemShapeType.Hemisphere; ds.radius = 0.7f; ds.rotation = new Vector3(-90f, 0f, 0f);
        ParticleSystem.SizeOverLifetimeModule dsz = dust.sizeOverLifetime;
        dsz.enabled = true;
        dsz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(1f, 1.5f)));
        ParticleSystem.ColorOverLifetimeModule dc = dust.colorOverLifetime;
        dc.enabled = true;
        dc.color = new ParticleSystem.MinMaxGradient(AlphaGradient(Color.white, 0f, 1f, 0.75f, 0f));
        ParticleSystem.LimitVelocityOverLifetimeModule dl = dust.limitVelocityOverLifetime;
        dl.enabled = true; dl.dampen = 0.12f; dl.limit = 0.6f;

        // fragmentos (malha)
        Mesh debris = AssetDatabase.LoadAllAssetsAtPath(DebrisModel).OfType<Mesh>().FirstOrDefault();
        if (debris == null) throw new InvalidOperationException("Malha de fragmento ausente em " + DebrisModel);
        ParticleSystem chunks = NewSystem(root.transform, "Debris", mats["Catapult_Stone"], 2.0f);
        ParticleSystemRenderer cr = chunks.GetComponent<ParticleSystemRenderer>();
        cr.renderMode = ParticleSystemRenderMode.Mesh;
        cr.mesh = debris;
        cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        ParticleSystem.MainModule cm = chunks.main;
        cm.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
        cm.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9.5f);
        cm.startSize3D = false;
        cm.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.34f);
        cm.startRotation3D = true;
        cm.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        cm.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        cm.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        cm.gravityModifier = 2.4f;
        cm.startColor = Color.white;
        chunks.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });
        ParticleSystem.ShapeModule cs = chunks.shape;
        cs.shapeType = ParticleSystemShapeType.Hemisphere; cs.radius = 0.35f; cs.rotation = new Vector3(-90f, 0f, 0f);
        ParticleSystem.RotationOverLifetimeModule rot = chunks.rotationOverLifetime;
        rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        ParticleSystem.SizeOverLifetimeModule csz = chunks.sizeOverLifetime;
        csz.enabled = true;
        csz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));

        // fagulhas / poeira fina
        ParticleSystem sparks = NewSystem(root.transform, "Sparks", mats["Fx_Spark"], 1.0f);
        ParticleSystem.MainModule sm = sparks.main;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
        sm.startColor = new Color(1f, 0.82f, 0.55f, 0.9f);
        sm.gravityModifier = 1.2f;
        sparks.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });
        ParticleSystem.ShapeModule ss = sparks.shape;
        ss.shapeType = ParticleSystemShapeType.Hemisphere; ss.radius = 0.2f; ss.rotation = new Vector3(-90f, 0f, 0f);
        ParticleSystem.ColorOverLifetimeModule sc = sparks.colorOverLifetime;
        sc.enabled = true;
        sc.color = new ParticleSystem.MinMaxGradient(AlphaGradient(Color.white, 1f, 0.7f, 0f));
        return SavePrefab(root, ImpactPrefabPath);
    }

    static GameObject BuildCollapseDustPrefab(Dictionary<string, Material> mats)
    {
        GameObject root = new GameObject("CatapultCollapseDust");
        ParticleSystem dust = NewSystem(root.transform, "Dust", mats["Fx_Smoke"], 3.0f);
        ParticleSystem.MainModule m = dust.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
        m.startSize = new ParticleSystem.MinMaxCurve(1.6f, 3.0f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.60f, 0.54f, 0.45f, 0.8f), new Color(0.48f, 0.45f, 0.41f, 0.8f));
        m.gravityModifier = -0.02f;
        dust.emission.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 16), new ParticleSystem.Burst(0.65f, 22), new ParticleSystem.Burst(1.2f, 14) });
        ParticleSystem.ShapeModule s = dust.shape;
        s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(2.2f, 0.2f, 3.0f); s.position = new Vector3(0f, 0.3f, 0f);
        ParticleSystem.SizeOverLifetimeModule sz = dust.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(1f, 1.5f)));
        ParticleSystem.ColorOverLifetimeModule col = dust.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(AlphaGradient(Color.white, 0f, 0.8f, 0.5f, 0f));

        Mesh debris = AssetDatabase.LoadAllAssetsAtPath(DebrisModel).OfType<Mesh>().FirstOrDefault();
        ParticleSystem chips = NewSystem(root.transform, "Splinters", mats["Catapult_Wood"], 2.0f);
        ParticleSystemRenderer cr = chips.GetComponent<ParticleSystemRenderer>();
        cr.renderMode = ParticleSystemRenderMode.Mesh;
        cr.mesh = debris;
        ParticleSystem.MainModule cm = chips.main;
        cm.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.0f);
        cm.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
        cm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        cm.startRotation3D = true;
        cm.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        cm.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        cm.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        cm.gravityModifier = 2.2f;
        chips.emission.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 10), new ParticleSystem.Burst(0.65f, 12) });
        ParticleSystem.ShapeModule cs = chips.shape;
        cs.shapeType = ParticleSystemShapeType.Box; cs.scale = new Vector3(1.8f, 0.2f, 2.4f); cs.position = new Vector3(0f, 0.9f, 0f);
        ParticleSystem.SizeOverLifetimeModule csz = chips.sizeOverLifetime;
        csz.enabled = true;
        csz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));
        return SavePrefab(root, CollapseDustPrefabPath);
    }

    static void ConfigureLods(GameObject instance)
    {
        LODGroup lodGroup = instance.GetComponentInChildren<LODGroup>(true);
        SkinnedMeshRenderer[] renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Where(r => r.name.Contains("_LOD")).OrderBy(r => r.name).ToArray();
        if (renderers.Length != 3) throw new InvalidOperationException("Esperado 3 SkinnedMeshRenderer _LOD0/1/2, encontrado " + renderers.Length);
        if (lodGroup == null)
        {
            lodGroup = instance.AddComponent<LODGroup>();
            Notes.Add("O importador nao criou LODGroup; criado manualmente.");
        }
        // Camera ortografica do jogo: zoom 8..26 (padrao 18), lodBias 2. A catapulta mede ~3,4 m (a do Arqueiro ~1,8 m), logo a altura
        // relativa e ~1,9x a do Arqueiro: LOD0 ate zoom ~13, LOD1 ate ~22, LOD2 alem disso (calibrado/verificado em CatapultPlayModeCheck).
        lodGroup.SetLODs(new[]
        {
            new LOD(0.33f, new Renderer[] { renderers[0] }),
            new LOD(0.15f, new Renderer[] { renderers[1] }),
            new LOD(0.0f, new Renderer[] { renderers[2] }),
        });
        lodGroup.fadeMode = LODFadeMode.None;
        lodGroup.RecalculateBounds();
        foreach (SkinnedMeshRenderer r in renderers)
        {
            r.updateWhenOffscreen = false;
            r.skinnedMotionVectors = false;
        }
        renderers[2].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static GameObject BuildVisualPrefab(AnimatorController controller, Avatar avatar, GameObject fire)
    {
        GameObject fbx = Require<GameObject>(BaseModel);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        instance.name = "CatapultVisual";
        Animator animator = instance.GetComponent<Animator>();
        if (animator == null) animator = instance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // o socket ProjectileSpawn precisa estar correto mesmo fora da tela

        Transform spawn = AdvancedUnitBuilders.FindDeep(instance.transform, "ProjectileSpawn");
        Transform arm = AdvancedUnitBuilders.FindDeep(instance.transform, "Arm");
        if (arm == null) throw new InvalidOperationException("Osso Arm ausente no modelo.");
        if (spawn == null)
        {
            // fallback: cria o socket na posicao do centro da concha medida pelo Blender (espaco do Blender -> Unity)
            BuildReport report = LoadBuildReport();
            Vector3 b = new Vector3(report.projectile_spawn_blender[0], report.projectile_spawn_blender[1], report.projectile_spawn_blender[2]);
            GameObject socket = new GameObject("ProjectileSpawn");
            socket.transform.SetParent(arm, false);
            socket.transform.position = instance.transform.TransformPoint(new Vector3(-b.x, b.z, -b.y));
            spawn = socket.transform;
            Notes.Add("ProjectileSpawn nao veio no FBX; criado como filho de Arm na posicao do Blender.");
        }
        else Notes.Add("ProjectileSpawn veio do FBX (filho de " + spawn.parent.name + ").");

        CatapultAnimationEvents events = instance.GetComponent<CatapultAnimationEvents>();
        if (events == null) events = instance.AddComponent<CatapultAnimationEvents>();
        events.projectileSpawn = spawn;
        events.fireEffectPrefab = fire;
        ConfigureLods(instance);
        return SavePrefab(instance, VisualPrefabPath);
    }

    static GameObject BuildDeathPrefab(AnimatorController deathController, Avatar avatar, GameObject dust)
    {
        GameObject fbx = Require<GameObject>(BaseModel);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        instance.name = "CatapultDestroyed";
        Animator animator = instance.GetComponent<Animator>();
        if (animator == null) animator = instance.AddComponent<Animator>();
        animator.runtimeAnimatorController = deathController;
        animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        ConfigureLods(instance);
        GameObject effect = (GameObject)PrefabUtility.InstantiatePrefab(dust, instance.transform);
        effect.name = "CollapseDust";
        effect.transform.localPosition = Vector3.zero;
        return SavePrefab(instance, DeathPrefabPath);
    }

    static GameObject BuildWrapperPrefab(GameObject visualPrefab, GameObject deathPrefab, float releaseSeconds)
    {
        GameObject root = AdvancedUnitBuilders.BuildWrapperRoot("Catapult", visualPrefab, RingRadius, out GameObject visual);
        Animator animator = visual.GetComponentInChildren<Animator>();

        BoxCollider box = root.GetComponent<BoxCollider>();
        box.center = new Vector3(0f, 1.15f, 0f);
        box.size = new Vector3(2.3f, 2.3f, 3.3f);

        AdvancedUnitBuilders.ConfigureAgent(root, AgentSpeed, 4f, 0.5f, 110f);
        NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
        agent.radius = 1.0f;
        agent.height = 2.2f;
        agent.avoidancePriority = 30;

        // marcadores do Soldier dimensionados para a pegada da catapulta
        Transform circle = root.transform.Find("SelectionCircle");
        if (circle != null)
        {
            Projector projector = circle.GetComponent<Projector>();
            if (projector != null) { projector.orthographicSize *= 3.0f; Notes.Add("SelectionCircle: orthographicSize x3"); }
        }
        foreach (Transform child in root.transform)
        {
            string n = child.name.ToLowerInvariant();
            if (n.Contains("health"))
            {
                child.localPosition = new Vector3(child.localPosition.x, 3.0f, child.localPosition.z);
                child.localScale = child.localScale * 2.2f;
                Notes.Add("Barra de vida '" + child.name + "' elevada para y=3.0, escala x2.2");
            }
        }

        CatapultContract contract = AdvancedUnitBuilders.LoadContract<CatapultContract>("Catapult");
        AdvancedUnitEraBinding binding = root.AddComponent<AdvancedUnitEraBinding>();
        binding.contract = contract;
        AdvancedUnitAnimatorDriver driver = root.AddComponent<AdvancedUnitAnimatorDriver>();
        driver.animator = animator;

        Transform spawn = AdvancedUnitBuilders.FindDeep(visual.transform, "ProjectileSpawn");
        CatapultCombatController combat = root.AddComponent<CatapultCombatController>();
        combat.contract = contract;
        combat.animator = animator;
        combat.projectileSpawn = spawn;
        combat.releaseDelay = releaseSeconds;
        combat.sourceHeight = 1.3f;
        combat.turnSpeed = 90f;

        AdvancedUnitDeathEffect death = root.AddComponent<AdvancedUnitDeathEffect>();
        death.deathPrefab = deathPrefab;
        death.lifetime = 20f;

        GameObject prefab = AdvancedUnitBuilders.SaveWrapper(root, WrapperPrefabPath);
        return prefab;
    }

    // --------------------------------------------------------------------------------------------- contract
    static void SetAnimationEntry(SerializedObject so, string state, string parameter)
    {
        SerializedProperty list = so.FindProperty("visual.animations");
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("state").stringValue != state) continue;
            e.FindPropertyRelative("animatorParameter").stringValue = parameter;
            e.FindPropertyRelative("required").boolValue = true;
            return;
        }
        list.arraySize++;
        SerializedProperty n = list.GetArrayElementAtIndex(list.arraySize - 1);
        n.FindPropertyRelative("state").stringValue = state;
        n.FindPropertyRelative("animatorParameter").stringValue = parameter;
        n.FindPropertyRelative("clip").objectReferenceValue = null;
        n.FindPropertyRelative("required").boolValue = true;
    }

    static void EnsureEffectEntry(SerializedObject so, string role, string expectedPath)
    {
        SerializedProperty list = so.FindProperty("visual.effects");
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("role").stringValue != role) continue;
            e.FindPropertyRelative("expectedAssetPath").stringValue = expectedPath;
            e.FindPropertyRelative("required").boolValue = true;
            return;
        }
        list.arraySize++;
        SerializedProperty n = list.GetArrayElementAtIndex(list.arraySize - 1);
        n.FindPropertyRelative("role").stringValue = role;
        n.FindPropertyRelative("expectedAssetPath").stringValue = expectedPath;
        n.FindPropertyRelative("asset").objectReferenceValue = null;
        n.FindPropertyRelative("required").boolValue = true;
    }

    static void BindContract(GameObject wrapper, GameObject boulder, GameObject fire, GameObject impact,
        Dictionary<string, AnimationClip> clips, out CharacterData data, out string registerReason)
    {
        CatapultContract contract = AdvancedUnitBuilders.LoadContract<CatapultContract>("Catapult");

        // desenho real: animacoes, parametros e efeitos (disparo e impacto)
        SerializedObject so = new SerializedObject(contract);
        SetAnimationEntry(so, "Idle", "Speed");
        SetAnimationEntry(so, "Move", "Speed");
        SetAnimationEntry(so, "Reload", "Fire");
        SetAnimationEntry(so, "Fire", "Fire");
        SetAnimationEntry(so, "Hit", "Damaged");
        SetAnimationEntry(so, "Destroy", "(prefab de morte)");
        EnsureEffectEntry(so, "efeito de disparo", FirePrefabPath);
        EnsureEffectEntry(so, "efeito de impacto", ImpactPrefabPath);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(contract);

        AdvancedUnitBuilders.BindPrefab(contract, wrapper);
        AdvancedUnitBuilders.BindVisualReference(contract, "meshes", "malha-fonte da catapulta", Require<GameObject>(BaseModel));
        AdvancedUnitBuilders.BindVisualReference(contract, "effects", "efeito de disparo", fire);
        AdvancedUnitBuilders.BindVisualReference(contract, "effects", "efeito de impacto", impact);
        foreach (string clip in ClipNames) AdvancedUnitBuilders.BindAnimation(contract, clip, clips[clip]);

        Transform spawn = AdvancedUnitBuilders.FindDeep(wrapper.transform, "ProjectileSpawn");
        Transform arm = AdvancedUnitBuilders.FindDeep(wrapper.transform, "Arm");
        if (spawn == null || arm == null) throw new InvalidOperationException("Sockets ausentes no wrapper (ProjectileSpawn/Arm).");
        string spawnPath = AdvancedUnitBuilders.RelativePath(wrapper.transform, spawn);
        string armPath = AdvancedUnitBuilders.RelativePath(wrapper.transform, arm);
        AdvancedUnitBuilders.BindSocket(contract, "ProjectileSpawn", spawnPath);
        AdvancedUnitBuilders.BindSocket(contract, "ArmPivot", armPath);
        Notes.Add("Socket ProjectileSpawn: " + spawnPath);
        Notes.Add("Socket ArmPivot (osso Arm, pivo do braco): " + armPath);

        ProjectileContract projectile = contract.Projectile;
        if (projectile == null) throw new InvalidOperationException("Contrato sem projetil.");
        AdvancedUnitBuilders.BindProjectile(projectile, boulder, impact);
        AssetDatabase.SaveAssets();

        data = AdvancedUnitBuilders.RegisterCharacterData(contract, "Catapult", out registerReason);
        Notes.Add(data != null ? "CharacterData registrado: " + CharacterDataPath : "CharacterData NAO registrado: " + registerReason);
    }

    static void WriteReport(Dictionary<string, AnimationClip> clips, GameObject wrapper, CharacterData data, string registerReason)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Catapult - Unity build report (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")");
        foreach (string n in Notes) sb.AppendLine("- " + n);
        sb.AppendLine("Wrapper: " + AssetDatabase.GetAssetPath(wrapper));
        sb.AppendLine(data != null ? "CharacterData: " + AssetDatabase.GetAssetPath(data) + " code=" + data.code : "CharacterData: nao criado (" + registerReason + ")");
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, sb.ToString());
    }
}
