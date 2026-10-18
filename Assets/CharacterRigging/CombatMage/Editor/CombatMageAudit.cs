using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Mage;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Auditoria em Edit Mode do Mago de combate: licenca documentada, avatar humanoide, malhas/pesos, UV/materiais sem magenta,
/// LODs decrescentes (renderizados), deformacao dos clipes (BakeMesh), cajado/CastPoint nos soquetes, controller, projetil
/// separado, wrapper, contrato -> assets e catalogo. Gera capturas (frente/costas/lado e poses).
/// Batch: -executeMethod CombatMageAudit.Run (sai com 0 se tudo passar). Menu: Tools > Character Rigging > Combat Mage > Audit.
/// </summary>
public static class CombatMageAudit
{
    const string OutDir = "Docs/CharacterMeshAudit/CombatMage/";
    static readonly List<string> Lines = new List<string>();
    static int failures;

    static void Check(bool ok, string message)
    {
        Lines.Add((ok ? "PASS " : "FAIL ") + message);
        if (!ok) failures++;
    }

    static void Info(string message) { Lines.Add("INFO " + message); }

    [MenuItem("Tools/Character Rigging/Combat Mage/Audit")]
    public static void RunFromMenu() { Run(); }

    public static void Run()
    {
        Lines.Clear();
        failures = 0;
        try { Execute(); }
        catch (Exception e) { Lines.Add("EXCEPTION " + e); failures++; }
        Directory.CreateDirectory(OutDir);
        Lines.Add(failures == 0 ? "COMBATMAGE_AUDIT_PASSED" : "COMBATMAGE_AUDIT_FAILED failures=" + failures);
        File.WriteAllText(OutDir + "unity-audit.txt", string.Join("\n", Lines) + "\n");
        Debug.Log(string.Join("\n", Lines));
        if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    static void Execute()
    {
        // ---- malha / licenca documentada
        string license = File.Exists(OutDir + "LICENSE.md") ? File.ReadAllText(OutDir + "LICENSE.md") : "";
        Check(license.Contains("Meshy") && license.Contains("Mixamo") && license.Contains("proceduralmente"), "LICENSE.md documenta Meshy, Mixamo e cajado procedural");
        Check(File.Exists("Tools/CharacterRigging/Inputs/CombatMage/combatmage_rigged.glb") && File.Exists("Tools/CharacterRigging/Inputs/CombatMage/rig_task.json"),
            "Malha-fonte (glb Meshy) e tarefas Meshy presentes em Tools/CharacterRigging/Inputs/CombatMage");
        Check(File.Exists("Tools/CharacterRigging/BuildCombatMage.py") && File.Exists("Tools/CharacterRigging/Build-CombatMage.ps1"), "Scripts de reconstrucao presentes");

        // ---- avatar / importers
        var avatar = AssetDatabase.LoadAllAssetsAtPath(CombatMageSetup.BaseModel).OfType<Avatar>().FirstOrDefault();
        Check(avatar != null && avatar.isValid && avatar.isHuman, "Avatar humanoide valido (CombatMage.fbx)");
        foreach (var info in CombatMageSetup.Clips)
        {
            string path = CombatMageSetup.ModelsDir + "/CombatMage@" + info.Name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var clip = CombatMageSetup.LoadClip(info.Name);
            Check(importer != null && importer.animationType == ModelImporterAnimationType.Human && importer.sourceAvatar == avatar,
                $"Clip {info.Name}: Humanoid copiando o avatar do CombatMage");
            Check(clip != null && clip.isHumanMotion && clip.length > 0.3f && clip.isLooping == info.Loop, $"Clip {info.Name}: importado, humanoide, {clip?.length:F2}s, loop={clip?.isLooping}");
        }

        // ---- visual prefab
        var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombatMageSetup.VisualPrefabPath);
        Check(visualPrefab != null, "CombatMageVisual.prefab existe");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab);
        var animator = instance.GetComponent<Animator>();
        Check(animator != null && animator.avatar == avatar && animator.runtimeAnimatorController != null && !animator.applyRootMotion,
            "Animator com avatar, controller e root motion desligado");
        var effects = instance.GetComponent<CombatMageStaffEffects>();
        Check(effects != null && effects.orbRenderer != null && effects.chargeEffects.Length == 2 && effects.releaseBurst != null, "CombatMageStaffEffects com orbe e efeitos ligados");

