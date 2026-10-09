using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Crossbow;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Auditoria em Edit Mode da Balestra: modelo/licenca, rig, UV/materiais, LODs, componentes do wrapper, sockets, clipes
/// (deformacao e continuidade), Animator, projetil separado, contrato e assets, catalogo e progressao nas 4 eras.
/// Gera renders (LODs, poses). Batch: -executeMethod CrossbowAudit.Run (sai com 0 se tudo passar).
/// </summary>
public static class CrossbowAudit
{
    const string OutDir = "Docs/CharacterMeshAudit/Crossbow/";
    static readonly List<string> Lines = new List<string>();
    static int failures;

    static void Check(bool ok, string message)
    {
        Lines.Add((ok ? "PASS " : "FAIL ") + message);
        if (!ok) failures++;
    }

    static void Info(string message) { Lines.Add("INFO " + message); }

    [MenuItem("Tools/Character Rigging/Crossbow/Audit")]
    public static void RunFromMenu() { Run(); }

    public static void Run()
    {
        Lines.Clear();
        failures = 0;
        try { Execute(); }
        catch (Exception e) { Lines.Add("EXCEPTION " + e); failures++; }
        Directory.CreateDirectory(OutDir);
        Lines.Add(failures == 0 ? "CROSSBOW_AUDIT_PASSED" : "CROSSBOW_AUDIT_FAILED failures=" + failures);
        File.WriteAllText(OutDir + "unity-audit.txt", string.Join("\n", Lines) + "\n");
        Debug.Log(string.Join("\n", Lines));
        if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    /// <summary>Diagnostico: lista as ligacoes (paths) de cada clipe e testa SampleAnimation.</summary>
    public static void DumpClips()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrossbowSetup.VisualPrefabPath);
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        foreach (string n in CrossbowSetup.ClipNames)
        {
            AnimationClip c = LoadClip(n);
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(c);
            sb.AppendLine($"{n}: {bindings.Length} curvas, len {c.length:F3}, legacy={c.legacy}; exemplos: " + string.Join(" | ", bindings.Select(b => b.path).Distinct()));
        }
        Transform wheel = AdvancedUnitBuilders.FindDeep(inst.transform, "Wheel_L");
        sb.AppendLine("Caminho Wheel_L: " + AdvancedUnitBuilders.RelativePath(inst.transform, wheel));
        Quaternion q0 = wheel.localRotation;
        LoadClip("Move").SampleAnimation(inst, 1.0f);
        sb.AppendLine($"Wheel_L rot antes {q0.eulerAngles} depois {wheel.localRotation.eulerAngles}");
        File.WriteAllText("TestResults/crossbow-dump.txt", sb.ToString());
        UnityEngine.Object.DestroyImmediate(inst);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void Execute()
    {
        AuditLicenseAndSource();
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(CrossbowSetup.BaseModel).OfType<Avatar>().FirstOrDefault();
        Check(avatar != null && avatar.isValid && !avatar.isHuman, "Avatar Generic valido (Crossbow.fbx)");

        GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrossbowSetup.VisualPrefabPath);
        Check(visualPrefab != null, "CrossbowVisual.prefab existe");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab);
        Animator animator = instance.GetComponent<Animator>();
        Check(animator != null && animator.avatar == avatar && animator.runtimeAnimatorController != null && !animator.applyRootMotion,
            "Animator com avatar, controller e root motion desligado");
        Check(instance.GetComponent<CrossbowFireEffects>() != null && instance.GetComponent<CrossbowFireEffects>().muzzle != null, "CrossbowFireEffects com efeito de disparo");

        AuditRigAndMeshes(instance, avatar, out LODGroup group, out SkinnedMeshRenderer[] lod0Parts);
        AuditMaterials(visualPrefab);
        AuditSockets(instance);
        AuditController(animator);
        AuditClips(instance, lod0Parts);
        AuditProjectile();
        AuditEffectsAndDeath();
        RenderLods(instance, group);
        RenderPoses(instance, group);
        UnityEngine.Object.DestroyImmediate(instance);
        AuditWrapper();
        AuditContractAndCatalog();
    }

    // ------------------------------------------------------------------------------------------------ licenca
    static void AuditLicenseAndSource()
    {
        Check(File.Exists("Tools/CharacterRigging/BuildCrossbow.py"), "Fonte do modelo procedural: Tools/CharacterRigging/BuildCrossbow.py");
        string license = File.Exists(OutDir + "LICENSE.md") ? File.ReadAllText(OutDir + "LICENSE.md") : "";
        Check(license.Length > 200 && license.Contains("BuildCrossbow.py") && license.ToLowerInvariant().Contains("sem terceiros"),
            "LICENSE.md registra origem procedural (obra do projeto, sem terceiros)");
        Check(!license.ToLowerInvariant().Contains("meshy") || license.ToLowerInvariant().Contains("nao utiliza"), "Nenhum modelo de terceiros declarado");
    }

    // ------------------------------------------------------------------------------------------------ rig e malhas
    static void AuditRigAndMeshes(GameObject instance, Avatar avatar, out LODGroup group, out SkinnedMeshRenderer[] lod0Parts)
    {
        string[] bones = { "Root", "Wheel_L", "Wheel_R", "LimbL", "LimbL_Tip", "LimbR", "LimbR_Tip", "String", "Bolt", "Winch", "Trigger" };
        foreach (string b in bones)
            Check(AdvancedUnitBuilders.FindDeep(instance.transform, b) != null, "Osso " + b);

        group = instance.GetComponent<LODGroup>();
        Check(group != null && group.lodCount == 3, "LODGroup com 3 niveis");
        SkinnedMeshRenderer[] all = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        long[] tris = new long[3];
        List<SkinnedMeshRenderer>[] byLod = { new List<SkinnedMeshRenderer>(), new List<SkinnedMeshRenderer>(), new List<SkinnedMeshRenderer>() };
        foreach (SkinnedMeshRenderer r in all)
            for (int i = 0; i < 3; i++)
                if (r.name.Contains("_LOD" + i)) { byLod[i].Add(r); tris[i] += r.sharedMesh.triangles.Length / 3L; }
        Info("Triangulos LOD0/1/2: " + string.Join(" / ", tris));
        Check(tris[0] <= 8000 && tris[1] <= 3000 && tris[2] <= 1000, "Orcamento de triangulos (<= 8000 / 3000 / 1000)");
        Check(tris[0] > tris[1] && tris[1] > tris[2] && tris[2] > 0, "Triangulos estritamente decrescentes por LOD");
        string[] partNames = { "Body", "WheelL", "WheelR", "LimbL", "LimbR", "String", "Bolt", "Winch", "Trigger" };
        foreach (string p in partNames)
            Check(byLod[0].Any(r => r.name == p + "_LOD0"), "Peca separada e nomeada " + p + "_LOD0");
        Check(byLod[1].Count == 1 && byLod[2].Count == 1, "LOD1 e LOD2 em malha unica");
        lod0Parts = byLod[0].ToArray();

        LOD[] lods = group.GetLODs();
        Check(lods.Length == 3 && lods[0].screenRelativeTransitionHeight > lods[1].screenRelativeTransitionHeight && lods[1].screenRelativeTransitionHeight > lods[2].screenRelativeTransitionHeight,
            "LODs com limiares decrescentes (" + string.Join(", ", lods.Select(l => l.screenRelativeTransitionHeight.ToString("F3"))) + ")");

        foreach (SkinnedMeshRenderer smr in all)
        {
            Mesh mesh = smr.sharedMesh;
            BoneWeight[] weights = mesh.boneWeights;
            int bad = weights.Count(w => Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1f) > 0.02f);
            int over4 = 0; // BoneWeight guarda no maximo 4 influencias
            Check(weights.Length == mesh.vertexCount && bad == 0 && smr.rootBone != null && smr.bones.Length >= 8 && over4 == 0,
                $"{smr.name}: {smr.bones.Length} ossos, rootBone ok, {mesh.vertexCount} vertices, sem peso fora de normalizado ({bad})");
            // UVs validos
            Vector2[] uv = mesh.uv;
            bool finite = uv.All(u => float.IsFinite(u.x) && float.IsFinite(u.y));
            int[] idx = mesh.triangles;
            int degenerate = 0;
            for (int i = 0; i < idx.Length; i += 3)
            {
                Vector2 a = uv[idx[i]], b = uv[idx[i + 1]], c = uv[idx[i + 2]];
                if (Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) < 1e-9f) degenerate++;
            }
            float degFraction = idx.Length > 0 ? degenerate / (idx.Length / 3f) : 1f;
            Check(uv.Length == mesh.vertexCount && finite && degFraction < 0.02f, $"{smr.name}: UVs validos (finitas, {degFraction:P1} de triangulos degenerados em UV)");
        }

        // dimensoes (LOD0, pose de bind)
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;
        foreach (SkinnedMeshRenderer r in lod0Parts)
        {
            Bounds b = r.bounds;
            if (first) { bounds = b; first = false; } else bounds.Encapsulate(b);
        }
        Vector3 size = bounds.size;
        Info($"Dimensoes LOD0 (m): largura {size.x:F2} x altura {size.y:F2} x comprimento {size.z:F2}; min y {bounds.min.y:F3}");
        Check(size.z > 2.2f && size.z < 2.7f && size.x > 1.6f && size.x < 2.0f && size.y > 1.3f && size.y < 1.7f, "Dimensoes ~2,4 x 1,8 x 1,5 m");
        Check(bounds.min.y > -0.02f && bounds.min.y < 0.05f, "Origem no chao (min y " + bounds.min.y.ToString("F3") + ")");
        Check(Mathf.Abs(instance.transform.rotation.eulerAngles.y) < 0.5f || Mathf.Abs(instance.transform.rotation.eulerAngles.y - 360f) < 0.5f, "Raiz do visual sem rotacao de importacao");
        // frente = +Z: o cabecote (arco) esta na frente, o cabrestante atras
        Transform winch = AdvancedUnitBuilders.FindDeep(instance.transform, "Winch");
        Transform limbL = AdvancedUnitBuilders.FindDeep(instance.transform, "LimbL");
        Check(limbL != null && winch != null && limbL.position.z > winch.position.z + 1f, $"Frente = +Z (arco z={limbL?.position.z:F2}, cabrestante z={winch?.position.z:F2})");
        Transform wheelL = AdvancedUnitBuilders.FindDeep(instance.transform, "Wheel_L");
        Check(wheelL != null && wheelL.position.x < -0.4f, "Lado L = esquerda da unidade (-X no Unity)");
    }

    static void AuditMaterials(GameObject visualPrefab)
    {
        GameObject projectile = AssetDatabase.LoadAssetAtPath<GameObject>(CrossbowSetup.BoltPrefabPath);
        GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(CrossbowSetup.WrapperPrefabPath);
        GameObject death = AssetDatabase.LoadAssetAtPath<GameObject>(CrossbowSetup.DeathPrefabPath);
        GameObject impact = AssetDatabase.LoadAssetAtPath<GameObject>(CrossbowSetup.ImpactPrefabPath);
        GameObject muzzle = AssetDatabase.LoadAssetAtPath<GameObject>(CrossbowSetup.MuzzlePrefabPath);
        foreach (GameObject root in new[] { visualPrefab, projectile, wrapper, death, impact, muzzle })
        {
            if (root == null) { Check(false, "Prefab ausente na auditoria de materiais"); continue; }
            int count = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                {
                    count++;
                    bool ok = material != null && material.shader != null && material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader) && material.shader.name != "Hidden/InternalErrorShader";
                    Check(ok, $"Material valido: {root.name}/{renderer.name} -> {(material != null ? material.name + " (" + material.shader.name + ")" : "NULL")}");
                }
            Check(count > 0, root.name + ": tem renderers");
        }
        foreach (string n in new[] { "Crossbow_Wood", "Crossbow_DarkWood", "Crossbow_Iron", "Crossbow_Rope" })
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(CrossbowSetup.MaterialsDir + "/" + n + ".mat");
            Check(m != null && m.shader.name == "Standard" && m.mainTexture != null, n + ": Standard com textura procedural");
        }
        Material feather = AssetDatabase.LoadAssetAtPath<Material>(CrossbowSetup.MaterialsDir + "/Crossbow_Feather.mat");
        Check(feather != null && feather.shader.name == "Standard", "Crossbow_Feather: Standard");
        foreach (string t in new[] { "Crossbow_Wood", "Crossbow_Iron", "Crossbow_Rope", "Crossbow_Smoke", "Crossbow_Spark" })
        {
            string path = CrossbowSetup.TexturesDir + "/" + t + ".png";
            long size = new FileInfo(path).Length;
            Check(size < 1200000, $"Textura {t}.png leve ({size / 1024} KB)");
        }
    }

    // ------------------------------------------------------------------------------------------------ sockets
    static void AuditSockets(GameObject instance)
    {
        Transform spawn = AdvancedUnitBuilders.FindDeep(instance.transform, "BoltSpawn");
        Transform mount = AdvancedUnitBuilders.FindDeep(instance.transform, "WeaponMount");
        Check(spawn != null && mount != null, "Sockets BoltSpawn e WeaponMount no visual");
        if (spawn == null || mount == null) return;
        Transform root = AdvancedUnitBuilders.FindDeep(instance.transform, "Root");
        Check(spawn.IsChildOf(root) && mount.IsChildOf(root), "Sockets sao filhos do osso Root");
        Info($"BoltSpawn {spawn.position:F3}, WeaponMount {mount.position:F3}");
        Check(spawn.position.z > 1.0f && spawn.position.z < 1.2f && Mathf.Abs(spawn.position.x) < 0.02f && spawn.position.y > 1.0f && spawn.position.y < 1.1f,
            "BoltSpawn na ponta do trilho (z ~ 1,05, y ~ 1,06)");
        Check(mount.position.z > 0.85f && mount.position.z < 1.0f && mount.position.y > 1.05f && mount.position.y < 1.2f, "WeaponMount no centro do cabecote");
        Check(Vector3.Angle(spawn.forward, Vector3.forward) < 1f, "BoltSpawn aponta para +Z");
    }

    // ------------------------------------------------------------------------------------------------ controller
    static void AuditController(Animator animator)
    {
        AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
        Check(controller != null, "AnimatorController da Balestra");
        if (controller == null) return;
        foreach (string p in new[] { "Running", "Attacking", "Hit", "Speed", "Fire", "Damaged" })
            Check(controller.parameters.Any(x => x.name == p), "Parametro " + p);
        Check(controller.parameters.First(x => x.name == "Fire").type == AnimatorControllerParameterType.Trigger
              && controller.parameters.First(x => x.name == "Damaged").type == AnimatorControllerParameterType.Trigger
              && controller.parameters.First(x => x.name == "Speed").type == AnimatorControllerParameterType.Float, "Tipos: Fire/Damaged trigger, Speed float");
        AnimatorStateMachine m = controller.layers[0].stateMachine;
        string[] names = m.states.Select(s => s.state.name).ToArray();
        Check(new[] { "Idle", "Move", "Fire", "Reload" }.All(names.Contains), "Estados da camada base: " + string.Join(", ", names));
        AnimatorState Get(string n) { return m.states.First(s => s.state.name == n).state; }
        Check(Get("Idle").transitions.Any(t => t.destinationState == Get("Fire") && t.conditions.Any(c => c.parameter == "Fire")), "Fire -> estado Fire");
        Check(Get("Fire").transitions.Any(t => t.destinationState == Get("Reload") && t.hasExitTime), "Fire -> Reload");
        Check(Get("Reload").transitions.Any(t => t.destinationState == Get("Idle") && t.hasExitTime), "Reload -> Idle");
        Check(Get("Idle").transitions.Any(t => t.destinationState == Get("Move") && t.conditions.Any(c => c.parameter == "Speed")), "Speed -> Move");
        Check(Get("Move").speedParameterActive && Get("Move").speedParameter == "Speed", "Move com velocidade de clipe dirigida por Speed");
        Check(controller.layers.Length == 2 && controller.layers[1].name == "Hit" && controller.layers[1].avatarMask != null, "Camada Hit com mascara");
        AnimatorState hit = controller.layers[1].stateMachine.states.FirstOrDefault(s => s.state.name == "Hit").state;
        Check(hit != null && hit.motion != null && controller.layers[1].stateMachine.states.Any(s => s.state.name == "Empty" && s.state.transitions.Any(t => t.destinationState == hit && t.conditions.Any(c => c.parameter == "Damaged"))),
            "Damaged -> Hit");
        Check(Get("Idle").motion != null && Get("Move").motion != null && Get("Fire").motion != null && Get("Reload").motion != null, "Todos os estados tem clipe");
    }

    // ------------------------------------------------------------------------------------------------ clipes
    static AnimationClip LoadClip(string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(CrossbowSetup.ModelsDir + "/Crossbow@" + name + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
    }

    static Bounds PoseBounds(GameObject instance, SkinnedMeshRenderer[] parts, out bool finite, out Dictionary<string, Bounds> perPart)
    {
        finite = true;
        perPart = new Dictionary<string, Bounds>();
        Bounds all = default;
        bool first = true;
        Mesh baked = new Mesh();
        foreach (SkinnedMeshRenderer r in parts)
        {
            r.BakeMesh(baked, true);
            Vector3[] v = baked.vertices;
            Matrix4x4 m = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
            Bounds b = default;
            bool fp = true;
            foreach (Vector3 p in v)
            {
                if (!(float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z))) { finite = false; continue; }
                Vector3 w = m.MultiplyPoint3x4(p);
                if (fp) { b = new Bounds(w, Vector3.zero); fp = false; } else b.Encapsulate(w);
            }
            perPart[r.name] = b;
            if (first) { all = b; first = false; } else all.Encapsulate(b);
        }
        UnityEngine.Object.DestroyImmediate(baked);
        return all;
    }

    static float PoseDifference(GameObject instance, SkinnedMeshRenderer[] parts, AnimationClip a, float ta, AnimationClip b, float tb)
    {
        Mesh m = new Mesh();
        List<Vector3[]> first = new List<Vector3[]>();
        a.SampleAnimation(instance, ta);
        foreach (SkinnedMeshRenderer r in parts) { r.BakeMesh(m, true); first.Add(m.vertices); }
        b.SampleAnimation(instance, tb);
        float worst = 0f;
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i].BakeMesh(m, true);
            Vector3[] v = m.vertices;
            for (int k = 0; k < v.Length; k += 3) worst = Mathf.Max(worst, Vector3.Distance(v[k], first[i][k]));
        }
        UnityEngine.Object.DestroyImmediate(m);
        return worst;
    }

    static void AuditClips(GameObject instance, SkinnedMeshRenderer[] lod0Parts)
    {
        ModelImporter baseImporter = (ModelImporter)AssetImporter.GetAtPath(CrossbowSetup.BaseModel);
        Dictionary<string, (float min, float max)> lengths = new Dictionary<string, (float, float)>
        {
            ["Idle"] = (1.5f, 3f), ["Move"] = (3.5f, 4.5f), ["Reload"] = (1.0f, 1.8f), ["Fire"] = (0.5f, 1.0f), ["Hit"] = (0.3f, 0.9f), ["Destroy"] = (1.2f, 3f)
        };
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(CrossbowSetup.BaseModel).OfType<Avatar>().First();
        Bounds bind = PoseBounds(instance, lod0Parts, out _, out _);
        foreach (string name in CrossbowSetup.ClipNames)
        {
            string path = CrossbowSetup.ModelsDir + "/Crossbow@" + name + ".fbx";
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
            AnimationClip clip = LoadClip(name);
            Check(importer != null && importer.animationType == ModelImporterAnimationType.Generic && importer.sourceAvatar == avatar, $"Clip {name}: Generic copiando o avatar da Balestra");
            Check(clip != null && clip.length >= lengths[name].min && clip.length <= lengths[name].max, $"Clip {name}: {clip?.length:F2}s dentro de [{lengths[name].min}, {lengths[name].max}]");
            Check(clip.isLooping == (name == "Idle" || name == "Move"), $"Clip {name}: loop={clip.isLooping}");

            bool finiteAll = true;
            float maxLow = 99f, maxHigh = -99f, extent = 0f;
            for (int k = 0; k <= 8; k++)
            {
                clip.SampleAnimation(instance, clip.length * k / 8f);
                Bounds b = PoseBounds(instance, lod0Parts, out bool finite, out _);
                finiteAll &= finite;
                maxLow = Mathf.Min(maxLow, b.min.y);
                maxHigh = Mathf.Max(maxHigh, b.max.y);
                extent = Mathf.Max(extent, Mathf.Max(b.size.x, b.size.z));
            }
            bool ground = name == "Destroy" ? maxLow > -0.06f : maxLow > -0.05f;
            Check(finiteAll && ground && maxHigh < 1.9f && extent < 3.3f, $"Clip {name}: deformacao finita, altura {maxLow:F2}..{maxHigh:F2} m, extensao max {extent:F2} m");
        }

        // continuidade: Reload (fim) = Idle (inicio) = Fire (inicio); Fire (fim) = Reload (inicio)
        AnimationClip idle = LoadClip("Idle"), reload = LoadClip("Reload"), fire = LoadClip("Fire");
        float d1 = PoseDifference(instance, lod0Parts, reload, reload.length, idle, 0f);
        float d2 = PoseDifference(instance, lod0Parts, fire, 0f, idle, 0f);
        float d3 = PoseDifference(instance, lod0Parts, fire, fire.length, reload, 0f);
        Check(d1 < 0.02f, $"Fim de Reload = pose de Idle (diferenca max {d1:F4} m)");
        Check(d2 < 0.02f, $"Inicio de Fire = pose de Idle (diferenca max {d2:F4} m)");
        Check(d3 < 0.02f, $"Fim de Fire = inicio de Reload (diferenca max {d3:F4} m)");

        // virote sai do trilho no disparo e volta no reload; corda e bracos se movem
        SkinnedMeshRenderer boltRenderer = lod0Parts.First(r => r.name == "Bolt_LOD0");
        SkinnedMeshRenderer stringRenderer = lod0Parts.First(r => r.name == "String_LOD0");
        float BoltSize(AnimationClip c, float t)
        {
            c.SampleAnimation(instance, t);
            PoseBounds(instance, new[] { boltRenderer }, out _, out Dictionary<string, Bounds> p);
            return p["Bolt_LOD0"].size.magnitude;
        }
        Check(BoltSize(idle, 0f) > 0.55f, "Idle: virote carregado visivel (" + BoltSize(idle, 0f).ToString("F2") + " m)");
        Check(BoltSize(fire, 0.04f) > 0.55f && BoltSize(fire, 0.4f) < 0.05f, "Fire: virote presente antes da soltura e sumido depois");
        Check(BoltSize(reload, 0.1f) < 0.05f && BoltSize(reload, reload.length) > 0.55f, "Reload: virote volta ao trilho");
        float StringY(AnimationClip c, float t)
        {
            c.SampleAnimation(instance, t);
            PoseBounds(instance, new[] { stringRenderer }, out _, out Dictionary<string, Bounds> p);
            return p["String_LOD0"].center.z;
        }
        Check(Mathf.Abs(StringY(fire, 0.02f) - StringY(fire, fire.length)) > 0.05f, "Fire: a corda se move (centro da corda muda de posicao)");
        idle.SampleAnimation(instance, 0f);
        Transform wheelL = AdvancedUnitBuilders.FindDeep(instance.transform, "Wheel_L");
        Quaternion q0 = wheelL.localRotation;
        LoadClip("Move").SampleAnimation(instance, 1.0f);
        Check(Quaternion.Angle(q0, wheelL.localRotation) > 60f, "Move: as rodas giram");
        // loop de Move: uma volta completa
        LoadClip("Move").SampleAnimation(instance, 0f);
        Quaternion m0 = wheelL.localRotation;
        LoadClip("Move").SampleAnimation(instance, LoadClip("Move").length);
        Check(Quaternion.Angle(m0, wheelL.localRotation) < 3f, "Move: loop fecha em uma volta de roda");
        idle.SampleAnimation(instance, 0f);
    }

    // ------------------------------------------------------------------------------------------------ projetil e efeitos
    static void AuditProjectile()
    {
        GameObject bolt = AssetDatabase.LoadAssetAtPath<GameObject>(CrossbowSetup.BoltPrefabPath);
        Check(bolt != null && bolt.GetComponentsInChildren<MeshRenderer>(true).Length + bolt.GetComponentsInChildren<MeshFilter>(true).Length > 0, "CrossbowBolt.prefab com malha propria (projetil separado)");
        if (bolt == null) return;
        Check(bolt.GetComponentInChildren<SkinnedMeshRenderer>(true) == null, "Virote do projetil nao e parte da malha da unidade");
        GameObject boltScene = (GameObject)PrefabUtility.InstantiatePrefab(bolt);
        Bounds b = default; bool first = true;
        foreach (Renderer r in boltScene.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
        Info($"Virote: tamanho {b.size:F3}, bounds z {b.min.z:F3}..{b.max.z:F3}");
        Check(b.size.z > 0.55f && b.size.z < 0.65f && b.size.x < 0.1f && b.size.y < 0.1f, "Virote com ~0,6 m de comprimento");
        Check(b.max.z < 0.02f && b.min.z < -0.55f, "Pivo do virote na ponta (+Z = direcao do voo)");
        UnityEngine.Object.DestroyImmediate(boltScene);
        ProjectileContract contract = AssetDatabase.LoadAssetAtPath<ProjectileContract>(CrossbowSetup.BoltContractPath);
        Check(contract != null && contract.code == "crossbow_bolt" && contract.damageType == DamageType.Piercing && !contract.ballistic, "ProjectileContract: crossbow_bolt, Piercing, voo reto");
        Check(contract != null && contract.prefab.asset == bolt && contract.impactEffect.asset != null && contract.EvaluateVisuals().IsReady, "Contrato do projetil aponta para o prefab e o efeito de impacto reais");
    }

    static void AuditEffectsAndDeath()
    {
        foreach (string path in new[] { CrossbowSetup.ImpactPrefabPath, CrossbowSetup.MuzzlePrefabPath })
        {
            GameObject g = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            ParticleSystem[] systems = g != null ? g.GetComponentsInChildren<ParticleSystem>(true) : new ParticleSystem[0];
            Check(systems.Length >= 2 && systems.All(s => s.GetComponent<ParticleSystemRenderer>().sharedMaterial != null), Path.GetFileName(path) + ": " + systems.Length + " sistemas de particulas com material");
        }
        GameObject death = AssetDatabase.LoadAssetAtPath<GameObject>(CrossbowSetup.DeathPrefabPath);
        Animator a = death != null ? death.GetComponent<Animator>() : null;
        AnimatorController c = a != null ? a.runtimeAnimatorController as AnimatorController : null;
        Check(c != null && c.layers[0].stateMachine.defaultState != null && c.layers[0].stateMachine.defaultState.name == "Destroy" && c.layers[0].stateMachine.defaultState.motion == LoadClip("Destroy"),
            "CrossbowDeath.prefab toca o clipe Destroy no estado inicial");
        Check(death != null && death.GetComponentsInChildren<ParticleSystem>(true).Length >= 2, "CrossbowDeath.prefab com poeira da queda");
    }

    // ------------------------------------------------------------------------------------------------ renders
    static Camera MakeCamera(out Light light, out RenderTexture target)
    {
        GameObject cameraObject = new GameObject("AuditCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.34f, 0.36f, 0.39f);
        camera.fieldOfView = 30f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 200f;
        GameObject lightObject = new GameObject("AuditLight");
        lightObject.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
        light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
        target = new RenderTexture(640, 480, 24);
        camera.targetTexture = target;
        return camera;
    }

    static (int foreground, int magenta) Capture(Camera camera, RenderTexture target, string path)
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
        Color bg = camera.backgroundColor;
        int magenta = pixels.Count(p => p.r > 220 && p.g < 40 && p.b > 220);
        int foreground = pixels.Count(p => Mathf.Abs(p.r / 255f - bg.r) > 0.04f || Mathf.Abs(p.g / 255f - bg.g) > 0.04f || Mathf.Abs(p.b / 255f - bg.b) > 0.04f);
        UnityEngine.Object.DestroyImmediate(texture);
        return (foreground, magenta);
    }

    static void Aim(Camera camera, string side)
    {
        Vector3 focus = new Vector3(0f, 0.75f, 0f);
        Vector3 dir;
        switch (side)
        {
            case "front": dir = new Vector3(0.0f, 0.18f, 1f); break;
            case "back": dir = new Vector3(0.0f, 0.18f, -1f); break;
            case "side": dir = new Vector3(-1f, 0.12f, 0f); break;
            case "iso": dir = new Vector3(-0.6f, 0.5f, 0.8f); break;
            default: dir = new Vector3(0f, 1f, 0.001f); break;
        }
        camera.transform.position = focus + dir.normalized * 6.4f;
        camera.transform.rotation = Quaternion.LookRotation(focus - camera.transform.position);
    }

    static void RenderLods(GameObject instance, LODGroup group)
    {
        Camera camera = MakeCamera(out Light light, out RenderTexture target);
        try
        {
            LoadClip("Idle").SampleAnimation(instance, 0f);
            for (int lod = 0; lod < 3; lod++)
            {
                group.ForceLOD(lod);
                foreach (string side in new[] { "front", "back", "side", "iso" })
                {
                    Aim(camera, side);
                    (int fg, int pink) = Capture(camera, target, OutDir + $"Unity-LOD{lod}-{side}.png");
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

    static void RenderPoses(GameObject instance, LODGroup group)
    {
        Camera camera = MakeCamera(out Light light, out RenderTexture target);
        group.ForceLOD(0);
        (string clip, float t, string tag)[] shots =
        {
            ("Idle", 0f, "Idle"), ("Fire", 0.2f, "Fire-release"), ("Fire", 0.27f, "Fire-recoil"), ("Fire", 0.8f, "Fire-relaxed"),
            ("Reload", 0.5f, "Reload-draw"), ("Reload", 1.0f, "Reload-bolt"), ("Move", 0.9f, "Move"), ("Hit", 0.1f, "Hit"),
            ("Destroy", 0.5f, "Destroy-mid"), ("Destroy", 2.0f, "Destroy-end"),
        };
        try
        {
            foreach ((string clip, float t, string tag) in shots)
            {
                LoadClip(clip).SampleAnimation(instance, t);
                foreach (string side in new[] { "iso", "top" })
                {
                    Aim(camera, side);
                    (int fg, int pink) = Capture(camera, target, OutDir + $"Unity-pose-{tag}-{side}.png");
                    Check(fg > 6000 && pink < 40, $"Pose {tag} {side}: renderiza (fg {fg}px, magenta {pink}px)");
                }
            }
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

    // ------------------------------------------------------------------------------------------------ wrapper, contrato, catalogo
    static void AuditWrapper()
    {
        GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(CrossbowSetup.WrapperPrefabPath);
        Check(wrapper != null, "Crossbow.prefab (wrapper da unidade) existe");
        if (wrapper == null) return;
        Check(wrapper.tag == "Unit" && wrapper.layer == 12, "Wrapper com tag Unit e layer 12");
        foreach (string component in new[] { "CharacterManager", "CharacterBT", "FogRendererToggler", "AdvancedUnitEraBinding", "AdvancedUnitAnimatorDriver", "ProjectileAttackController", "AdvancedUnitDeathEffect" })
            Check(wrapper.GetComponent(component) != null, "Wrapper tem " + component);
        NavMeshAgent agent = wrapper.GetComponent<NavMeshAgent>();
        Check(agent != null && Mathf.Abs(agent.speed - 3.5f) < 0.01f, "NavMeshAgent com velocidade 3,5");
        BoxCollider box = wrapper.GetComponent<BoxCollider>();
        Check(box != null && Mathf.Abs(box.size.x - 1.8f) < 0.05f && Mathf.Abs(box.size.z - 2.45f) < 0.1f, "BoxCollider com a pegada da carreta (1,8 x 2,45)");
        Check(wrapper.GetComponent<AudioSource>() != null, "Wrapper tem AudioSource");
        Check(wrapper.transform.Find("Mesh/MinimapIcon") != null, "Caminho Mesh/MinimapIcon (exigido por Unit) existe");
        Transform ring = wrapper.transform.Find("Mesh/TeamRing");
        MeshFilter ringFilter = ring != null ? ring.GetComponent<MeshFilter>() : null;
        float ringSize = ringFilter != null ? ringFilter.sharedMesh.bounds.size.x : 0f;
        Check(ringSize > 2.0f && ringSize <= 2.4f, $"Anel de time ~ pegada (diametro {ringSize:F2} m)");
        MonoBehaviour manager = (MonoBehaviour)wrapper.GetComponent("CharacterManager");
        SerializedObject so = new SerializedObject(manager);
        Check(so.FindProperty("meshRenderer").objectReferenceValue != null, "CharacterManager.meshRenderer atribuido (anel do time)");
        Check(so.FindProperty("animator").objectReferenceValue != null, "CharacterManager.animator atribuido");
        Check(so.FindProperty("selectionCircle").objectReferenceValue != null && so.FindProperty("fov").objectReferenceValue != null && so.FindProperty("contextualSource").objectReferenceValue != null,
            "selectionCircle, fov e contextualSource atribuidos");
        CrossbowContract contract = AssetDatabase.LoadAssetAtPath<CrossbowContract>(CrossbowSetup.ContractPath);
        AdvancedUnitEraBinding binding = wrapper.GetComponent<AdvancedUnitEraBinding>();
        Check(binding != null && binding.contract == contract, "AdvancedUnitEraBinding.contract = Crossbow.asset");
        ProjectileAttackController attack = wrapper.GetComponent<ProjectileAttackController>();
        Check(attack != null && attack.contract == contract && attack.animator != null && attack.projectileSpawn != null && attack.projectileSpawn.name == "BoltSpawn"
              && Mathf.Abs(attack.releaseDelay - CrossbowSetup.ReleaseDelay) < 0.001f, "ProjectileAttackController: contrato, animator, BoltSpawn e releaseDelay " + attack?.releaseDelay.ToString("F2"));
        AdvancedUnitDeathEffect death = wrapper.GetComponent<AdvancedUnitDeathEffect>();
        Check(death != null && death.deathPrefab == AssetDatabase.LoadAssetAtPath<GameObject>(CrossbowSetup.DeathPrefabPath), "AdvancedUnitDeathEffect usa CrossbowDeath.prefab");
        AdvancedUnitAnimatorDriver driver = wrapper.GetComponent<AdvancedUnitAnimatorDriver>();
        Check(driver != null && driver.animator != null && driver.animator == attack.animator, "AdvancedUnitAnimatorDriver ligado ao mesmo Animator");
        Check(wrapper.GetComponentInChildren<LODGroup>() != null && wrapper.GetComponentInChildren<LODGroup>().lodCount == 3, "LODGroup preservado no wrapper");
    }

    static void AuditContractAndCatalog()
    {
        CrossbowContract contract = AssetDatabase.LoadAssetAtPath<CrossbowContract>(CrossbowSetup.ContractPath);
        GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(CrossbowSetup.WrapperPrefabPath);
        Check(contract != null, "Contrato Crossbow.asset");
        if (contract == null) return;
        Check(contract.code == "crossbow" && contract.dataKind == UnitDataKind.Character, "Codigo crossbow, dataKind Character");
        ValidationReport v = contract.Validate();
        Check(v.IsValid, "Contrato valido (dados de jogo): " + v);
        VisualReadinessReport vr = contract.EvaluateVisuals();
        Check(vr.IsReady, "Contrato com visual pronto: " + vr);
        Check(contract.visual.prefab.asset == wrapper, "visual.prefab = wrapper Crossbow.prefab");
        string[] states = contract.visual.animations.Select(a => a.state).ToArray();
        Check(states.SequenceEqual(new[] { "Idle", "Move", "Reload", "Fire", "Hit", "Destroy" }), "Animacoes do contrato: " + string.Join(", ", states));
        foreach (AnimationRequirement a in contract.visual.animations)
            Check(a.clip != null && a.clip == LoadClip(a.state), $"Contrato: clipe {a.state} = Crossbow@{a.state}.fbx");
        foreach (SocketRequirement s in contract.visual.sockets)
            Check(wrapper.transform.Find(s.expectedTransformPath) != null, $"Socket {s.socketName} resolve em {s.expectedTransformPath}");
        Check(contract.visual.effects.Length == 2 && contract.visual.effects.All(e => e.asset != null && AssetDatabase.GetAssetPath(e.asset) == e.expectedAssetPath), "Efeitos do contrato apontam para assets reais");
        Check(contract.visual.meshes.All(m => m.asset != null && AssetDatabase.GetAssetPath(m.asset) == m.expectedAssetPath), "Malhas do contrato apontam para assets reais");
        Check(!contract.buildSettingsApproved, "buildSettingsApproved permanece falso (decisao do coordenador)");

        // progressao nas 4 eras
        CrossbowEraTier[] t = contract.tiers;
        Check(t.Length == 4 && t.Select(x => x.era).SequenceEqual(new[] { ImperialEra.Ancient, ImperialEra.Classical, ImperialEra.Medieval, ImperialEra.Imperial }), "Quatro eras em ordem");
        bool hp = true, dmg = true, range = true, cd = true, pen = true;
        for (int i = 1; i < t.Length; i++)
        {
            hp &= t[i].healthpoints > t[i - 1].healthpoints;
            dmg &= t[i].attackDamage > t[i - 1].attackDamage;
            range &= t[i].attackRange >= t[i - 1].attackRange;
            cd &= t[i].attackCooldown <= t[i - 1].attackCooldown;
            pen &= t[i].armorPenetration > t[i - 1].armorPenetration;
        }
        Check(hp && dmg && range && cd && pen, "Progressao: vida e dano crescentes, alcance nao decrescente, cooldown nao crescente, penetracao crescente");
        for (int i = 0; i < t.Length; i++)
        {
            UnitDataFields f = contract.BuildUnitDataFields(t[i].era);
            Check(f.healthpoints == t[i].healthpoints && f.attackDamage == t[i].attackDamage && Mathf.Approximately(f.attackRange, t[i].attackRange), $"Era {t[i].era}: UnitDataFields {f.healthpoints} PV / {f.attackDamage} dano / alcance {f.attackRange}");
        }

        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(CrossbowSetup.CharacterDataPath);
        Check(data != null && data.code == "crossbow" && data.prefab == wrapper, "CharacterData 'crossbow' referencia o wrapper");
        Check(data != null && data.attackRange >= 12f && data.attackDamage > 0 && data.attackRate > 0 && data.healthpoints == t[0].healthpoints, "CharacterData com valores da era Ancient (alcance " + data?.attackRange + ")");
        CharacterData[] catalog = Resources.LoadAll<CharacterData>("ScriptableObjects/Units/Characters");
        Check(catalog.Any(c => c.code == "crossbow"), "Catalogo Resources carrega 'crossbow' (" + catalog.Length + " personagens)");
        List<string> codes = catalog.Select(c => c.code).ToList();
        Check(codes.Count == codes.Distinct().Count(), "Codigos de personagem unicos: " + string.Join(", ", codes));
        string reason;
        Check(AdvancedUnitDataBridge.CanBind(contract, out reason), "AdvancedUnitDataBridge.CanBind: " + (reason ?? "ok"));
    }
}
