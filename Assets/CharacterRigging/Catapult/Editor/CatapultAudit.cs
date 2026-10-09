using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Siege;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Auditoria em Edit Mode da Catapulta: licenca documentada, modelo/rig/pesos/UV/materiais, LODs, clipes e eventos, controller,
/// sockets (que acompanham o braco), efeitos, projetil separado, wrapper, contrato x assets, progressao nas 4 eras, catalogo e renders.
/// Batch: -executeMethod CatapultAudit.Run (sai com 0 se tudo passar). Menu: Tools > Character Rigging > Catapult > Audit.
/// Saida: Docs/CharacterMeshAudit/Catapult/unity-audit.txt e PNGs Unity-*.png.
/// </summary>
public static class CatapultAudit
{
    const string OutDir = "Docs/CharacterMeshAudit/Catapult/";
    static readonly List<string> Lines = new List<string>();
    static int failures;

    static void Check(bool ok, string message)
    {
        Lines.Add((ok ? "PASS " : "FAIL ") + message);
        if (!ok) failures++;
    }

    static void Info(string message) { Lines.Add("INFO " + message); }

    [MenuItem("Tools/Character Rigging/Catapult/Audit")]
    public static void RunFromMenu() { Run(); }

    public static void Run()
    {
        Lines.Clear();
        failures = 0;
        try { Execute(); }
        catch (Exception e) { Lines.Add("EXCEPTION " + e); failures++; }
        Directory.CreateDirectory(OutDir);
        Lines.Add(failures == 0 ? "CATAPULT_AUDIT_PASSED" : "CATAPULT_AUDIT_FAILED failures=" + failures);
        File.WriteAllText(OutDir + "unity-audit.txt", string.Join("\n", Lines) + "\n");
        Debug.Log(string.Join("\n", Lines));
        if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    static AnimationClip LoadClip(string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(CatapultSetup.ModelsDir + "/Catapult@" + name + ".fbx")
            .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
    }

    static Transform Find(Transform root, string name) { return AdvancedUnitBuilders.FindDeep(root, name); }

    static void Execute()
    {
        // ---- origem e licenca
        string license = File.Exists(OutDir + "LICENSE.md") ? File.ReadAllText(OutDir + "LICENSE.md") : "";
        Check(license.Length > 500 && license.Contains("obra do proprio projeto") && license.Contains("BuildCatapult.py"),
            "LICENSE.md documenta o modelo procedural (obra do projeto, sem terceiros)");
        Check(!Directory.Exists("Tools/CharacterRigging/Inputs/Catapult"), "Nenhuma entrada de terceiros em Tools/CharacterRigging/Inputs/Catapult");

        // ---- importadores
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(CatapultSetup.BaseModel).OfType<Avatar>().FirstOrDefault();
        Check(avatar != null && avatar.isValid && !avatar.isHuman, "Avatar Generic valido (Catapult.fbx)");
        foreach (string clipName in CatapultSetup.ClipNames)
        {
            string path = CatapultSetup.ModelsDir + "/Catapult@" + clipName + ".fbx";
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            Check(importer != null && importer.animationType == ModelImporterAnimationType.Generic && importer.sourceAvatar == avatar,
                $"Clip {clipName}: Generic copiando o avatar da Catapult");
            Check(clip != null && clip.length > 0.3f && !clip.isHumanMotion, $"Clip {clipName}: importado, {clip?.length:F2}s");
            if (clip != null)
            {
                bool wantLoop = clipName == "Idle" || clipName == "Move";
                Check(clip.isLooping == wantLoop, $"Clip {clipName}: loop = {wantLoop}");
                bool animatesArm = AnimationUtility.GetCurveBindings(clip).Any(b => b.path.EndsWith("Arm") || b.path.Contains("/Arm"));
                bool animatesWheel = AnimationUtility.GetCurveBindings(clip).Any(b => b.path.Contains("Wheel_"));
                Check(animatesArm && animatesWheel, $"Clip {clipName}: curvas nos ossos Arm e Wheel_*");
            }
        }
        AnimationClip fireClip = LoadClip("Fire");
        AnimationEvent release = AnimationUtility.GetAnimationEvents(fireClip).FirstOrDefault(e => e.functionName == "CatapultRelease");
        Check(release != null && release.time > 0.2f && release.time < fireClip.length - 0.2f, $"Fire: evento CatapultRelease em {release?.time:F3}s de {fireClip.length:F3}s");

        // ---- prefab visual
        GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CatapultSetup.VisualPrefabPath);
        Check(visualPrefab != null, "CatapultVisual.prefab existe");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab);
        Animator animator = instance.GetComponent<Animator>();
        Check(animator != null && animator.avatar == avatar && animator.runtimeAnimatorController != null && !animator.applyRootMotion,
            "Animator com avatar, controller e root motion desligado");
        Check(instance.GetComponent<CatapultAnimationEvents>() != null, "CatapultAnimationEvents no GameObject do Animator");
        Check(Quaternion.Angle(instance.transform.rotation, Quaternion.identity) < 0.5f, "Visual chega ao Unity sem rotacao de importacao");