        var group = instance.GetComponentInChildren<LODGroup>();
        Check(group != null && group.lodCount == 3, "LODGroup com 3 niveis");
        var smrs = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(r => r.name).ToArray();
        Check(smrs.Length == 3, "3 SkinnedMeshRenderer (LOD0/1/2): " + string.Join(", ", smrs.Select(r => r.name)));
        var lod0 = smrs.First(r => r.name.EndsWith("LOD0"));
        long[] tris = smrs.Select(r => r.sharedMesh.triangles.Length / 3L).ToArray();
        Info("Triangulos LOD0/1/2: " + string.Join(" / ", tris));
        Check(tris[0] <= 31000 && tris[1] < tris[0] && tris[2] < tris[1], "Triangulos estritamente decrescentes por LOD e LOD0 <= 31 k");
        Check(tris[1] < tris[0] * 0.4 && tris[2] < tris[1] * 0.5, "LOD1 < 40% e LOD2 < 50% do nivel anterior");
        foreach (var smr in smrs)
        {
            var mesh = smr.sharedMesh;
            var weights = mesh.boneWeights;
            int bad = weights.Count(w => Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1f) > 0.02f);
            int maxInfluences = 0;
            foreach (byte bones in mesh.GetBonesPerVertex()) maxInfluences = Mathf.Max(maxInfluences, bones);
            Check(smr.bones.Length >= 22 && smr.rootBone != null && bad == 0,
                $"{smr.name}: {smr.bones.Length} ossos, rootBone ok, vertices sem peso normalizado = {bad}");
            Check(mesh.uv != null && mesh.uv.Length == mesh.vertexCount && mesh.uv.Any(u => u != Vector2.zero), $"{smr.name}: UV presentes ({mesh.uv.Length})");
            Check(smr.sharedMaterials.Length == 1 && smr.sharedMaterial != null && smr.sharedMaterial.name == "CombatMage_Mat", $"{smr.name}: material CombatMage_Mat");
            Check(maxInfluences >= 1 && maxInfluences <= 4, $"{smr.name}: no maximo 4 influencias por vertice (max {maxInfluences})");
        }

        // ---- materiais (visual, cajado, projetil, efeitos, morte, wrapper) e texturas
        var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(CombatMageEffectsBuilder.BoltPrefabPath);
        var impact = AssetDatabase.LoadAssetAtPath<GameObject>(CombatMageEffectsBuilder.ImpactPrefabPath);
        var cast = AssetDatabase.LoadAssetAtPath<GameObject>(CombatMageEffectsBuilder.CastPrefabPath);
        var death = AssetDatabase.LoadAssetAtPath<GameObject>(CombatMageSetup.DeathPrefabPath);
        var wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(CombatMageSetup.WrapperPrefabPath);
        Check(projectile != null && impact != null && cast != null && death != null, "Prefabs CombatMageBolt, Impact, Cast e Death existem");
        foreach (var root in new[] { visualPrefab, projectile, impact, cast, death, wrapper })
        {
            if (root == null) continue;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    bool ok = material != null && material.shader != null && material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader)
                              && material.shader.name != "Hidden/InternalErrorShader";
                    Check(ok, $"Material valido: {root.name}/{renderer.name} -> {(material != null ? material.name + " (" + material.shader.name + ")" : "NULL")}");
                }
        }
        var body = lod0.sharedMaterial;
        Check(body.mainTexture != null && body.GetTexture("_BumpMap") != null, "CombatMage_Mat com textura base e normal map");
        foreach (var tex in new[] { "CombatMage_BaseColor", "CombatMage_Normal", "CombatMage_FxGlow", "CombatMage_FxRing", "CombatMage_FxSpark", "CombatMage_FxStreak" })
            Check(AssetDatabase.LoadAssetAtPath<Texture2D>(CombatMageSetup.TexturesDir + "/" + tex + ".png") != null, "Textura importada: " + tex);

        // ---- cajado e sockets
        var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        var mount = instance.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "StaffMount");
        var staff = instance.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Staff");
        var castPoint = instance.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "CastPoint");
        var orb = instance.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Orb");
        Check(mount != null && mount.parent == rightHand, "StaffMount e filho do osso da mao direita");
        Check(staff != null && staff.IsChildOf(mount) && castPoint != null && castPoint.IsChildOf(staff) && orb != null && orb.IsChildOf(staff),
            "Cajado e objeto separado (filho de StaffMount) com CastPoint e Orb");
        Check(!lod0.GetComponentsInChildren<Transform>().Any(t => t.name == "Staff"), "Cajado nao faz parte da malha do corpo");
        Check(orb != null && orb.GetComponent<MeshRenderer>().sharedMaterial.IsKeywordEnabled("_EMISSION") &&
              orb.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_EmissionColor").maxColorComponent > 0.5f, "Orbe com material emissivo");
        long staffTris = staff.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3L);
        Check(staffTris <= 8000, $"Cajado dentro do orcamento de props ({staffTris} triangulos, <= 8 k)");
        Check(castPoint.position.y > 1.6f && castPoint.position.y < 2.4f, $"CastPoint na ponta do cajado (altura {castPoint.position.y:F2} m no Idle)");

        // ---- controller
        var controller = (AnimatorController)animator.runtimeAnimatorController;
        var states = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
        Check(new[] { "Locomotion", "Cast", "Hit" }.All(states.Contains), "Estados: " + string.Join(", ", states));
        foreach (var p in new[] { "Speed", "Fire", "Damaged", "Heal" })
            Check(controller.parameters.Any(x => x.name == p), "Parametro " + p);
        foreach (var p in new[] { "Running", "Attacking" })
            Check(controller.parameters.Any(x => x.name == p && x.type == AnimatorControllerParameterType.Bool), "Parametro bool " + p + " (behavior tree)");
        Check(controller.parameters.Any(x => x.name == "Hit" && x.type == AnimatorControllerParameterType.Trigger), "Parametro trigger Hit (behavior tree, sem transicoes)");
        var machine = controller.layers[0].stateMachine;
        var loco = machine.states.First(s => s.state.name == "Locomotion").state;
        Check(loco.transitions.Any(t => t.destinationState.name == "Cast" && t.conditions.Any(c => c.parameter == "Fire")), "Fire -> Cast");
        Check(loco.transitions.Any(t => t.destinationState.name == "Hit" && t.conditions.Any(c => c.parameter == "Damaged")), "Damaged -> Hit");
        Check(loco.motion is BlendTree bt && bt.blendParameter == "Speed" && bt.children.Length == 3, "Locomocao por BlendTree em Speed (Idle/Walk/Run)");
        var deathAnimator = death.GetComponent<Animator>();
        var deathController = (AnimatorController)deathAnimator.runtimeAnimatorController;
        Check(deathController != null && deathController.layers[0].stateMachine.defaultState.name == "Death" &&
              deathController.layers[0].stateMachine.defaultState.motion == CombatMageSetup.LoadClip("Death"), "Prefab de morte toca Death sozinho (estado padrao)");

        // ---- deformacao em todos os clipes
        DeformationAudit(instance, lod0);

        // ---- cajado segue a mao durante o movimento
        var staffRoot = staff;
        foreach (var (clipName, t) in new[] { ("Idle", 0f), ("Walk", 0.5f), ("Run", 0.3f), ("Cast", 0.4f), ("Cast", 0.77f), ("Cast", 1.3f), ("Hit", 0.3f) })
        {
            CombatMageSetup.LoadClip(clipName).SampleAnimation(instance, t);
            float distance = Vector3.Distance(staffRoot.position, rightHand.position);
            Check(distance < 0.15f, $"Cajado preso a mao em {clipName}@{t:F2}s: distancia punho-mao {distance:F3} m");
        }
        // direcao do orbe no instante do disparo: aponta para a frente (+Z) da unidade
        CombatMageSetup.LoadClip("Cast").SampleAnimation(instance, CombatMageSetup.ReleaseSeconds);
        Vector3 orbDirection = (castPoint.position - staff.position).normalized;
        float forwardDot = Vector3.Dot(orbDirection, instance.transform.forward);
        Check(forwardDot > 0.8f, $"No disparo o orbe aponta para a frente (cos {forwardDot:F2})");
        var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        var rightElbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Vector3 local(Transform t) { return instance.transform.InverseTransformPoint(t.position); }
        Info($"Cast@disparo (espaco do personagem): mao direita {local(rightHand):F2}, cotovelo direito {local(rightElbow):F2}, mao esquerda {local(leftHand):F2}");
        {
            // a malha segue o osso? vertices dominados pela mao direita (BakeMesh) vs posicao do osso
            var bakedHand = new Mesh();
            lod0.BakeMesh(bakedHand, true);
            var bw = lod0.sharedMesh.boneWeights;
            int handIndex = Array.IndexOf(lod0.bones, rightHand);
            var sum = Vector3.zero; int n = 0;
            var bv = bakedHand.vertices;
            var toWorld = Matrix4x4.TRS(lod0.transform.position, lod0.transform.rotation, Vector3.one);
            for (int i = 0; i < bw.Length; i++)
                if (bw[i].boneIndex0 == handIndex && bw[i].weight0 > 0.9f) { sum += toWorld.MultiplyPoint3x4(bv[i]); n++; }
            Info($"Malha da mao direita no disparo: {n} vertices, centro {(n > 0 ? sum / n : Vector3.zero):F2}, osso {rightHand.position:F2}");
            UnityEngine.Object.DestroyImmediate(bakedHand);
        }
        Check(local(rightHand).z > 0.15f && local(leftHand).z > 0.2f, "No disparo as duas maos avancam para a frente (a mao do cajado e a mao livre)");

        // ---- renders: LODs e poses
        RenderLods(instance, group);
        RenderPoses(instance, animator, effects, rightHand);

        // ---- morte: o corpo termina deitado
        var deathInstance = (GameObject)PrefabUtility.InstantiatePrefab(death);
        var deathClip = CombatMageSetup.LoadClip("Death");
        deathClip.SampleAnimation(deathInstance, deathClip.length);
        var deathHips = deathInstance.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips);
        Check(deathHips.position.y < 0.5f, $"Death termina com o corpo deitado (quadril a {deathHips.position.y:F2} m)");
        {
            // o corpo nunca atravessa o piso (y >= 0 da origem do prefab) durante todo o clipe Death
            var smr = deathInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.EndsWith("LOD0"));
            var baked = new Mesh();
            float lowest = 99f;
            for (int k = 0; k <= 24; k++)
            {
                deathClip.SampleAnimation(deathInstance, deathClip.length * k / 24f);
                smr.BakeMesh(baked, true);
                var toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                foreach (var v in baked.vertices) lowest = Mathf.Min(lowest, toWorld.MultiplyPoint3x4(v).y);
            }
            UnityEngine.Object.DestroyImmediate(baked);
            Check(lowest > 0f, $"Death: nenhum vertice abaixo do piso em todo o clipe (minimo {lowest:F3} m)");
            Check(deathInstance.GetComponent<CombatMageDeathGrounding>() != null, "Prefab de morte com CombatMageDeathGrounding (alinha ao terreno)");
        }
        UnityEngine.Object.DestroyImmediate(deathInstance);

        // ---- contrato / wrapper / catalogo
        ContractAudit(wrapper, projectile, impact, cast, death);

        UnityEngine.Object.DestroyImmediate(instance);
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
        var edges = new List<(int a, int b, float len)>();
        for (int i = 0; i < indices.Length; i += 3)
            for (int k = 0; k < 3; k++)
            {
                int a = indices[i + k], b = indices[i + (k + 1) % 3];
                float len = Vector3.Distance(refVertices[a], refVertices[b]);
                if (len > 0.004f && a < b) edges.Add((a, b, len));
            }
        Info("Arestas medidas: " + edges.Count);
        foreach (var info in CombatMageSetup.Clips)
        {
            var clip = CombatMageSetup.LoadClip(info.Name);
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
            // Death termina deitado: a altura vertical encolhe (a caixa fica ~0,6 m); os demais clipes mantem o humanoide em pe
            bool standing = info.Name != "Death";
            bool heightOk = standing ? (maxY - minY > 1.4f && maxY - minY < 2.4f) : (maxY - minY < 2.4f);
            bool ok = finite && p99 < 2.2f && p999 < 4.0f && p01 > 0.4f && over3 < 0.003f && heightOk;
            Check(ok, $"Deformacao {info.Name}: arestas p1%={p01:F2} p99%={p99:F2} p99.9%={p999:F2} max={ratios[ratios.Count - 1]:F2} (>x3: {over3:P3}), deslocamento max {maxMove:F2} m, altura {maxY - minY:F2} m, finito={finite}");
        }
        UnityEngine.Object.DestroyImmediate(baked);
        CombatMageSetup.LoadClip("Idle").SampleAnimation(instance, 0f);
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
        // em Edit Mode o skinning so reflete a pose amostrada depois que o renderer e reativado
        foreach (var smr in UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
        {
            smr.forceMatrixRecalculationPerRender = true;
            smr.updateWhenOffscreen = true;
            smr.enabled = false;
            smr.enabled = true;
        }
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
            CombatMageSetup.LoadClip("Idle").SampleAnimation(instance, 0f);
            for (int lod = 0; lod < 3; lod++)
            {
                group.ForceLOD(lod);
                foreach (var side in new[] { "front", "back", "side" })
                {
                    PlaceCamera(camera, side, 5.4f, 1.05f);
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

    // a unidade olha para +Z; "front" = camera em +Z olhando para -Z; "side" = camera no lado direito do personagem (-X)... visto do lado esquerdo da imagem
    static void PlaceCamera(Camera camera, string side, float distance, float height)
    {
        switch (side)
        {
            case "front":
                camera.transform.position = new Vector3(0f, height, distance);
                camera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                break;
            case "back":
                camera.transform.position = new Vector3(0f, height, -distance);
                camera.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                break;
            case "side":
                camera.transform.position = new Vector3(-distance, height, 0f);
                camera.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                break;
            default: // 3/4
                camera.transform.position = new Vector3(-distance * 0.7f, height + 0.35f, distance * 0.7f);
                camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.1f, 0f) - camera.transform.position);
                break;
        }
    }

    static void RenderPoses(GameObject instance, Animator animator, CombatMageStaffEffects effects, Transform rightHand)
    {
        var camera = MakeCamera(out var light, out var target);
        var group = instance.GetComponentInChildren<LODGroup>();
        group.ForceLOD(0);
        var shots = new (string clip, float t, bool charge, bool burst)[]
        {
            ("Idle", 1f, false, false), ("Walk", 0.3f, false, false), ("Run", 0.2f, false, false),
            ("Cast", 0.25f, true, false), ("Cast", 0.45f, true, false), ("Cast", CombatMageSetup.ReleaseSeconds, false, true), ("Cast", 1.1f, false, false),
            ("Hit", 0.3f, false, false), ("Death", 2.0f, false, false), ("Death", 5.7f, false, false),
        };
        try
        {
            foreach (var shot in shots)
            {
                CombatMageSetup.LoadClip(shot.clip).SampleAnimation(instance, shot.t);
                foreach (var ps in effects.chargeEffects) { ps.Clear(true); if (shot.charge) ps.Simulate(0.6f, true, true, true); }
                effects.releaseBurst.Clear(true);
                if (shot.burst) effects.releaseBurst.Simulate(0.12f, true, true, true);
                var views = new List<string> { "front", "side" };
                if (shot.clip == "Cast") views.Add("threeq");
                foreach (var side in views)
                {
                    PlaceCamera(camera, side, 5.4f, 1.05f);
                    string name = $"Unity-pose-{shot.clip}-{(int)(shot.t * 100):D3}-{side}.png";
                    var (fg, pink) = Capture(camera, target, OutDir + name);
                    Check(fg > 6000 && pink < 40, $"Pose {shot.clip}@{shot.t:F2}s {side}: renderiza (fg {fg}px, magenta {pink}px)");
                }
                if (shot.clip == "Cast")
                {
                    camera.transform.position = new Vector3(-1.6f, 1.7f, 1.8f);
                    camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.3f, 0.2f) - camera.transform.position);
                    var (cfg, cpink) = Capture(camera, target, OutDir + $"Unity-closeup-Cast-{(int)(shot.t * 100):D3}.png");
                    Check(cfg > 6000 && cpink < 40, $"Close-up Cast@{shot.t:F2}s: renderiza (fg {cfg}px, magenta {cpink}px)");
                }
            }
            foreach (var ps in effects.chargeEffects) ps.Clear(true);
            effects.releaseBurst.Clear(true);
        }
        finally
        {
            group.ForceLOD(-1);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            UnityEngine.Object.DestroyImmediate(light.gameObject);
            target.Release();
        }
    }

    static void ContractAudit(GameObject wrapper, GameObject projectilePrefab, GameObject impact, GameObject cast, GameObject death)
    {
        Check(wrapper != null, "CombatMage.prefab (wrapper da unidade) existe");
        if (wrapper == null) return;
        Check(wrapper.tag == "Unit" && wrapper.layer == 12, "Wrapper com tag Unit e layer 12");
        foreach (var component in new[] { "CharacterManager", "CharacterBT", "FogRendererToggler", "AdvancedUnitEraBinding", "AdvancedUnitAnimatorDriver", "ProjectileAttackController", "AdvancedUnitDeathEffect" })
            Check(wrapper.GetComponent(component) != null, "Wrapper tem " + component);
        Check(wrapper.GetComponent<UnityEngine.AI.NavMeshAgent>() != null && wrapper.GetComponent<BoxCollider>() != null && wrapper.GetComponent<AudioSource>() != null,
            "Wrapper tem NavMeshAgent, BoxCollider e AudioSource");
        var box = wrapper.GetComponent<BoxCollider>();
        var agent = wrapper.GetComponent<UnityEngine.AI.NavMeshAgent>();
        Check(box.size.x < 1f && box.size.y > 1.7f && agent.radius < 0.5f && agent.height > 1.7f, $"Colisor/agente ajustados a pegada do humanoide (caixa {box.size}, raio {agent.radius})");
        Check(wrapper.transform.Find("Mesh/MinimapIcon") != null, "Caminho Mesh/MinimapIcon (exigido por Unit) existe");
        var manager = (MonoBehaviour)wrapper.GetComponent("CharacterManager");
        var so = new SerializedObject(manager);
        Check(so.FindProperty("meshRenderer").objectReferenceValue != null, "CharacterManager.meshRenderer atribuido (anel do time)");
        Check(so.FindProperty("animator").objectReferenceValue != null, "CharacterManager.animator atribuido");
        Check(so.FindProperty("selectionCircle").objectReferenceValue != null && so.FindProperty("fov").objectReferenceValue != null &&
              so.FindProperty("contextualSource").objectReferenceValue != null, "selectionCircle, fov e contextualSource atribuidos");
        var fog = (MonoBehaviour)wrapper.GetComponent("FogRendererToggler");
        Check(new SerializedObject(fog).FindProperty("mesh").objectReferenceValue != null, "FogRendererToggler.mesh atribuido");

        var contract = AssetDatabase.LoadAssetAtPath<CombatMageContract>(AdvancedUnitBuilders.DataRoot + "/CombatMage.asset");
        Check(contract != null, "Contrato CombatMage.asset carregado");
        Check(contract.Validate().IsValid, "Contrato: dados de jogo validos (" + contract.Validate() + ")");
        Check(contract.EvaluateVisuals().IsReady, "Contrato: visual 100% real (" + contract.EvaluateVisuals() + ")");
        Check(contract.visual.prefab.asset == wrapper, "Contrato aponta para o wrapper jogavel");
        Check(contract.visual.meshes.Length >= 2 && contract.visual.meshes.All(m => m.asset != null && AssetDatabase.GetAssetPath(m.asset) == m.expectedAssetPath),
            "Contrato: malhas (corpo e cajado) apontam para os FBX reais");
        Check(contract.visual.effects.Length == 2 && contract.visual.effects.All(e => e.asset != null && AssetDatabase.GetAssetPath(e.asset) == e.expectedAssetPath),
            "Contrato: efeitos (conjuracao, impacto) apontam para os prefabs reais");
        Check(contract.visual.effects.Any(e => e.asset == cast) && contract.visual.effects.Any(e => e.asset == impact), "Contrato: efeitos = CombatMageCast e CombatMageImpact");
        foreach (var socket in contract.visual.sockets)
            Check(wrapper.transform.Find(socket.expectedTransformPath) != null, $"Socket '{socket.socketName}' resolve em Transform.Find: {socket.expectedTransformPath}");
        var states = contract.visual.animations.Select(a => a.state).ToArray();
        Check(states.SequenceEqual(new[] { "Idle", "Walk", "Run", "Cast", "Hit", "Death" }), "Contrato: animacoes " + string.Join(", ", states));
        Check(contract.visual.animations.All(a => a.clip != null && AssetDatabase.GetAssetPath(a.clip).Contains("/Models/CombatMage@" + a.state + ".fbx")), "Contrato: clipes importados reais por estado");
        Check(contract.visual.animations.First(a => a.state == "Cast").animatorParameter == "Fire" && contract.visual.animations.First(a => a.state == "Hit").animatorParameter == "Damaged",
            "Contrato: Cast -> Fire, Hit -> Damaged");
        var proj = contract.Projectile;
        Check(proj != null && proj.prefab.asset == projectilePrefab && proj.impactEffect.asset == impact && proj.EvaluateVisuals().IsReady, "Projetil separado: contrato aponta para CombatMageBolt e impacto reais");
        Check(projectilePrefab.GetComponent<AdvancedProjectile>() != null && projectilePrefab.GetComponentsInChildren<ParticleSystem>().Length >= 2 &&
              projectilePrefab.GetComponentInChildren<TrailRenderer>() != null, "CombatMageBolt: orbe + particulas + rastro (TrailRenderer)");
        Check(impact.GetComponentsInChildren<ParticleSystem>().Length >= 4, "CombatMageImpact: " + impact.GetComponentsInChildren<ParticleSystem>().Length + " sistemas de particulas");

        var era = wrapper.GetComponent<AdvancedUnitEraBinding>();
        var attack = wrapper.GetComponent<ProjectileAttackController>();
        var driver = wrapper.GetComponent<AdvancedUnitAnimatorDriver>();
        var deathEffect = wrapper.GetComponent<AdvancedUnitDeathEffect>();
        Check(era.contract == contract && attack.contract == contract, "AdvancedUnitEraBinding e ProjectileAttackController usam o contrato CombatMage");
        Check(attack.projectileSpawn != null && attack.projectileSpawn.name == "CastPoint" && attack.projectileSpawn == wrapper.transform.Find(contract.visual.sockets.First(s => s.socketName == "CastPoint").expectedTransformPath),
            "projectileSpawn = CastPoint do cajado");
        Check(attack.animator != null && driver.animator == attack.animator, "Animator ligado ao ataque e ao driver");
        var castClip = CombatMageSetup.LoadClip("Cast");
        var release = AnimationUtility.GetAnimationEvents(castClip).First(e => e.functionName == "CombatMageRelease").time;
        Check(Mathf.Abs(attack.releaseDelay - release) < 0.05f, $"releaseDelay ({attack.releaseDelay:F2}s) = evento de disparo do clipe Cast ({release:F2}s)");
        Check(deathEffect.deathPrefab == death, "AdvancedUnitDeathEffect usa o prefab de morte");

        // progressao nas 4 eras (contrato) e catalogo
        var tiers = contract.GetTiers();
        Check(tiers.Count == 4 && tiers.Select(t => t.Era).SequenceEqual(new[] { ImperialEra.Ancient, ImperialEra.Classical, ImperialEra.Medieval, ImperialEra.Imperial }), "Contrato: 4 eras em ordem");
        for (int i = 1; i < tiers.Count; i++)
            Check(tiers[i].HealthPoints > tiers[i - 1].HealthPoints && tiers[i].AttackDamage > tiers[i - 1].AttackDamage && tiers[i].AttackRange >= tiers[i - 1].AttackRange,
                $"Progressao {tiers[i - 1].Era}->{tiers[i].Era}: vida {tiers[i - 1].HealthPoints}->{tiers[i].HealthPoints}, dano {tiers[i - 1].AttackDamage}->{tiers[i].AttackDamage}, alcance {tiers[i - 1].AttackRange}->{tiers[i].AttackRange}");
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(CombatMageSetup.CharacterDataPath);
        Check(data != null && data.code == "combat_mage" && data.prefab == wrapper, "CharacterData 'combat_mage' referencia o wrapper");
        Check(data != null && data.healthpoints == tiers[0].HealthPoints && data.attackDamage == tiers[0].AttackDamage && Mathf.Approximately(data.attackRange, tiers[0].AttackRange) &&
              Mathf.Approximately(data.attackRate, AdvancedUnitDataBridge.AttackPollSeconds), "CharacterData: vida/dano/alcance Ancient do contrato e attackRate = AttackPollSeconds (poll do BT; a recarga real e do controlador)");
        var catalog = Resources.LoadAll<CharacterData>("ScriptableObjects/Units/Characters");
        Check(catalog.Any(c => c.code == "combat_mage"), "Catalogo Resources carrega 'combat_mage' (" + catalog.Length + " personagens)");
        var codes = catalog.Select(c => c.code).ToList();
        Check(codes.Count == codes.Distinct().Count(), "Codigos de personagem unicos: " + string.Join(", ", codes));
        var buildScenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray();
        Check(!buildScenes.Any(p => p.Contains("CombatMage")), "Nada de CombatMage no Build Settings");
    }
}
