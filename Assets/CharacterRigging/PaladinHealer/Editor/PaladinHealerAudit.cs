using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Paladin;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Auditoria em Edit Mode do Paladino curador: licenca/origem documentada, avatar humanoide, malhas/pesos/LODs,
/// materiais (sem shader com erro), espada e escudo nos soquetes, controller, deformacao dos 7 clipes (BakeMesh),
/// prefab de morte, efeito de cura (diametro 1 em XZ), wrapper jogavel, contrato x assets e progressao nas 4 eras.
/// Gera capturas (frente/costas/lado/poses/cura) sem pixels magenta.
/// Batch: -executeMethod PaladinHealerAudit.Run (sai com 0 se tudo passar). Menu: Tools > Character Rigging > Paladin Healer > Audit.
/// </summary>
public static class PaladinHealerAudit
{
    const string OutDir = "Docs/CharacterMeshAudit/PaladinHealer/";
    static readonly List<string> Lines = new List<string>();
    static int failures;

    static void Check(bool ok, string message)
    {
        Lines.Add((ok ? "PASS " : "FAIL ") + message);
        if (!ok) failures++;
    }

    static void Info(string message) { Lines.Add("INFO " + message); }

    [MenuItem("Tools/Character Rigging/Paladin Healer/Audit")]
    public static void RunFromMenu() { Run(); }

    public static void Run()
    {
        Lines.Clear();
        failures = 0;
        try { Execute(); }
        catch (Exception e) { Lines.Add("EXCEPTION " + e); failures++; }
        Directory.CreateDirectory(OutDir);
        Lines.Add(failures == 0 ? "PALADINHEALER_AUDIT_PASSED" : "PALADINHEALER_AUDIT_FAILED failures=" + failures);
        File.WriteAllText(OutDir + "unity-audit.txt", string.Join("\n", Lines) + "\n");
        Debug.Log(string.Join("\n", Lines));
        if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    static Transform Find(Transform root, string name) { return AdvancedUnitBuilders.FindDeep(root, name); }

    static void Execute()
    {
        // ---- licenca / origem documentada
        string license = File.Exists(OutDir + "LICENSE.md") ? File.ReadAllText(OutDir + "LICENSE.md") : "";
        Check(license.Contains("Meshy") && license.Contains("Mixamo") && license.Contains("proceduralmente"),
            "LICENSE.md documenta origem/licenca (Meshy, Mixamo, pecas procedurais)");
        Check(!license.ToLowerInvariant().Contains("msy_"), "LICENSE.md sem chave de API");

        // ---- avatar / importers
        var avatar = AssetDatabase.LoadAllAssetsAtPath(PaladinHealerSetup.BaseModel).OfType<Avatar>().FirstOrDefault();
        Check(avatar != null && avatar.isValid && avatar.isHuman, "Avatar humanoide valido (PaladinHealer.fbx)");
        foreach (var clipName in PaladinHealerSetup.ClipNames)
        {
            string path = PaladinHealerSetup.ModelsDir + "/PaladinHealer@" + clipName + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var clip = LoadClip(clipName);
            Check(importer != null && importer.animationType == ModelImporterAnimationType.Human && importer.sourceAvatar == avatar,
                $"Clip {clipName}: Humanoid copiando o avatar do PaladinHealer");
            Check(clip != null && clip.isHumanMotion && clip.length > 0.3f, $"Clip {clipName}: importado, humanoide, {clip?.length:F2}s");
        }
        Check(LoadClip("Idle").isLooping && LoadClip("Walk").isLooping && LoadClip("Run").isLooping, "Idle/Walk/Run em loop");
        Check(LoadClip("Heal").length > 0.9f && LoadClip("Heal").length < 1.6f, $"Heal dura {LoadClip("Heal").length:F2}s (1,0-1,5 s)");

        // ---- prefab visual
        var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PaladinHealerSetup.VisualPrefabPath);
        Check(visualPrefab != null, "PaladinHealerVisual.prefab existe");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab);
        var animator = instance.GetComponent<Animator>();
        Check(animator != null && animator.avatar == avatar && animator.runtimeAnimatorController != null && !animator.applyRootMotion,
            "Animator com avatar, controller e root motion desligado");

