using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Auditoria em Edit Mode do Archer: avatar humanoide, malhas/pesos, materiais, LODs (renderizados), deformacao
/// dos clipes (BakeMesh), acessorios nos soquetes, controller, wrapper e catalogo. Gera capturas das poses.
/// Batch: -executeMethod ArcherAudit.Run (sai com 0 se tudo passar). Menu: Tools > Character Rigging > Archer > Audit.
/// </summary>
public static class ArcherAudit
{
    const string OutDir = "Docs/CharacterMeshAudit/Archer/";
    static readonly List<string> Lines = new List<string>();
    static int failures;

    static void Check(bool ok, string message)
    {
        Lines.Add((ok ? "PASS " : "FAIL ") + message);
        if (!ok) failures++;
    }

    static void Info(string message) { Lines.Add("INFO " + message); }

    [MenuItem("Tools/Character Rigging/Archer/Audit")]
    public static void RunFromMenu() { Run(); }

    public static void Run()
    {
        Lines.Clear();
        failures = 0;
        try { Execute(); }
        catch (Exception e) { Lines.Add("EXCEPTION " + e); failures++; }
        Directory.CreateDirectory(OutDir);
        Lines.Add(failures == 0 ? "ARCHER_AUDIT_PASSED" : "ARCHER_AUDIT_FAILED failures=" + failures);
        File.WriteAllText(OutDir + "unity-audit.txt", string.Join("\n", Lines) + "\n");
        Debug.Log(string.Join("\n", Lines));
        if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    static void Execute()
    {
        // ---- avatar / importers
        var avatar = AssetDatabase.LoadAllAssetsAtPath(ArcherSetup.BaseModel).OfType<Avatar>().FirstOrDefault();
        Check(avatar != null && avatar.isValid && avatar.isHuman, "Avatar humanoide valido (Archer.fbx)");
        foreach (var clipName in new[] { "Idle", "Walk", "Run", "Aim", "DrawString", "Fire" })
        {
            string path = ArcherSetup.Root + "/Models/Archer@" + clipName + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            Check(importer != null && importer.animationType == ModelImporterAnimationType.Human && importer.sourceAvatar == avatar,
                $"Clip {clipName}: Humanoid copiando o avatar do Archer");
            Check(clip != null && clip.isHumanMotion && clip.length > 0.3f, $"Clip {clipName}: importado, humanoide, {clip?.length:F2}s");
        }

        // ---- visual prefab
        var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArcherSetup.VisualPrefabPath);
        Check(visualPrefab != null, "ArcherVisual.prefab existe");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab);
        var animator = instance.GetComponent<Animator>();
        Check(animator != null && animator.avatar == avatar && animator.runtimeAnimatorController != null && !animator.applyRootMotion,
            "Animator com avatar, controller e root motion desligado");
        Check(instance.GetComponent<ArcherAnimationEvents>() != null, "ArcherAnimationEvents no GameObject do Animator");

