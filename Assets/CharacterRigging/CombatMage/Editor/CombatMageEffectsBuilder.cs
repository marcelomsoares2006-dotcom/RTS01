using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gera, por script, as texturas suaves (procedurais) e os prefabs de efeitos magicos do Mago de combate:
/// projetil CombatMageBolt (orbe + rastro), impacto e conjuracao. Tudo com sombreadores do Built-in RP
/// (Legacy Shaders/Particles/Additive e Standard emissivo), sem terceiros e sem magenta.
/// </summary>
public static class CombatMageEffectsBuilder
{
    public const string Root = "Assets/CharacterRigging/CombatMage";
    public const string TexturesDir = Root + "/Textures";
    public const string MaterialsDir = Root + "/Materials";
    public const string PrefabsDir = Root + "/Prefabs";
    public const string BoltPrefabPath = PrefabsDir + "/CombatMageBolt.prefab";
    public const string ImpactPrefabPath = PrefabsDir + "/CombatMageImpact.prefab";
    public const string CastPrefabPath = PrefabsDir + "/CombatMageCast.prefab";

    static readonly Color Arcane = new Color(0.40f, 0.72f, 1.00f, 1f);
    static readonly Color ArcaneHot = new Color(0.85f, 0.95f, 1.00f, 1f);
    static readonly Color Violet = new Color(0.55f, 0.45f, 1.00f, 1f);