        string[] bones = { "Root", "Arm", "Wheel_FL", "Wheel_FR", "Wheel_BL", "Wheel_BR", "Winch" };
        foreach (string b in bones) Check(Find(instance.transform, b) != null, "Osso " + b + " presente");
        Transform arm = Find(instance.transform, "Arm");
        Transform spawn = Find(instance.transform, "ProjectileSpawn");
        Check(spawn != null && spawn.IsChildOf(arm), "ProjectileSpawn existe e e filho do osso Arm");

        // ---- LODs, malhas, pesos, UV
        LODGroup group = instance.GetComponentInChildren<LODGroup>();
        Check(group != null && group.lodCount == 3, "LODGroup com 3 niveis");
        SkinnedMeshRenderer[] smrs = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(r => r.name).ToArray();
        Check(smrs.Length == 3, "3 SkinnedMeshRenderer (LOD0/1/2): " + string.Join(", ", smrs.Select(r => r.name)));
        int[] limits = { 8000, 3000, 1000 };
        long[] tris = smrs.Select(r => r.sharedMesh.triangles.Length / 3L).ToArray();
        Info("Triangulos LOD0/1/2: " + string.Join(" / ", tris));
        Check(tris[0] <= limits[0] && tris[1] <= limits[1] && tris[2] <= limits[2] && tris[0] > tris[1] && tris[1] > tris[2],
            $"LODs dentro do orcamento (<= 8k/3k/1k) e estritamente decrescentes: {tris[0]} > {tris[1]} > {tris[2]}");
        for (int i = 0; i < smrs.Length; i++)
        {
            SkinnedMeshRenderer smr = smrs[i];
            Mesh mesh = smr.sharedMesh;
            BoneWeight[] weights = mesh.boneWeights;
            int bad = weights.Count(w => Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1f) > 0.02f);
            int over4 = 0; // BoneWeight guarda no maximo 4 influencias
            Check(smr.bones.Length >= 8 && smr.rootBone != null && bad == 0 && weights.Length == mesh.vertexCount,
                $"{smr.name}: {smr.bones.Length} ossos, rootBone ok, vertices sem peso normalizado = {bad}, >4 influencias = {over4}");
            Vector2[] uv = mesh.uv;
            int[] idx = mesh.triangles;
            bool finite = uv.All(u => float.IsFinite(u.x) && float.IsFinite(u.y));
            int degenerate = 0;
            for (int t = 0; t < idx.Length; t += 3)
            {
                Vector2 a = uv[idx[t]], b = uv[idx[t + 1]], c = uv[idx[t + 2]];
                float area = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
                if (area < 1e-9f) degenerate++;
            }
            float uMin = uv.Min(u => u.x), uMax = uv.Max(u => u.x), vMin = uv.Min(u => u.y), vMax = uv.Max(u => u.y);
            Check(uv.Length == mesh.vertexCount && finite && uMin >= -0.001f && uMax <= 1.001f && vMin >= -0.001f && vMax <= 1.001f && degenerate < idx.Length / 3 * 0.02f,
                $"{smr.name}: UVs validas (finitas, [0,1], triangulos degenerados em UV {degenerate}/{idx.Length / 3})");
            Check(mesh.normals.Length == mesh.vertexCount, $"{smr.name}: normais presentes");
            foreach (Material material in smr.sharedMaterials)
            {
                bool ok = material != null && material.shader != null && material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader) &&
                          material.shader.name == "Standard" && material.mainTexture != null;
                Check(ok, $"{smr.name}: material valido {(material != null ? material.name + " (" + material.shader.name + ")" : "NULL")}");
            }
            Check(smr.sharedMaterials.Length >= (i == 2 ? 2 : 4), $"{smr.name}: {smr.sharedMaterials.Length} materiais (madeira, ferro, corda, pedra; o LOD2 dispensa o ferro)");
        }
        Bounds bounds = smrs[0].bounds;
        Info($"Tamanho (LOD0, mundo, pose armada): {bounds.size:F2} m");
        Check(bounds.size.z > 3.0f && bounds.size.z < 3.8f && bounds.size.x > 2.0f && bounds.size.x < 2.5f && bounds.size.y > 1.8f && bounds.size.y < 2.8f,
            "Dimensoes ~3,2 x 2,2 x ~2-2,6 m (comprimento ao longo de Z = frente)");
        Check(bounds.min.y > -0.05f && bounds.min.y < 0.05f, $"Origem no chao (base em y={bounds.min.y:F3})");

        // ---- controller
        AnimatorController controller = (AnimatorController)animator.runtimeAnimatorController;
        string[] states = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
        Check(new[] { "Idle", "Move", "Fire", "Reload", "Hit" }.All(states.Contains), "Estados: " + string.Join(", ", states));
        foreach (string p in new[] { "Speed", "Fire", "Damaged", "Running", "Attacking", "Hit" })
            Check(controller.parameters.Any(x => x.name == p), "Parametro " + p);
        Check(controller.parameters.First(x => x.name == "Fire").type == AnimatorControllerParameterType.Trigger &&
              controller.parameters.First(x => x.name == "Damaged").type == AnimatorControllerParameterType.Trigger &&
              controller.parameters.First(x => x.name == "Speed").type == AnimatorControllerParameterType.Float, "Tipos: Fire/Damaged trigger, Speed float");
        AnimatorState Get(string n) { return controller.layers[0].stateMachine.states.First(s => s.state.name == n).state; }
        bool Goes(string from, string to, string param) { return Get(from).transitions.Any(t => t.destinationState == Get(to) && (param == null || t.conditions.Any(c => c.parameter == param))); }
        Check(Goes("Idle", "Fire", "Fire") && Goes("Move", "Fire", "Fire"), "Fire (trigger) leva a Fire");
        Check(Goes("Fire", "Reload", null) && Goes("Reload", "Idle", null), "Fluxo Fire -> Reload -> Idle");
        Check(Goes("Idle", "Move", "Speed") && Goes("Move", "Idle", "Speed"), "Speed alterna Idle <-> Move");
        Check(Goes("Idle", "Hit", "Damaged") && Goes("Hit", "Idle", null), "Damaged leva a Hit e volta a Idle");
        Check(Get("Move").speedParameterActive && Get("Move").speedParameter == "Speed", "Move acompanha o parametro Speed (rodas giram conforme a velocidade)");
        Check(Get("Idle").motion == LoadClip("Idle") && Get("Move").motion == LoadClip("Move") && Get("Fire").motion == fireClip &&
              Get("Reload").motion == LoadClip("Reload") && Get("Hit").motion == LoadClip("Hit"), "Cada estado usa o clipe certo");

        foreach (Transform tr in instance.GetComponentsInChildren<Transform>(true))
            if (!tr.name.Contains("_LOD")) Info($"hierarquia {AdvancedUnitBuilders.RelativePath(instance.transform, tr)} pos={tr.position:F2} rot={tr.rotation.eulerAngles:F0} escala={tr.lossyScale:F2}");
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(fireClip).Take(6)) Info($"Fire curva: path='{b.path}' prop={b.propertyName}");
        // ---- animacao: o braco varre, o socket acompanha o braco, as rodas giram
        AnimationClip move = LoadClip("Move");
        Transform wheel = Find(instance.transform, "Wheel_FL");
        float wheelDelta = RotationDelta(instance, move, wheel, 0f, move.length * 0.25f);
        Check(wheelDelta > 40f, $"Move: Wheel_FL gira {wheelDelta:F0} graus em um quarto do ciclo");
        LoadClip("Idle").SampleAnimation(instance, 0f);
        Vector3 spawnRest = spawn.position;
        Vector3 armRest = arm.position;
        fireClip.SampleAnimation(instance, release != null ? release.time : 0.4f);
        Vector3 spawnRelease = spawn.position;
        Check(spawnRelease.y > spawnRest.y + 1.0f && Vector3.Distance(armRest, arm.position) < 0.2f,
            $"ProjectileSpawn acompanha o braco: y {spawnRest.y:F2} (armada) -> {spawnRelease.y:F2} (disparo); pivo parado");
        Info($"ProjectileSpawn no disparo: {spawnRelease:F2}; no repouso: {spawnRest:F2}");
        Check(spawnRelease.z > spawnRest.z + 0.7f, $"ProjectileSpawn vai de tras (z={spawnRest.z:F2}) para perto da vertical, mais a frente (z={spawnRelease.z:F2}): braco varre para +Z (frente)");
        LoadClip("Idle").SampleAnimation(instance, 0f);
        AnimationClip destroy = LoadClip("Destroy");
        Vector3 armBefore = arm.position;
        destroy.SampleAnimation(instance, destroy.length);
        float armMoved = Vector3.Distance(armBefore, arm.position);
        Check(armMoved > 0.5f, $"Destroy: o braco termina {armMoved:F2} m do lugar de repouso (desabado)");
        LoadClip("Idle").SampleAnimation(instance, 0f);

        // ---- deformacao (BakeMesh): cada osso move a sua parte da malha
        DeformationAudit(instance, smrs[0]);

        // ---- renders
        RenderViews(instance, group, arm);

        // ---- efeitos, projetil, morte, wrapper e contrato
        EffectsAndProjectileAudit();
        WrapperAudit(instance);
        UnityEngine.Object.DestroyImmediate(instance);
        ContractAudit();
    }

    static void DeformationAudit(GameObject instance, SkinnedMeshRenderer lod0)
    {
        Mesh baked = new Mesh();
        LoadClip("Idle").SampleAnimation(instance, 0f);
        lod0.BakeMesh(baked, true);
        Vector3[] rest = baked.vertices;
        BoneWeight[] weights = lod0.sharedMesh.boneWeights;
        string[] boneNames = lod0.bones.Select(b => b.name).ToArray();
        var expectations = new (string clip, float t, string bone, float min)[]
        {
            ("Fire", 0.40f, "Arm", 1.2f), ("Fire", 0.40f, "Ammo", 1.2f), ("Reload", 1.00f, "Arm", 0.8f), ("Reload", 1.00f, "Winch", 0.01f),
            ("Move", 0.28f, "Wheel_FL", 0.25f), ("Move", 0.28f, "Wheel_BR", 0.25f), ("Hit", 0.07f, "Root", 0.003f),
            ("Destroy", 2.30f, "Arm", 1.0f), ("Destroy", 2.30f, "Wheel_FR", 0.8f), ("Destroy", 2.30f, "Wheel_BR", 0.8f), ("Destroy", 2.30f, "Root", 0.1f),
        };
        foreach (var e in expectations)
        {
            AnimationClip clip = LoadClip(e.clip);
            clip.SampleAnimation(instance, Mathf.Min(e.t, clip.length));
            lod0.BakeMesh(baked, true);
            Vector3[] now = baked.vertices;
            float max = 0f;
            int count = 0;
            for (int i = 0; i < now.Length && i < rest.Length; i++)
            {
                if (boneNames[weights[i].boneIndex0] != e.bone) continue;
                count++;
                max = Mathf.Max(max, Vector3.Distance(now[i], rest[i]));
            }
            Check(count > 0 && max >= e.min, $"Deformacao {e.clip}@{e.t:F2}s: vertices do osso {e.bone} ({count}) se deslocam ate {max:F2} m (minimo {e.min:F2})");
        }
        UnityEngine.Object.DestroyImmediate(baked);
        LoadClip("Idle").SampleAnimation(instance, 0f);
    }

    static float RotationDelta(GameObject root, AnimationClip clip, Transform bone, float t0, float t1)
    {
        clip.SampleAnimation(root, t0);
        Quaternion a = bone.localRotation;
        clip.SampleAnimation(root, t1);
        return Quaternion.Angle(a, bone.localRotation);
    }

    // ------------------------------------------------------------------------------------------------ renders
    static Camera MakeCamera(out List<GameObject> temp, out RenderTexture target)
    {
        temp = new List<GameObject>();
        GameObject cameraObject = new GameObject("AuditCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.34f, 0.36f, 0.39f);
        camera.fieldOfView = 28f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 200f;
        GameObject lightObject = new GameObject("AuditLight");
        lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(3f, 1f, 3f);
        Material gm = new Material(Shader.Find("Standard")) { color = new Color(0.30f, 0.34f, 0.27f) };
        gm.SetFloat("_Glossiness", 0f);
        ground.GetComponent<Renderer>().sharedMaterial = gm;
        temp.Add(cameraObject); temp.Add(lightObject); temp.Add(ground);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
        target = new RenderTexture(960, 720, 24);
        camera.targetTexture = target;
        return camera;
    }

    static (int distinct, int magenta) Capture(Camera camera, RenderTexture target, string path)
    {
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Color32[] pixels = texture.GetPixels32();
        int magenta = pixels.Count(p => p.r > 220 && p.g < 40 && p.b > 220);
        Color bg = camera.backgroundColor;
        int distinct = pixels.Count(p => Mathf.Abs(p.r / 255f - bg.r) > 0.04f || Mathf.Abs(p.g / 255f - bg.g) > 0.04f || Mathf.Abs(p.b / 255f - bg.b) > 0.04f);
        UnityEngine.Object.DestroyImmediate(texture);
        return (distinct, magenta);
    }

    // Em Edit Mode a camera nao atualiza a pele dos SkinnedMeshRenderer apos SampleAnimation: desenha a malha "baked" na pose atual.
    static (int distinct, int magenta) CaptureSkinned(Camera camera, RenderTexture target, string path, SkinnedMeshRenderer smr)
    {
        Mesh baked = new Mesh();
        smr.BakeMesh(baked, true);
        Matrix4x4 matrix = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
        bool wasEnabled = smr.enabled;
        smr.enabled = false;
        Material[] materials = smr.sharedMaterials;
        for (int i = 0; i < baked.subMeshCount; i++)
            Graphics.DrawMesh(baked, matrix, materials[Mathf.Min(i, materials.Length - 1)], 0, camera, i);
        (int, int) result = Capture(camera, target, path);
        smr.enabled = wasEnabled;
        UnityEngine.Object.DestroyImmediate(baked);
        return result;
    }

    static void Aim(Camera camera, Vector3 position, Vector3 target)
    {
        camera.transform.position = position;
        camera.transform.rotation = Quaternion.LookRotation(target - position);
    }

    static void RenderViews(GameObject instance, LODGroup group, Transform arm)
    {
        Directory.CreateDirectory(OutDir);
        Camera camera = MakeCamera(out List<GameObject> temp, out RenderTexture target);
        try
        {
            SkinnedMeshRenderer[] lodRenderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(r => r.name).ToArray();
            SkinnedMeshRenderer smrs0 = lodRenderers[0];
            foreach (SkinnedMeshRenderer r in lodRenderers) r.enabled = true;
            group.ForceLOD(0);
            LoadClip("Idle").SampleAnimation(instance, 0f);
            Vector3 focus = new Vector3(0f, 1.1f, 0f);
            // Unity: frente da unidade = +Z
            var views = new (string name, Vector3 pos)[]
            {
                ("front", new Vector3(0f, 2.3f, 9.5f)), ("back", new Vector3(0f, 2.3f, -9.5f)), ("side", new Vector3(9.5f, 1.8f, 0f)),
                ("q-front", new Vector3(6.2f, 3.4f, 6.4f)), ("q-rear", new Vector3(-6.2f, 3.4f, -6.4f)),
            };
            foreach ((string name, Vector3 pos) v in views)
            {
                Aim(camera, v.pos, focus);
                (int fg, int pink) = CaptureSkinned(camera, target, OutDir + "Unity-" + v.name + ".png", smrs0);
                Check(fg > 6000 && pink < 40, $"Render {v.name}: ok (pixels distintos {fg}, magenta {pink})");
            }
            for (int lod = 0; lod < 3; lod++)
            {
                group.ForceLOD(lod);
                Aim(camera, new Vector3(6.2f, 3.4f, 6.4f), focus);
                (int fg, int pink) = CaptureSkinned(camera, target, OutDir + $"Unity-LOD{lod}-q-front.png", lodRenderers[lod]);
                Check(fg > 5000 && pink < 40, $"LOD{lod}: renderiza (pixels distintos {fg}, magenta {pink})");
            }
            group.ForceLOD(0);
            Info("LOD screen heights: " + string.Join(", ", group.GetLODs().Select(l => l.screenRelativeTransitionHeight.ToString("F3"))));
            LOD[] lods = group.GetLODs();
            Check(lods.Length == 3 && lods[0].screenRelativeTransitionHeight > lods[1].screenRelativeTransitionHeight && lods[1].screenRelativeTransitionHeight > lods[2].screenRelativeTransitionHeight,
                "LODs com limiares decrescentes");

            var poses = new (string clip, float t)[]
            {
                ("Fire", 0.00f), ("Fire", 0.25f), ("Fire", 0.40f), ("Fire", 0.50f), ("Reload", 0.15f), ("Reload", 1.00f), ("Reload", 1.80f), ("Reload", 2.55f),
                ("Hit", 0.08f), ("Move", 0.30f), ("Destroy", 0.45f), ("Destroy", 1.00f), ("Destroy", 2.35f),
            };
            foreach ((string clip, float t) in poses)
            {
                AnimationClip c = LoadClip(clip);
                c.SampleAnimation(instance, Mathf.Min(t, c.length));
                bool sideView = clip == "Fire" || clip == "Reload";
                Aim(camera, sideView ? new Vector3(9.5f, 2.0f, 0f) : new Vector3(6.2f, 3.4f, 6.4f), sideView ? new Vector3(0f, 1.3f, 0f) : focus);
                (int fg, int pink) = CaptureSkinned(camera, target, OutDir + $"Unity-pose-{clip}-{Mathf.RoundToInt(t * 100):D3}.png", smrs0);
                Check(fg > 5000 && pink < 40, $"Pose {clip}@{t:F2}s: renderiza (pixels distintos {fg}, magenta {pink})");
            }
            LoadClip("Idle").SampleAnimation(instance, 0f);
        }
        finally
        {
            group.ForceLOD(-1);
            foreach (GameObject go in temp) UnityEngine.Object.DestroyImmediate(go);
            target.Release();
        }
    }

    // ------------------------------------------------------------------------------------- efeitos e projetil
    static void CheckMaterials(GameObject root, string label)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
            {
                bool ok = material != null && material.shader != null && material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader);
                Check(ok, $"Material valido: {label}/{renderer.name} -> {(material != null ? material.name + " (" + material.shader.name + ")" : "NULL")}");
            }
    }

    static void EffectsAndProjectileAudit()
    {
        GameObject boulder = AssetDatabase.LoadAssetAtPath<GameObject>(CatapultSetup.BoulderPrefabPath);
        GameObject fire = AssetDatabase.LoadAssetAtPath<GameObject>(CatapultSetup.FirePrefabPath);
        GameObject impact = AssetDatabase.LoadAssetAtPath<GameObject>(CatapultSetup.ImpactPrefabPath);
        GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(CatapultSetup.WrapperPrefabPath);
        Check(boulder != null && fire != null && impact != null, "Prefabs CatapultBoulder, CatapultFire e CatapultImpact existem");
        if (boulder == null || fire == null || impact == null) return;

        Check(boulder.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "Projetil sem scripts (AdvancedProjectile e adicionado em runtime)");
        Check(boulder.GetComponentsInChildren<MeshFilter>(true).Length == 1 && wrapper.GetComponentsInChildren<MeshFilter>(true).All(f => f.sharedMesh == null || !AssetDatabase.GetAssetPath(f.sharedMesh).Contains("CatapultBoulder")),
            "Projetil e um prefab separado (a malha da pedra nao faz parte do wrapper)");
        MeshFilter stone = boulder.GetComponentInChildren<MeshFilter>();
        Vector3 size = stone.sharedMesh.bounds.size;
        Check(size.x > 0.33f && size.x < 0.47f && size.y > 0.30f && size.z > 0.33f, $"Pedra com ~0,4 m: {size:F2} m");
        int stoneTris = stone.sharedMesh.triangles.Length / 3;
        Check(stoneTris > 100 && stoneTris <= 1000, $"Pedra low-poly: {stoneTris} triangulos");
        CheckMaterials(boulder, "CatapultBoulder");
        CheckMaterials(fire, "CatapultFire");
        CheckMaterials(impact, "CatapultImpact");
        CheckMaterials(AssetDatabase.LoadAssetAtPath<GameObject>(CatapultSetup.CollapseDustPrefabPath), "CatapultCollapseDust");
        foreach (GameObject fx in new[] { fire, impact })
        {
            ParticleSystem[] systems = fx.GetComponentsInChildren<ParticleSystem>(true);
            Check(systems.Length >= 2 && systems.All(s => s.main.playOnAwake && s.emission.burstCount > 0), $"{fx.name}: {systems.Length} sistemas de particulas com rajada");
            foreach (ParticleSystemRenderer r in fx.GetComponentsInChildren<ParticleSystemRenderer>(true))
                Check(r.sharedMaterial != null && (r.renderMode != ParticleSystemRenderMode.Mesh || r.mesh != null), $"{fx.name}/{r.name}: material e malha atribuidos");
            float maxLife = systems.Max(s => s.main.duration + s.main.startLifetime.constantMax);
            Check(maxLife < 6f, $"{fx.name}: vida maxima {maxLife:F1} s (< 6 s)");
        }
        Check(impact.GetComponentsInChildren<ParticleSystem>(true).Any(s => s.name == "Debris") && impact.GetComponentsInChildren<ParticleSystem>(true).Any(s => s.name == "Dust") &&
              impact.GetComponentsInChildren<ParticleSystem>(true).Any(s => s.name == "Flash"), "Impacto: poeira, fragmentos e flash");
        foreach (string tex in new[] { "Fx_Smoke", "Fx_Flash", "Fx_Spark" })
        {
            Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(CatapultSetup.TexturesDir + "/" + tex + ".png");
            Check(t != null && t.format == TextureFormat.RGBA32 || t != null && t.alphaIsTransparency, $"Textura de particula {tex} com alfa");
        }
    }

    // ------------------------------------------------------------------------------------------------ wrapper
    static void WrapperAudit(GameObject visualInstance)
    {
        GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(CatapultSetup.WrapperPrefabPath);
        Check(wrapper != null, "Catapult.prefab (wrapper da unidade) existe");
        if (wrapper == null) return;
        Check(wrapper.tag == "Unit" && wrapper.layer == 12, "Wrapper com tag Unit e layer 12");
        foreach (string component in new[] { "CharacterManager", "CharacterBT", "FogRendererToggler", "AdvancedUnitEraBinding", "AdvancedUnitAnimatorDriver",
                     "CatapultCombatController", "AdvancedUnitDeathEffect" })
            Check(wrapper.GetComponent(component) != null, "Wrapper tem " + component);
        BoxCollider box = wrapper.GetComponent<BoxCollider>();
        Check(box != null && box.size.z > 3.0f && box.size.x > 2.0f && box.size.y > 2.0f && box.center.y > 0.9f, $"BoxCollider cobre a catapulta: centro {box?.center} tamanho {box?.size}");
        NavMeshAgent agent = wrapper.GetComponent<NavMeshAgent>();
        Check(agent != null && Mathf.Abs(agent.speed - CatapultSetup.AgentSpeed) < 0.01f && agent.radius >= 0.9f && agent.height >= 2f,
            $"NavMeshAgent lento: velocidade {agent?.speed}, raio {agent?.radius}, altura {agent?.height}");
        Check(wrapper.GetComponent<AudioSource>() != null && wrapper.transform.Find("Mesh/MinimapIcon") != null, "AudioSource e Mesh/MinimapIcon presentes");
        Transform ring = wrapper.transform.Find("Mesh/TeamRing");
        Check(ring != null && ring.GetComponent<MeshRenderer>() != null, "Anel de time presente");
        if (ring != null)
        {
            Vector3 ringSize = ring.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            Check(ringSize.x > 2.8f && ringSize.x < 3.4f, $"Anel do time com raio ~ pegada: diametro {ringSize.x:F2} m");
        }
        MonoBehaviour manager = (MonoBehaviour)wrapper.GetComponent("CharacterManager");
        SerializedObject so = new SerializedObject(manager);
        Check(so.FindProperty("meshRenderer").objectReferenceValue != null && so.FindProperty("animator").objectReferenceValue != null, "CharacterManager.meshRenderer e animator atribuidos");
        Check(so.FindProperty("selectionCircle").objectReferenceValue != null && so.FindProperty("fov").objectReferenceValue != null, "selectionCircle e fov atribuidos");

        CatapultContract contract = AssetDatabase.LoadAssetAtPath<CatapultContract>(CatapultSetup.ContractPath);
        CatapultCombatController combat = wrapper.GetComponent<CatapultCombatController>();
        Check(combat.contract == contract && combat.animator != null && combat.projectileSpawn != null && combat.releaseDelay > 0.2f && combat.sourceHeight > 0.5f,
            $"CatapultCombatController: contrato, animator, projectileSpawn, releaseDelay={combat.releaseDelay:F2}, sourceHeight={combat.sourceHeight:F2}");
        Check(combat.projectileSpawn != null && combat.projectileSpawn.name == "ProjectileSpawn", "projectileSpawn aponta para o socket real");
        AnimationEvent ev = AnimationUtility.GetAnimationEvents(LoadClip("Fire")).First(e => e.functionName == "CatapultRelease");
        Check(Mathf.Abs(combat.releaseDelay - ev.time) < 0.05f, $"releaseDelay ({combat.releaseDelay:F3}s) = instante do evento de disparo do clipe ({ev.time:F3}s)");
        Check(wrapper.GetComponent<AdvancedUnitEraBinding>().contract == contract, "AdvancedUnitEraBinding.contract = Catapult.asset");
        Check(wrapper.GetComponent<AdvancedUnitAnimatorDriver>().animator != null, "AdvancedUnitAnimatorDriver.animator atribuido");
        AdvancedUnitDeathEffect death = wrapper.GetComponent<AdvancedUnitDeathEffect>();
        GameObject deathPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CatapultSetup.DeathPrefabPath);
        Check(death.deathPrefab == deathPrefab && deathPrefab != null, "AdvancedUnitDeathEffect.deathPrefab = CatapultDestroyed.prefab");
        Animator deathAnimator = deathPrefab.GetComponent<Animator>();
        AnimatorController deathController = deathAnimator != null ? deathAnimator.runtimeAnimatorController as AnimatorController : null;
        Check(deathController != null && deathController.layers[0].stateMachine.defaultState != null && deathController.layers[0].stateMachine.defaultState.name == "Destroy" &&
              deathController.layers[0].stateMachine.defaultState.motion == LoadClip("Destroy"), "Prefab de morte toca Destroy sozinho (estado padrao)");
        Check(deathPrefab.GetComponentInChildren<ParticleSystem>(true) != null, "Prefab de morte tem poeira do colapso");
        CheckMaterials(wrapper, "Catapult wrapper");
        CheckMaterials(deathPrefab, "CatapultDestroyed");
        Animator wrapperAnimator = wrapper.GetComponentInChildren<Animator>();
        Check(wrapperAnimator != null && wrapperAnimator.runtimeAnimatorController == AssetDatabase.LoadAssetAtPath<AnimatorController>(CatapultSetup.ControllerPath), "Animator do wrapper usa Catapult.controller");

        // catalogo
        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(CatapultSetup.CharacterDataPath);
        Check(data != null && data.code == "catapult" && data.prefab == wrapper, "CharacterData 'catapult' referencia o wrapper");
        CharacterData[] catalog = Resources.LoadAll<CharacterData>("ScriptableObjects/Units/Characters");
        Check(catalog.Any(c => c.code == "catapult"), "Catalogo Resources carrega 'catapult' (" + catalog.Length + " personagens)");
        List<string> codes = catalog.Select(c => c.code).ToList();
        Check(codes.Count == codes.Distinct().Count(), "Codigos de personagem unicos: " + string.Join(", ", codes));
        ImperialEra ancient = ImperialEra.Ancient;
        CatapultContract c0 = contract;
        UnitDataFields f = c0.BuildUnitDataFields(ancient);
        Check(data != null && data.healthpoints == f.healthpoints && data.attackDamage == f.attackDamage && Mathf.Abs(data.attackRange - f.attackRange) < 0.01f,
            $"CharacterData com os valores Ancient do contrato (vida {data?.healthpoints}, dano {data?.attackDamage}, alcance {data?.attackRange})");
    }

    // ------------------------------------------------------------------------------------------------ contrato
    static void ContractAudit()
    {
        CatapultContract contract = AssetDatabase.LoadAssetAtPath<CatapultContract>(CatapultSetup.ContractPath);
        GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(CatapultSetup.WrapperPrefabPath);
        Check(contract != null, "Contrato Catapult.asset existe");
        if (contract == null) return;
        ValidationReport validation = contract.Validate();
        Check(validation.IsValid, "Contrato valido (dados de jogo): " + validation);
        VisualReadinessReport visual = contract.EvaluateVisuals();
        Check(visual.IsReady, "Contrato com visual pronto (unidade + projetil): " + visual);
        Check(contract.visual.prefab.asset == wrapper, "visual.prefab = wrapper jogavel Catapult.prefab");
        foreach (AnimationRequirement a in contract.visual.animations)
            Check(a.clip != null && a.clip == LoadClip(a.state), $"Animacao {a.state} ({a.animatorParameter}) = clipe real Catapult@{a.state}");
        Check(new[] { "Idle", "Move", "Reload", "Fire", "Hit", "Destroy" }.All(s => contract.visual.animations.Any(a => a.state == s)), "visual.animations = Idle, Move, Reload, Fire, Hit, Destroy");
        foreach (SocketRequirement s in contract.visual.sockets)
            Check(s.IsBoundTo(wrapper), $"Socket {s.socketName} em '{s.expectedTransformPath}' (Transform.Find a partir da raiz do wrapper)");
        foreach (VisualAssetReference r in contract.visual.effects)
            Check(r.IsResolved && r.asset is GameObject, $"Efeito '{r.role}' -> {AssetDatabase.GetAssetPath(r.asset)}");
        Check(contract.visual.effects.Any(r => r.role.Contains("disparo")) && contract.visual.effects.Any(r => r.role.Contains("impacto")), "Efeitos cobrem disparo e impacto");
        foreach (VisualAssetReference r in contract.visual.meshes)
            Check(r.IsResolved, $"Malha '{r.role}' -> {AssetDatabase.GetAssetPath(r.asset)}");
        ProjectileContract projectile = contract.Projectile;
        Check(projectile != null && projectile.Validate().IsValid && projectile.EvaluateVisuals().IsReady, "Projetil (contrato separado) valido e com prefab + impacto reais");
        Check(projectile.prefab.asset == AssetDatabase.LoadAssetAtPath<GameObject>(CatapultSetup.BoulderPrefabPath) &&
              projectile.impactEffect.asset == AssetDatabase.LoadAssetAtPath<GameObject>(CatapultSetup.ImpactPrefabPath), "ProjectileContract aponta para CatapultBoulder.prefab e CatapultImpact.prefab");
        Check(AssetDatabase.GetAssetPath(projectile).Contains("Projectiles/CatapultBoulder.asset") && projectile.ballistic && projectile.arcHeight > 0f, "Projetil balistico em arco (contrato separado)");

        // progressao nas 4 eras
        IReadOnlyList<IEraTier> tiers = contract.GetTiers();
        Check(tiers.Count == 4 && tiers.Select(t => t.Era).SequenceEqual(ImperialEras.Ordered), "4 eras em ordem: Ancient, Classical, Medieval, Imperial");
        bool hp = true, dmg = true, rng = true, cd = true;
        for (int i = 1; i < tiers.Count; i++)
        {
            hp &= tiers[i].HealthPoints > tiers[i - 1].HealthPoints;
            dmg &= tiers[i].AttackDamage > tiers[i - 1].AttackDamage;
            rng &= tiers[i].AttackRange >= tiers[i - 1].AttackRange;
            cd &= tiers[i].AttackCooldown <= tiers[i - 1].AttackCooldown;
        }
        Check(hp && dmg && rng && cd, "Progressao por era: vida e dano crescem; alcance nao diminui; recarga nao aumenta");
        Info("Eras: " + string.Join(" | ", tiers.Select(t => $"{t.Era}: vida {t.HealthPoints}, dano {t.AttackDamage}, alcance {t.AttackRange}, recarga {t.AttackCooldown}")));
        foreach (ImperialEra era in ImperialEras.Ordered)
        {
            UnitDataFields f = contract.BuildUnitDataFields(era);
            Check(f.code == "catapult" && f.healthpoints > 0 && f.attackDamage > 0, $"BuildUnitDataFields({era}): vida {f.healthpoints}, dano {f.attackDamage}, alcance {f.attackRange}");
        }
    }
}
