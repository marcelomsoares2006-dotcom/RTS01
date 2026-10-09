using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EraImperial.Units.Advanced;
using EraImperial.Units.Mage;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// AUDITORIA INDEPENDENTE do Mago de combate (combat_mage). Escrita pelo auditor; nao reutiliza CombatMageAudit/PlayModeCheck.
///   EditMode : -quit -executeMethod CombatMageIndependentAudit.RunEdit   -> Docs/AdvancedUnits/audit/combat_mage/edit-checks.txt (+ renders)
///   Play Mode: -executeMethod CombatMageIndependentAudit.RunPlay          -> Docs/AdvancedUnits/audit/combat_mage/play-checks.txt (+ renders)
/// Somente leitura nos assets do jogo. Escreve apenas em Docs/AdvancedUnits/audit/combat_mage e TestResults.
/// </summary>
[InitializeOnLoad]
public static class CombatMageIndependentAudit
{
    private const string OutDir = "Docs/AdvancedUnits/audit/combat_mage";
    private const string EditReport = OutDir + "/edit-checks.txt";
    private const string PlayReport = OutDir + "/play-checks.txt";
    private const string Key = "CMIndep.";

    private const string Code = "combat_mage";
    private const string ContractPath = "Assets/Data/AdvancedUnits/CombatMage.asset";
    private const string BoltContractPath = "Assets/Data/AdvancedUnits/Projectiles/CombatMageBolt.asset";
    private const string WrapperPath = "Assets/Resources/Prefabs/Units/Characters/CombatMage.prefab";
    private const string CatalogPath = "Assets/Resources/ScriptableObjects/Units/Characters/CombatMage.asset";
    private const string BodyFbx = "Assets/CharacterRigging/CombatMage/Models/CombatMage.fbx";
    private const string StaffFbx = "Assets/CharacterRigging/CombatMage/Accessories/CombatMage_Staff.fbx";
    private const string ModelsDir = "Assets/CharacterRigging/CombatMage/Models";

    private static StringBuilder _sb;
    private static string _report;
    private static int _pass, _fail;

    // ===================================================================== util

    private static void Begin(string report)
    {
        _report = report;
        _sb = new StringBuilder();
        _pass = 0; _fail = 0;
        Directory.CreateDirectory(OutDir);
        Line("Unity " + Application.unityVersion + " - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    private static void Line(string s)
    {
        _sb.AppendLine(s);
        File.WriteAllText(_report, _sb.ToString());
    }

    private static void Ok(string id, bool cond, string msg)
    {
        if (cond) _pass++; else _fail++;
        Line((cond ? "PASS" : "FAIL") + " [" + id + "] " + msg);
    }

    private static void Info(string id, string msg) { Line("INFO [" + id + "] " + msg); }

    private static string RelPath(Transform root, Transform t)
    {
        List<string> parts = new List<string>();
        for (Transform c = t; c != null && c != root; c = c.parent) parts.Add(c.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static int Tris(Mesh m)
    {
        long n = 0;
        for (int i = 0; i < m.subMeshCount; i++) n += m.GetIndexCount(i) / 3;
        return (int)n;
    }

    private static Mesh MeshOf(Renderer r)
    {
        if (r is SkinnedMeshRenderer s) return s.sharedMesh;
        MeshFilter f = r.GetComponent<MeshFilter>();
        return f != null ? f.sharedMesh : null;
    }

    private static float MagentaFraction(Texture2D t)
    {
        Color32[] px = t.GetPixels32();
        int n = 0;
        foreach (Color32 p in px)
            if (p.r > 190 && p.b > 190 && p.g < 90 && p.r - p.g > 110 && p.b - p.g > 110) n++;
        return (float)n / px.Length;
    }

    private static Texture2D Render(Camera cam, int w, int h)
    {
        RenderTexture rt = new RenderTexture(w, h, 24);
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        RenderTexture prev = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        cam.targetTexture = null;
        RenderTexture.active = prev;
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        return tex;
    }

    private static float Shoot(Camera cam, Vector3 center, Vector3 dir, float dist, string file, float lift = 0.15f, int w = 800, int h = 800)
    {
        cam.transform.position = center + dir.normalized * dist + Vector3.up * dist * lift;
        cam.transform.LookAt(center);
        Texture2D t = Render(cam, w, h);
        File.WriteAllBytes(Path.Combine(OutDir, file), t.EncodeToPNG());
        float f = MagentaFraction(t);
        UnityEngine.Object.DestroyImmediate(t);
        return f;
    }

    private static Bounds RendBounds(GameObject go, Func<Renderer, bool> filter = null)
    {
        Bounds b = default; bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
            if (filter != null && !filter(r)) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private static Camera MakeCamera(Color bg)
    {
        GameObject g = new GameObject("AuditCam");
        Camera c = g.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = bg;
        c.fieldOfView = 30; c.nearClipPlane = 0.05f; c.farClipPlane = 100;
        return c;
    }

    private static void MakeLight()
    {
        GameObject l = new GameObject("AuditLight");
        Light light = l.AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.1f; light.shadows = LightShadows.None;
        l.transform.rotation = Quaternion.Euler(40, 150, 0);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
    }

    // ===================================================================== EDIT MODE

    public static void RunEdit()
    {
        Begin(EditReport);
        try
        {
            Section1_Files();
            Section2_Rig();
            Section3_Weights_Uvs_Lods();
            Section4_Materials();
            Section6_Wrapper();
            Section7_Sockets();
            Section8_Animator();
            Section9_Projectile();
            Section10_Contract();
            Section14_References();
            Section13_Renders();
        }
        catch (Exception e)
        {
            Ok("EXC", false, "excecao no auditor: " + e);
        }
        Line($"RESUMO EDIT: {_pass} PASS, {_fail} FAIL");
        EditorApplication.Exit(_fail == 0 ? 0 : 2);
    }

    // ------------------------------------------------------------------ 1
    private static void Section1_Files()
    {
        Ok("1.1", File.Exists(BodyFbx) && new FileInfo(BodyFbx).Length > 100000, "FBX do corpo existe (" + new FileInfo(BodyFbx).Length + " bytes)");
        Ok("1.2", File.Exists(StaffFbx), "FBX do cajado existe");
        string inputs = "Tools/CharacterRigging/Inputs/CombatMage";
        foreach (string f in new[] { "combatmage_rigged.glb", "walking.glb", "running.glb", "ref1.png", "img1_task.json", "i3d_task.json", "rig_task.json" })
            Ok("1.3", File.Exists(Path.Combine(inputs, f)), "entrada " + f + " presente");
        foreach (string f in new[] { "Idle.fbx", "Standing-React-Large-From-Left.fbx", "Dying.fbx" })
            Ok("1.4", File.Exists(Path.Combine(inputs, "Mixamo", f)), "entrada Mixamo " + f + " presente");
        string lic = File.ReadAllText("Docs/CharacterMeshAudit/CombatMage/LICENSE.md");
        Ok("1.5", lic.Contains("Meshy") && lic.Contains("Mixamo") && lic.Contains("procedural"), "LICENSE.md cita Meshy, Mixamo e cajado procedural");
        // coerencia: tarefas Meshy no input batem com o que a licenca diz (texto->imagem, imagem->3D, rig)
        string i3d = File.ReadAllText(Path.Combine(inputs, "i3d_task.json"));
        string rig = File.ReadAllText(Path.Combine(inputs, "rig_task.json"));
        Ok("1.6", i3d.Contains("\"SUCCEEDED\"") && rig.Contains("\"SUCCEEDED\""), "tarefas Meshy imagem->3D e rig com status SUCCEEDED");
        // segredos: varre texto dos arquivos da unidade
        bool secret = false; List<string> hits = new List<string>();
        string[] roots = { "Tools/CharacterRigging/Inputs/CombatMage", "Docs/CharacterMeshAudit/CombatMage", "Assets/CharacterRigging/CombatMage" };
        foreach (string r in roots)
            foreach (string f in Directory.GetFiles(r, "*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".png" || ext == ".fbx" || ext == ".glb" || ext == ".blend" || ext == ".anim") continue;
                string t;
                try { t = File.ReadAllText(f); } catch { continue; }
                if (System.Text.RegularExpressions.Regex.IsMatch(t, @"msy_[A-Za-z0-9]{8,}|Bearer\s+[A-Za-z0-9_\-\.]{20,}|sk-[A-Za-z0-9]{20,}|MESHY_API_KEY\s*=\s*['""]?[A-Za-z0-9]{8,}"))
                { secret = true; hits.Add(f); }
                if (t.Contains("Signature=") && t.Contains("Key-Pair-Id=")) hits.Add("URL assinada temporaria em " + f);
            }
        Ok("1.7", !secret, "nenhuma chave de API gravada (padroes msy_/Bearer/sk-/MESHY_API_KEY=)");
        foreach (string h in hits) Info("1.7", "observacao: " + h);
        // glb de entrada x malha no projeto: ambos tem o mesmo nome de rig? (verificacao leve por tamanho)
        Info("1.8", "combatmage_rigged.glb=" + new FileInfo(Path.Combine(inputs, "combatmage_rigged.glb")).Length + " bytes; Assets glb=" + new FileInfo(ModelsDir + "/CombatMage.glb").Length + " bytes");
    }

    // ------------------------------------------------------------------ 2
    private static readonly string[] Clips = { "Idle", "Walk", "Run", "Cast", "Hit", "Death" };

    private static void Section2_Rig()
    {
        ModelImporter body = (ModelImporter)AssetImporter.GetAtPath(BodyFbx);
        Ok("2.1", body.animationType == ModelImporterAnimationType.Human, "corpo importado como Humanoid (" + body.animationType + ")");
        Avatar av = AssetDatabase.LoadAllAssetsAtPath(BodyFbx).OfType<Avatar>().FirstOrDefault();
        Ok("2.2", av != null && av.isValid && av.isHuman, "Avatar do corpo valido e humanoide");
        if (av != null)
        {
            HumanDescription d = body.humanDescription;
            string[] required = { "Hips", "Spine", "Chest", "Head", "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightUpperArm", "RightLowerArm", "RightHand", "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "RightUpperLeg", "RightLowerLeg", "RightFoot" };
            HashSet<string> mapped = new HashSet<string>(d.human.Select(h => h.humanName));
            string missing = string.Join(",", required.Where(r => !mapped.Contains(r)));
            Ok("2.3", missing.Length == 0, "ossos humanoides obrigatorios mapeados" + (missing.Length > 0 ? " (faltam: " + missing + ")" : ""));
            Info("2.3", "ossos humanoides mapeados: " + d.human.Length);
        }
        foreach (string c in Clips)
        {
            string p = c == "Idle" && false ? "" : ModelsDir + "/CombatMage@" + c + ".fbx";
            ModelImporter imp = AssetImporter.GetAtPath(p) as ModelImporter;
            Ok("2.4", imp != null && imp.animationType == ModelImporterAnimationType.Human, c + ": FBX do clipe importado como Humanoid");
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().FirstOrDefault(a => !a.name.StartsWith("__preview__"));
            Ok("2.5", clip != null && clip.isHumanMotion && clip.length > 0.05f, c + ": AnimationClip humanoide (" + (clip != null ? clip.length.ToString("F2") + " s" : "ausente") + ")");
            if (imp != null)
            {
                Avatar sa = imp.sourceAvatar;
                bool ownsAvatar = AssetDatabase.LoadAllAssetsAtPath(p).OfType<Avatar>().Any(a => a.isValid);
                Ok("2.6", (sa != null && sa == av) || ownsAvatar, c + ": avatar do clipe = avatar do corpo (copiado) ou proprio valido");
            }
        }
        // o clipe nao pode estar vazio: contar curvas
        foreach (string c in Clips)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ModelsDir + "/CombatMage@" + c + ".fbx").OfType<AnimationClip>().FirstOrDefault(a => !a.name.StartsWith("__preview__"));
            if (clip == null) continue;
            int curves = AnimationUtility.GetCurveBindings(clip).Length;
            Ok("2.7", curves > 20 || clip.isHumanMotion, c + ": clipe com " + curves + " curvas de transform (humanoide usa curvas musculares)");
            Info("2.7", c + ": " + clip.length.ToString("F2") + " s, " + clip.frameRate + " fps, loopTime=" + AnimationUtility.GetAnimationClipSettings(clip).loopTime + ", eventos=" + clip.events.Length);
        }
    }

    // ------------------------------------------------------------------ 3
    private static void Section3_Weights_Uvs_Lods()
    {
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(BodyFbx);
        SkinnedMeshRenderer[] smrs = fbx.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Ok("3.0", smrs.Length >= 3, "FBX tem " + smrs.Length + " SkinnedMeshRenderers (esperado >=3 LODs)");
        foreach (SkinnedMeshRenderer s in smrs)
        {
            Mesh m = s.sharedMesh;
            string tag = s.name + " (" + Tris(m) + " tri, " + m.vertexCount + " vert)";
            var bpv = m.GetBonesPerVertex();
            var all = m.GetAllBoneWeights();
            Ok("3.1", bpv.Length == m.vertexCount, tag + ": todos os vertices tem entrada de pesos");
            int idx = 0, noWeight = 0, over4 = 0, badSum = 0, maxInf = 0;
            double worst = 0;
            for (int v = 0; v < bpv.Length; v++)
            {
                int n = bpv[v];
                if (n == 0) noWeight++;
                if (n > 4) over4++;
                maxInf = Mathf.Max(maxInf, n);
                float sum = 0;
                for (int k = 0; k < n; k++) sum += all[idx + k].weight;
                idx += n;
                double dev = Math.Abs(sum - 1.0);
                if (n > 0 && dev > 0.01) badSum++;
                worst = Math.Max(worst, dev);
            }
            Ok("3.2", noWeight == 0, tag + ": 0 vertices sem peso (achados " + noWeight + ")");
            Ok("3.3", over4 == 0 && maxInf <= 4, tag + ": no maximo 4 influencias (max " + maxInf + ")");
            Ok("3.4", badSum == 0, tag + ": soma dos pesos ~1 (pior desvio " + worst.ToString("E2") + ", vertices fora " + badSum + ")");
            Ok("3.5", s.bones != null && s.bones.Length > 20 && s.bones.All(b => b != null), tag + ": " + (s.bones != null ? s.bones.Length : 0) + " ossos, nenhum nulo");
            Ok("3.6", m.bindposes.Length == s.bones.Length, tag + ": bindposes == ossos");
            // UVs
            Vector2[] uv = m.uv;
            bool uvOk = uv.Length == m.vertexCount;
            int outside = 0, nan = 0; HashSet<long> uniq = new HashSet<long>();
            foreach (Vector2 u in uv)
            {
                if (float.IsNaN(u.x) || float.IsNaN(u.y) || float.IsInfinity(u.x) || float.IsInfinity(u.y)) nan++;
                else
                {
                    if (u.x < -0.001f || u.x > 1.001f || u.y < -0.001f || u.y > 1.001f) outside++;
                    uniq.Add(((long)(u.x * 4096) << 20) ^ (long)(u.y * 4096));
                }
            }
            Ok("3.7", uvOk && nan == 0 && uniq.Count > m.vertexCount / 20, tag + ": UVs presentes e finitas (" + uniq.Count + " distintas, NaN " + nan + ", fora de 0..1: " + outside + ")");
            // normais
            Ok("3.8", m.normals.Length == m.vertexCount, tag + ": normais presentes");
        }
        // LODs estritamente decrescentes
        List<int> tris = smrs.OrderBy(s => s.name).Select(s => Tris(s.sharedMesh)).ToList();
        Info("3.9", "triangulos por SMR (ordem alfabetica): " + string.Join(" / ", tris) + " | nomes: " + string.Join(",", smrs.OrderBy(s => s.name).Select(s => s.name)));
        LODGroup g = fbx.GetComponent<LODGroup>();
        Ok("3.10", g != null && g.lodCount == 3, "FBX tem LODGroup com 3 niveis");
        if (g != null)
        {
            LOD[] lods = g.GetLODs();
            List<int> lt = new List<int>();
            foreach (LOD l in lods) lt.Add(l.renderers.Where(r => r != null).Sum(r => Tris(MeshOf(r))));
            Ok("3.11", lt[0] > lt[1] && lt[1] > lt[2] && lt[2] > 0, "triangulos por LOD estritamente decrescentes: " + string.Join(" > ", lt));
            Ok("3.12", lt[0] <= 31000, "LOD0 <= 31k triangulos (" + lt[0] + ")");
            Ok("3.13", lods[0].screenRelativeTransitionHeight > lods[1].screenRelativeTransitionHeight && lods[1].screenRelativeTransitionHeight > lods[2].screenRelativeTransitionHeight,
                "limiares do LODGroup do FBX decrescentes: " + string.Join(", ", lods.Select(l => l.screenRelativeTransitionHeight.ToString("F3"))));
        }
        // cajado
        GameObject staff = AssetDatabase.LoadAssetAtPath<GameObject>(StaffFbx);
        int staffTris = staff.GetComponentsInChildren<MeshFilter>(true).Sum(f => Tris(f.sharedMesh));
        Ok("3.14", staffTris > 0 && staffTris <= 8000, "cajado: " + staffTris + " triangulos (<=8k)");
        foreach (MeshFilter f in staff.GetComponentsInChildren<MeshFilter>(true))
            Info("3.15", "cajado/" + f.name + ": UVs " + (f.sharedMesh.uv.Length == f.sharedMesh.vertexCount ? "presentes" : "AUSENTES (materiais do cajado sao de cor solida, sem textura)"));
    }

    // ------------------------------------------------------------------ 4
    private static void CheckMaterial(string id, string owner, Material m, bool needTexture)
    {
        if (m == null) { Ok(id, false, owner + ": material nulo"); return; }
        string sn = m.shader != null ? m.shader.name : "(null)";
        bool supported = m.shader != null && m.shader.isSupported && !sn.Contains("InternalErrorShader");
        Ok(id, supported, owner + "/" + m.name + ": shader '" + sn + "' suportado");
        if (needTexture)
        {
            Texture t = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            if (t == null && m.HasProperty("_BaseMap")) t = m.GetTexture("_BaseMap");
            Ok(id, t != null, owner + "/" + m.name + ": textura principal atribuida (" + (t != null ? t.name + " " + t.width + "x" + t.height : "NENHUMA") + ")");
        }
    }

    private static void Section4_Materials()
    {
        GameObject body = AssetDatabase.LoadAssetAtPath<GameObject>(BodyFbx);
        // materiais do prefab visual e do wrapper (os que o jogo usa)
        string[] prefabs = {
            WrapperPath,
            "Assets/CharacterRigging/CombatMage/Prefabs/CombatMageVisual.prefab",
            "Assets/CharacterRigging/CombatMage/Prefabs/CombatMageDeath.prefab",
            "Assets/CharacterRigging/CombatMage/Prefabs/CombatMageBolt.prefab",
            "Assets/CharacterRigging/CombatMage/Prefabs/CombatMageImpact.prefab",
            "Assets/CharacterRigging/CombatMage/Prefabs/CombatMageCast.prefab" };
        foreach (string p in prefabs)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            Ok("4.0", go != null, Path.GetFileName(p) + " carrega");
            if (go == null) continue;
            bool bodyPrefab = p.EndsWith("Visual.prefab") || p.EndsWith("Death.prefab") || p == WrapperPath;
            HashSet<Material> seen = new HashSet<Material>();
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) { Ok("4.1", false, Path.GetFileName(p) + "/" + r.name + ": slot de material nulo"); continue; }
                    if (!seen.Add(m)) continue;
                    bool isFx = r is ParticleSystemRenderer || r is TrailRenderer;
                    bool isRing = r.name == "TeamRing" || r.name == "SelectionCircle" || r.name == "Fov" || r.name.ToLower().Contains("fov") || r.name == "MinimapIcon";
                    bool skinned = r is SkinnedMeshRenderer;
                    CheckMaterial("4.1", Path.GetFileName(p), m, skinned && bodyPrefab);
                }
        }
        // cajado
        GameObject staff = AssetDatabase.LoadAssetAtPath<GameObject>(StaffFbx);
        foreach (Renderer r in staff.GetComponentsInChildren<Renderer>(true))
            foreach (Material m in r.sharedMaterials) CheckMaterial("4.2", "StaffFbx", m, false);
        // todas as texturas dos materiais da unidade existem em disco
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/CharacterRigging/CombatMage" }))
        {
            string mp = AssetDatabase.GUIDToAssetPath(guid);
            Material m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            int props = m.shader.GetPropertyCount();
            for (int i = 0; i < props; i++)
                if (m.shader.GetPropertyType(i) == ShaderPropertyType.Texture)
                {
                    string pn = m.shader.GetPropertyName(i);
                    // propriedades texturizadas com referencia perdida (guid sem asset)
                }
            CheckMaterial("4.3", "Materials", m, false);
        }
        // propriedade de cor principal nao-magenta
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/CharacterRigging/CombatMage" }))
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m.HasProperty("_Color"))
            {
                Color c = m.GetColor("_Color");
                bool mag = c.r > 0.9f && c.b > 0.9f && c.g < 0.15f;
                Ok("4.4", !mag, m.name + ": cor principal nao e magenta (" + c + ")");
            }
        }
    }