    // ------------------------------------------------------------------------------------------ textures
    public static Texture2D EnsureTexture(string name, int width, int height, Func<float, float, float> alphaAt)
    {
        string path = TexturesDir + "/" + name + ".png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width * 2f - 1f;   // -1..1
                    float v = (y + 0.5f) / height * 2f - 1f;
                    float a = Mathf.Clamp01(alphaAt(u, v));
                    byte b = (byte)Mathf.RoundToInt(a * 255f);
                    pixels[y * width + x] = new Color32(255, 255, 255, b);
                }
            tex.SetPixels32(pixels);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 256;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    public static void BuildTextures(out Texture2D glow, out Texture2D ring, out Texture2D spark, out Texture2D streak)
    {
        AdvancedUnitBuilders.EnsureFolder(TexturesDir);
        glow = EnsureTexture("CombatMage_FxGlow", 128, 128, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            return (Mathf.Exp(-r * r * 5.5f) * 0.85f + Mathf.Exp(-r * r * 22f) * 0.6f) * (1f - Smooth((r - 0.75f) / 0.25f));
        });
        ring = EnsureTexture("CombatMage_FxRing", 256, 256, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float band = Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.085f, 2f));
            return band * (1f - Smooth((r - 0.93f) / 0.07f));
        });
        spark = EnsureTexture("CombatMage_FxSpark", 64, 64, (u, v) =>
        {
            float a = Mathf.Exp(-Mathf.Abs(u) * 11f - Mathf.Abs(v) * 2.6f);
            float b = Mathf.Exp(-Mathf.Abs(v) * 11f - Mathf.Abs(u) * 2.6f);
            float core = Mathf.Exp(-(u * u + v * v) * 40f);
            return Mathf.Max(Mathf.Max(a, b) * 0.9f, core) * (1f - Smooth((Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) - 0.85f) / 0.15f));
        });
        streak = EnsureTexture("CombatMage_FxStreak", 128, 32, (u, v) =>
        {
            float along = Mathf.Clamp01((u + 1f) * 0.5f);          // 0 = cauda, 1 = cabeca (TrailRenderer em Stretch)
            float across = Mathf.Exp(-v * v * 4.5f);
            return across * Mathf.Pow(along, 1.4f);
        });
    }

    // ------------------------------------------------------------------------------------------ materials
    public static Material EnsureAdditive(string name, Texture2D texture, Color tint)
    {
        AdvancedUnitBuilders.EnsureFolder(MaterialsDir);
        string path = MaterialsDir + "/" + name + ".mat";
        var shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null) throw new InvalidOperationException("Shader Legacy Shaders/Particles/Additive nao encontrado.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_TintColor", tint * 0.5f);
        material.mainTexture = texture;
        EditorUtility.SetDirty(material);
        return material;
    }

    public static Material EnsureEmissive(string name, Color color, Color emission)
    {
        AdvancedUnitBuilders.EnsureFolder(MaterialsDir);
        string path = MaterialsDir + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = Shader.Find("Standard");
        material.color = color;
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", 0.6f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(material);
        return material;
    }

    // ------------------------------------------------------------------------------------------ particle helpers
    static ParticleSystem NewSystem(string name, Transform parent, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.maxParticles = 200;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.enabled = false;
        return ps;
    }

    static Gradient FadeGradient(Color color, float inAlpha, float peak, float peakAt)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(inAlpha, 0f), new GradientAlphaKey(peak, peakAt), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    static void SetColorOverLifetime(ParticleSystem ps, Gradient gradient)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    static void SetSizeOverLifetime(ParticleSystem ps, float from, float to)
    {
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
    }

    static void Burst(ParticleSystem ps, int count)
    {
        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    static void Sphere(ParticleSystem ps, float radius, float thickness = 1f)
    {
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        shape.radiusThickness = thickness;
    }

    static void SavePrefab(GameObject root, string path)
    {
        AdvancedUnitBuilders.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
    }

    // ------------------------------------------------------------------------------------------ prefabs
    public static GameObject BuildBolt(Material glowMat, Material sparkMat, Material trailMat)
    {
        var root = new GameObject("CombatMageBolt");
        root.AddComponent<AdvancedProjectile>();

        var coreMat = EnsureEmissive("CombatMage_BoltCore", new Color(0.18f, 0.45f, 1f), new Color(0.16f, 0.45f, 1f) * 1.7f);
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        UnityEngine.Object.DestroyImmediate(core.GetComponent<Collider>());
        core.transform.SetParent(root.transform, false);
        core.transform.localScale = Vector3.one * 0.26f;
        var coreRenderer = core.GetComponent<MeshRenderer>();
        coreRenderer.sharedMaterial = coreMat;
        coreRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        coreRenderer.receiveShadows = false;

        var glow = NewSystem("Glow", root.transform, glowMat);
        var main = glow.main;
        main.loop = true; main.duration = 1f;
        main.startLifetime = 0.35f; main.startSize = 0.95f; main.startSpeed = 0f;
        main.startColor = new Color(0.30f, 0.58f, 1f, 0.30f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = glow.emission; emission.rateOverTime = 9f;
        SetColorOverLifetime(glow, FadeGradient(Color.white, 0f, 1f, 0.3f));

        var sparks = NewSystem("Sparks", root.transform, sparkMat);
        main = sparks.main;
        main.loop = true; main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(Arcane, Violet);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.05f;
        emission = sparks.emission; emission.rateOverTime = 55f;
        Sphere(sparks, 0.1f);
        SetColorOverLifetime(sparks, FadeGradient(Color.white, 1f, 1f, 0.2f));
        SetSizeOverLifetime(sparks, 1f, 0.1f);

        foreach (var ps in new[] { glow, sparks })
        {
            var m = ps.main;
            m.playOnAwake = true;
        }

        var trailObject = new GameObject("Trail");
        trailObject.transform.SetParent(root.transform, false);
        var trail = trailObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = trailMat;
        trail.time = 0.32f;
        trail.minVertexDistance = 0.05f;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.30f), new Keyframe(1f, 0f));
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(ArcaneHot, 0f), new GradientColorKey(Arcane, 0.4f), new GradientColorKey(Violet, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.autodestruct = false;

        SavePrefab(root, BoltPrefabPath);
        return AssetDatabase.LoadAssetAtPath<GameObject>(BoltPrefabPath);
    }

    public static GameObject BuildImpact(Material glowMat, Material ringMat, Material sparkMat)
    {
        var root = new GameObject("CombatMageImpact");

        var flash = NewSystem("Flash", root.transform, glowMat);
        var main = flash.main;
        main.duration = 0.6f; main.startLifetime = 0.28f; main.startSpeed = 0f; main.startSize = 2.6f;
        main.startColor = new Color(0.75f, 0.9f, 1f, 0.9f);
        Burst(flash, 1);
        SetColorOverLifetime(flash, FadeGradient(Color.white, 1f, 1f, 0.1f));
        SetSizeOverLifetime(flash, 0.55f, 1.2f);

        var ring = NewSystem("Ring", root.transform, ringMat);
        main = ring.main;
        main.duration = 0.6f; main.startLifetime = 0.5f; main.startSpeed = 0f; main.startSize = 3.4f;
        main.startColor = new Color(0.5f, 0.78f, 1f, 0.85f);
        Burst(ring, 1);
        SetColorOverLifetime(ring, FadeGradient(Color.white, 1f, 1f, 0.2f));
        SetSizeOverLifetime(ring, 0.15f, 1f);

        var sparks = NewSystem("Sparks", root.transform, sparkMat);
        main = sparks.main;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(Arcane, Violet);
        main.gravityModifier = 0.6f;
        Burst(sparks, 30);
        Sphere(sparks, 0.12f);
        SetColorOverLifetime(sparks, FadeGradient(Color.white, 1f, 1f, 0.3f));
        SetSizeOverLifetime(sparks, 1f, 0.15f);

        var motes = NewSystem("Motes", root.transform, glowMat);
        main = motes.main;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startColor = new Color(0.45f, 0.65f, 1f, 0.35f);
        main.gravityModifier = -0.15f;
        Burst(motes, 12);
        Sphere(motes, 0.35f);
        SetColorOverLifetime(motes, FadeGradient(Color.white, 0f, 1f, 0.25f));
        SetSizeOverLifetime(motes, 0.6f, 1.3f);

        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>())
        {
            var m = ps.main;
            m.playOnAwake = true;
            m.stopAction = ParticleSystemStopAction.None;
        }
        SavePrefab(root, ImpactPrefabPath);
        return AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPrefabPath);
    }

    public static GameObject BuildCast(Material glowMat, Material sparkMat)
    {
        var root = new GameObject("CombatMageCast");

        var charge = NewSystem("Charge", root.transform, sparkMat);
        var main = charge.main;
        main.loop = true; main.duration = 1f;
        main.startLifetime = 0.42f; main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
        main.startColor = new ParticleSystem.MinMaxGradient(Arcane, ArcaneHot);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = charge.emission; emission.rateOverTime = 70f;
        Sphere(charge, 0.5f, 0f);
        var velocity = charge.velocityOverLifetime; velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f);
        velocity.radial = new ParticleSystem.MinMaxCurve(-1.2f);
        SetColorOverLifetime(charge, FadeGradient(Color.white, 0f, 1f, 0.3f));
        SetSizeOverLifetime(charge, 1f, 0.3f);

        var core = NewSystem("Core", root.transform, glowMat);
        main = core.main;
        main.loop = true; main.duration = 1f;
        main.startLifetime = 0.45f; main.startSpeed = 0f; main.startSize = 0.6f;
        main.startColor = new Color(0.55f, 0.8f, 1f, 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        emission = core.emission; emission.rateOverTime = 9f;
        SetColorOverLifetime(core, FadeGradient(Color.white, 0f, 1f, 0.4f));
        SetSizeOverLifetime(core, 0.6f, 1.4f);

        var burst = NewSystem("ReleaseBurst", root.transform, glowMat);
        main = burst.main;
        main.duration = 0.5f; main.startLifetime = 0.3f; main.startSpeed = 0f; main.startSize = 1.5f;
        main.startColor = new Color(0.8f, 0.92f, 1f, 0.9f);
        Burst(burst, 1);
        SetColorOverLifetime(burst, FadeGradient(Color.white, 1f, 1f, 0.1f));
        SetSizeOverLifetime(burst, 0.4f, 1.2f);

        var flare = NewSystem("ReleaseSparks", burst.transform, sparkMat);
        main = flare.main;
        main.duration = 0.5f; main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(Arcane, Violet);
        Burst(flare, 16);
        Sphere(flare, 0.08f);
        SetColorOverLifetime(flare, FadeGradient(Color.white, 1f, 1f, 0.3f));
        SetSizeOverLifetime(flare, 1f, 0.1f);

        SavePrefab(root, CastPrefabPath);
        return AssetDatabase.LoadAssetAtPath<GameObject>(CastPrefabPath);
    }

    /// <summary>Cria texturas, materiais e os tres prefabs. Devolve (projetil, impacto, conjuracao).</summary>
    public static (GameObject bolt, GameObject impact, GameObject cast) BuildAll()
    {
        BuildTextures(out var glow, out var ring, out var spark, out var streak);
        var glowMat = EnsureAdditive("CombatMage_FxGlow", glow, new Color(0.7f, 0.85f, 1f));
        var ringMat = EnsureAdditive("CombatMage_FxRing", ring, new Color(0.65f, 0.85f, 1f));
        var sparkMat = EnsureAdditive("CombatMage_FxSpark", spark, new Color(0.8f, 0.9f, 1f));
        var trailMat = EnsureAdditive("CombatMage_FxTrail", streak, new Color(0.7f, 0.85f, 1f));
        AssetDatabase.SaveAssets();
        var bolt = BuildBolt(glowMat, sparkMat, trailMat);
        var impact = BuildImpact(glowMat, ringMat, sparkMat);
        var cast = BuildCast(glowMat, sparkMat);
        return (bolt, impact, cast);
    }
}