        var group = instance.GetComponentInChildren<LODGroup>();
        Check(group != null && group.lodCount == 3, "LODGroup com 3 niveis");
        var smrs = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(r => r.name).ToArray();
        Check(smrs.Length == 3, "3 SkinnedMeshRenderer (LOD0/1/2): " + string.Join(", ", smrs.Select(r => r.name)));
        var lod0 = smrs.First(r => r.name.EndsWith("LOD0"));
        long[] tris = smrs.Select(r => r.sharedMesh.triangles.Length / 3L).ToArray();
        Info("Triangulos LOD0/1/2: " + string.Join(" / ", tris));
        Check(tris[0] > 25000 && tris[0] <= 31000 && tris[1] < tris[0] && tris[2] < tris[1], "Triangulos estritamente decrescentes por LOD, LOD0 <= 31 k");
        foreach (var smr in smrs)
        {
            var mesh = smr.sharedMesh;
            var weights = mesh.boneWeights;
            int bad = weights.Count(w => Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1f) > 0.02f);
            int maxInfluences = weights.Max(w => (w.weight0 > 0 ? 1 : 0) + (w.weight1 > 0 ? 1 : 0) + (w.weight2 > 0 ? 1 : 0) + (w.weight3 > 0 ? 1 : 0));
            Check(smr.bones.Length >= 22 && smr.rootBone != null && bad == 0 && maxInfluences <= 4,
                $"{smr.name}: {smr.bones.Length} ossos, rootBone ok, pesos normalizados (vertices fora = {bad}), <= 4 influencias ({maxInfluences})");
            Check(smr.sharedMaterials.Length == 1 && smr.sharedMaterial != null && smr.sharedMaterial.name == "PaladinHealer_Mat", $"{smr.name}: material PaladinHealer_Mat");
            Check(mesh.uv.Length == mesh.vertexCount, $"{smr.name}: UV presentes");
        }
        var body = lod0.sharedMaterial;
        Check(body.mainTexture != null && body.GetTexture("_BumpMap") != null && body.GetTexture("_MetallicGlossMap") != null,
            "PaladinHealer_Mat com base color, normal map e mapa metalico/suavidade");

        // ---- materiais de todos os prefabs
        var death = AssetDatabase.LoadAssetAtPath<GameObject>(PaladinHealerSetup.DeathPrefabPath);
        var effect = AssetDatabase.LoadAssetAtPath<GameObject>(PaladinHealerSetup.EffectPrefabPath);
        var wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(PaladinHealerSetup.WrapperPrefabPath);
        foreach (var root in new[] { visualPrefab, death, effect, wrapper })
        {
            Check(root != null, "Prefab existe: " + (root != null ? root.name : "?"));
            if (root == null) continue;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    bool ok = material != null && material.shader != null && material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader) && material.shader.name != "Hidden/InternalErrorShader";
                    Check(ok, $"Material valido: {root.name}/{renderer.name} -> {(material != null ? material.name + " (" + material.shader.name + ")" : "NULL")}");
                }
        }

        // ---- espada e escudo
        var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        var leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        var weaponMount = Find(instance.transform, "WeaponMount");
        var shieldMount = Find(instance.transform, "ShieldMount");
        var center = Find(instance.transform, "HealAuraCenter");
        var sword = Find(instance.transform, "Sword");
        var shield = Find(instance.transform, "Shield");
        Check(weaponMount != null && weaponMount.parent == rightHand && sword != null && sword.parent == weaponMount, "Espada: objeto separado, filho de WeaponMount (mao direita)");
        Check(shieldMount != null && shieldMount.parent == leftLowerArm && shield != null && shield.parent == shieldMount, "Escudo: objeto separado, filho de ShieldMount (antebraco esquerdo)");
        Check(center != null && center.parent == instance.transform && center.localPosition.y > 1.0f && center.localPosition.y < 1.5f, $"HealAuraCenter na altura do peito (y = {center?.localPosition.y:F2})");
        Check(!lod0.GetComponentsInChildren<Transform>().Any(t => t.name == "Sword" || t.name == "Shield"), "Pecas nao fazem parte da malha do corpo");
        var swordMaterials = sword.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Select(m => m.name).Distinct().ToArray();
        var shieldMaterials = shield.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Select(m => m.name).Distinct().ToArray();
        Check(new[] { "PaladinHealer_Steel", "PaladinHealer_Gold", "PaladinHealer_Leather" }.All(swordMaterials.Contains), "Espada: materiais aco, ouro e couro (" + string.Join(", ", swordMaterials) + ")");
        Check(new[] { "PaladinHealer_Steel", "PaladinHealer_Gold", "PaladinHealer_Leather", "PaladinHealer_Wood", "PaladinHealer_Linen" }.All(shieldMaterials.Contains), "Escudo: materiais aco, ouro, couro, madeira e linho (" + string.Join(", ", shieldMaterials) + ")");
        int swordTris = sword.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3);
        int shieldTris = shield.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3);
        Info($"Triangulos espada/escudo: {swordTris} / {shieldTris}");
        Check(swordTris <= 3000 && shieldTris <= 8000, "Pecas dentro do orcamento (espada <= 3 k, escudo <= 8 k)");
        var swordBounds = Bounds(sword);
        var shieldBounds = Bounds(shield);
        Check(swordBounds.size.magnitude > 0.9f && swordBounds.size.magnitude < 1.4f, $"Espada com tamanho plausivel (diagonal {swordBounds.size.magnitude:F2} m)");
        Check(shieldBounds.size.y > 0.6f && shieldBounds.size.y < 1.0f, $"Escudo com tamanho plausivel (altura {shieldBounds.size.y:F2} m)");

        // ---- controller
        var controller = (AnimatorController)animator.runtimeAnimatorController;
        var states = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
        Check(new[] { "Locomotion", "Attack", "HitReact" }.All(states.Contains), "Estados da camada base: " + string.Join(", ", states));
        Check(controller.layers.Length == 2 && controller.layers[1].avatarMask != null && controller.layers[1].stateMachine.states.Any(s => s.state.name == "Heal"),
            "Camada 'Heal' com mascara (tronco, bracos, cabeca)");
        var mask = controller.layers[1].avatarMask;
        Check(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm) && mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Body) &&
              !mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg) && !mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg), "Mascara: tronco/bracos ativos, pernas desligadas");
        string[] expectedParameters = { "Running", "Attacking", "Hit", "Speed", "Fire", "Damaged", "Heal" };
        Check(expectedParameters.All(p => controller.parameters.Any(q => q.name == p)), "Parametros padrao do Animator (" + string.Join(", ", expectedParameters) + ")");
        Check(controller.parameters.First(p => p.name == "Hit").type == AnimatorControllerParameterType.Trigger &&
              controller.parameters.First(p => p.name == "Running").type == AnimatorControllerParameterType.Bool &&
              controller.parameters.First(p => p.name == "Attacking").type == AnimatorControllerParameterType.Bool, "Hit=trigger, Running/Attacking=bool (como o behavior tree escreve)");
        var attackTransitions = controller.layers[0].stateMachine.states.First(s => s.state.name == "Locomotion").state.transitions;
        Check(attackTransitions.Any(t => t.destinationState != null && t.destinationState.name == "Attack" && t.conditions.Any(c => c.parameter == "Hit")), "Hit (BT) dispara o estado Attack");
        Check(controller.layers[0].stateMachine.anyStateTransitions.Any(t => t.destinationState.name == "HitReact" && t.conditions.Any(c => c.parameter == "Damaged")), "Damaged dispara HitReact");

        // ---- deformacao em todos os clipes
        DeformationAudit(instance, lod0);

        // ---- pecas acompanham os soquetes durante o movimento
        foreach (var (clipName, t) in new[] { ("Idle", 0f), ("Idle", 1.3f), ("Attack", 0.4f), ("Attack", 0.8f), ("Attack", 1.2f), ("Heal", 0.6f), ("Hit", 0.4f), ("Run", 0.3f) })
        {
            var clip = LoadClip(clipName);
            clip.SampleAnimation(instance, t);
            float swordHand = Vector3.Distance(weaponMount.position, rightHand.position);
            float shieldArm = Vector3.Distance(shieldMount.position, leftLowerArm.position);
            Check(swordHand < 0.2f && shieldArm < 0.45f, $"Pecas em {clipName}@{t:F1}s: empunhadura-mao {swordHand:F2} m, escudo-antebraco {shieldArm:F2} m");
        }
        var leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        LoadClip("Idle").SampleAnimation(instance, 0f);
        Check(Vector3.Dot(shield.TransformDirection(Vector3.forward), instance.transform.forward) > 0.6f || Vector3.Dot(-shield.TransformDirection(Vector3.forward), instance.transform.forward) > 0.6f,
            "Escudo em Idle voltado para a frente do personagem");
        Check(Vector3.Distance(shield.position, leftHandBone.position) < 0.7f, "Escudo em Idle perto da mao esquerda");

        // ---- renders
        RenderLods(instance, group);
        RenderPoses(instance, group);
        RenderEffect(instance, effect);

        // ---- prefab de morte
        DeathAudit(death);

        // ---- wrapper + contrato + catalogo
        WrapperAudit(wrapper, effect, death);
        ContractAudit(wrapper);

        UnityEngine.Object.DestroyImmediate(instance);
    }

    static Bounds Bounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    static AnimationClip LoadClip(string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(PaladinHealerSetup.ModelsDir + "/PaladinHealer@" + name + ".fbx")
            .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
    }

    static float Percentile(List<float> sorted, float p)
    {
        return sorted[Mathf.Clamp(Mathf.RoundToInt((sorted.Count - 1) * p), 0, sorted.Count - 1)];
    }

    static void DeformationAudit(GameObject instance, SkinnedMeshRenderer lod0)
    {
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
        foreach (var name in PaladinHealerSetup.ClipNames)
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
                var toWorld = Matrix4x4.TRS(lod0.transform.position, lod0.transform.rotation, Vector3.one);
                float lo = 99f, hi = -99f;
                for (int i = 0; i < v.Length; i += 3) { float y = toWorld.MultiplyPoint3x4(v[i]).y; lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y); }
                minY = Mathf.Min(minY, lo); maxY = Mathf.Max(maxY, hi);
            }
            ratios.Sort();
            float p01 = Percentile(ratios, 0.01f), p99 = Percentile(ratios, 0.99f), p999 = Percentile(ratios, 0.999f);
            float over3 = ratios.Count(r => r > 3f) / (float)ratios.Count;
            bool lying = name == "Death";
            bool heightOk = lying ? maxY - minY > 0.3f && maxY - minY < 2.6f : maxY - minY > 1.4f && maxY - minY < 2.4f;
            bool ok = finite && p99 < 2.2f && p999 < 4.0f && p01 > 0.4f && over3 < 0.003f && heightOk;
            Check(ok, $"Deformacao {name}: arestas p1%={p01:F2} p99%={p99:F2} p99.9%={p999:F2} max={ratios[ratios.Count - 1]:F2} (>x3: {over3:P3}), deslocamento max {maxMove:F2} m, altura {maxY - minY:F2} m, finito={finite}");
        }
        UnityEngine.Object.DestroyImmediate(baked);
        LoadClip("Idle").SampleAnimation(instance, 0f);
    }

    // ------------------------------------------------------------------------------------------- renders
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

    /// <summary>Cola PNGs lado a lado numa folha de contato (reduzida a 1/2).</summary>
    static void Sheet(string name, IList<string> files)
    {
        const int w = 240, h = 320;
        int cols = Mathf.Min(files.Count, 6), rows = (files.Count + cols - 1) / cols;
        var sheet = new Texture2D(cols * w, rows * h, TextureFormat.RGB24, false);
        for (int i = 0; i < files.Count; i++)
        {
            var tile = new Texture2D(2, 2);
            tile.LoadImage(File.ReadAllBytes(files[i]));
            var small = new Texture2D(w, h, TextureFormat.RGB24, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    small.SetPixel(x, y, tile.GetPixelBilinear(x / (float)w, y / (float)h));
            sheet.SetPixels((i % cols) * w, (rows - 1 - i / cols) * h, w, h, small.GetPixels());
            UnityEngine.Object.DestroyImmediate(tile);
            UnityEngine.Object.DestroyImmediate(small);
        }
        sheet.Apply();
        File.WriteAllBytes(OutDir + name, sheet.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(sheet);
    }

    static void RenderLods(GameObject instance, LODGroup group)
    {
        var camera = MakeCamera(out var light, out var target);
        Directory.CreateDirectory(OutDir);
        var files = new List<string>();
        try
        {
            var lods = group.GetLODs();
            Info("LOD screen heights: " + string.Join(", ", lods.Select(l => l.screenRelativeTransitionHeight.ToString("F3"))));
            Check(lods.Length == 3 && lods[0].screenRelativeTransitionHeight > lods[1].screenRelativeTransitionHeight && lods[1].screenRelativeTransitionHeight > lods[2].screenRelativeTransitionHeight,
                "LODs com limiares decrescentes");
            LoadClip("Idle").SampleAnimation(instance, 0f);
            // pose de ligacao em T/A para mostrar frente/costas/lado com o personagem de frente para a camera
            for (int lod = 0; lod < 3; lod++)
            {
                group.ForceLOD(lod);
                foreach (var side in new[] { "front", "back", "side" })
                {
                    Vector3 pos = side == "front" ? new Vector3(0f, 0.95f, 5.2f) : side == "back" ? new Vector3(0f, 0.95f, -5.2f) : new Vector3(-5.2f, 0.95f, 0f);
                    camera.transform.position = pos;
                    camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.95f, 0f) - pos);
                    string path = OutDir + $"Unity-LOD{lod}-{side}.png";
                    var (fg, pink) = Capture(camera, target, path);
                    files.Add(path);
                    Check(fg > 6000 && pink < 40, $"LOD{lod} {side}: renderiza (primeiro plano {fg}px, magenta {pink}px)");
                }
            }
            group.ForceLOD(-1);
            Sheet("sheet_unity_lods.png", files);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            UnityEngine.Object.DestroyImmediate(light.gameObject);
            target.Release();
        }
    }

    static void RenderPoses(GameObject instance, LODGroup group)
    {
        var camera = MakeCamera(out var light, out var target);
        group.ForceLOD(0);
        var shots = new (string clip, float t)[]
        {
            ("Idle", 0.3f), ("Walk", 0.3f), ("Run", 0.2f), ("Attack", 0.35f), ("Attack", 0.7f), ("Attack", 1.0f),
            ("Heal", 0.15f), ("Heal", 0.6f), ("Heal", 1.1f), ("Hit", 0.3f), ("Death", 1.0f), ("Death", 5.7f),
        };
        var fronts = new List<string>();
        try
        {
            foreach (var shot in shots)
            {
                LoadClip(shot.clip).SampleAnimation(instance, shot.t);
                bool lying = shot.clip == "Death" && shot.t > 3f;
                foreach (var side in new[] { "front", "side" })
                {
                    Vector3 target3 = new Vector3(0f, lying ? 0.4f : 1.0f, 0f);
                    Vector3 pos = side == "front" ? new Vector3(0f, lying ? 3.5f : 1.05f, 5.4f) : new Vector3(-5.4f, lying ? 3.5f : 1.05f, 0f);
                    camera.transform.position = pos;
                    camera.transform.rotation = Quaternion.LookRotation(target3 - pos);
                    string path = OutDir + $"Unity-pose-{shot.clip}-{(int)(shot.t * 100):D3}-{side}.png";
                    var (fg, pink) = Capture(camera, target, path);
                    if (side == "front") fronts.Add(path);
                    Check(fg > 5000 && pink < 40, $"Pose {shot.clip}@{shot.t:F2}s {side}: renderiza (fg {fg}px, magenta {pink}px)");
                }
            }
            Sheet("sheet_unity_poses.png", fronts);
            // close-up da espada e do escudo em Idle
            LoadClip("Idle").SampleAnimation(instance, 0.3f);
            camera.transform.position = new Vector3(-1.6f, 1.45f, 2.3f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.0f, 0f) - camera.transform.position);
            var (cfg, cpink) = Capture(camera, target, OutDir + "Unity-closeup-Idle.png");
            Check(cfg > 6000 && cpink < 40, $"Close-up Idle: renderiza (fg {cfg}px, magenta {cpink}px)");
            LoadClip("Idle").SampleAnimation(instance, 0f);
        }
        finally
        {
            group.ForceLOD(-1);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            UnityEngine.Object.DestroyImmediate(light.gameObject);
            target.Release();
        }
    }

    static void RenderEffect(GameObject visual, GameObject effectPrefab)
    {
        var camera = MakeCamera(out var light, out var target);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ground.name = "AuditGround";
        ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        ground.transform.localScale = new Vector3(30f, 30f, 1f);
        var groundMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.25f, 0.33f, 0.2f) };
        ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
        var group = visual.GetComponentInChildren<LODGroup>();
        group.ForceLOD(0);
        LoadClip("Heal").SampleAnimation(visual, 0.6f);
        float radius = 4f;
        var fx = (GameObject)PrefabUtility.InstantiatePrefab(effectPrefab);
        fx.transform.position = new Vector3(0f, PaladinHealerSetup.HealAuraHeight, 0f);
        fx.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
        var aura = fx.GetComponent<PaladinHealAuraEffect>();
        var ps = fx.GetComponentInChildren<ParticleSystem>();
        var files = new List<string>();
        try
        {
            camera.fieldOfView = 40f;
            foreach (float t in new[] { 0.12f, 0.35f, 0.7f, 1.05f })
            {
                aura.Evaluate(t);
                ps.Simulate(t, true, true, true);
                camera.transform.position = new Vector3(0f, 8.5f, -9.5f);
                camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.8f, 0f) - camera.transform.position);
                string path = OutDir + $"Unity-heal-{(int)(t * 100):D3}.png";
                var (fg, pink) = Capture(camera, target, path);
                files.Add(path);
                Check(fg > 8000 && pink < 40, $"Efeito de cura @{t:F2}s: renderiza (fg {fg}px, magenta {pink}px)");
            }
            Sheet("sheet_unity_heal.png", files);
        }
        finally
        {
            group.ForceLOD(-1);
            UnityEngine.Object.DestroyImmediate(fx);
            UnityEngine.Object.DestroyImmediate(ground);
            UnityEngine.Object.DestroyImmediate(groundMaterial);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            UnityEngine.Object.DestroyImmediate(light.gameObject);
            target.Release();
            LoadClip("Idle").SampleAnimation(visual, 0f);
        }
    }

    // ----------------------------------------------------------------------------------- prefabs / contrato
    static void DeathAudit(GameObject death)
    {
        var animator = death.GetComponent<Animator>();
        Check(animator != null && animator.runtimeAnimatorController != null && animator.runtimeAnimatorController.name == "PaladinHealerDeath",
            "Prefab de morte com Animator do controller PaladinHealerDeath");
        var controller = (AnimatorController)animator.runtimeAnimatorController;
        var states = controller.layers[0].stateMachine.states;
        Check(states.Length == 1 && states[0].state.name == "Death" && controller.layers[0].stateMachine.defaultState == states[0].state && states[0].state.motion == LoadClip("Death"),
            "Controller de morte: unico estado Death (padrao) tocando o clipe Death sozinho");
        Check(!LoadClip("Death").isLooping, "Death nao repete (fica no ultimo quadro)");
        Check(death.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Sword") && death.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Shield"),
            "Prefab de morte inclui espada e escudo");
    }

    static void WrapperAudit(GameObject wrapper, GameObject effect, GameObject death)
    {
        Check(wrapper != null, "PaladinHealer.prefab (wrapper da unidade) existe");
        if (wrapper == null) return;
        Check(wrapper.tag == "Unit" && wrapper.layer == 12, "Wrapper com tag Unit e layer 12");
        foreach (var component in new[] { "CharacterManager", "CharacterBT", "FogRendererToggler", "AdvancedUnitEraBinding", "AdvancedUnitAnimatorDriver", "PaladinHealBehaviour", "AdvancedUnitDeathEffect" })
            Check(wrapper.GetComponent(component) != null, "Wrapper tem " + component);
        var agent = wrapper.GetComponent<UnityEngine.AI.NavMeshAgent>();
        var box = wrapper.GetComponent<BoxCollider>();
        Check(agent != null && box != null && wrapper.GetComponent<AudioSource>() != null, "Wrapper tem NavMeshAgent, BoxCollider e AudioSource");
        Check(Mathf.Abs(agent.speed - PaladinHealerSetup.AgentSpeed) < 0.01f && agent.radius > 0.3f && agent.height >= 1.8f, $"NavMeshAgent ajustado (velocidade {agent.speed}, raio {agent.radius}, altura {agent.height})");
        Check(box.size.y >= 1.9f && box.size.x >= 0.7f && Mathf.Abs(box.center.y - box.size.y / 2f) < 0.15f, $"BoxCollider ajustado (tamanho {box.size}, centro {box.center})");
        Check(wrapper.transform.Find("Mesh/MinimapIcon") != null, "Caminho Mesh/MinimapIcon (exigido por Unit) existe");
        Check(wrapper.transform.Find("Mesh/TeamRing") != null, "Anel de time (TeamRing) presente");
        var ringMesh = wrapper.transform.Find("Mesh/TeamRing").GetComponent<MeshFilter>().sharedMesh;
        var ringTriangles = ringMesh.triangles; var ringVertices = ringMesh.vertices;
        bool ringUp = true;
        for (int i = 0; i < ringTriangles.Length; i += 3)
            ringUp &= Vector3.Cross(ringVertices[ringTriangles[i + 1]] - ringVertices[ringTriangles[i]], ringVertices[ringTriangles[i + 2]] - ringVertices[ringTriangles[i]]).y > 0f;
        Check(ringUp, "Anel de time visivel de cima (todas as faces com normal para cima, raio externo " + ringMesh.bounds.extents.x.ToString("F2") + ")");
        var manager = (MonoBehaviour)wrapper.GetComponent("CharacterManager");
        var so = new SerializedObject(manager);
        var ringRenderer = so.FindProperty("meshRenderer").objectReferenceValue as MeshRenderer;
        Check(ringRenderer != null && ringRenderer.name == "TeamRing", "CharacterManager.meshRenderer = anel do time");
        var animator = wrapper.GetComponentInChildren<Animator>();
        Check(so.FindProperty("animator").objectReferenceValue == animator && animator != null, "CharacterManager.animator = Animator do visual");
        Check(so.FindProperty("selectionCircle").objectReferenceValue != null && so.FindProperty("fov").objectReferenceValue != null &&
              so.FindProperty("contextualSource").objectReferenceValue != null, "selectionCircle, fov e contextualSource atribuidos");
        Check(new SerializedObject(wrapper.GetComponent("FogRendererToggler")).FindProperty("mesh").objectReferenceValue != null, "FogRendererToggler.mesh atribuido");
        var contract = AdvancedUnitBuilders.LoadContract<PaladinHealerContract>("PaladinHealer");
        Check(wrapper.GetComponent<AdvancedUnitEraBinding>().contract == contract, "AdvancedUnitEraBinding.contract = PaladinHealer.asset");
        Check(wrapper.GetComponent<AdvancedUnitAnimatorDriver>().animator == animator, "AdvancedUnitAnimatorDriver.animator = Animator do visual");
        var heal = wrapper.GetComponent<PaladinHealBehaviour>();
        Check(heal.contract == contract && heal.animator == animator && heal.effectOrigin != null && heal.effectOrigin.name == "HealAuraCenter" &&
              heal.healEffectPrefab == effect, "PaladinHealBehaviour: contrato, animator, HealAuraCenter e prefab do efeito");
        Check(wrapper.GetComponent<AdvancedUnitDeathEffect>().deathPrefab == death, "AdvancedUnitDeathEffect.deathPrefab = PaladinHealerDeath");
        Check(!wrapper.GetComponents<MonoBehaviour>().Any(m => m is IAttackHandler), "Ataque corpo a corpo segue o behavior tree padrao (sem IAttackHandler)");

        // efeito de cura: diametro 1 em XZ
        var fxRenderers = effect.GetComponentsInChildren<MeshRenderer>(true);
        var fxBounds = fxRenderers[0].bounds;
        foreach (var r in fxRenderers) fxBounds.Encapsulate(r.bounds);
        Check(fxBounds.size.x > 0.95f && fxBounds.size.x <= 1.001f && fxBounds.size.z > 0.95f && fxBounds.size.z <= 1.001f, $"Efeito de cura autorado com 1 unidade de diametro em XZ (tamanho {fxBounds.size.x:F3} x {fxBounds.size.z:F3})");
        Check(effect.transform.localScale == Vector3.one && effect.GetComponentInChildren<ParticleSystem>() != null && effect.GetComponent<PaladinHealAuraEffect>() != null,
            "Efeito: raiz em escala 1, ParticleSystem de particulas ascendentes e PaladinHealAuraEffect (aneis expandindo)");
        var ps = effect.GetComponentInChildren<ParticleSystem>();
        Check(ps.main.scalingMode == ParticleSystemScalingMode.Shape && ps.velocityOverLifetime.enabled && ps.velocityOverLifetime.y.constantMax > 0f, "Particulas: sobem e a escala do efeito so alarga a area de emissao");
        foreach (var name in new[] { "PaladinHealAura_Ring", "PaladinHealAura_Disc", "PaladinHealAura_Mote" })
            Check(AssetDatabase.LoadAssetAtPath<Texture2D>(PaladinHealerSetup.TexturesDir + "/" + name + ".png") != null, "Textura procedural " + name + ".png");
    }

    static void ContractAudit(GameObject wrapper)
    {
        var contract = AdvancedUnitBuilders.LoadContract<PaladinHealerContract>("PaladinHealer");
        Check(contract.IsGameplayValid, "Contrato valido: " + contract.Validate());
        Check(contract.IsVisualReady, "Contrato com visual pronto: " + contract.EvaluateVisuals());
        Check(contract.visual.prefab.asset == wrapper && contract.visual.prefab.expectedAssetPath == PaladinHealerSetup.WrapperPrefabPath, "contract.visual.prefab = wrapper jogavel");
        foreach (var socket in contract.visual.sockets)
        {
            bool ok = socket.IsBoundTo(wrapper) && wrapper.transform.Find(socket.expectedTransformPath).name == socket.socketName;
            Check(ok, $"Socket {socket.socketName} -> {socket.expectedTransformPath}");
        }
        var wanted = new[] { "Idle", "Walk", "Run", "Attack", "Heal", "Hit", "Death" };
        Check(contract.visual.animations.Select(a => a.state).SequenceEqual(wanted), "visual.animations: " + string.Join(", ", contract.visual.animations.Select(a => a.state)));
        foreach (var a in contract.visual.animations)
            Check(a.clip != null && a.clip == LoadClip(a.state), $"Animacao {a.state} -> clipe real do PaladinHealer ({a.animatorParameter})");
        Check(contract.visual.effects.Length >= 2 && contract.visual.effects.All(e => e.asset != null && AssetDatabase.GetAssetPath(e.asset) == e.expectedAssetPath), "Efeitos do contrato apontam para assets reais");
        Check(contract.visual.meshes.Select(m => m.role).Intersect(new[] { "malha-fonte do corpo", "espada", "escudo" }).Count() == 3 &&
              contract.visual.meshes.All(m => m.asset != null && AssetDatabase.GetAssetPath(m.asset) == m.expectedAssetPath), "Malhas do contrato (corpo, espada, escudo) apontam para assets reais");
        Check(!contract.buildSettingsApproved, "Build Settings: contrato nao aprovado (nada foi adicionado ao Build Settings)");
        Check(!EditorBuildSettings.scenes.Any(s => s.path.Contains("PaladinHealer")), "Nenhuma cena do PaladinHealer no Build Settings");

        // progressao nas 4 eras
        var tiers = contract.tiers;
        Check(tiers.Select(t => t.era).SequenceEqual(ImperialEras.Ordered), "Quatro eras em ordem");
        Check(Increasing(tiers.Select(t => (float)t.healthpoints)), "Vida cresce: " + string.Join(" < ", tiers.Select(t => t.healthpoints)));
        Check(Increasing(tiers.Select(t => (float)t.attackDamage)), "Dano cresce: " + string.Join(" < ", tiers.Select(t => t.attackDamage)));
        Check(Increasing(tiers.Select(t => (float)t.healAmount)), "Cura cresce: " + string.Join(" < ", tiers.Select(t => t.healAmount)));
        Check(Increasing(tiers.Select(t => t.healRadius)), "Raio de cura cresce: " + string.Join(" < ", tiers.Select(t => t.healRadius)));
        Check(tiers.All(t => t.attackRange <= AdvancedUnitRules.MeleeRangeCeiling), "Ataque corpo a corpo em todas as eras");

        // catalogo
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(PaladinHealerSetup.CharacterDataPath);
        Check(data != null && data.code == "paladin_healer" && data.prefab == wrapper, "CharacterData 'paladin_healer' referencia o wrapper");
        var ancient = tiers[0];
        Check(data != null && data.healthpoints == ancient.healthpoints && data.attackDamage == ancient.attackDamage && Mathf.Approximately(data.attackRange, ancient.attackRange) &&
              Mathf.Approximately(data.attackRate, ancient.attackCooldown), "CharacterData com os valores da era Ancient do contrato");
        var catalog = Resources.LoadAll<CharacterData>("ScriptableObjects/Units/Characters");
        Check(catalog.Any(c => c.code == "paladin_healer"), "Catalogo Resources carrega 'paladin_healer' (" + catalog.Length + " personagens)");
        var codes = catalog.Select(c => c.code).ToList();
        Check(codes.Count == codes.Distinct().Count(), "Codigos de personagem unicos: " + string.Join(", ", codes));
    }

    static bool Increasing(IEnumerable<float> values)
    {
        var list = values.ToList();
        for (int i = 1; i < list.Count; i++) if (!(list[i] > list[i - 1])) return false;
        return true;
    }
}