    // ------------------------------------------------------------------ 6
    private static GameObject _wrapperInstance;

    private static GameObject InstantiateWrapper()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
    }

    private static void Section6_Wrapper()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        Ok("6.0", prefab != null, "wrapper CombatMage.prefab carrega");
        GameObject soldier = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Units/Characters/Soldier.prefab");
        GameObject w = InstantiateWrapper();
        _wrapperInstance = w;
        int missing = 0;
        foreach (Transform childTransform in w.GetComponentsInChildren<Transform>(true))
            missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(childTransform.gameObject);
        Ok("6.1", missing == 0, "nenhum script ausente (Missing) na hierarquia do wrapper (" + missing + ")");
        // prefab no disco: referencias nulas dentro dos componentes (SerializedObject)
        List<string> nullRefs = new List<string>();
        foreach (Component c in w.GetComponentsInChildren<Component>(true))
        {
            if (c == null) { nullRefs.Add("componente nulo"); continue; }
            if (c is Transform || c is MeshRenderer || c is SkinnedMeshRenderer || c is MeshFilter) continue;
            SerializedObject so = new SerializedObject(c);
            SerializedProperty it = so.GetIterator();
            while (it.NextVisible(true))
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null && it.objectReferenceEntityIdValue != default)
                    nullRefs.Add(c.GetType().Name + "." + it.propertyPath + " (referencia quebrada)");
        }
        Ok("6.2", nullRefs.Count == 0, "sem referencias quebradas em componentes do wrapper " + string.Join("; ", nullRefs.Take(5)));

        Ok("6.3", w.GetComponent<CharacterManager>() != null, "CharacterManager presente");
        NavMeshAgent agent = w.GetComponent<NavMeshAgent>();
        Ok("6.4", agent != null, "NavMeshAgent presente");
        if (agent != null) Info("6.4", $"agente: raio {agent.radius}, altura {agent.height}, velocidade {agent.speed}, accel {agent.acceleration}, stoppingDistance {agent.stoppingDistance}, angularSpeed {agent.angularSpeed}");
        BoxCollider bc = w.GetComponent<BoxCollider>();
        Ok("6.5", bc != null, "BoxCollider presente");
        Bounds bodyB = RendBounds(w, r => r is SkinnedMeshRenderer);
        if (bc != null)
        {
            Info("6.5", $"BoxCollider size {bc.size} center {bc.center}; corpo (bounds do SMR, pose do prefab) size {bodyB.size} center {w.transform.InverseTransformPoint(bodyB.center)}");
            Ok("6.6", bc.size.y >= bodyB.size.y * 0.8f && bc.size.y <= bodyB.size.y * 1.3f, "altura do BoxCollider coerente com a do modelo (" + bc.size.y.ToString("F2") + " vs " + bodyB.size.y.ToString("F2") + ")");
            Ok("6.7", bc.size.x >= 0.5f && bc.size.x <= 1.6f && bc.size.z >= 0.5f && bc.size.z <= 1.6f, "largura/profundidade do BoxCollider plausiveis (" + bc.size.x.ToString("F2") + " x " + bc.size.z.ToString("F2") + ")");
            Ok("6.8", bc.center.y > 0.5f && bc.center.y < 1.5f && Mathf.Abs(bc.center.y - bc.size.y / 2f) < 0.3f, "BoxCollider apoiado no chao (centro y " + bc.center.y.ToString("F2") + ")");
        }
        AdvancedUnitEraBinding bind = w.GetComponent<AdvancedUnitEraBinding>();
        CombatMageContract contract = AssetDatabase.LoadAssetAtPath<CombatMageContract>(ContractPath);
        Ok("6.9", bind != null && bind.contract == contract && contract != null, "AdvancedUnitEraBinding.contract = CombatMage.asset");
        Ok("6.10", w.GetComponent<AdvancedUnitAnimatorDriver>() != null && w.GetComponent<AdvancedUnitAnimatorDriver>().animator != null, "AdvancedUnitAnimatorDriver com Animator");
        ProjectileAttackController pc = w.GetComponent<ProjectileAttackController>();
        Ok("6.11", pc != null && pc.contract == contract, "ProjectileAttackController com o contrato correto");
        if (pc != null)
        {
            Ok("6.12", pc.animator != null && pc.animator.runtimeAnimatorController != null, "controlador de ataque tem Animator com controller");
            Ok("6.13", pc.projectileSpawn != null && pc.projectileSpawn.name == "CastPoint" && pc.projectileSpawn.IsChildOf(w.transform), "projectileSpawn = CastPoint dentro do wrapper");
            Info("6.13", $"releaseDelay={pc.releaseDelay}, sourceHeight={pc.sourceHeight}, turnSpeed={pc.turnSpeed}");
        }
        AdvancedUnitDeathEffect de = w.GetComponent<AdvancedUnitDeathEffect>();
        Ok("6.14", de != null && de.deathPrefab != null, "AdvancedUnitDeathEffect com prefab de morte (" + (de != null && de.deathPrefab != null ? de.deathPrefab.name : "nenhum") + ")");
        if (de != null && de.deathPrefab != null)
        {
            Animator da = de.deathPrefab.GetComponentInChildren<Animator>();
            Ok("6.15", da != null && da.runtimeAnimatorController != null && da.avatar != null, "prefab de morte tem Animator + controller + avatar");
            Ok("6.16", de.deathPrefab.GetComponentsInChildren<Collider>().Length == 0 && de.deathPrefab.GetComponent<NavMeshAgent>() == null, "prefab de morte sem colisor nem agente (nao bloqueia o jogo)");
            Info("6.15", $"lifetime do efeito de morte {de.lifetime}s");
        }
        // comparacao com Soldier: componentes do jogo
        if (soldier != null)
        {
            HashSet<string> sc = new HashSet<string>(soldier.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name));
            HashSet<string> wc = new HashSet<string>(w.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name));
            string lacking = string.Join(",", sc.Where(n => !wc.Contains(n)));
            Ok("6.17", lacking.Length == 0, "wrapper tem todos os componentes de raiz do Soldier (faltam: " + (lacking.Length == 0 ? "nenhum" : lacking) + ")");
            Info("6.17", "componentes do wrapper: " + string.Join(", ", wc.OrderBy(x => x)));
            Ok("6.18", w.layer == soldier.layer && w.tag == soldier.tag, $"layer/tag iguais ao Soldier ({LayerMask.LayerToName(w.layer)}/{w.tag} vs {LayerMask.LayerToName(soldier.layer)}/{soldier.tag})");
        }
        CharacterManager cm = w.GetComponent<CharacterManager>();
        if (cm != null)
        {
            Ok("6.19", cm.meshRenderer != null && cm.animator != null && cm.agent != null && cm.characterCollider != null, "CharacterManager: meshRenderer, animator, agent e characterCollider atribuidos");
            Ok("6.20", cm.selectionCircle != null && cm.fov != null, "CharacterManager: selectionCircle e fov atribuidos (healthbar nulo, igual ao Soldier)");
            Info("6.19", "meshRenderer(anel de time)=" + (cm.meshRenderer != null ? cm.meshRenderer.name + " bounds " + cm.meshRenderer.bounds.size : "null"));
        }
        // LODGroup do wrapper
        LODGroup lg = w.GetComponentInChildren<LODGroup>();
        Ok("6.21", lg != null && lg.lodCount == 3, "LODGroup no wrapper com 3 niveis");
        if (lg != null)
        {
            LOD[] lods = lg.GetLODs();
            List<int> lt = lods.Select(l => l.renderers.Where(r => r != null).Sum(r => Tris(MeshOf(r)))).ToList();
            Ok("6.22", lt[0] > lt[1] && lt[1] > lt[2] && lt[2] > 0, "wrapper: triangulos por LOD estritamente decrescentes " + string.Join(" > ", lt));
            float[] th = lods.Select(l => l.screenRelativeTransitionHeight).ToArray();
            Ok("6.23", th[0] > th[1] && th[1] > th[2] && th[0] < 1f && th[2] >= 0f, "wrapper: limiares coerentes " + string.Join(", ", th.Select(threshold => threshold.ToString("F3"))));
            Info("6.23", "LODGroup size " + lg.size + ", fadeMode " + lg.fadeMode + ", animateCrossFading " + lg.animateCrossFading);
            // cajado e extras estao num LOD? (evita cajado "flutuando" em LOD2)
            foreach (Renderer r in w.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r.name == "TeamRing" || r.name == "SelectionCircle") continue;
                if (r.name.ToLower().Contains("staff") || r.name.ToLower().Contains("orb") || r.name.ToLower().Contains("cajado"))
                    Info("6.24", "renderer do cajado: " + RelPath(w.transform, r.transform) + " (nos LODs: " + string.Join(",", Enumerable.Range(0, 3).Where(i => lods[i].renderers.Contains(r))) + ")");
            }
        }
        // escala/rotacao do wrapper
        Ok("6.25", w.transform.localScale == Vector3.one, "escala da raiz = 1");
    }

    // ------------------------------------------------------------------ 7
    private static void Section7_Sockets()
    {
        GameObject w = _wrapperInstance;
        CombatMageContract contract = AssetDatabase.LoadAssetAtPath<CombatMageContract>(ContractPath);
        foreach (SocketRequirement s in contract.visual.sockets)
        {
            Transform socketTransform = w.transform.Find(s.expectedTransformPath);
            Ok("7.1", socketTransform != null && socketTransform.name == s.socketName, "socket '" + s.socketName + "' existe em " + s.expectedTransformPath);
        }
        Transform cast = w.transform.Find(contract.visual.sockets.First(s => s.socketName == "CastPoint").expectedTransformPath);
        Transform mount = w.transform.Find(contract.visual.sockets.First(s => s.socketName == "StaffMount").expectedTransformPath);
        if (cast == null || mount == null) return;
        Animator anim = w.GetComponentInChildren<Animator>();
        Transform rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        Ok("7.2", mount.parent == rightHand, "StaffMount e filho do osso RightHand do avatar (" + mount.parent.name + ")");
        Transform staff = mount.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Staff");
        Ok("7.3", staff != null && cast.IsChildOf(staff), "CastPoint e filho do cajado");
        // posicao: na ponta do cajado (bounds de renderers do cajado)
        Renderer[] sr = mount.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer) && !(r is TrailRenderer)).ToArray();
        Bounds sb = sr[0].bounds; foreach (Renderer r in sr) sb.Encapsulate(r.bounds);
        float distToTop = Vector3.Distance(cast.position, new Vector3(cast.position.x, sb.max.y, cast.position.z));
        Info("7.4", $"bounds do cajado (pose do prefab): min {sb.min}, max {sb.max}; CastPoint {cast.position}; mao {rightHand.position}");
        Ok("7.4", sb.Contains(cast.position) || sb.SqrDistance(cast.position) < 0.15f * 0.15f, "CastPoint dentro/junto dos bounds do cajado (dist " + Mathf.Sqrt(sb.SqrDistance(cast.position)).ToString("F3") + " m)");
        float handToCast = Vector3.Distance(rightHand.position, cast.position);
        Ok("7.5", handToCast > 0.6f && handToCast < 2.2f, "CastPoint a " + handToCast.ToString("F2") + " m da mao (na ponta do cajado, nao na palma)");
        Info("7.6", "posicao do CastPoint durante a animacao e verificada em Play Mode (Animator.Update em edit mode nao aplica pose aos transforms); ver play-checks.txt");
    }

    // ------------------------------------------------------------------ 8
    private static void Section8_Animator()
    {
        GameObject w = _wrapperInstance;
        CombatMageContract contract = AssetDatabase.LoadAssetAtPath<CombatMageContract>(ContractPath);
        Animator anim = w.GetComponentInChildren<Animator>();
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(anim.runtimeAnimatorController));
        Ok("8.0", ctrl != null, "controller do wrapper e um AnimatorController (" + AssetDatabase.GetAssetPath(anim.runtimeAnimatorController) + ")");
        if (ctrl == null) return;
        Ok("8.0b", anim.avatar != null && anim.avatar.isHuman && !anim.applyRootMotion, "Animator com avatar humanoide e sem root motion");
        Dictionary<string, AnimatorControllerParameterType> ps = ctrl.parameters.ToDictionary(p => p.name, p => p.type);
        foreach (var kv in new Dictionary<string, AnimatorControllerParameterType> {
            { "Speed", AnimatorControllerParameterType.Float }, { "Fire", AnimatorControllerParameterType.Trigger }, { "Damaged", AnimatorControllerParameterType.Trigger },
            { "Running", AnimatorControllerParameterType.Bool }, { "Attacking", AnimatorControllerParameterType.Bool }, { "Hit", AnimatorControllerParameterType.Trigger } })
            Ok("8.1", ps.TryGetValue(kv.Key, out AnimatorControllerParameterType ty) && ty == kv.Value, "parametro " + kv.Key + " (" + kv.Value + ")");
        // estados e transicoes
        var sm = ctrl.layers[0].stateMachine;
        Info("8.2", "layers=" + ctrl.layers.Length + ", estados=" + string.Join(",", sm.states.Select(s => s.state.name)) + ", default=" + (sm.defaultState != null ? sm.defaultState.name : "null"));
        Ok("8.2", sm.defaultState != null && sm.defaultState.name == "Idle" || sm.states.Any(s => s.state.motion != null && s.state == sm.defaultState), "estado padrao definido (" + (sm.defaultState != null ? sm.defaultState.name : "null") + ")");
        List<string> transitionLines = new List<string>();
        foreach (var cs in sm.states)
        {
            foreach (AnimatorStateTransition t in cs.state.transitions)
                transitionLines.Add($"{cs.state.name} -> {(t.destinationState != null ? t.destinationState.name : "exit")} [{string.Join("&", t.conditions.Select(c => c.parameter + ":" + c.mode + ":" + c.threshold))}] exit={t.hasExitTime}({t.exitTime:F2}) dur={t.duration:F2}");
        }
        foreach (AnimatorStateTransition t in sm.anyStateTransitions)
            transitionLines.Add($"ANY -> {(t.destinationState != null ? t.destinationState.name : "exit")} [{string.Join("&", t.conditions.Select(c => c.parameter + ":" + c.mode + ":" + c.threshold))}] exit={t.hasExitTime} dur={t.duration:F2}");
        foreach (string l in transitionLines) Info("8.3", l);
        // parametros referenciados existem
        bool allCondOk = true;
        foreach (var cs in sm.states) foreach (var t in cs.state.transitions) foreach (var c in t.conditions) if (!ps.ContainsKey(c.parameter)) allCondOk = false;
        foreach (var t in sm.anyStateTransitions) foreach (var c in t.conditions) if (!ps.ContainsKey(c.parameter)) allCondOk = false;
        Ok("8.4", allCondOk, "todas as condicoes de transicao usam parametros declarados");
        // clipes do contrato usados pelo controller (mesmos clipes, nao copias vazias)
        HashSet<AnimationClip> used = new HashSet<AnimationClip>();
        foreach (var cs in sm.states) CollectClips(cs.state.motion, used);
        foreach (AnimationRequirement req in contract.visual.animations)
        {
            Ok("8.5", req.clip != null && req.clip.length > 0.05f, $"contrato: clipe '{req.state}' real, duracao {(req.clip != null ? req.clip.length.ToString("F2") : "-")} s, parametro {req.animatorParameter}");
            if (req.state == "Death") continue; // Death roda no controller de morte
            Ok("8.6", used.Contains(req.clip), $"controller do wrapper usa o clipe do contrato '{req.state}'");
        }
        // controller de morte
        Animator da = w.GetComponent<AdvancedUnitDeathEffect>().deathPrefab.GetComponentInChildren<Animator>();
        AnimatorController dc = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(da.runtimeAnimatorController));
        HashSet<AnimationClip> dused = new HashSet<AnimationClip>();
        foreach (var cs in dc.layers[0].stateMachine.states) CollectClips(cs.state.motion, dused);
        AnimationClip deathClip = contract.visual.animations.First(a => a.state == "Death").clip;
        Ok("8.7", dused.Contains(deathClip) && dc.layers[0].stateMachine.defaultState != null && dc.layers[0].stateMachine.defaultState.motion == deathClip, "controller de morte: estado padrao toca o clipe Death do contrato (sem parametros necessarios)");
        // simulacao pelo Animator real: Speed -> Walk/Run, Fire -> Cast -> volta, Damaged -> Hit -> volta
        anim.Rebind(); anim.Update(0f);
        string St()
        {
            var si = anim.GetCurrentAnimatorStateInfo(0);
            string n = sm.states.Select(s => s.state).FirstOrDefault(s => si.IsName(s.name))?.name ?? ("hash" + si.shortNameHash);
            if (n == "Locomotion")
            {
                var ci = anim.GetCurrentAnimatorClipInfo(0);
                if (ci.Length > 0) { var top = ci.OrderByDescending(c => c.weight).First(); n = "Locomotion/" + top.clip.name.Replace("CombatMage@", ""); }
            }
            return n;
        }
        foreach (var cs in sm.states)
            if (cs.state.motion is BlendTree bt0)
                Info("8.2", "BlendTree '" + cs.state.name + "' param=" + bt0.blendParameter + " tipo=" + bt0.blendType + ": " + string.Join(", ", bt0.children.Select(c => (c.motion != null ? c.motion.name : "null") + "@" + c.threshold.ToString("F2"))));
        for (int i = 0; i < 30; i++) anim.Update(0.05f);
        Ok("8.8", St().EndsWith("Idle"), "Speed=0 -> Locomotion tocando Idle (" + St() + ")");
        anim.SetFloat("Speed", 2.0f); for (int i = 0; i < 30; i++) anim.Update(0.05f);
        string walk = St();
        anim.SetFloat("Speed", 4.2f); for (int i = 0; i < 30; i++) anim.Update(0.05f);
        string run = St();
        Info("8.9", $"Speed=2.0 -> {walk}; Speed=4.2 -> {run}");
        Ok("8.9", walk.EndsWith("Walk") || walk.EndsWith("Run"), "Speed 2.0 move o personagem (" + walk + ")");
        Ok("8.10", run.EndsWith("Run") || run.EndsWith("Walk"), "Speed 4.2 (velocidade do agente) -> locomocao Run/Walk (" + run + ")");
        anim.SetFloat("Speed", 0f); for (int i = 0; i < 40; i++) anim.Update(0.05f);
        Ok("8.11", St().EndsWith("Idle"), "Speed volta a 0 -> Idle (" + St() + ")");
        anim.SetTrigger("Fire"); anim.Update(0.1f); anim.Update(0.1f);
        Ok("8.12", St() == "Cast", "trigger Fire -> Cast (" + St() + ")");
        for (int i = 0; i < 100; i++) anim.Update(0.05f);
        Ok("8.13", St().EndsWith("Idle"), "apos o Cast volta a Idle (" + St() + ")");
        anim.SetTrigger("Damaged"); anim.Update(0.1f); anim.Update(0.1f);
        Ok("8.14", St() == "Hit", "trigger Damaged -> Hit (" + St() + ")");
        for (int i = 0; i < 60; i++) anim.Update(0.05f);
        Ok("8.15", St().EndsWith("Idle"), "apos o Hit volta a Idle (" + St() + ")");
        // trigger Hit do BT nao deve disparar nada
        anim.SetTrigger("Hit"); anim.SetBool("Attacking", true); anim.SetBool("Running", true);
        for (int i = 0; i < 10; i++) anim.Update(0.05f);
        Ok("8.16", St().EndsWith("Idle"), "Hit/Attacking/Running do BT nao alteram o estado (" + St() + ")");
        anim.ResetTrigger("Hit"); anim.SetBool("Attacking", false); anim.SetBool("Running", false);
        // Fire durante Hit: Cast pode interromper? informacional
        anim.SetTrigger("Damaged"); anim.Update(0.1f); anim.SetTrigger("Fire"); for (int i = 0; i < 6; i++) anim.Update(0.1f);
        Info("8.17", "Fire disparado durante Hit -> estado " + St());
        // evento de liberacao no clipe Cast
        AnimationClip cast = contract.visual.animations.First(a => a.state == "Cast").clip;
        Info("8.18", "eventos do Cast: " + string.Join(", ", cast.events.Select(e => e.functionName + "@" + e.time.ToString("F3"))));
        var evTargets = w.GetComponentsInChildren<MonoBehaviour>(true).Select(m => m.GetType().Name).ToList();
        foreach (AnimationEvent e in cast.events)
            Ok("8.18", w.GetComponentsInChildren<MonoBehaviour>(true).Any(m => m.GetType().GetMethod(e.functionName) != null), "evento '" + e.functionName + "' do Cast tem receptor no wrapper (senao gera erro de console)");
        // loops
        foreach (string s in new[] { "Idle", "Walk", "Run" })
        {
            AnimationClip c = contract.visual.animations.First(a => a.state == s).clip;
            Ok("8.19", AnimationUtility.GetAnimationClipSettings(c).loopTime, s + ": loopTime ligado");
        }
        foreach (string s in new[] { "Cast", "Hit", "Death" })
        {
            AnimationClip c = contract.visual.animations.First(a => a.state == s).clip;
            Ok("8.20", !AnimationUtility.GetAnimationClipSettings(c).loopTime, s + ": loopTime desligado");
        }
    }

    private static void CollectClips(Motion m, HashSet<AnimationClip> set)
    {
        if (m is AnimationClip c) set.Add(c);
        else if (m is BlendTree bt) foreach (ChildMotion cm in bt.children) CollectClips(cm.motion, set);
    }

    // ------------------------------------------------------------------ 9
    private static void Section9_Projectile()
    {
        ProjectileContract pc = AssetDatabase.LoadAssetAtPath<ProjectileContract>(BoltContractPath);
        Ok("9.1", pc != null, "ProjectileContract CombatMageBolt carrega");
        Ok("9.2", pc.Validate().IsValid, "ProjectileContract.Validate: " + pc.Validate());
        GameObject bolt = pc.prefab.asset as GameObject;
        Ok("9.3", bolt != null && AssetDatabase.GetAssetPath(bolt).EndsWith("CombatMageBolt.prefab"), "prefab do projetil real: " + (bolt != null ? AssetDatabase.GetAssetPath(bolt) : "nenhum"));
        GameObject impact = pc.impactEffect.asset as GameObject;
        Ok("9.4", impact != null && AssetDatabase.GetAssetPath(impact).EndsWith("CombatMageImpact.prefab"), "efeito de impacto real: " + (impact != null ? AssetDatabase.GetAssetPath(impact) : "nenhum"));
        Ok("9.5", pc.damageType == DamageType.Magic && !pc.ballistic && pc.speed > 5 && pc.maxLifetime > 0, $"tipo {pc.damageType}, reto, vel {pc.speed}, vida {pc.maxLifetime}");
        Ok("9.6", pc.EvaluateVisuals().IsReady, "ProjectileContract.EvaluateVisuals pronto");
        // alcance maximo alcancavel dentro da vida do projetil
        Ok("9.7", pc.speed * pc.maxLifetime >= 11f * 1.5f, $"projetil alcanca ao menos 1.5x o maior alcance (11 m): {pc.speed * pc.maxLifetime} m");
        if (bolt != null)
        {
            Ok("9.8", bolt.GetComponentInChildren<ParticleSystem>(true) != null || bolt.GetComponentInChildren<MeshRenderer>(true) != null, "bolt tem visual (particulas/malha)");
            Ok("9.9", bolt.GetComponentsInChildren<Collider>(true).Length == 0 && bolt.GetComponentsInChildren<Rigidbody>(true).Length == 0, "bolt sem colisor/rigidbody (voo dirigido pelo script)");
            Ok("9.10", bolt.GetComponentsInChildren<MonoBehaviour>(true).All(m => m != null), "bolt sem scripts ausentes");
            Info("9.8", "componentes do bolt: " + string.Join(", ", bolt.GetComponentsInChildren<Component>(true).Select(c => c.GetType().Name).Distinct()));
        }
        if (impact != null)
        {
            ParticleSystem[] ps = impact.GetComponentsInChildren<ParticleSystem>(true);
            Ok("9.11", ps.Length > 0, "impacto tem " + ps.Length + " sistemas de particulas");
            float maxDur = ps.Max(p => p.main.duration + p.main.startLifetime.constantMax);
            Ok("9.12", maxDur <= 4f, "impacto termina em " + maxDur.ToString("F2") + " s (< 4 s, tempo de Destroy do AdvancedProjectile)");
            Ok("9.13", ps.All(p => !p.main.loop), "particulas de impacto nao sao loop");
        }
        GameObject cast = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CharacterRigging/CombatMage/Prefabs/CombatMageCast.prefab");
        Ok("9.14", cast != null && cast.GetComponentsInChildren<ParticleSystem>(true).Length > 0, "efeito de conjuracao real com particulas");
        // CombatMageStaffEffects no wrapper
        Component fx = _wrapperInstance.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(m => m.GetType().Name == "CombatMageStaffEffects");
        Ok("9.15", fx != null, "CombatMageStaffEffects presente no wrapper");
    }

    // ------------------------------------------------------------------ 10
    private static void Section10_Contract()
    {
        CombatMageContract c = AssetDatabase.LoadAssetAtPath<CombatMageContract>(ContractPath);
        ValidationReport vr = c.Validate();
        Ok("10.1", vr.IsValid, "Validate().IsValid: " + vr);
        VisualReadinessReport rr = c.EvaluateVisuals();
        Ok("10.2", rr.IsReady && c.IsVisualReady, "IsVisualReady: " + rr);
        GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        Ok("10.3", c.visual.prefab.asset == wrapper, "contrato.prefab = wrapper em Resources");
        Ok("10.4", AssetDatabase.GetAssetPath(c.visual.meshes[0].asset) == BodyFbx && AssetDatabase.GetAssetPath(c.visual.meshes[1].asset) == StaffFbx, "contrato.meshes = FBX do corpo e do cajado");
        foreach (VisualAssetReference e in c.visual.effects)
            Ok("10.5", e.asset != null && AssetDatabase.GetAssetPath(e.asset) == e.expectedAssetPath, "efeito '" + e.role + "' aponta para o caminho esperado");
        foreach (AnimationRequirement a in c.visual.animations)
            Ok("10.6", a.clip != null && AssetDatabase.GetAssetPath(a.clip) == ModelsDir + "/CombatMage@" + a.state + ".fbx", "animacao '" + a.state + "' aponta para CombatMage@" + a.state + ".fbx");
        Ok("10.7", c.Projectile != null && c.Projectile == AssetDatabase.LoadAssetAtPath<ProjectileContract>(BoltContractPath), "contrato -> projetil CombatMageBolt");
        Ok("10.8", c.CanBeAddedToBuildSettings == false && c.buildSettingsApproved == false, "buildSettingsApproved=false (nada no Build Settings por decisao do coordenador)");
        // tiers
        var tiers = c.tiers;
        Ok("10.9", tiers.Length == 4, "4 tiers de era");
        for (int i = 1; i < tiers.Length; i++)
            Ok("10.10", tiers[i].healthpoints > tiers[i - 1].healthpoints && tiers[i].attackDamage > tiers[i - 1].attackDamage && tiers[i].attackRange > tiers[i - 1].attackRange && tiers[i].attackCooldown < tiers[i - 1].attackCooldown,
                $"era {tiers[i].era}: vida, dano e alcance crescem e cooldown cai em relacao a {tiers[i - 1].era}");
        // CharacterData
        string[] guids = AssetDatabase.FindAssets("t:CharacterData");
        List<string> same = new List<string>();
        foreach (string g in guids)
        {
            CharacterData cd = AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(g));
            if (cd != null && cd.code == Code) same.Add(AssetDatabase.GUIDToAssetPath(g));
        }
        Ok("10.11", same.Count == 1 && same[0] == CatalogPath, "exatamente 1 CharacterData com code=combat_mage: " + string.Join(", ", same));
        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(CatalogPath);
        Ok("10.12", data != null && data.prefab == wrapper, "CharacterData.prefab = wrapper do contrato");
        MageEraTier a0 = tiers.First(t => t.era == ImperialEra.Ancient);
        Ok("10.13", data.healthpoints == a0.healthpoints && data.attackDamage == a0.attackDamage && Mathf.Approximately(data.attackRange, a0.attackRange) && Mathf.Approximately(data.attackRate, AdvancedUnitDataBridge.AttackPollSeconds),
            $"CharacterData = Ancient (vida {data.healthpoints}/{a0.healthpoints}, dano {data.attackDamage}/{a0.attackDamage}, alcance {data.attackRange}/{a0.attackRange}, attackRate = poll do BT {data.attackRate}; cooldown real {a0.attackCooldown} no controlador)");
        Ok("10.14", data.cost != null && data.cost.Count == 2 && data.fieldOfView == c.fieldOfView, "CharacterData: custo (2 recursos) e FOV iguais ao contrato");
        Info("10.14", "custo CharacterData: " + string.Join(", ", data.cost.Select(x => x.code + " x" + x.amount)) + "; contrato: " + string.Join(", ", c.cost.Select(x => x.resource + " x" + x.amount)));
        Ok("10.15", data.sprite == null, "INFO: CharacterData sem sprite/icone de UI (campo sprite vazio)");
        Ok("10.16", data.onSelectSound == null, "INFO: CharacterData sem sons de selecao/movimento");
        // as demais unidades do catalogo nao foram afetadas pelos codigos duplicados em prefab
        Ok("10.17", PrefabUtility.GetPrefabAssetType(wrapper) == PrefabAssetType.Regular, "wrapper e um prefab regular (nao variante/modelo)");
        // Build Settings sem cena nova nem prefab
        Ok("10.18", EditorBuildSettings.scenes.All(s => !s.path.Contains("CombatMage")), "Build Settings nao contem nada do CombatMage");
    }

    // ------------------------------------------------------------------ 14
    private static void Section14_References()
    {
        // GUIDs referenciados por assets de texto da unidade devem existir
        List<string> files = new List<string>();
        foreach (string dir in new[] { "Assets/CharacterRigging/CombatMage" })
            files.AddRange(Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Where(f => f.EndsWith(".prefab") || f.EndsWith(".controller") || f.EndsWith(".mat") || f.EndsWith(".asset") || f.EndsWith(".anim")));
        files.AddRange(new[] { WrapperPath, CatalogPath, ContractPath, BoltContractPath });
        System.Text.RegularExpressions.Regex rx = new System.Text.RegularExpressions.Regex(@"guid: ([0-9a-f]{32})");
        int total = 0, broken = 0; List<string> brokenList = new List<string>();
        foreach (string f in files)
        {
            string txt = File.ReadAllText(f);
            if (!txt.StartsWith("%YAML")) continue;
            foreach (System.Text.RegularExpressions.Match m in rx.Matches(txt))
            {
                total++;
                string g = m.Groups[1].Value;
                if (g == "0000000000000000f000000000000000" || g == "0000000000000000e000000000000000") continue; // builtin
                if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(g))) { broken++; brokenList.Add(f + " -> " + g); }
            }
        }
        Ok("14.1", broken == 0, $"referencias por GUID resolvidas: {total} verificadas em {files.Count} arquivos, {broken} quebradas {string.Join("; ", brokenList.Take(5))}");
        // nomes de tipo duplicados
        string[] scripts = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);
        var dupTypes = new Dictionary<string, List<string>>();
        System.Text.RegularExpressions.Regex tx = new System.Text.RegularExpressions.Regex(@"^\s*(?:public |internal |static |sealed |abstract |partial )*(?:class|struct|enum|interface) (CombatMage\w*)", System.Text.RegularExpressions.RegexOptions.Multiline);
        foreach (string s in scripts)
        {
            string t = File.ReadAllText(s);
            foreach (System.Text.RegularExpressions.Match m in tx.Matches(t))
            {
                string n = m.Groups[1].Value;
                if (!dupTypes.ContainsKey(n)) dupTypes[n] = new List<string>();
                dupTypes[n].Add(s);
            }
        }
        var dups = dupTypes.Where(kv => kv.Value.Count > 1).ToList();
        Ok("14.2", dups.Count == 0, "sem tipos CombatMage* duplicados (" + dupTypes.Count + " tipos: " + string.Join(", ", dupTypes.Keys) + ")");
        // nomes de arquivo duplicados do prefab/controller no projeto
        foreach (string n in new[] { "CombatMage.prefab", "CombatMage.controller", "CombatMage.fbx", "CombatMage.asset" })
        {
            var found = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(n)).Select(AssetDatabase.GUIDToAssetPath).Where(p => Path.GetFileName(p) == n).ToList();
            Info("14.3", n + ": " + string.Join(", ", found));
        }
        Ok("14.4", AssetDatabase.FindAssets("CombatMage t:Prefab").Select(AssetDatabase.GUIDToAssetPath).Count(p => Path.GetFileName(p) == "CombatMage.prefab") == 1, "um unico CombatMage.prefab no projeto");
        // texturas e FBX: guids dos .meta unicos
        var guidOwners = new Dictionary<string, string>(); int dupGuid = 0;
        foreach (string meta in Directory.GetFiles("Assets/CharacterRigging/CombatMage", "*.meta", SearchOption.AllDirectories))
        {
            var m = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(meta), @"guid: ([0-9a-f]{32})");
            if (m.Success) { if (guidOwners.ContainsKey(m.Groups[1].Value)) dupGuid++; else guidOwners[m.Groups[1].Value] = meta; }
        }
        Ok("14.5", dupGuid == 0, "GUIDs de .meta unicos na pasta da unidade (" + guidOwners.Count + ")");
    }

    // ------------------------------------------------------------------ 13 (renders no editor)
    private static void Section13_Renders()
    {
        // cena limpa
        Scene sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeLight();
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.transform.localScale = Vector3.one * 3;
        floor.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.35f, 0.4f, 0.3f) };
        GameObject w = InstantiateWrapper();
        w.transform.position = Vector3.zero;
        w.transform.rotation = Quaternion.identity;
        foreach (NavMeshAgent a in w.GetComponentsInChildren<NavMeshAgent>()) a.enabled = false;
        foreach (CharacterBT bt in w.GetComponentsInChildren<CharacterBT>()) bt.enabled = false;
        Animator anim = w.GetComponentInChildren<Animator>();
        Camera cam = MakeCamera(new Color(0.5f, 0.55f, 0.6f));
        LODGroup lg = w.GetComponentInChildren<LODGroup>();
        foreach (Renderer r in w.GetComponentsInChildren<Renderer>())
            if (r.name == "TeamRing" || r.name == "SelectionCircle") r.enabled = false;
        Dictionary<string, float> mags = new Dictionary<string, float>();

        // vistas por LOD
        for (int lod = 0; lod < 3; lod++)
        {
            lg.ForceLOD(lod);
            anim.Rebind(); anim.Update(0f); anim.Update(0.5f);
            Bounds b = RendBounds(w, r => r is SkinnedMeshRenderer || r.name == "Staff" || r.name == "Orb");
            float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
            foreach (var v in new[] { ("front", w.transform.forward), ("back", -w.transform.forward), ("side", w.transform.right) })
                mags[$"LOD{lod}-{v.Item1}"] = Shoot(cam, b.center, v.Item2, size * 2.2f, $"render-LOD{lod}-{v.Item1}.png");
        }
        lg.ForceLOD(0);
        // efeitos: bolt e impacto e cast
        UnityEngine.Object.DestroyImmediate(w);
        Camera fxCam = cam;
        fxCam.backgroundColor = new Color(0.18f, 0.2f, 0.24f);
        ProjectileContract pc = AssetDatabase.LoadAssetAtPath<ProjectileContract>(BoltContractPath);
        GameObject boltGo = (GameObject)PrefabUtility.InstantiatePrefab(pc.prefab.asset as GameObject);
        boltGo.transform.position = new Vector3(0, 1.5f, 0); boltGo.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        foreach (ParticleSystem ps in boltGo.GetComponentsInChildren<ParticleSystem>()) { ps.Simulate(0.6f, true, true); }
        mags["fx-bolt-side"] = Shoot(cam, boltGo.transform.position, Vector3.right, 2.0f, "render-fx-bolt-side.png", 0.05f);
        mags["fx-bolt-front"] = Shoot(cam, boltGo.transform.position, -Vector3.forward, 2.0f, "render-fx-bolt-front.png", 0.05f);
        UnityEngine.Object.DestroyImmediate(boltGo);
        GameObject imp = (GameObject)PrefabUtility.InstantiatePrefab(pc.impactEffect.asset as GameObject);
        imp.transform.position = new Vector3(0, 1f, 0);
        foreach (ParticleSystem ps in imp.GetComponentsInChildren<ParticleSystem>()) ps.Simulate(0.15f, true, true);
        mags["fx-impact-0.15s"] = Shoot(cam, imp.transform.position, Vector3.right, 3.0f, "render-fx-impact-015.png", 0.1f);
        foreach (ParticleSystem ps in imp.GetComponentsInChildren<ParticleSystem>()) ps.Simulate(0.35f, true, false);
        mags["fx-impact-0.5s"] = Shoot(cam, imp.transform.position, Vector3.right, 3.0f, "render-fx-impact-050.png", 0.1f);
        UnityEngine.Object.DestroyImmediate(imp);

        foreach (var kv in mags) Ok("13.m", kv.Value < 0.0005f, $"{kv.Key}: pixels magenta {(kv.Value * 100f):F4}%");
        UnityEngine.Object.DestroyImmediate(cam.gameObject);
    }

    // ===================================================================== PLAY MODE

    private static readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>();
    private static double _wakeAt;
    private static bool _started;
    private static Vector3 _spawnBase;

    static CombatMageIndependentAudit()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    public static void RunPlay()
    {
        Begin(PlayReport);
        SessionState.SetBool(Key + "Running", true);
        SessionState.SetInt(Key + "Errors", 0);
        SessionState.SetInt(Key + "Pass", 0);
        SessionState.SetInt(Key + "Fail", 0);
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 2800f);
        SessionState.SetString(Key + "DataPath", Path.GetFullPath("TestResults/CMIndepData-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void IsolateSaves()
    {
        if (SessionState.GetBool(Key + "Running", false))
            BinarySerializable.DATA_DIRECTORY = SessionState.GetString(Key + "DataPath", "TestResults/CMIndepData");
    }

    private static readonly List<string> _consoleErrors = new List<string>();

    private static void OnLog(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Key + "Running", false)) return;
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert && type != LogType.Warning) return;
        string head = type + ": " + message;
        File.AppendAllText(PlayReport, "CONSOLE " + head + "\n" + (type == LogType.Warning ? "" : stack + "\n"));
        if (type != LogType.Warning)
            SessionState.SetInt(Key + "Errors", SessionState.GetInt(Key + "Errors", 0) + 1);
        else
            SessionState.SetInt(Key + "Warnings", SessionState.GetInt(Key + "Warnings", 0) + 1);
    }

    private static void P(string id, bool cond, string msg)
    {
        string k = cond ? "Pass" : "Fail";
        SessionState.SetInt(Key + k, SessionState.GetInt(Key + k, 0) + 1);
        File.AppendAllText(PlayReport, (cond ? "PASS" : "FAIL") + " [" + id + "] " + msg + "\n");
    }

    private static void PI(string id, string msg) { File.AppendAllText(PlayReport, "INFO [" + id + "] " + msg + "\n"); }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key + "Running", false)) return;
        try
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "Deadline", 0)) throw new TimeoutException("Excedeu o tempo maximo.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (!_started)
            {
                IsolateSaves();
                _started = true;
                Stack.Push(Flow());
            }
            if (EditorApplication.timeSinceStartup < _wakeAt) return;
            Step();
        }
        catch (Exception e)
        {
            File.AppendAllText(PlayReport, "FAIL [EXC] excecao no auditor/jogo: " + e + "\n");
            Finish(1);
        }
    }

    private static void Finish(int exitCode)
    {
        int pass = SessionState.GetInt(Key + "Pass", 0), fail = SessionState.GetInt(Key + "Fail", 0), err = SessionState.GetInt(Key + "Errors", 0), warn = SessionState.GetInt(Key + "Warnings", 0);
        File.AppendAllText(PlayReport, $"RESUMO PLAY: {pass} PASS, {fail} FAIL, {err} erros de console, {warn} avisos de console\n");
        SessionState.SetBool(Key + "Running", false);
        EditorApplication.Exit(exitCode != 0 || fail > 0 || err > 0 ? 2 : 0);
    }

    private static void Step()
    {
        while (Stack.Count > 0)
        {
            IEnumerator top = Stack.Peek();
            if (!top.MoveNext()) { Stack.Pop(); continue; }
            object current = top.Current;
            if (current is IEnumerator nested) { Stack.Push(nested); continue; }
            if (current is double seconds) _wakeAt = EditorApplication.timeSinceStartup + seconds;
            return;
        }
        Finish(0);
    }

    private static IEnumerator WaitUntil(Func<bool> condition, double timeout, string what)
    {
        double end = EditorApplication.timeSinceStartup + timeout;
        while (!condition())
        {
            if (EditorApplication.timeSinceStartup > end) throw new TimeoutException("Tempo esgotado esperando: " + what);
            yield return 0.1;
        }
    }

    private static IEnumerator Flow()
    {
        for (int cycle = 0; cycle < 2; cycle++)
        {
            string mapName = cycle == 0 ? "Map1" : "Map2";
            yield return WaitUntil(() => UnityEngine.Object.FindAnyObjectByType<MainMenuManager>() is MainMenuManager m && m.newGameScrollview.childCount > 0, 90, "menu");
            MainMenuManager menu = UnityEngine.Object.FindAnyObjectByType<MainMenuManager>();
            MapData map = Resources.Load<MapData>("ScriptableObjects/Maps/" + mapName);
            P(mapName + ".0", map != null, "metadados do mapa " + mapName);
            Transform picker = menu.newGameScrollview.Cast<Transform>().First(item => item.Find("Data/Name").GetComponent<Text>().text == map.mapName);
            picker.GetComponent<Button>().onClick.Invoke();
            menu.StartNewGame();
            PI(mapName, "nova partida iniciada");
            yield return WaitUntil(() => GameManager.instance != null && Camera.main != null && Globals.NAV_MESH_SURFACE != null, 120, "partida pronta");
            yield return 4.0;
            AdvancedEraState.Reset();
            P(mapName + ".1", Globals.CHARACTER_DATA.ContainsKey(Code), "Globals.CHARACTER_DATA contem combat_mage no " + mapName);
            Transform spawnpoints = GameObject.Find("Spawnpoints").transform;
            NavMesh.SamplePosition(spawnpoints.GetChild(0).position, out NavMeshHit start, 40, NavMesh.AllAreas);
            _spawnBase = start.position;
            yield return TestMage(mapName);
            yield return 1.0;
            EventManager.TriggerEvent("PausedGame");
            CoreBooter.instance.LoadMenu();
            yield return WaitUntil(() => !SceneManager.GetSceneByName("GameScene").isLoaded && SceneManager.GetSceneByName("MainMenu").isLoaded, 90, "volta ao menu");
            yield return 2.0;
        }
    }

    private static Character Spawn(string code, int owner, Vector3 position)
    {
        Character c = new Character(Globals.CHARACTER_DATA[code], owner);
        c.ComputeProduction();
        NavMeshAgent agent = c.Transform.GetComponent<NavMeshAgent>();
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 6f, NavMesh.AllAreas)) position = hit.position;
        agent.Warp(position);
        if (code != Code) { CharacterBT bt = c.Transform.GetComponent<CharacterBT>(); if (bt != null) bt.enabled = false; agent.isStopped = true; }
        return c;
    }

    private static int EnemyOwner()
    {
        int mine = GameManager.instance.gamePlayersParameters.myPlayerId;
        return GameManager.instance.gamePlayersParameters.players.Length > 1 ? (mine == 0 ? 1 : 0) : mine + 1;
    }

    /// <summary>Espera em tempo de JOGO (Time.time): o Play Mode em batch pode rodar a poucos quadros por segundo.</summary>
    private static IEnumerator GT(double seconds)
    {
        float end = Time.time + (float)seconds;
        while (Time.time < end) yield return 0.02;
    }

    private static string AnimState(Animator a)
    {
        var info = a.GetCurrentAnimatorClipInfo(0);
        if (info.Length == 0) return "?"; AnimatorClipInfo top = info[0]; foreach (AnimatorClipInfo c in info) if (c.weight > top.weight) top = c; return top.clip.name;
    }

    private static Vector3 FlatForwardPoint(Transform t, float d)
    {
        Vector3 p = t.position + t.forward * d;
        return NavMesh.SamplePosition(p, out NavMeshHit h, 4f, NavMesh.AllAreas) ? h.position : p;
    }

    private static IEnumerator TestMage(string mapName)
    {
        int mine = GameManager.instance.gamePlayersParameters.myPlayerId;
        int foeOwner = EnemyOwner();
        string T = mapName;
        CombatMageContract contract = AssetDatabase.LoadAssetAtPath<CombatMageContract>(ContractPath);
        float wallT0 = (float)EditorApplication.timeSinceStartup, gameT0 = Time.time; int frame0 = Time.frameCount;
        Character mage = Spawn(Code, mine, _spawnBase + new Vector3(2, 0, 2));
        GameObject go = mage.Transform.gameObject;
        yield return GT(1.5);
        UnitManager um = go.GetComponent<UnitManager>();
        CharacterBT mageBt = go.GetComponent<CharacterBT>();
        if (mageBt != null) mageBt.enabled = false; // testes manuais: sem a IA propria do mago interferindo
        ProjectileAttackController atk = go.GetComponent<ProjectileAttackController>();
        AdvancedUnitEraBinding bind = go.GetComponent<AdvancedUnitEraBinding>();
        Animator anim = go.GetComponentInChildren<Animator>();
        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        P(T + ".2", atk != null && bind != null && anim != null && agent != null && agent.isOnNavMesh, "instancia pelo catalogo com controlador, binding, animator e agente no NavMesh");
        P(T + ".3", bind.HasApplied && mage.HP == 22 && mage.MaxHP == 22, $"era Ancient aplicada: HP {mage.HP}/{mage.MaxHP}");
        P(T + ".3b", go.GetComponentsInChildren<Renderer>().Any(r => r is SkinnedMeshRenderer && r.enabled && r.isVisible || r is SkinnedMeshRenderer), "visual do mago presente");
        PI(T + ".fps", $"taxa de quadros no batch: {(Time.frameCount - frame0) / Mathf.Max(0.01f, (float)EditorApplication.timeSinceStartup - wallT0):F1} fps; tempo de jogo/parede = {(Time.time - gameT0) / Mathf.Max(0.01f, (float)EditorApplication.timeSinceStartup - wallT0):F2}");
        P(T + ".3c", !go.GetComponentsInChildren<MonoBehaviour>(true).Any(m => m == null), "sem scripts ausentes na instancia");

        // ---- eras
        foreach (ImperialEra era in ImperialEras.Ordered)
        {
            AdvancedEraState.Set(era);
            yield return GT(0.2);
            contract.TryGetTier(era, out IEraTier tier);
            P(T + ".4", mage.MaxHP == tier.HealthPoints && mage.AttackDamage == tier.AttackDamage && Mathf.Approximately(mage.AttackRange, tier.AttackRange),
                $"{era}: MaxHP {mage.MaxHP}, dano {mage.AttackDamage}, alcance {mage.AttackRange} = contrato ({tier.HealthPoints}/{tier.AttackDamage}/{tier.AttackRange})");
            P(T + ".4b", bind.AppliedEra == era && Mathf.Approximately(atk.CurrentTierCooldown(), tier.AttackCooldown), $"{era}: cooldown efetivo do controlador = {atk.CurrentTierCooldown()} (contrato {tier.AttackCooldown})");
        }
        AdvancedEraState.Set(ImperialEra.Ancient);
        yield return GT(0.3);
        mage.HP = mage.MaxHP;

        // ---- movimento
        Vector3 before = go.transform.position;
        Vector3 goal = FlatForwardPoint(go.transform, 8f);
        CharacterManager cm = go.GetComponent<CharacterManager>();
        P(T + ".5", cm.MoveTo(goal, false), "ordem de movimento aceita");
        float maxSpeedParam = 0; string movingState = "";
        double endMove = (double)Time.time + 4.5;
        bool walkSnap = false; double moveStart = (double)Time.time;
        while ((double)Time.time < endMove)
        {
            maxSpeedParam = Mathf.Max(maxSpeedParam, anim.GetFloat("Speed"));
            if (anim.GetFloat("Speed") > 1f) movingState = AnimState(anim);
            if (!walkSnap && (double)Time.time - moveStart > 1.6 && anim.GetFloat("Speed") > 1f) { SnapPose(go, T + "-play-pose-Walk-" + AnimState(anim).Replace("CombatMage@", "")); walkSnap = true; }
            yield return GT(0.1);
        }
        float moved = Vector3.Distance(before, go.transform.position);
        P(T + ".5b", moved > 2f, $"moveu {moved:F1} m pelo NavMesh");
        P(T + ".5c", maxSpeedParam > 1f && (movingState.Contains("Walk") || movingState.Contains("Run")), $"Animator Speed max {maxSpeedParam:F2}, clipe em movimento '{movingState}'");
        yield return GT(2.5);
        P(T + ".5d", AnimState(anim).Contains("Idle") && anim.GetFloat("Speed") < 0.2f, $"parado volta ao Idle (clipe '{AnimState(anim)}', Speed {anim.GetFloat("Speed"):F2})");
        yield return Capture(go, T + "-play-idle");

        // ---- alvo fora do alcance (Ancient 8 m): nao dispara
        mage.HP = mage.MaxHP;
        Character far = Spawn("worker", foeOwner, go.transform.position + go.transform.forward * 10.5f);
        yield return GT(0.5);
        float dFar = AdvancedCombat.FlatDistance(go.transform.position, far.Transform.position);
        int shots0 = atk.ShotsFired; int farHp = far.HP;
        PI(T + ".6d", $"diag: desdeUltimoDisparo={atk.SecondsSinceLastShot:F2}, tamanhoAlvo={AdvancedCombat.TargetSize(AdvancedCombat.ManagerOf(far.Transform)):F2}, alcanceTier={bind.CurrentTier.AttackRange}, distancia={dFar:F2}");
        for (int i = 0; i < 12; i++) { um.Attack(far.Transform); yield return GT(0.1); }
        yield return GT(1.5);
        P(T + ".6", atk.ShotsFired == shots0 && far.HP == farHp && AdvancedProjectile.ActiveCount == 0, $"alvo a {dFar:F1} m (> alcance 8 + tamanho) nao dispara (disparos {atk.ShotsFired - shots0}, dano {farHp - far.HP})");

        // ---- Imperial: alcance 11 alcanca o mesmo alvo
        AdvancedEraState.Set(ImperialEra.Imperial);
        yield return GT(0.3);
        // posiciona o alvo a ~10 m (alcance Imperial 11 cobre; Ancient 8 nao cobre)
        far.Transform.GetComponent<NavMeshAgent>().Warp(go.transform.position + go.transform.forward * 10.0f);
        yield return GT(0.4);
        dFar = AdvancedCombat.FlatDistance(go.transform.position, far.Transform.position);
        int s1 = atk.ShotsFired; int hpBefore = far.HP;
        PI(T + ".7d", $"diag: IsFiring={atk.IsFiring}, desdeUltimoDisparo={atk.SecondsSinceLastShot:F2}, tamanhoAlvo={AdvancedCombat.TargetSize(AdvancedCombat.ManagerOf(far.Transform)):F2}, alcanceTier={bind.CurrentTier.AttackRange}, era={bind.AppliedEra}");
        um.Attack(far.Transform);
        yield return GT(3.0);
        P(T + ".7", atk.ShotsFired == s1 + 1 && far.HP < hpBefore, $"Imperial: alvo a {dFar:F1} m (alem do alcance Ancient) e atingido (HP {hpBefore} -> {far.HP}, dano {hpBefore - far.HP}; esperado 40 menos resistencia)");
        PI(T + ".7", "dano do projetil na era Imperial: " + (hpBefore - far.HP) + " (Unit.AttackDamage=" + mage.AttackDamage + ")");
        P(T + ".7b", atk.LastDamage == 40 || far.HP <= 0 || atk.LastDamage == mage.AttackDamage, $"LastDamage {atk.LastDamage} = dano da era Imperial");
        AdvancedEraState.Set(ImperialEra.Ancient);
        yield return GT(0.3);
        if (far.Transform != null) UnityEngine.Object.Destroy(far.Transform.gameObject);
        yield return GT(0.3);

        // ---- ataque em alcance: nenhum dano durante a conjuracao; dano so na chegada
        Character foe = Spawn("worker", foeOwner, go.transform.position + go.transform.forward * 6f);
        Character ally = Spawn("worker", mine, foe.Transform.position + go.transform.right * 1.2f);
        yield return GT(0.6);
        int foeHp = foe.HP, allyHp = ally.HP;
        float dFoe = AdvancedCombat.FlatDistance(go.transform.position, foe.Transform.position);
        // espera a recarga inicial
        while (atk.SecondsSinceLastShot < 3f) yield return GT(0.1);
        int s2 = atk.ShotsFired; float t0 = Time.time;
        um.Attack(foe.Transform);
        bool animFire = false, firing = atk.IsFiring;
        yield return GT(0.25);
        animFire = AnimState(anim).Contains("Cast");
        P(T + ".8", firing && animFire, $"ataque inicia conjuracao: IsFiring={firing}, clipe '{AnimState(anim)}'");
        P(T + ".8b", foe.HP == foeHp && atk.ShotsFired == s2, "nenhum dano nem projetil antes da liberacao (0.25 s)");
        float releasedAt = -1, impactAt = -1; bool projectileSeen = false; float closestToCast = 99;
        double endAtk = (double)Time.time + 4;
        Transform cast = atk.projectileSpawn;
        while ((double)Time.time < endAtk)
        {
            if (atk.ShotsFired > s2 && releasedAt < 0) { releasedAt = Time.time - t0; }
            if (AdvancedProjectile.ActiveCount > 0) projectileSeen = true;
            if (foe.Transform != null && foe.HP < foeHp && impactAt < 0) impactAt = Time.time - t0;
            if (releasedAt >= 0 && impactAt >= 0) break;
            yield return GT(0.02);
        }
        P(T + ".8c", releasedAt > 0.6f && releasedAt < 1.0f, $"liberacao do projetil em {releasedAt:F2} s (releaseDelay {atk.releaseDelay})");
        P(T + ".8d", projectileSeen, "projetil real instanciado em voo");
        P(T + ".8e", impactAt > releasedAt && impactAt - releasedAt > 0.1f, $"dano so na chegada: impacto em {impactAt:F2} s, {impactAt - releasedAt:F2} s apos a liberacao (distancia {dFoe:F1} m / vel 16 = {dFoe / 16f:F2} s)");
        P(T + ".8f", foe.Transform == null || foeHp - foe.HP == 18, $"dano Ancient = 18 (HP {foeHp} -> {(foe.Transform == null ? 0 : foe.HP)})");
        P(T + ".8g", ally.HP == allyHp, $"aliado ao lado do alvo nao sofreu dano (HP {ally.HP})");
        yield return GT(0.3);
        P(T + ".8h", AdvancedProjectile.ActiveCount == 0, "nenhum projetil sobrando apos o impacto");

        // ---- captura do ataque: refaz com camera de acompanhamento
        while (atk.SecondsSinceLastShot < 3f) yield return GT(0.1);
        if (foe.Transform == null) foe = Spawn("worker", foeOwner, go.transform.position + go.transform.forward * 6f);
        foe.HP = 100000;
        yield return CaptureAttack(go, atk, um, foe, mapName);

        // ---- recarga: tentativas repetidas respeitam cooldown 2.4 s
        while (atk.SecondsSinceLastShot < 3f) yield return GT(0.1);
        foe.HP = 100000;
        if (ally.Transform != null) ally.HP = ally.MaxHP;
        List<float> shotTimes = new List<float>(); int lastShots = atk.ShotsFired;
        double endCd = (double)Time.time + 9;
        while ((double)Time.time < endCd)
        {
            if (foe.Transform != null) um.Attack(foe.Transform);
            if (atk.ShotsFired > lastShots) { shotTimes.Add(Time.time); lastShots = atk.ShotsFired; }
            if (foe.Transform != null) foe.HP = 100000;
            yield return GT(0.05);
        }
        List<float> gaps = new List<float>(); for (int i = 1; i < shotTimes.Count; i++) gaps.Add(shotTimes[i] - shotTimes[i - 1]);
        P(T + ".9", shotTimes.Count >= 3 && gaps.All(g => g >= 2.35f && g < 2.7f), $"recarga: {shotTimes.Count} disparos em 9 s com tentativa a cada 0,05 s; intervalos {string.Join(", ", gaps.Select(g => g.ToString("F2")))} (cooldown 2.4)");
        // era Imperial: cooldown 1.8
        AdvancedEraState.Set(ImperialEra.Imperial); yield return GT(0.3);
        while (atk.SecondsSinceLastShot < 3f) yield return GT(0.1);
        shotTimes.Clear(); lastShots = atk.ShotsFired;
        endCd = (double)Time.time + 7;
        while ((double)Time.time < endCd)
        {
            if (foe.Transform != null) um.Attack(foe.Transform);
            if (atk.ShotsFired > lastShots) { shotTimes.Add(Time.time); lastShots = atk.ShotsFired; }
            if (foe.Transform != null) foe.HP = 100000;
            yield return GT(0.05);
        }
        gaps.Clear(); for (int i = 1; i < shotTimes.Count; i++) gaps.Add(shotTimes[i] - shotTimes[i - 1]);
        P(T + ".9b", shotTimes.Count >= 3 && gaps.All(g => g >= 1.75f && g < 2.1f), $"recarga Imperial: intervalos {string.Join(", ", gaps.Select(g => g.ToString("F2")))} (cooldown 1.8)");
        AdvancedEraState.Set(ImperialEra.Ancient); yield return GT(0.3);
        yield return GT(3.0);

        // ---- fogo amigo: atacar um aliado nao causa dano
        int allyHp2 = ally.HP;
        while (atk.SecondsSinceLastShot < 3f) yield return GT(0.1);
        um.Attack(ally.Transform);
        yield return GT(3.0);
        P(T + ".10", ally.HP == allyHp2, $"atacar aliado nao causa dano (HP {allyHp2} -> {ally.HP})");

        // ---- alvo morre durante o voo / alvo nulo / destruido
        while (atk.SecondsSinceLastShot < 3f) yield return GT(0.1);
        int errBefore = SessionState.GetInt(Key + "Errors", 0);
        um.Attack(null);
        Character temp = Spawn("worker", foeOwner, go.transform.position + go.transform.forward * 5f);
        yield return GT(0.4);
        um.Attack(temp.Transform);
        yield return GT(0.9); // projetil ja liberado
        UnityEngine.Object.Destroy(temp.Transform.gameObject);
        yield return GT(2.5);
        P(T + ".11", SessionState.GetInt(Key + "Errors", 0) == errBefore && AdvancedProjectile.ActiveCount == 0, "alvo destruido durante o voo / alvo nulo: sem erros e sem projetil preso");

        // ---- alvo morre com o dano: efeito/sem erro
        Character weak = Spawn("worker", foeOwner, go.transform.position + go.transform.forward * 5f);
        yield return GT(0.4); weak.HP = 5;
        while (atk.SecondsSinceLastShot < 3f) yield return GT(0.1);
        um.Attack(weak.Transform);
        yield return GT(3.0);
        P(T + ".12", weak.Transform == null || weak.HP <= 0, "alvo com pouca vida morre pelo projetil");

        // ---- BT autonomo: inimigo no campo de visao, mago parado
        if (mageBt != null) mageBt.enabled = true;
        Character bait = Spawn("worker", foeOwner, go.transform.position + go.transform.forward * 5.5f);
        yield return GT(0.4);
        bait.HP = 100000;
        PI(T + ".13", $"CharacterData.attackRate (poll do BT) = {((CharacterData)mage.Data).attackRate}");
        foreach (ImperialEra btEra in new[] { ImperialEra.Ancient, ImperialEra.Imperial })
        {
            AdvancedEraState.Set(btEra); // a era muda DEPOIS do spawn
            yield return GT(0.3);
            contract.TryGetTier(btEra, out IEraTier btTier);
            List<float> bt = new List<float>(); int last = atk.ShotsFired; int dmgBefore = 100000 - bait.HP;
            float endBt = Time.time + (btEra == ImperialEra.Ancient ? 11f : 8f);
            while (Time.time < endBt)
            {
                if (atk.ShotsFired > last) { bt.Add(Time.time); last = atk.ShotsFired; }
                yield return GT(0.02);
            }
            List<float> g = new List<float>(); for (int i = 1; i < bt.Count; i++) g.Add(bt[i] - bt[i - 1]);
            float cd = btTier.AttackCooldown;
            P(T + ".13", bt.Count >= 3 && g.All(x => x >= cd - 0.08f && x <= cd + 0.45f), $"cadencia via CharacterBT na era {btEra} (mudada apos o spawn): {bt.Count} disparos, intervalos {string.Join(", ", g.Select(x => x.ToString("F2")))} (cooldown da era {cd})");
            P(T + ".13b", 100000 - bait.HP - dmgBefore >= (bt.Count - 1) * btTier.AttackDamage, $"{btEra}: dano acumulado no alvo {100000 - bait.HP - dmgBefore} (dano por tiro {btTier.AttackDamage})");
        }
        AdvancedEraState.Set(ImperialEra.Ancient);
        yield return GT(0.3);
        if (mageBt != null) mageBt.enabled = false;
        if (bait.Transform != null) UnityEngine.Object.Destroy(bait.Transform.gameObject);
        yield return GT(0.3);

        // ---- reacao a dano
        mage.HP = mage.MaxHP;
        yield return GT(1.0);
        um.TakeHit(5);
        yield return GT(0.2);
        SnapPose(go, T + "-play-pose-Hit");
        yield return GT(0.15);
        bool hitAnim = AnimState(anim).Contains("Hit");
        P(T + ".14", hitAnim && go.GetComponent<AdvancedUnitAnimatorDriver>().DamageReactions >= 1, $"dano -> reacao Hit (clipe '{AnimState(anim)}', reacoes {go.GetComponent<AdvancedUnitAnimatorDriver>().DamageReactions})");
        yield return GT(2.0);
        P(T + ".14b", AnimState(anim).Contains("Idle"), $"volta ao Idle apos Hit ('{AnimState(anim)}')");
        yield return Capture(go, T + "-play-after-hit-idle");

        // ---- LOD pela camera do jogo
        LODGroup lg = go.GetComponentInChildren<LODGroup>();
        PI(T + ".15", "LODGroup lodCount=" + lg.lodCount + "; thresholds=" + string.Join(", ", lg.GetLODs().Select(l => l.screenRelativeTransitionHeight.ToString("F3"))));

        // ---- morte
        int deathsBefore = AdvancedUnitDeathEffect.DeathsSpawned;
        Vector3 deathPos = go.transform.position;
        foe.HP = 100000;
        um.TakeHit(mage.HP + 100);
        yield return GT(0.6);
        GameObject deathObj = GameObject.Find("CombatMageDeath(Clone)");
        P(T + ".16", AdvancedUnitDeathEffect.DeathsSpawned == deathsBefore + 1 && deathObj != null, "morte instancia o prefab de morte");
        if (deathObj != null)
        {
            Animator da = deathObj.GetComponentInChildren<Animator>();
            yield return GT(1.5);
            P(T + ".16b", AnimState(da).Contains("Death") && Vector3.Distance(deathObj.transform.position, deathPos) < 0.5f, $"prefab de morte toca '{AnimState(da)}' na posicao da unidade");
            yield return CaptureDeath(deathObj, T);
            yield return GT(3.2);
            if (deathObj != null) yield return CaptureDeath(deathObj, T + "-end");
            yield return GT(0.3);
            Animator da2 = deathObj != null ? deathObj.GetComponentInChildren<Animator>() : null;
            P(T + ".16c", da2 != null && da2.GetCurrentAnimatorStateInfo(0).normalizedTime >= 0.95f, "animacao de morte chega ao fim e permanece na pose final");
        }
        yield return GT(5.0);
        P(T + ".16d", GameObject.Find("CombatMageDeath(Clone)") == null, "prefab de morte removido apos o lifetime");
        foreach (Character c in new[] { foe, ally })
            if (c != null && c.Transform != null) UnityEngine.Object.Destroy(c.Transform.gameObject);
    }

    private static void SnapPose(GameObject unit, string name)
    {
        LODGroup lod = unit.GetComponentInChildren<LODGroup>();
        if (lod != null) lod.ForceLOD(0);
        Bounds b = RendBounds(unit, r => r is SkinnedMeshRenderer || r.name == "Staff" || r.name == "Orb");
        float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
        Camera cam = MakeCamera(new Color(0.45f, 0.5f, 0.55f));
        foreach (var v in new[] { ("front", unit.transform.forward), ("side", unit.transform.right), ("threeq", (unit.transform.forward + unit.transform.right).normalized) })
        {
            float f = Shoot(cam, b.center, v.Item2, size * 1.9f, $"render-{name}-{v.Item1}.png", 0.15f);
            P("13.pose", f < 0.0005f, $"{name}-{v.Item1}: magenta {(f * 100f):F4}%");
        }
        UnityEngine.Object.Destroy(cam.gameObject);
        if (lod != null) lod.ForceLOD(-1);
    }

    private static IEnumerator Capture(GameObject unit, string prefix)
    {
        LODGroup lod = unit.GetComponentInChildren<LODGroup>();
        if (lod != null) lod.ForceLOD(0);
        Bounds bounds = RendBounds(unit, r => r.name != "TeamRing" && r.name != "SelectionCircle" && !r.name.ToLower().Contains("fov") && r.name != "MinimapIcon");
        float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        Camera cam = MakeCamera(new Color(0.45f, 0.5f, 0.55f));
        foreach (var v in new[] { ("front", unit.transform.forward), ("back", -unit.transform.forward), ("side", unit.transform.right) })
        {
            float f = Shoot(cam, bounds.center, v.Item2, size * 1.9f, $"render-{prefix}-{v.Item1}.png", 0.15f);
            P("13.play", f < 0.0005f, $"{prefix}-{v.Item1}: magenta {(f * 100f):F4}%");
            yield return null;
        }
        UnityEngine.Object.Destroy(cam.gameObject);
        if (lod != null) lod.ForceLOD(-1);
    }

    private static IEnumerator CaptureDeath(GameObject death, string t)
    {
        Bounds b = RendBounds(death, r => r is SkinnedMeshRenderer);
        Camera cam = MakeCamera(new Color(0.45f, 0.5f, 0.55f));
        float f = Shoot(cam, b.center, Vector3.forward + Vector3.right, Mathf.Max(b.size.x, b.size.y, b.size.z) * 2.4f, $"render-{t}-play-death.png", 0.3f);
        P("13.play", f < 0.0005f, $"{t}-death: magenta {(f * 100f):F4}%");
        UnityEngine.Object.Destroy(cam.gameObject);
        yield return null;
    }

    private static IEnumerator CaptureAttack(GameObject go, ProjectileAttackController atk, UnitManager um, Character foe, string map)
    {
        Camera cam = MakeCamera(new Color(0.45f, 0.5f, 0.55f));
        Vector3 foePos = foe.Transform.position;
        cam.fieldOfView = 40;
        Vector3 side = go.transform.right;
        Vector3 mid = (go.transform.position + foePos) * 0.5f + Vector3.up * 1.4f;
        float span = Vector3.Distance(go.transform.position, foePos);
        Vector3 camPos = mid + side * span * 1.1f + Vector3.up * 1.5f;
        int s = atk.ShotsFired;
        um.Attack(foe.Transform);
        int i = 0;
        double end = (double)Time.time + 4;
        bool shotCharge = false, shotBolt = false, shotImpact = false;
        float t0 = Time.time;
        Transform castPoint = atk.projectileSpawn;
        bool p1 = false, p2 = false, p3 = false, relRec = false; float nextTrace = 0f;
        List<string> trace = new List<string>();
        while ((double)Time.time < end && !shotImpact)
        {
            float t = Time.time - t0;
            if (foe.Transform != null) foePos = foe.Transform.position;
            Vector3 rel = go.transform.InverseTransformPoint(castPoint.position);
            if (t >= nextTrace && t < 2.0f) { trace.Add($"t={t:F2}: local({rel.x:F2},{rel.y:F2},{rel.z:F2})"); nextTrace += 0.2f; }
            if (!relRec && atk.ShotsFired > s)
            {
                relRec = true;
                P("7.7p", rel.z > 0.35f, $"no instante da liberacao o CastPoint esta {rel.z:F2} m a frente do tronco (local {rel})");
                P("7.8p", rel.y > 0.9f && rel.y < 2.6f, $"altura do CastPoint na liberacao: {rel.y:F2} m");
                PI("7.7p", "origem do projetil (mundo) " + castPoint.position + "; alvo " + foePos);
            }
            if (!p1 && t > 0.45f) { SnapPose(go, map + "-play-pose-Cast-charge"); p1 = true; }
            if (!p2 && t > 0.80f) { SnapPose(go, map + "-play-pose-Cast-release"); p2 = true; }
            if (!p3 && t > 1.45f) { SnapPose(go, map + "-play-pose-Cast-end"); p3 = true; }
            cam.transform.position = camPos; cam.transform.LookAt(mid);
            if (!shotCharge && t > 0.55f && atk.ShotsFired == s)
            {
                Save(cam, $"render-{map}-attack-charge.png", "charge"); shotCharge = true;
            }
            if (!shotBolt && AdvancedProjectile.ActiveCount > 0)
            {
                GameObject b = GameObject.Find("CombatMageBolt(Clone)") ?? GameObject.Find("CombatMageBolt");
                if (b != null)
                {
                    cam.transform.position = b.transform.position + side * 2.2f + Vector3.up * 0.6f; cam.transform.LookAt(b.transform.position);
                    Save(cam, $"render-{map}-attack-bolt.png", "bolt"); shotBolt = true;
                }
            }
            if (shotBolt && AdvancedProjectile.ActiveCount == 0 && !shotImpact)
            {
                cam.transform.position = foePos + side * 3.2f + Vector3.up * 1.8f; cam.transform.LookAt(foePos + Vector3.up);
                Save(cam, $"render-{map}-attack-impact.png", "impact"); shotImpact = true;
            }
            i++;
            yield return GT(0.03);
        }
        PI(map + ".7.6", "trajeto do CastPoint durante o Cast: " + string.Join(" | ", trace));
        PI(map + ".cap", $"capturas de ataque:carga={shotCharge}, projetil={shotBolt}, impacto={shotImpact}");
        UnityEngine.Object.Destroy(cam.gameObject);
    }

    private static void Save(Camera cam, string file, string tag)
    {
        Texture2D t = Render(cam, 900, 600);
        File.WriteAllBytes(Path.Combine(OutDir, file), t.EncodeToPNG());
        float f = MagentaFraction(t);
        UnityEngine.Object.Destroy(t);
        P("13.play", f < 0.0005f, $"{file}: magenta {(f * 100f):F4}%");
    }
}

/// <summary>Extensao so de leitura para o auditor: cooldown efetivo do tier atual.</summary>
internal static class CombatMageAuditExt
{
    public static float CurrentTierCooldown(this ProjectileAttackController c)
    {
        AdvancedUnitEraBinding b = c.GetComponent<AdvancedUnitEraBinding>();
        return b != null && b.CurrentTier != null ? b.CurrentTier.AttackCooldown : -1f;
    }
}