        var group = instance.GetComponentInChildren<LODGroup>();
        Check(group != null && group.lodCount == 3, "LODGroup com 3 niveis");
        var smrs = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(r => r.name).ToArray();
        Check(smrs.Length == 3, "3 SkinnedMeshRenderer (LOD0/1/2): " + string.Join(", ", smrs.Select(r => r.name)));
        var lod0 = smrs.First(r => r.name.EndsWith("LOD0"));
        long[] tris = smrs.Select(r => r.sharedMesh.triangles.Length / 3L).ToArray();
        Info("Triangulos LOD0/1/2: " + string.Join(" / ", tris));
        Check(tris[0] > 25000 && tris[0] < 36000 && tris[1] < tris[0] * 0.4 && tris[2] < tris[1] * 0.5, "Orcamento de triangulos decrescente por LOD");
        foreach (var smr in smrs)
        {
            var mesh = smr.sharedMesh;
            var weights = mesh.boneWeights;
            int bad = weights.Count(w => Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1f) > 0.02f);
            Check(smr.bones.Length >= 22 && smr.rootBone != null && bad == 0,
                $"{smr.name}: {smr.bones.Length} ossos, rootBone ok, vertices sem peso normalizado = {bad}");
            Check(smr.sharedMaterials.Length == 1 && smr.sharedMaterial != null && smr.sharedMaterial.name == "Archer_Mat", $"{smr.name}: material Archer_Mat");
        }

        // ---- materials (visual + accessories + projectile + wrapper)
        var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ArcherSetup.ProjectilePrefabPath);
        var wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(ArcherSetup.WrapperPrefabPath);
        foreach (var root in new[] { visualPrefab, projectile, wrapper })
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    bool ok = material != null && material.shader != null && material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader);
                    Check(ok, $"Material valido: {root.name}/{renderer.name} -> {(material != null ? material.name + " (" + material.shader.name + ")" : "NULL")}");
                }
        }
        var body = lod0.sharedMaterial;
        Check(body.mainTexture != null && body.GetTexture("_BumpMap") != null, "Archer_Mat com textura base e normal map");

        // ---- socket parenting
        var bow = instance.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Bow");
        var quiver = instance.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Quiver");
        var held = instance.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "HeldArrow");
        var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        var spine = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        Check(bow != null && bow.IsChildOf(leftHand), "Arco e objeto separado, filho da mao esquerda");
        Check(quiver != null && quiver.IsChildOf(spine), "Aljava e objeto separado, filha do tronco (UpperChest)");
        Check(held != null && !held.gameObject.activeSelf, "Flecha da mao existe e comeca oculta");
        Check(!lod0.GetComponentsInChildren<Transform>().Any(t => t.name == "Bow" || t.name == "Quiver"), "Acessorios nao fazem parte da malha do corpo");

        // ---- controller
        var controller = (AnimatorController)animator.runtimeAnimatorController;
        var states = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
        Check(new[] { "Locomotion", "DrawString", "Aim", "Fire" }.All(states.Contains), "Estados: " + string.Join(", ", states));
        Check(controller.parameters.Any(p => p.name == "Speed") && controller.parameters.Any(p => p.name == "Hit" && p.type == AnimatorControllerParameterType.Trigger),
            "Parametros Speed e Hit");

        // ---- deformation across all clips
        DeformationAudit(instance, lod0, avatar);

        // ---- accessories follow the sockets during motion
        var fire = LoadClip("Fire");
        var draw = LoadClip("DrawString");
        foreach (var (clip, t) in new[] { (draw, 0f), (draw, 1.4f), (fire, 0.4f), (fire, 1.2f) })
        {
            clip.SampleAnimation(instance, t);
            float bowDistance = Vector3.Distance(bow.position, leftHand.position);
            float quiverDistance = Vector3.Distance(quiver.position, spine.position);
            Check(bowDistance < 0.30f && quiverDistance < 0.80f, $"Soquetes em {clip.name}@{t:F1}s: arco-mao {bowDistance:F2} m, aljava-tronco {quiverDistance:F2} m");
        }

        // ---- renders: LODs and poses
        RenderLods(instance, group);
        RenderPoses(instance, animator, bow, held, leftHand, rightHand);

        // ---- wrapper + catalog
        WrapperAudit(wrapper);

        UnityEngine.Object.DestroyImmediate(instance);
    }

    static AnimationClip LoadClip(string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(ArcherSetup.Root + "/Models/Archer@" + name + ".fbx")
            .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
    }

    static float Percentile(List<float> sorted, float p)
    {
        return sorted[Mathf.Clamp(Mathf.RoundToInt((sorted.Count - 1) * p), 0, sorted.Count - 1)];
    }

    static void DeformationAudit(GameObject instance, SkinnedMeshRenderer lod0, Avatar avatar)
    {
        // reference = the renderer baked in bind pose (same space as every later bake)
        var baked = new Mesh();
        lod0.BakeMesh(baked, true);
        var refVertices = baked.vertices;
        var indices = baked.triangles;
        var sourceVertices = lod0.sharedMesh.vertices;
        float spaceError = 0f;
        for (int i = 0; i < refVertices.Length; i += 11) spaceError = Mathf.Max(spaceError, Vector3.Distance(refVertices[i], sourceVertices[i]));
        Info($"Controle: bake em bind vs sharedMesh, diferenca max {spaceError:F4} m");
        var edges = new List<(int a, int b, float len)>();
        for (int i = 0; i < indices.Length; i += 3)
            for (int k = 0; k < 3; k++)
            {
                int a = indices[i + k], b = indices[i + (k + 1) % 3];
                float len = Vector3.Distance(refVertices[a], refVertices[b]);
                if (len > 0.004f && a < b) edges.Add((a, b, len));
            }
        Info("Arestas medidas: " + edges.Count);
        foreach (var name in new[] { "Idle", "Walk", "Run", "Aim", "DrawString", "Fire" })
        {
            var clip = LoadClip(name);
            var ratios = new List<float>();
            float maxMove = 0f, minY = 99f, maxY = -99f;
            bool finite = true;
            for (int k = 0; k <= 8; k++)
            {
                clip.SampleAnimation(instance, clip.length * k / 8f);
                lod0.BakeMesh(baked, true);
                baked.RecalculateBounds();
                var v = baked.vertices;
                foreach (var p in v) if (!(float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z))) finite = false;
                foreach (var e in edges) ratios.Add(Vector3.Distance(v[e.a], v[e.b]) / e.len);
                for (int i = 0; i < v.Length; i += 7) maxMove = Mathf.Max(maxMove, Vector3.Distance(v[i], refVertices[i]));
                // height in world space (BakeMesh returns renderer-local vertices; the renderer may be rotated by the FBX axis conversion)
                var toWorld = Matrix4x4.TRS(lod0.transform.position, lod0.transform.rotation, Vector3.one);
                float lo = 99f, hi = -99f;
                for (int i = 0; i < v.Length; i += 3) { float y = toWorld.MultiplyPoint3x4(v[i]).y; lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y); }
                minY = Mathf.Min(minY, lo); maxY = Mathf.Max(maxY, hi);
            }
            ratios.Sort();
            float p001 = Percentile(ratios, 0.001f), p01 = Percentile(ratios, 0.01f), p99 = Percentile(ratios, 0.99f), p999 = Percentile(ratios, 0.999f);
            float over3 = ratios.Count(r => r > 3f) / (float)ratios.Count;
            bool ok = finite && p99 < 2.2f && p999 < 4.0f && p01 > 0.4f && over3 < 0.003f && maxY - minY > 1.4f && maxY - minY < 2.4f;
            Check(ok, $"Deformacao {name}: arestas p1%={p01:F2} p99%={p99:F2} p99.9%={p999:F2} max={ratios[ratios.Count - 1]:F2} (>x3: {over3:P3}), deslocamento max {maxMove:F2} m, altura {maxY - minY:F2} m, finito={finite}");
        }
        UnityEngine.Object.DestroyImmediate(baked);
        LoadClip("Idle").SampleAnimation(instance, 0f);
    }

    static Camera MakeCamera(out Light light, out RenderTexture target)
    {
        var cameraObject = new GameObject("AuditCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.34f, 0.36f, 0.39f);
        camera.fieldOfView = 30f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 200f;
        var lightObject = new GameObject("AuditLight");
        lightObject.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
        light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
        target = new RenderTexture(480, 640, 24);
        camera.targetTexture = target;
        return camera;
    }

    static (int foreground, int magenta) Capture(Camera camera, RenderTexture target, string path)
    {
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;
        File.WriteAllBytes(path, texture.EncodeToPNG());
        var pixels = texture.GetPixels32();
        var bg = camera.backgroundColor;
        int magenta = pixels.Count(p => p.r > 220 && p.g < 40 && p.b > 220);
        int foreground = pixels.Count(p => Mathf.Abs(p.r / 255f - bg.r) > 0.04f || Mathf.Abs(p.g / 255f - bg.g) > 0.04f || Mathf.Abs(p.b / 255f - bg.b) > 0.04f);
        UnityEngine.Object.DestroyImmediate(texture);
        return (foreground, magenta);
    }

    static void RenderLods(GameObject instance, LODGroup group)
    {
        var camera = MakeCamera(out var light, out var target);
        Directory.CreateDirectory(OutDir);
        try
        {
            var lods = group.GetLODs();
            Info("LOD screen heights: " + string.Join(", ", lods.Select(l => l.screenRelativeTransitionHeight.ToString("F3"))));
            Check(lods.Length == 3 && lods[0].screenRelativeTransitionHeight > lods[1].screenRelativeTransitionHeight && lods[1].screenRelativeTransitionHeight > lods[2].screenRelativeTransitionHeight,
                "LODs com limiares decrescentes");
            LoadClip("Idle").SampleAnimation(instance, 0f);
            for (int lod = 0; lod < 3; lod++)
            {
                group.ForceLOD(lod);
                foreach (var side in new[] { "front", "back" })
                {
                    camera.transform.position = new Vector3(0f, 0.95f, side == "front" ? 5.2f : -5.2f);
                    camera.transform.rotation = Quaternion.Euler(0f, side == "front" ? 180f : 0f, 0f);
                    var (fg, pink) = Capture(camera, target, OutDir + $"Unity-LOD{lod}-{side}.png");
                    Check(fg > 6000 && pink < 40, $"LOD{lod} {side}: renderiza (primeiro plano {fg}px, magenta {pink}px)");
                }
            }
            group.ForceLOD(-1);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            UnityEngine.Object.DestroyImmediate(light.gameObject);
            target.Release();
        }
    }

    static void RenderPoses(GameObject instance, Animator animator, Transform bow, Transform held, Transform leftHand, Transform rightHand)
    {
        var camera = MakeCamera(out var light, out var target);
        var bowString = bow.GetComponentInChildren<ArcherBowString>(true);
        var line = bowString.GetComponent<LineRenderer>();
        var group = instance.GetComponentInChildren<LODGroup>();
        group.ForceLOD(0);
        var bowRestLocal = bow.localRotation;
        var shots = new (string clip, float t, bool arrow, bool pulled)[]
        {
            ("Idle", 1f, false, false), ("Walk", 0.3f, false, false), ("Run", 0.2f, false, false), ("Aim", 1f, true, true),
            ("DrawString", 0.45f, true, false), ("DrawString", 1.3f, true, true), ("DrawString", 2.4f, true, true),
            ("Fire", 0.1f, true, true), ("Fire", 0.55f, false, false), ("Fire", 1.1f, false, false),
        };
        try
        {
            foreach (var shot in shots)
            {
                LoadClip(shot.clip).SampleAnimation(instance, shot.t);
                held.gameObject.SetActive(shot.arrow);
                bow.localRotation = bowRestLocal;
                if (shot.clip == "DrawString" || shot.clip == "Aim" || shot.clip == "Fire")
                    bow.rotation = Quaternion.LookRotation(instance.transform.forward, Vector3.up); // igual ao ArcherCombatController durante o ataque
                if (shot.arrow)
                {
                    Vector3 forward = shot.pulled ? (leftHand.position - rightHand.position).normalized : (instance.transform.forward + Vector3.up * 0.8f).normalized;
                    held.SetPositionAndRotation(rightHand.position, Quaternion.LookRotation(forward));
                }
                var tipTop = bowString.tipTop.position;
                var tipBottom = bowString.tipBottom.position;
                var middle = shot.pulled ? rightHand.position : (tipTop + tipBottom) * 0.5f;
                line.positionCount = 3;
                line.SetPositions(new[] { tipTop, middle, tipBottom });
                if (shot.clip == "Aim" || (shot.clip == "DrawString" && shot.t > 2f) || shot.clip == "Run")
                {
                    camera.transform.position = new Vector3(-1.5f, 1.55f, 2.1f);
                    camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.35f, 0f) - camera.transform.position);
                    var (cfg, cpink) = Capture(camera, target, OutDir + $"Unity-closeup-{shot.clip}.png");
                    Check(cfg > 6000 && cpink < 40, $"Close-up {shot.clip}: renderiza (fg {cfg}px, magenta {cpink}px)");
                }
                foreach (var side in new[] { "front", "side" })
                {
                    if (side == "front")
                    {
                        camera.transform.position = new Vector3(0f, 1.05f, 5.4f);
                        camera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    }
                    else
                    {
                        camera.transform.position = new Vector3(-5.4f, 1.05f, 0f);
                        camera.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    }
                    string name = $"Unity-pose-{shot.clip}-{(int)(shot.t * 100):D3}-{side}.png";
                    var (fg, pink) = Capture(camera, target, OutDir + name);
                    Check(fg > 6000 && pink < 40, $"Pose {shot.clip}@{shot.t:F2}s {side}: renderiza (fg {fg}px, magenta {pink}px)");
                }
            }
            held.gameObject.SetActive(false);
            bow.localRotation = bowRestLocal;
        }
        finally
        {
            group.ForceLOD(-1);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            UnityEngine.Object.DestroyImmediate(light.gameObject);
            target.Release();
        }
    }

    static void WrapperAudit(GameObject wrapper)
    {
        Check(wrapper != null, "Archer.prefab (wrapper da unidade) existe");
        if (wrapper == null) return;
        Check(wrapper.tag == "Unit" && wrapper.layer == 12, "Wrapper com tag Unit e layer 12");
        foreach (var component in new[] { "CharacterManager", "CharacterBT", "FogRendererToggler", "ArcherCombatController" })
            Check(wrapper.GetComponent(component) != null, "Wrapper tem " + component);
        Check(wrapper.GetComponent<UnityEngine.AI.NavMeshAgent>() != null && wrapper.GetComponent<BoxCollider>() != null && wrapper.GetComponent<AudioSource>() != null,
            "Wrapper tem NavMeshAgent, BoxCollider e AudioSource");
        Check(wrapper.transform.Find("Mesh/MinimapIcon") != null, "Caminho Mesh/MinimapIcon (exigido por Unit) existe");
        var manager = (MonoBehaviour)wrapper.GetComponent("CharacterManager");
        var so = new SerializedObject(manager);
        Check(so.FindProperty("meshRenderer").objectReferenceValue != null, "CharacterManager.meshRenderer atribuido (anel do time)");
        Check(so.FindProperty("animator").objectReferenceValue != null, "CharacterManager.animator atribuido");
        Check(so.FindProperty("selectionCircle").objectReferenceValue != null && so.FindProperty("fov").objectReferenceValue != null &&
              so.FindProperty("contextualSource").objectReferenceValue != null, "selectionCircle, fov e contextualSource atribuidos");
        var combat = wrapper.GetComponent<ArcherCombatController>();
        Check(combat.animator != null && combat.bow != null && combat.heldArrow != null && combat.bowString != null &&
              combat.arrowProjectilePrefab != null && combat.profile != null, "ArcherCombatController com todas as referencias");
        var fog = (MonoBehaviour)wrapper.GetComponent("FogRendererToggler");
        Check(new SerializedObject(fog).FindProperty("mesh").objectReferenceValue != null, "FogRendererToggler.mesh atribuido");

        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(ArcherSetup.CharacterDataPath);
        Check(data != null && data.code == "archer" && data.prefab == wrapper, "CharacterData 'archer' referencia o wrapper");
        Check(data != null && data.attackRange >= 8f && data.attackDamage > 0 && data.attackRate > 0, "CharacterData com ataque a distancia (alcance " + data?.attackRange + ")");
        var catalog = Resources.LoadAll<CharacterData>("ScriptableObjects/Units/Characters");
        Check(catalog.Any(c => c.code == "archer"), "Catalogo Resources carrega 'archer' (" + catalog.Length + " personagens)");
        var codes = catalog.Select(c => c.code).ToList();
        Check(codes.Count == codes.Distinct().Count(), "Codigos de personagem unicos: " + string.Join(", ", codes));
    }
}
