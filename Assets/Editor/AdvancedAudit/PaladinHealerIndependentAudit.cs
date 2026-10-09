using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using EraImperial.Units.Advanced;
using EraImperial.Units.Paladin;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// AUDITORIA INDEPENDENTE do Paladino curador (paladin_healer). Escrita pelo auditor; nao reutiliza PaladinHealerAudit/PlayModeCheck.
///   EditMode : -quit -executeMethod PaladinHealerIndependentAudit.RunEdit   -> Docs/AdvancedUnits/audit/paladin_healer/edit-checks.txt (+ renders)
///   Play Mode: -executeMethod PaladinHealerIndependentAudit.RunPlay          -> Docs/AdvancedUnits/audit/paladin_healer/play-checks.txt (+ renders)
/// Somente leitura nos assets do jogo. Escreve apenas em Docs/AdvancedUnits/audit/paladin_healer e TestResults.
/// </summary>
[InitializeOnLoad]
public static class PaladinHealerIndependentAudit
{
    private const string OutDir = "Docs/AdvancedUnits/audit/paladin_healer";
    private const string EditReport = OutDir + "/edit-checks.txt";
    private const string PlayReport = OutDir + "/play-checks.txt";
    private const string Key = "PHIndep.";

    private const string Code = "paladin_healer";
    private const string ContractPath = "Assets/Data/AdvancedUnits/PaladinHealer.asset";
    private const string WrapperPath = "Assets/Resources/Prefabs/Units/Characters/PaladinHealer.prefab";
    private const string CatalogPath = "Assets/Resources/ScriptableObjects/Units/Characters/PaladinHealer.asset";
    private const string Root = "Assets/CharacterRigging/PaladinHealer";
    private const string ModelsDir = Root + "/Models";
    private const string BodyFbx = ModelsDir + "/PaladinHealer.fbx";
    private const string SwordFbx = Root + "/Accessories/PaladinHealer_Sword.fbx";
    private const string ShieldFbx = Root + "/Accessories/PaladinHealer_Shield.fbx";
    private const string EffectPath = "Assets/Resources/Prefabs/Effects/PaladinHealAura.prefab";
    private const string VisualPath = Root + "/Prefabs/PaladinHealerVisual.prefab";
    private const string DeathPath = Root + "/Prefabs/PaladinHealerDeath.prefab";

    private static readonly string[] Clips = { "Idle", "Walk", "Run", "Attack", "Heal", "Hit", "Death" };

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

    /// <summary>Renderiza, salva em OutDir/file e devolve a fracao de pixels magenta. keep != null guarda a textura (tile de montagem).</summary>
    private static float Shoot(Camera cam, Vector3 center, Vector3 dir, float dist, string file, float lift = 0.15f, int w = 800, int h = 800, List<Texture2D> keep = null, bool save = true)
    {
        cam.transform.position = center + dir.normalized * dist + Vector3.up * dist * lift;
        cam.transform.LookAt(center);
        Texture2D t = Render(cam, w, h);
        if (save) File.WriteAllBytes(Path.Combine(OutDir, file), t.EncodeToPNG());
        float f = MagentaFraction(t);
        if (keep != null) keep.Add(t); else UnityEngine.Object.DestroyImmediate(t);
        return f;
    }

    private static void Sheet(string file, int cols, List<Texture2D> tiles, int tw, int th)
    {
        if (tiles.Count == 0) return;
        int rows = (tiles.Count + cols - 1) / cols;
        Texture2D sheet = new Texture2D(cols * tw, rows * th, TextureFormat.RGB24, false);
        for (int i = 0; i < tiles.Count; i++)
        {
            Texture2D t = tiles[i];
            int cx = i % cols, cy = rows - 1 - i / cols;
            sheet.SetPixels(cx * tw, cy * th, tw, th, t.GetPixels());
        }
        sheet.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, file), sheet.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(sheet);
        foreach (Texture2D t in tiles) UnityEngine.Object.DestroyImmediate(t);
        tiles.Clear();
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
        c.fieldOfView = 30; c.nearClipPlane = 0.05f; c.farClipPlane = 200;
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

    private static AnimationClip LoadClip(string c)
    {
        return AssetDatabase.LoadAllAssetsAtPath(ModelsDir + "/PaladinHealer@" + c + ".fbx").OfType<AnimationClip>().FirstOrDefault(a => !a.name.StartsWith("__preview__"));
    }

    private static GameObject InstantiateWrapper()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
    }

    private static GameObject _wrapperInstance;

    private static Transform FindSocket(GameObject w, string name)
    {
        PaladinHealerContract c = AssetDatabase.LoadAssetAtPath<PaladinHealerContract>(ContractPath);
        SocketRequirement s = c.visual.sockets.FirstOrDefault(x => x.socketName == name);
        return s != null ? w.transform.Find(s.expectedTransformPath) : null;
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
            Section9_Effect();
            Section10_Contract();
            Section11_Progression();
            Section14_References();
            Section13_Renders();
            Section12_PoseGeometry();
            Section9b_EffectRenders();
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
        Ok("1.2", File.Exists(SwordFbx) && File.Exists(ShieldFbx), "FBX da espada e do escudo existem");
        string inputs = "Tools/CharacterRigging/Inputs/PaladinHealer";
        foreach (string f in new[] { "paladinhealer_rigged.glb", "walking.glb", "running.glb", "ref1.png", "img1_task.json", "i3d_task.json", "rig_task.json" })
            Ok("1.3", File.Exists(Path.Combine(inputs, f)), "entrada " + f + " presente");
        foreach (string f in new[] { "SwordShield-Idle.fbx", "SwordShield-Slash.fbx", "React-Large-From-Left.fbx", "Dying.fbx" })
            Ok("1.4", File.Exists(Path.Combine(inputs, "Mixamo", f)), "entrada Mixamo " + f + " presente");
        string lic = File.ReadAllText("Docs/CharacterMeshAudit/PaladinHealer/LICENSE.md");
        Ok("1.5", lic.Contains("Meshy") && lic.Contains("Mixamo") && lic.Contains("Heal") && lic.Contains("proceduralmente"), "LICENSE.md cita Meshy, Mixamo, Heal (Blender) e espada/escudo procedurais");
        // coerencia entre a licenca e o script de build: as fontes Mixamo declaradas no script
        string py = File.ReadAllText("Tools/CharacterRigging/BuildPaladinHealer.py");
        foreach (string f in new[] { "SwordShield-Idle.fbx", "SwordShield-Slash.fbx", "React-Large-From-Left.fbx", "Dying.fbx" })
            Ok("1.5b", py.Contains(f), "BuildPaladinHealer.py usa " + f);
        Ok("1.5c", lic.Contains("Sword And Shield Idle") && lic.Contains("Sword And Shield Slash") && lic.Contains("Standing React Large From Left") && lic.Contains("Dying"), "LICENSE.md nomeia os 4 clipes Mixamo usados");
        string[] unusedMixamo = Directory.GetFiles(Path.Combine(inputs, "Mixamo"), "*.fbx").Select(Path.GetFileName).Where(n => !py.Contains(n)).ToArray();
        Info("1.5d", "arquivos Mixamo em Inputs NAO usados pelo build: " + (unusedMixamo.Length == 0 ? "nenhum" : string.Join(", ", unusedMixamo)));
        string i3d = File.ReadAllText(Path.Combine(inputs, "i3d_task.json"));
        string rig = File.ReadAllText(Path.Combine(inputs, "rig_task.json"));
        string img = File.ReadAllText(Path.Combine(inputs, "img1_task.json"));
        Ok("1.6", i3d.Contains("\"SUCCEEDED\"") && rig.Contains("\"SUCCEEDED\"") && img.Contains("\"SUCCEEDED\""), "tarefas Meshy texto->imagem, imagem->3D e rig com status SUCCEEDED");
        // segredos
        bool secret = false; List<string> hits = new List<string>(); List<string> signed = new List<string>();
        string[] roots = { inputs, "Docs/CharacterMeshAudit/PaladinHealer", Root, "Tools/CharacterRigging" };
        foreach (string r in roots)
            foreach (string f in Directory.GetFiles(r, "*", r == "Tools/CharacterRigging" ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".png" || ext == ".fbx" || ext == ".glb" || ext == ".blend" || ext == ".anim" || ext == ".meta" || ext == ".tga") continue;
                if (r == "Tools/CharacterRigging" && !Path.GetFileName(f).Contains("Paladin")) continue;
                string t;
                try { t = File.ReadAllText(f); } catch { continue; }
                if (Regex.IsMatch(t, @"msy_[A-Za-z0-9]{8,}|Bearer\s+[A-Za-z0-9_\-\.]{20,}|sk-[A-Za-z0-9]{20,}|MESHY_API_KEY\s*=\s*['""]?[A-Za-z0-9]{8,}"))
                { secret = true; hits.Add(f); }
                if (t.Contains("Signature=") && t.Contains("Key-Pair-Id=")) signed.Add(f);
            }
        Ok("1.7", !secret, "nenhuma chave de API gravada (padroes msy_/Bearer/sk-/MESHY_API_KEY=)" + (hits.Count > 0 ? " ACHADOS: " + string.Join("; ", hits) : ""));
        Ok("1.8", signed.Count == 0, "nenhuma URL assinada temporaria (Signature=/Key-Pair-Id=) gravada" + (signed.Count > 0 ? " ACHADO(S): " + string.Join("; ", signed) : ""));
        Info("1.9", "rigged.glb de entrada=" + new FileInfo(Path.Combine(inputs, "paladinhealer_rigged.glb")).Length + " bytes; Assets .glb=" + new FileInfo(ModelsDir + "/PaladinHealer.glb").Length + " bytes; importador do .glb em Assets: " + AssetImporter.GetAtPath(ModelsDir + "/PaladinHealer.glb")?.GetType().Name);
        // textura/ref: o design pedido e o resultado
        Info("1.10", "imagem de referencia ref1.png " + new FileInfo(Path.Combine(inputs, "ref1.png")).Length + " bytes; textura base " + new FileInfo(Root + "/Textures/PaladinHealer_BaseColor.png").Length + " bytes");
    }

    // ------------------------------------------------------------------ 2
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
            Info("2.3", "ossos humanoides mapeados: " + d.human.Length + " -> " + string.Join(", ", d.human.Select(h => h.humanName + "=" + h.boneName)));
            Info("2.3", "avatar: hasRootMotionDof/skeleton bones=" + d.skeleton.Length);
        }
        foreach (string c in Clips)
        {
            string p = ModelsDir + "/PaladinHealer@" + c + ".fbx";
            ModelImporter imp = AssetImporter.GetAtPath(p) as ModelImporter;
            Ok("2.4", imp != null && imp.animationType == ModelImporterAnimationType.Human, c + ": FBX do clipe importado como Humanoid");
            AnimationClip clip = LoadClip(c);
            Ok("2.5", clip != null && clip.isHumanMotion && clip.length > 0.05f, c + ": AnimationClip humanoide (" + (clip != null ? clip.length.ToString("F2") + " s" : "ausente") + ")");
            if (imp != null)
            {
                Avatar sa = imp.sourceAvatar;
                bool ownsAvatar = AssetDatabase.LoadAllAssetsAtPath(p).OfType<Avatar>().Any(a => a.isValid);
                Ok("2.6", (sa != null && sa == av) || ownsAvatar, c + ": avatar do clipe = avatar do corpo (copiado) ou proprio valido");
            }
        }
        // clipes nao vazios + costura de loop + deriva da raiz
        foreach (string c in Clips)
        {
            AnimationClip clip = LoadClip(c);
            if (clip == null) continue;
            EditorCurveBinding[] binds = AnimationUtility.GetCurveBindings(clip);
            int moving = 0; double seamMax = 0, seamSum = 0; int seamN = 0; string seamWorst = "";
            Vector3 rootDrift = Vector3.zero; float[] rt = new float[3]; float[] rt0 = new float[3];
            foreach (EditorCurveBinding b in binds)
            {
                AnimationCurve cv = AnimationUtility.GetEditorCurve(clip, b);
                if (cv == null || cv.length == 0) continue;
                float mn = float.MaxValue, mx = float.MinValue;
                foreach (Keyframe k in cv.keys) { mn = Mathf.Min(mn, k.value); mx = Mathf.Max(mx, k.value); }
                if (mx - mn > 0.01f) moving++;
                double seam = Math.Abs(cv.Evaluate(0f) - cv.Evaluate(clip.length));
                if (!b.propertyName.StartsWith("RootT") && !b.propertyName.StartsWith("RootQ")) { seamSum += seam; seamN++; if (seam > seamMax) { seamMax = seam; seamWorst = b.propertyName; } }
                if (b.propertyName == "RootT.x") { rt0[0] = cv.Evaluate(0f); rt[0] = cv.Evaluate(clip.length); }
                if (b.propertyName == "RootT.y") { rt0[1] = cv.Evaluate(0f); rt[1] = cv.Evaluate(clip.length); }
                if (b.propertyName == "RootT.z") { rt0[2] = cv.Evaluate(0f); rt[2] = cv.Evaluate(clip.length); }
            }
            Ok("2.7", moving >= 20, c + ": " + binds.Length + " curvas, " + moving + " com variacao > 0.01 (clipe nao vazio/estatico)");
            bool loop = AnimationUtility.GetAnimationClipSettings(clip).loopTime;
            Info("2.7", $"{c}: {clip.length:F2} s, {clip.frameRate} fps, loopTime={loop}, loopPose={AnimationUtility.GetAnimationClipSettings(clip).loopBlend}, eventos={clip.events.Length}, costura(muscle) max={seamMax:F3} ({seamWorst}) media={(seamN > 0 ? seamSum / seamN : 0):F4}, RootT inicio=({rt0[0]:F2},{rt0[1]:F2},{rt0[2]:F2}) fim=({rt[0]:F2},{rt[1]:F2},{rt[2]:F2})");
            if (c == "Idle" || c == "Walk" || c == "Run")
                Ok("2.8", loop && seamMax < 0.08, c + ": loop ligado e costura do ciclo suave (max " + seamMax.ToString("F3") + " em " + seamWorst + "; limiar 0.08)");
            else
                Ok("2.8", !loop, c + ": nao-loop");
            if (c == "Idle" || c == "Walk" || c == "Run")
            {
                float driftXZ = Mathf.Sqrt((rt[0] - rt0[0]) * (rt[0] - rt0[0]) + (rt[2] - rt0[2]) * (rt[2] - rt0[2]));
                Ok("2.9", driftXZ < 0.15f, c + ": deriva horizontal da raiz ao longo do ciclo " + driftXZ.ToString("F3") + " (unid. de altura do avatar) < 0.15");
            }
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
            Ok("3.8", m.normals.Length == m.vertexCount, tag + ": normais presentes");
            Info("3.8", tag + ": tangentes " + (m.tangents.Length == m.vertexCount ? "presentes" : "ausentes (geradas pelo shader/normal map)") + "; blendshapes " + m.blendShapeCount);
        }
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
        foreach (string acc in new[] { SwordFbx, ShieldFbx })
        {
            GameObject a = AssetDatabase.LoadAssetAtPath<GameObject>(acc);
            int t = a.GetComponentsInChildren<MeshFilter>(true).Sum(f => Tris(f.sharedMesh));
            Ok("3.14", t > 0 && t <= 8000, Path.GetFileNameWithoutExtension(acc) + ": " + t + " triangulos (<=8k)");
            foreach (MeshFilter f in a.GetComponentsInChildren<MeshFilter>(true))
                Info("3.15", Path.GetFileNameWithoutExtension(acc) + "/" + f.name + ": " + Tris(f.sharedMesh) + " tri, UVs " + (f.sharedMesh.uv.Length == f.sharedMesh.vertexCount ? "presentes" : "ausentes (cor solida)") + ", submeshes " + f.sharedMesh.subMeshCount);
        }
    }

    // ------------------------------------------------------------------ 4
    private static void CheckMaterial(string id, string owner, Material m, bool needTexture)
    {
        if (m == null) { Ok(id, false, owner + ": material nulo"); return; }
        string sn = m.shader != null ? m.shader.name : "(null)";
        bool supported = m.shader != null && m.shader.isSupported && !sn.Contains("InternalErrorShader") && sn != "Hidden/InternalErrorShader";
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
        string[] prefabs = { WrapperPath, VisualPath, DeathPath, EffectPath };
        foreach (string p in prefabs)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            Ok("4.0", go != null, Path.GetFileName(p) + " carrega");
            if (go == null) continue;
            HashSet<Material> seen = new HashSet<Material>();
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) { Ok("4.1", false, Path.GetFileName(p) + "/" + r.name + ": slot de material nulo"); continue; }
                    if (!seen.Add(m)) continue;
                    bool isFx = r is ParticleSystemRenderer || r is TrailRenderer || p == EffectPath;
                    bool bodyTex = r is SkinnedMeshRenderer;
                    CheckMaterial("4.1", Path.GetFileName(p), m, bodyTex || isFx);
                }
        }
        foreach (string acc in new[] { SwordFbx, ShieldFbx })
        {
            GameObject a = AssetDatabase.LoadAssetAtPath<GameObject>(acc);
            foreach (Renderer r in a.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials) CheckMaterial("4.2", Path.GetFileName(acc), m, false);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
        {
            string mp = AssetDatabase.GUIDToAssetPath(guid);
            Material m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            CheckMaterial("4.3", "Materials", m, false);
            if (m.HasProperty("_Color"))
            {
                Color c = m.GetColor("_Color");
                bool mag = c.r > 0.9f && c.b > 0.9f && c.g < 0.15f;
                Ok("4.4", !mag, m.name + ": cor principal nao e magenta (" + c + ")");
            }
            if (m.HasProperty("_TintColor"))
            {
                Texture mt = m.mainTexture;
                Ok("4.5", mt != null, m.name + ": material de efeito tem textura (" + (mt != null ? mt.name : "NENHUMA") + ")");
            }
        }
        // o material do corpo: normal e metal/rugosidade atribuidos
        Material body = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/PaladinHealer_Mat.mat");
        Ok("4.6", body != null && body.GetTexture("_BumpMap") != null && body.GetTexture("_MetallicGlossMap") != null && body.mainTexture != null, "PaladinHealer_Mat: base + normal + metal/suavidade atribuidos");
        // texturas da unidade sem magenta
        foreach (string tp in Directory.GetFiles(Root + "/Textures", "*.png"))
        {
            Texture2D t = new Texture2D(2, 2);
            t.LoadImage(File.ReadAllBytes(tp));
            float mf = MagentaFraction(t);
            Ok("4.7", mf < 0.002f, Path.GetFileName(tp) + " " + t.width + "x" + t.height + ": magenta " + (mf * 100).ToString("F3") + "%");
            UnityEngine.Object.DestroyImmediate(t);
        }
    }

    // ------------------------------------------------------------------ 6
    private static void Section6_Wrapper()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        Ok("6.0", prefab != null, "wrapper PaladinHealer.prefab carrega");
        GameObject soldier = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Units/Characters/Soldier.prefab");
        GameObject w = InstantiateWrapper();
        _wrapperInstance = w;
        int missing = 0;
        foreach (Transform childTransform in w.GetComponentsInChildren<Transform>(true))
            missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(childTransform.gameObject);
        Ok("6.1", missing == 0, "nenhum script ausente (Missing) na hierarquia do wrapper (" + missing + ")");
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
            Info("6.5", $"BoxCollider size {bc.size} center {bc.center}; corpo (bounds dos SMR, pose do prefab) size {bodyB.size} center {w.transform.InverseTransformPoint(bodyB.center)}");
            Ok("6.6", bc.size.y >= bodyB.size.y * 0.8f && bc.size.y <= bodyB.size.y * 1.3f, "altura do BoxCollider coerente com a do modelo (" + bc.size.y.ToString("F2") + " vs " + bodyB.size.y.ToString("F2") + ")");
            Ok("6.7", bc.size.x >= 0.5f && bc.size.x <= 1.6f && bc.size.z >= 0.5f && bc.size.z <= 1.6f, "largura/profundidade do BoxCollider plausiveis (" + bc.size.x.ToString("F2") + " x " + bc.size.z.ToString("F2") + ")");
            Ok("6.8", Mathf.Abs(bc.center.y - bc.size.y / 2f) < 0.3f, "BoxCollider apoiado no chao (centro y " + bc.center.y.ToString("F2") + ")");
        }
        AdvancedUnitEraBinding bind = w.GetComponent<AdvancedUnitEraBinding>();
        PaladinHealerContract contract = AssetDatabase.LoadAssetAtPath<PaladinHealerContract>(ContractPath);
        Ok("6.9", bind != null && bind.contract == contract && contract != null, "AdvancedUnitEraBinding.contract = PaladinHealer.asset");
        Ok("6.10", w.GetComponent<AdvancedUnitAnimatorDriver>() != null && w.GetComponent<AdvancedUnitAnimatorDriver>().animator != null, "AdvancedUnitAnimatorDriver com Animator");
        PaladinHealBehaviour heal = w.GetComponent<PaladinHealBehaviour>();
        Ok("6.11", heal != null && heal.contract == contract, "PaladinHealBehaviour com o contrato correto");
        if (heal != null)
        {
            Ok("6.12", heal.animator != null && heal.animator.runtimeAnimatorController != null, "PaladinHealBehaviour tem Animator com controller");
            Transform hac = FindSocket(w, "HealAuraCenter");
            Ok("6.13", heal.effectOrigin != null && heal.effectOrigin == hac, "effectOrigin = socket HealAuraCenter do contrato (" + (heal.effectOrigin != null ? RelPath(w.transform, heal.effectOrigin) : "nulo") + ")");
            Ok("6.13b", heal.healEffectPrefab != null && AssetDatabase.GetAssetPath(heal.healEffectPrefab) == EffectPath, "healEffectPrefab = PaladinHealAura.prefab (" + (heal.healEffectPrefab != null ? AssetDatabase.GetAssetPath(heal.healEffectPrefab) : "nulo") + ")");
        }
        PaladinMeleeAttackController melee = w.GetComponent<PaladinMeleeAttackController>();
        Ok("6.14", melee != null, "PaladinMeleeAttackController presente (cadencia do golpe = cooldown da era)");
        if (melee != null) Ok("6.14b", melee.animator != null && melee.animator.runtimeAnimatorController != null, "PaladinMeleeAttackController tem Animator com controller");
        int handlers = w.GetComponents<MonoBehaviour>().Count(m => m is IAttackHandler);
        Ok("6.15", handlers == 1, "exatamente 1 IAttackHandler na raiz (" + handlers + ")");
        AdvancedUnitDeathEffect de = w.GetComponent<AdvancedUnitDeathEffect>();
        Ok("6.16", de != null && de.deathPrefab != null, "AdvancedUnitDeathEffect com prefab de morte (" + (de != null && de.deathPrefab != null ? de.deathPrefab.name : "nenhum") + ")");
        if (de != null && de.deathPrefab != null)
        {
            Animator da = de.deathPrefab.GetComponentInChildren<Animator>();
            Ok("6.17", da != null && da.runtimeAnimatorController != null && da.avatar != null, "prefab de morte tem Animator + controller + avatar");
            Ok("6.18", de.deathPrefab.GetComponentsInChildren<Collider>().Length == 0 && de.deathPrefab.GetComponent<NavMeshAgent>() == null, "prefab de morte sem colisor nem agente (nao bloqueia o jogo)");
            Info("6.17", $"lifetime do efeito de morte {de.lifetime}s; renderers do prefab de morte: {string.Join(", ", de.deathPrefab.GetComponentsInChildren<Renderer>().Select(r => r.name))}");
            Ok("6.19", AssetDatabase.GetAssetPath(de.deathPrefab) == DeathPath, "deathPrefab = PaladinHealerDeath.prefab");
        }
        if (soldier != null)
        {
            HashSet<string> sc = new HashSet<string>(soldier.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name));
            HashSet<string> wc = new HashSet<string>(w.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name));
            string lacking = string.Join(",", sc.Where(n => !wc.Contains(n)));
            Ok("6.20", lacking.Length == 0, "wrapper tem todos os componentes de raiz do Soldier (faltam: " + (lacking.Length == 0 ? "nenhum" : lacking) + ")");
            Info("6.20", "componentes do wrapper: " + string.Join(", ", wc.OrderBy(x => x)));
            Ok("6.21", w.layer == soldier.layer && w.tag == soldier.tag, $"layer/tag iguais ao Soldier ({LayerMask.LayerToName(w.layer)}/{w.tag} vs {LayerMask.LayerToName(soldier.layer)}/{soldier.tag})");
        }
        CharacterManager cm = w.GetComponent<CharacterManager>();
        if (cm != null)
        {
            Ok("6.22", cm.meshRenderer != null && cm.animator != null && cm.agent != null && cm.characterCollider != null, "CharacterManager: meshRenderer, animator, agent e characterCollider atribuidos");
            Ok("6.23", cm.selectionCircle != null && cm.fov != null, "CharacterManager: selectionCircle e fov atribuidos");
            Info("6.22", "meshRenderer(anel de time)=" + (cm.meshRenderer != null ? cm.meshRenderer.name + " bounds " + cm.meshRenderer.bounds.size : "null"));
            // anel de time: faces para cima (correcao recente do coordenador na camada compartilhada)
            MeshFilter rf = cm.meshRenderer != null ? cm.meshRenderer.GetComponent<MeshFilter>() : null;
            if (rf != null && rf.sharedMesh != null)
            {
                Mesh rm = rf.sharedMesh; Vector3[] v = rm.vertices; int[] t = rm.triangles; double ny = 0; int up = 0, down = 0;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                    ny += n.y; if (n.y > 0) up++; else if (n.y < 0) down++;
                }
                Ok("6.24", up > 0 && down == 0, $"anel de time ({AssetDatabase.GetAssetPath(rm)}): {up} triangulos voltados para cima, {down} para baixo (visivel de cima)");
                Info("6.24", "raio do anel (bounds) " + rm.bounds.extents.x.ToString("F2") + " m; agente raio " + agent.radius);
            }
        }
        LODGroup lg = w.GetComponentInChildren<LODGroup>();
        Ok("6.25", lg != null && lg.lodCount == 3, "LODGroup no wrapper com 3 niveis");
        if (lg != null)
        {
            LOD[] lods = lg.GetLODs();
            List<int> lt = lods.Select(l => l.renderers.Where(r => r != null).Sum(r => Tris(MeshOf(r)))).ToList();
            Ok("6.26", lt[0] > lt[1] && lt[1] > lt[2] && lt[2] > 0, "wrapper: triangulos por LOD estritamente decrescentes " + string.Join(" > ", lt));
            float[] th = lods.Select(l => l.screenRelativeTransitionHeight).ToArray();
            Ok("6.27", th[0] > th[1] && th[1] > th[2] && th[0] < 1f && th[2] >= 0f, "wrapper: limiares coerentes " + string.Join(", ", th.Select(threshold => threshold.ToString("F3"))));
            Info("6.27", "LODGroup size " + lg.size + ", fadeMode " + lg.fadeMode + ", animateCrossFading " + lg.animateCrossFading);
            foreach (Renderer r in w.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r.name == "TeamRing" || r.name == "SelectionCircle") continue;
                string n = r.name.ToLower();
                if (n.Contains("sword") || n.Contains("shield") || n.Contains("espada") || n.Contains("escudo"))
                    Info("6.28", "renderer " + RelPath(w.transform, r.transform) + " (nos LODs: " + string.Join(",", Enumerable.Range(0, 3).Where(i => lods[i].renderers.Contains(r))) + ")");
            }
        }
        Ok("6.29", w.transform.localScale == Vector3.one, "escala da raiz = 1");
    }

    // ------------------------------------------------------------------ 7
    private static void Section7_Sockets()
    {
        GameObject w = _wrapperInstance;
        PaladinHealerContract contract = AssetDatabase.LoadAssetAtPath<PaladinHealerContract>(ContractPath);
        foreach (SocketRequirement s in contract.visual.sockets)
        {
            Transform t = w.transform.Find(s.expectedTransformPath);
            Ok("7.1", t != null && t.name == s.socketName, "socket '" + s.socketName + "' existe em " + s.expectedTransformPath);
        }
        Transform wm = FindSocket(w, "WeaponMount");
        Transform sm = FindSocket(w, "ShieldMount");
        Transform hc = FindSocket(w, "HealAuraCenter");
        Animator anim = w.GetComponentInChildren<Animator>();
        Transform rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        Transform leftHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform leftLower = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        Info("7.0", "Sockets: WeaponMount pai=" + (wm != null ? wm.parent.name : "?") + ", ShieldMount pai=" + (sm != null ? sm.parent.name : "?") + ", HealAuraCenter pai=" + (hc != null ? hc.parent.name : "?"));
        if (wm != null)
        {
            // os sockets ficam na hierarquia "Sockets/..."? o contrato exige Find a partir da raiz; verifica se o socket e o transform montado no osso
            Info("7.2", "WeaponMount world " + wm.position.ToString("F3") + "; mao direita " + rightHand.position.ToString("F3") + "; dist " + Vector3.Distance(wm.position, rightHand.position).ToString("F3"));
        }
        if (hc != null)
        {
            Vector3 local = w.transform.InverseTransformPoint(hc.position);
            Ok("7.3", local.y > 0.9f && local.y < 1.5f && Mathf.Abs(local.x) < 0.25f && Mathf.Abs(local.z) < 0.35f, "HealAuraCenter no tronco (local " + local.ToString("F2") + "; altura do peito 0.9-1.5 m)");
        }
        // espada
        Transform sword = w.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.ToLower().Contains("sword"));
        Transform shield = w.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.ToLower().Contains("shield"));
        Ok("7.4", sword != null && wm != null && sword.IsChildOf(wm), "espada e filha de WeaponMount (" + (sword != null ? RelPath(w.transform, sword) : "nao achada") + ")");
        Ok("7.5", shield != null && sm != null && shield.IsChildOf(sm), "escudo e filho de ShieldMount (" + (shield != null ? RelPath(w.transform, shield) : "nao achado") + ")");
        Ok("7.6", wm != null && wm.IsChildOf(rightHand), "WeaponMount descende do osso RightHand do avatar");
        Ok("7.7", sm != null && (sm.IsChildOf(leftLower)), "ShieldMount descende do osso LeftLowerArm (antebraco) do avatar");
        if (sword != null)
        {
            Renderer[] sr = sword.GetComponentsInChildren<Renderer>();
            Bounds sb = sr[0].bounds; foreach (Renderer r in sr) sb.Encapsulate(r.bounds);
            float len = Mathf.Max(sb.size.x, sb.size.y, sb.size.z);
            Ok("7.8", len > 0.7f && len < 1.4f, "espada com comprimento plausivel " + len.ToString("F2") + " m (0.7-1.4)");
            // distancia do ponto do cabo (origem do WeaponMount) ate a mao
            float gripToHand = Vector3.Distance(wm.position, rightHand.position);
            Ok("7.9", gripToHand < 0.15f, "cabo (WeaponMount) a " + gripToHand.ToString("F3") + " m do osso da mao (< 0.15)");
            // ponta
            Mesh sm0 = sr[0] is SkinnedMeshRenderer ? ((SkinnedMeshRenderer)sr[0]).sharedMesh : sr[0].GetComponent<MeshFilter>().sharedMesh;
            Info("7.10", $"espada: bounds mundo size {sb.size.ToString("F2")} center {sb.center.ToString("F2")}; escala local {sword.lossyScale.ToString("F3")}");
        }
        if (shield != null)
        {
            Renderer[] hr = shield.GetComponentsInChildren<Renderer>();
            Bounds hb = hr[0].bounds; foreach (Renderer r in hr) hb.Encapsulate(r.bounds);
            Ok("7.11", hb.size.x > 0.3f || hb.size.y > 0.3f, "escudo com tamanho plausivel " + hb.size.ToString("F2"));
            float shieldToForearm = Vector3.Distance(hb.center, leftLower.position);
            Info("7.12", $"escudo: center {hb.center.ToString("F2")}; antebraco {leftLower.position.ToString("F2")}; mao esquerda {leftHand.position.ToString("F2")}; dist centro->antebraco {shieldToForearm:F2} m");
            Ok("7.13", shieldToForearm < 0.6f, "centro do escudo a " + shieldToForearm.ToString("F2") + " m do antebraco (< 0.6)");
        }
    }

    // ------------------------------------------------------------------ 8
    private static void CollectClips(Motion m, HashSet<AnimationClip> set)
    {
        if (m is AnimationClip c) set.Add(c);
        else if (m is BlendTree bt) foreach (ChildMotion cm in bt.children) CollectClips(cm.motion, set);
    }

    private static void Section8_Animator()
    {
        GameObject w = _wrapperInstance;
        PaladinHealerContract contract = AssetDatabase.LoadAssetAtPath<PaladinHealerContract>(ContractPath);
        Animator anim = w.GetComponentInChildren<Animator>();
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(anim.runtimeAnimatorController));
        Ok("8.0", ctrl != null, "controller do wrapper e um AnimatorController (" + AssetDatabase.GetAssetPath(anim.runtimeAnimatorController) + ")");
        if (ctrl == null) return;
        Ok("8.0b", anim.avatar != null && anim.avatar.isHuman && !anim.applyRootMotion, "Animator com avatar humanoide e sem root motion");
        Dictionary<string, AnimatorControllerParameterType> ps = ctrl.parameters.ToDictionary(p => p.name, p => p.type);
        Info("8.1", "parametros: " + string.Join(", ", ps.Select(kv => kv.Key + ":" + kv.Value)));
        foreach (var kv in new Dictionary<string, AnimatorControllerParameterType> {
            { "Speed", AnimatorControllerParameterType.Float }, { "Heal", AnimatorControllerParameterType.Trigger }, { "Damaged", AnimatorControllerParameterType.Trigger },
            { "Running", AnimatorControllerParameterType.Bool }, { "Attacking", AnimatorControllerParameterType.Bool }, { "Hit", AnimatorControllerParameterType.Trigger } })
            Ok("8.1", ps.TryGetValue(kv.Key, out AnimatorControllerParameterType ty) && ty == kv.Value, "parametro " + kv.Key + " (" + kv.Value + ")");
        Info("8.1", "layers: " + string.Join(" | ", ctrl.layers.Select((l, i) => i + ":" + l.name + " peso=" + l.defaultWeight + " blend=" + l.blendingMode + " mask=" + (l.avatarMask != null ? l.avatarMask.name : "nenhuma") + " sync=" + l.syncedLayerIndex)));
        // mascara da camada Heal
        for (int li = 1; li < ctrl.layers.Length; li++)
        {
            AvatarMask mk = ctrl.layers[li].avatarMask;
            if (mk == null) { Ok("8.1m", false, "camada " + ctrl.layers[li].name + " sem AvatarMask"); continue; }
            bool legsOff = !mk.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg) && !mk.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg);
            bool upperOn = mk.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Body) && mk.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm) && mk.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm);
            Ok("8.1m", legsOff && upperOn, $"camada {ctrl.layers[li].name}: mascara sem pernas, com tronco/bracos (pernas {(legsOff ? "off" : "ON")}, tronco+bracos {(upperOn ? "on" : "OFF")}; cabeca {mk.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Head)}, raiz {mk.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root)})");
        }
        for (int li = 0; li < ctrl.layers.Length; li++)
        {
            var sm = ctrl.layers[li].stateMachine;
            Info("8.2", "layer " + li + " estados=" + string.Join(",", sm.states.Select(s => s.state.name + "{" + (s.state.motion != null ? s.state.motion.name : "vazio") + "}")) + ", default=" + (sm.defaultState != null ? sm.defaultState.name : "null"));
            foreach (var cs in sm.states)
            {
                if (cs.state.motion is BlendTree bt0)
                    Info("8.2", "BlendTree '" + cs.state.name + "' param=" + bt0.blendParameter + " tipo=" + bt0.blendType + ": " + string.Join(", ", bt0.children.Select(c => (c.motion != null ? c.motion.name : "null") + "@" + c.threshold.ToString("F2"))));
                foreach (AnimatorStateTransition t in cs.state.transitions)
                    Info("8.3", $"L{li} {cs.state.name} -> {(t.destinationState != null ? t.destinationState.name : "exit")} [{string.Join("&", t.conditions.Select(c => c.parameter + ":" + c.mode + ":" + c.threshold))}] exit={t.hasExitTime}({t.exitTime:F2}) dur={t.duration:F2} interrupt={t.interruptionSource}");
            }
            foreach (AnimatorStateTransition t in sm.anyStateTransitions)
                Info("8.3", $"L{li} ANY -> {(t.destinationState != null ? t.destinationState.name : "exit")} [{string.Join("&", t.conditions.Select(c => c.parameter + ":" + c.mode + ":" + c.threshold))}] exit={t.hasExitTime} dur={t.duration:F2} canTransitionToSelf={t.canTransitionToSelf}");
        }
        bool allCondOk = true;
        foreach (var l in ctrl.layers)
        {
            var sm = l.stateMachine;
            foreach (var cs in sm.states) foreach (var t in cs.state.transitions) foreach (var c in t.conditions) if (!ps.ContainsKey(c.parameter)) allCondOk = false;
            foreach (var t in sm.anyStateTransitions) foreach (var c in t.conditions) if (!ps.ContainsKey(c.parameter)) allCondOk = false;
        }
        Ok("8.4", allCondOk, "todas as condicoes de transicao usam parametros declarados");
        HashSet<AnimationClip> used = new HashSet<AnimationClip>();
        foreach (var l in ctrl.layers) foreach (var cs in l.stateMachine.states) CollectClips(cs.state.motion, used);
        foreach (AnimationRequirement req in contract.visual.animations)
        {
            Ok("8.5", req.clip != null && req.clip.length > 0.05f, $"contrato: clipe '{req.state}' real, duracao {(req.clip != null ? req.clip.length.ToString("F2") : "-")} s, parametro declarado '{req.animatorParameter}'");
            if (req.state == "Death") continue;
            Ok("8.6", used.Contains(req.clip), $"controller do wrapper usa o clipe do contrato '{req.state}'");
            Ok("8.6b", ps.ContainsKey(req.animatorParameter), $"contrato '{req.state}' declara o parametro '{req.animatorParameter}' -> existe no controller? " + (ps.ContainsKey(req.animatorParameter) ? "sim" : "NAO (parametro do contrato nao existe no Animator; o real e outro)"));
        }
        Info("8.6c", "estados exigidos pelo contrato: " + string.Join(", ", contract.visual.animations.Select(a => a.state)) + "; clipes importados: " + string.Join(", ", Clips));
        Animator da = w.GetComponent<AdvancedUnitDeathEffect>().deathPrefab.GetComponentInChildren<Animator>();
        AnimatorController dc = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(da.runtimeAnimatorController));
        HashSet<AnimationClip> dused = new HashSet<AnimationClip>();
        foreach (var cs in dc.layers[0].stateMachine.states) CollectClips(cs.state.motion, dused);
        AnimationClip deathClip = contract.visual.animations.First(a => a.state == "Death").clip;
        Ok("8.7", dused.Contains(deathClip) && dc.layers[0].stateMachine.defaultState != null && dc.layers[0].stateMachine.defaultState.motion == deathClip, "controller de morte: estado padrao toca o clipe Death do contrato");

        // ---- simulacao com o Animator real (edit mode)
        anim.Rebind(); anim.Update(0f);
        string St(int layer = 0)
        {
            var si = anim.GetCurrentAnimatorStateInfo(layer);
            var sm = ctrl.layers[layer].stateMachine;
            string n = sm.states.Select(s => s.state).FirstOrDefault(s => si.IsName(s.name))?.name ?? ("hash" + si.shortNameHash);
            if (n == "Locomotion")
            {
                var ci = anim.GetCurrentAnimatorClipInfo(layer);
                if (ci.Length > 0) { var top = ci.OrderByDescending(c => c.weight).First(); n = "Locomotion/" + top.clip.name.Replace("PaladinHealer@", ""); }
            }
            return n;
        }
        for (int i = 0; i < 30; i++) anim.Update(0.05f);
        Ok("8.8", St().EndsWith("Idle"), "Speed=0 -> Locomotion tocando Idle (" + St() + ")");
        anim.SetFloat("Speed", 1.5f); for (int i = 0; i < 30; i++) anim.Update(0.05f);
        string walk = St();
        anim.SetFloat("Speed", 4.0f); for (int i = 0; i < 30; i++) anim.Update(0.05f);
        string run = St();
        Info("8.9", $"Speed=1.5 -> {walk}; Speed=4.0 -> {run}");
        Ok("8.9", walk.EndsWith("Walk"), "Speed 1.5 -> Walk (" + walk + ")");
        Ok("8.10", run.EndsWith("Run"), "Speed 4.0 (velocidade do agente) -> Run (" + run + ")");
        anim.SetFloat("Speed", 0f); for (int i = 0; i < 40; i++) anim.Update(0.05f);
        Ok("8.11", St().EndsWith("Idle"), "Speed volta a 0 -> Idle (" + St() + ")");
        // Hit (gatilho do BT) -> Attack -> volta
        anim.SetTrigger("Hit"); anim.Update(0.1f); anim.Update(0.1f);
        string afterHit = St();
        Ok("8.12", afterHit == "Attack", "trigger Hit (BT) -> Attack (" + afterHit + ")");
        float clipLen = contract.visual.animations.First(a => a.state == "Attack").clip.length;
        for (int i = 0; i < (int)(clipLen / 0.05f) + 40; i++) anim.Update(0.05f);
        Ok("8.13", St().EndsWith("Idle"), "apos o Attack volta a Locomotion/Idle (" + St() + ")");
        anim.SetTrigger("Damaged"); anim.Update(0.1f); anim.Update(0.1f);
        string afterDmg = St();
        Ok("8.14", afterDmg.Contains("Hit") || afterDmg.Contains("React"), "trigger Damaged -> reacao a dano (" + afterDmg + ")");
        for (int i = 0; i < 60; i++) anim.Update(0.05f);
        Ok("8.15", St().EndsWith("Idle"), "apos a reacao volta a Idle (" + St() + ")");
        // Heal: camada de cura
        int healLayer = -1; for (int li = 1; li < ctrl.layers.Length; li++) if (ctrl.layers[li].name.ToLower().Contains("heal")) healLayer = li;
        Ok("8.16", healLayer > 0, "existe camada de cura (indice " + healLayer + ")");
        if (healLayer > 0)
        {
            float w0 = anim.GetLayerWeight(healLayer);
            string st0 = St(healLayer);
            anim.SetTrigger("Heal"); anim.Update(0.1f); anim.Update(0.1f);
            string st1 = St(healLayer);
            var ci = anim.GetCurrentAnimatorClipInfo(healLayer);
            Info("8.16", $"camada Heal: peso {w0} estado em repouso '{st0}' -> apos trigger '{st1}', clipes {string.Join(",", ci.Select(c => c.clip.name + ":" + c.weight.ToString("F2")))}");
            Ok("8.17", st1.Contains("Heal") && ci.Any(c => c.clip.name == "Heal"), "trigger Heal -> camada de cura toca o clipe Heal (" + st1 + ")");
            Ok("8.17b", St(0).EndsWith("Idle"), "camada base nao e perturbada pelo Heal (" + St(0) + ")");
            float hl = contract.visual.animations.First(a => a.state == "Heal").clip.length;
            for (int i = 0; i < (int)(hl / 0.05f) + 30; i++) anim.Update(0.05f);
            Ok("8.18", !St(healLayer).Contains("Heal") || St(healLayer) == st0, "apos o Heal a camada volta ao repouso (" + St(healLayer) + ")");
        }
        // gatilhos do BT: Running/Attacking sem efeito
        anim.SetBool("Attacking", true); anim.SetBool("Running", true);
        for (int i = 0; i < 10; i++) anim.Update(0.05f);
        Ok("8.19", St().EndsWith("Idle"), "Running/Attacking (BT) nao alteram o estado (" + St() + ")");
        anim.SetBool("Attacking", false); anim.SetBool("Running", false);
        // trigger Hit durante Heal e Heal durante Attack: nao deve travar
        anim.SetTrigger("Hit"); anim.Update(0.2f); anim.SetTrigger("Heal"); for (int i = 0; i < 6; i++) anim.Update(0.1f);
        Info("8.20", "Hit seguido de Heal: estado base " + St(0) + (healLayer > 0 ? ", camada heal " + St(healLayer) : ""));
        for (int i = 0; i < 80; i++) anim.Update(0.05f);
        Ok("8.21", St(0).EndsWith("Idle") && (healLayer < 0 || !St(healLayer).Contains("Heal")), "apos Hit+Heal tudo volta ao repouso (" + St(0) + (healLayer > 0 ? " / " + St(healLayer) : "") + ")");
        foreach (string s in new[] { "Idle", "Walk", "Run" })
        {
            AnimationClip c = LoadClip(s);
            Ok("8.22", AnimationUtility.GetAnimationClipSettings(c).loopTime, s + ": loopTime ligado");
        }
        foreach (string s in new[] { "Attack", "Heal", "Hit", "Death" })
        {
            AnimationClip c = LoadClip(s);
            Ok("8.23", !AnimationUtility.GetAnimationClipSettings(c).loopTime, s + ": loopTime desligado");
        }
        foreach (string s in Clips)
        {
            AnimationClip c = LoadClip(s);
            foreach (AnimationEvent e in c.events)
                Ok("8.24", w.GetComponentsInChildren<MonoBehaviour>(true).Any(m => m.GetType().GetMethod(e.functionName) != null), s + ": evento '" + e.functionName + "' tem receptor");
        }
        anim.Rebind();
    }

    // ------------------------------------------------------------------ 9 (efeito de cura)
    private static void Section9_Effect()
    {
        GameObject fx = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath);
        Ok("9.1", fx != null, "PaladinHealAura.prefab carrega");
        PaladinHealAuraEffect comp = fx.GetComponent<PaladinHealAuraEffect>();
        Ok("9.2", comp != null && comp.ring != null && comp.ringTrail != null && comp.disc != null, "PaladinHealAuraEffect com ring, ringTrail e disc atribuidos");
        ParticleSystem[] ps = fx.GetComponentsInChildren<ParticleSystem>(true);
        Ok("9.3", ps.Length > 0, "efeito tem " + ps.Length + " sistema(s) de particulas");
        if (ps.Length > 0)
        {
            float maxDur = ps.Max(p => (p.main.loop ? 99f : p.main.duration) + p.main.startLifetime.constantMax);
            Ok("9.4", maxDur <= 3f, "particulas terminam em " + maxDur.ToString("F2") + " s (<= 3 s, o Destroy do PaladinHealBehaviour)");
            foreach (ParticleSystem p in ps)
                Info("9.4", $"particulas '{p.name}': loop={p.main.loop}, dur={p.main.duration}, vida={p.main.startLifetime.constantMax}, simulacao={p.main.simulationSpace}, scalingMode={p.main.scalingMode}, forma={p.shape.shapeType} raio={p.shape.radius:F2}, max={p.main.maxParticles}, emissao={p.emission.rateOverTime.constantMax}/s bursts={p.emission.burstCount}, gravidade={p.main.gravityModifier.constantMax}");
        }
        // quad e textura do anel: o raio visivel do anel confere com o raio de cura?
        Info("9.5", "duracao " + comp.duration + " s, expandTime " + comp.expandTime + " s, trailDelay " + comp.trailDelay);
        Ok("9.6", comp.expandTime < comp.duration && comp.duration <= 3f, "expansao termina antes do fim, e o efeito dura <= 3 s (Destroy em 3 s)");
        // diametro 1 em XZ
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(fx);
        PaladinHealAuraEffect c2 = inst.GetComponent<PaladinHealAuraEffect>();
        c2.Evaluate(c2.expandTime);
        Bounds b = default; bool any = false;
        foreach (Renderer r in inst.GetComponentsInChildren<Renderer>()) { if (r is ParticleSystemRenderer) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
        Ok("9.7", Mathf.Abs(b.size.x - 1f) < 0.05f && Mathf.Abs(b.size.z - 1f) < 0.05f, "diametro do efeito (anel/disco) em escala 1 = " + b.size.x.ToString("F3") + " x " + b.size.z.ToString("F3") + " (esperado 1.000)");
        Info("9.7", "altura do efeito (y) " + b.size.y.ToString("F3") + "; centro " + b.center.ToString("F2"));
        UnityEngine.Object.DestroyImmediate(inst);
        // perfil radial da textura do anel
        string rt = Root + "/Textures/PaladinHealAura_Ring.png";
        Texture2D tex = new Texture2D(2, 2); tex.LoadImage(File.ReadAllBytes(rt));
        int N = tex.width; float[] prof = new float[64]; int[] cnt = new int[64];
        Color32[] px = tex.GetPixels32();
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f; float rr = Mathf.Sqrt(dx * dx + dy * dy);
                if (rr >= 1f) continue;
                int bi = Mathf.Min(63, (int)(rr * 64)); Color32 p = px[y * N + x]; float a = Mathf.Max(p.a / 255f, 0f); float lum = (p.r + p.g + p.b) / 765f; prof[bi] += a * (tex.format == TextureFormat.RGB24 ? lum : 1f); cnt[bi]++;
            }
        int peak = 0; float pv = 0; for (int i = 0; i < 64; i++) { float v = cnt[i] > 0 ? prof[i] / cnt[i] : 0; if (v > pv) { pv = v; peak = i; } }
        int outer = 0; for (int i = 63; i >= 0; i--) { float v = cnt[i] > 0 ? prof[i] / cnt[i] : 0; if (v > pv * 0.15f) { outer = i; break; } }
        Ok("9.8", (peak + 0.5f) / 64f > 0.7f && (outer + 1f) / 64f > 0.85f, $"textura do anel: pico em {(peak + 0.5f) / 64f:F2} do raio do quad, borda visivel ate {(outer + 1f) / 64f:F2} (anel visivel ~ no raio de cura quando o quad = 2R)");
        UnityEngine.Object.DestroyImmediate(tex);
    }

    // ------------------------------------------------------------------ 10
    private static void Section10_Contract()
    {
        PaladinHealerContract c = AssetDatabase.LoadAssetAtPath<PaladinHealerContract>(ContractPath);
        ValidationReport vr = c.Validate();
        Ok("10.1", vr.IsValid, "Validate().IsValid: " + vr);
        VisualReadinessReport rr = c.EvaluateVisuals();
        Ok("10.2", rr.IsReady && c.IsVisualReady, "IsVisualReady: " + rr);
        GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        Ok("10.3", c.visual.prefab.asset == wrapper, "contrato.prefab = wrapper em Resources");
        Info("10.4", "contrato.meshes: " + string.Join("; ", c.visual.meshes.Select(m => m.role + " -> " + (m.asset != null ? AssetDatabase.GetAssetPath(m.asset) : "NULO") + " (esperado " + m.expectedAssetPath + ")")));
        Ok("10.4", c.visual.meshes.Length >= 1 && c.visual.meshes.All(m => m.asset != null), "contrato.meshes: todas resolvidas");
        Ok("10.4b", c.visual.meshes.Any(m => AssetDatabase.GetAssetPath(m.asset) == BodyFbx), "contrato.meshes inclui o FBX do corpo");
        Ok("10.4c", c.visual.meshes.Any(m => AssetDatabase.GetAssetPath(m.asset) == SwordFbx) && c.visual.meshes.Any(m => AssetDatabase.GetAssetPath(m.asset) == ShieldFbx), "contrato.meshes inclui espada e escudo");
        foreach (VisualAssetReference e in c.visual.effects)
            Ok("10.5", e.asset != null && AssetDatabase.GetAssetPath(e.asset) == e.expectedAssetPath, "efeito '" + e.role + "' aponta para " + (e.asset != null ? AssetDatabase.GetAssetPath(e.asset) : "NULO") + " (esperado " + e.expectedAssetPath + ")");
        foreach (AnimationRequirement a in c.visual.animations)
            Ok("10.6", a.clip != null && AssetDatabase.GetAssetPath(a.clip) == ModelsDir + "/PaladinHealer@" + a.state + ".fbx", "animacao '" + a.state + "' aponta para PaladinHealer@" + a.state + ".fbx");
        Ok("10.7", c.Projectile == null, "paladino corpo a corpo: sem projetil no contrato");
        Ok("10.8", !c.CanBeAddedToBuildSettings && !c.buildSettingsApproved, "buildSettingsApproved=false (nada no Build Settings por decisao do coordenador)");
        Ok("10.9", c.tiers.Length == 4, "4 tiers de era");
        string[] guids = AssetDatabase.FindAssets("t:CharacterData");
        List<string> same = new List<string>();
        foreach (string g in guids)
        {
            CharacterData cd = AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(g));
            if (cd != null && cd.code == Code) same.Add(AssetDatabase.GUIDToAssetPath(g));
        }
        Ok("10.11", same.Count == 1 && same[0] == CatalogPath, "exatamente 1 CharacterData com code=paladin_healer: " + string.Join(", ", same));
        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(CatalogPath);
        Ok("10.12", data != null && data.prefab == wrapper, "CharacterData.prefab = wrapper do contrato");
        PaladinEraTier a0 = c.tiers.First(t => t.era == ImperialEra.Ancient);
        Ok("10.13", data.healthpoints == a0.healthpoints && data.attackDamage == a0.attackDamage && Mathf.Approximately(data.attackRange, a0.attackRange), $"CharacterData = Ancient (vida {data.healthpoints}/{a0.healthpoints}, dano {data.attackDamage}/{a0.attackDamage}, alcance {data.attackRange}/{a0.attackRange})");
        Ok("10.13b", Mathf.Approximately(data.attackRate, AdvancedUnitDataBridge.AttackPollSeconds), $"CharacterData.attackRate = {data.attackRate} = AttackPollSeconds ({AdvancedUnitDataBridge.AttackPollSeconds}); cooldown real da era vem do controlador (Ancient {a0.attackCooldown})");
        Ok("10.14", data.cost != null && data.cost.Count == c.cost.Length && Mathf.Approximately(data.fieldOfView, c.fieldOfView), "CharacterData: custo e FOV iguais ao contrato");
        Info("10.14", "custo CharacterData: " + string.Join(", ", data.cost.Select(x => x.code + " x" + x.amount)) + "; contrato: " + string.Join(", ", c.cost.Select(x => x.resource + " x" + x.amount)));
        Info("10.15", "CharacterData.sprite " + (data.sprite == null ? "VAZIO (sem icone de UI)" : data.sprite.name) + "; sons: select=" + (data.onSelectSound != null) + ", move=" + (data.onMoveValidSound != null) + "; unitName=" + data.unitName);
        Ok("10.17", PrefabUtility.GetPrefabAssetType(wrapper) == PrefabAssetType.Regular, "wrapper e um prefab regular");
        Ok("10.18", EditorBuildSettings.scenes.All(s => !s.path.Contains("Paladin")), "Build Settings nao contem nada do Paladin");
        // contrato -> UnitDataFields por era (a ponte)
        foreach (ImperialEra era in ImperialEras.Ordered)
        {
            UnitDataFields f = c.BuildUnitDataFields(era);
            c.TryGetTier(era, out IEraTier tier);
            Ok("10.19", f.healthpoints == tier.HealthPoints && f.attackDamage == tier.AttackDamage && Mathf.Approximately(f.attackRange, tier.AttackRange), era + ": BuildUnitDataFields = tier");
        }
    }

    // ------------------------------------------------------------------ 11
    private static void Section11_Progression()
    {
        PaladinHealerContract c = AssetDatabase.LoadAssetAtPath<PaladinHealerContract>(ContractPath);
        int[] hp = { 70, 90, 115, 145 }, dmg = { 16, 20, 25, 31 }, heal = { 6, 9, 13, 18 };
        float[] rad = { 4f, 4.5f, 5f, 6f }, cd = { 1.8f, 1.7f, 1.6f, 1.5f };
        ImperialEra[] eras = { ImperialEra.Ancient, ImperialEra.Classical, ImperialEra.Medieval, ImperialEra.Imperial };
        for (int i = 0; i < 4; i++)
        {
            PaladinEraTier t = c.tiers.First(x => x.era == eras[i]);
            Ok("11.1", t.healthpoints == hp[i] && t.attackDamage == dmg[i] && t.healAmount == heal[i] && Mathf.Approximately(t.healRadius, rad[i]) && Mathf.Approximately(t.attackCooldown, cd[i]),
                $"{eras[i]}: vida {t.healthpoints}/{hp[i]}, dano {t.attackDamage}/{dmg[i]}, cura {t.healAmount}/{heal[i]}, raio {t.healRadius}/{rad[i]}, cooldown golpe {t.attackCooldown}/{cd[i]}");
            Info("11.1", $"{eras[i]}: alcance {t.attackRange}, cooldown de cura {t.healCooldown}, limite por alvo {t.maxHealPerTarget} (= {t.maxHealPerTarget / (float)t.healAmount:F1} curas)");
        }
        for (int i = 1; i < 4; i++)
        {
            PaladinEraTier p = c.tiers[i - 1], q = c.tiers[i];
            Ok("11.2", q.healthpoints > p.healthpoints && q.attackDamage > p.attackDamage && q.healAmount > p.healAmount && q.healRadius > p.healRadius && q.attackCooldown < p.attackCooldown && q.healCooldown <= p.healCooldown && q.maxHealPerTarget > p.maxHealPerTarget,
                $"{q.era}: vida, dano, cura, raio e limite crescem; cooldowns diminuem em relacao a {p.era}");
        }
        Ok("11.3", c.tiers.All(t => t.attackRange <= 2f), "alcance de ataque corpo a corpo <= 2 m em todas as eras");
        Ok("11.4", !c.canHealSelf, "canHealSelf=false (o paladino nao se cura)");
        Ok("11.5", Mathf.Approximately(c.healCapWindowSeconds, 10f), "janela do limite por alvo = " + c.healCapWindowSeconds + " s");
    }

    // ------------------------------------------------------------------ 14
    private static void Section14_References()
    {
        List<string> files = new List<string>();
        files.AddRange(Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Where(f => f.EndsWith(".prefab") || f.EndsWith(".controller") || f.EndsWith(".mat") || f.EndsWith(".asset") || f.EndsWith(".anim") || f.EndsWith(".mask")));
        files.AddRange(new[] { WrapperPath, CatalogPath, ContractPath, EffectPath });
        Regex rx = new Regex(@"guid: ([0-9a-f]{32})");
        int total = 0, broken = 0; List<string> brokenList = new List<string>();
        foreach (string f in files)
        {
            string txt = File.ReadAllText(f);
            if (!txt.StartsWith("%YAML")) continue;
            foreach (Match m in rx.Matches(txt))
            {
                total++;
                string g = m.Groups[1].Value;
                if (g == "0000000000000000f000000000000000" || g == "0000000000000000e000000000000000") continue;
                if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(g))) { broken++; brokenList.Add(f + " -> " + g); }
            }
        }
        Ok("14.1", broken == 0, $"referencias por GUID resolvidas: {total} verificadas em {files.Count} arquivos, {broken} quebradas {string.Join("; ", brokenList.Take(5))}");
        string[] scripts = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);
        var dupTypes = new Dictionary<string, List<string>>();
        Regex tx = new Regex(@"^\s*(?:public |internal |static |sealed |abstract |partial )*(?:class|struct|enum|interface) (Paladin\w*)", RegexOptions.Multiline);
        foreach (string s in scripts)
        {
            string t = File.ReadAllText(s);
            foreach (Match m in tx.Matches(t))
            {
                string n = m.Groups[1].Value;
                if (!dupTypes.ContainsKey(n)) dupTypes[n] = new List<string>();
                dupTypes[n].Add(s);
            }
        }
        var dups = dupTypes.Where(kv => kv.Value.Count > 1).ToList();
        Ok("14.2", dups.Count == 0, "sem tipos Paladin* duplicados (" + dupTypes.Count + " tipos: " + string.Join(", ", dupTypes.Keys) + ")");
        foreach (string n in new[] { "PaladinHealer.prefab", "PaladinHealer.controller", "PaladinHealer.fbx", "PaladinHealAura.prefab" })
        {
            var found = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(n)).Select(AssetDatabase.GUIDToAssetPath).Where(p => Path.GetFileName(p) == n).ToList();
            Ok("14.3", found.Count == 1, n + ": " + found.Count + " no projeto (" + string.Join(", ", found) + ")");
        }
        Info("14.3", "PaladinHealer.asset: " + string.Join(", ", AssetDatabase.FindAssets("PaladinHealer t:ScriptableObject").Select(AssetDatabase.GUIDToAssetPath).Where(p => Path.GetFileName(p) == "PaladinHealer.asset")) + " (contrato em Data + CharacterData em Resources: nomes iguais por convencao)");
        var guidOwners = new Dictionary<string, string>(); int dupGuid = 0;
        foreach (string meta in Directory.GetFiles(Root, "*.meta", SearchOption.AllDirectories))
        {
            var m = Regex.Match(File.ReadAllText(meta), @"guid: ([0-9a-f]{32})");
            if (m.Success) { if (guidOwners.ContainsKey(m.Groups[1].Value)) dupGuid++; else guidOwners[m.Groups[1].Value] = meta; }
        }
        Ok("14.5", dupGuid == 0, "GUIDs de .meta unicos na pasta da unidade (" + guidOwners.Count + ")");
        // todo arquivo de Assets/CharacterRigging/PaladinHealer tem .meta (e vice-versa)
        int orphan = 0;
        foreach (string f in Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta")))
            if (!File.Exists(f + ".meta")) orphan++;
        Ok("14.6", orphan == 0, "todo arquivo da unidade tem .meta (" + orphan + " sem)");
    }

    // ------------------------------------------------------------------ 13 (renders no editor)
    private static void PoseAt(Animator anim, AnimationClip clip, float t)
    {
        clip.SampleAnimation(anim.gameObject, Mathf.Clamp(t, 0f, clip.length));
    }

    private static void Section13_Renders()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
        AnimationClip idle = LoadClip("Idle");

        // teste de que SampleAnimation realmente move os ossos (humanoide em edit mode)
        Transform rh = anim.GetBoneTransform(HumanBodyBones.RightHand);
        PoseAt(anim, idle, 0f); Vector3 p0 = rh.position;
        PoseAt(anim, LoadClip("Attack"), 0.6f); Vector3 p1 = rh.position;
        Ok("13.0", Vector3.Distance(p0, p1) > 0.05f, "SampleAnimation move o osso da mao (" + Vector3.Distance(p0, p1).ToString("F3") + " m entre Idle@0 e Attack@0.6) - validade dos renders de pose");

        // vistas por LOD (pose Idle, quadro 0.5 s)
        List<Texture2D> tiles = new List<Texture2D>();
        for (int lod = 0; lod < 3; lod++)
        {
            lg.ForceLOD(lod);
            PoseAt(anim, idle, 0.5f);
            foreach (var v in new[] { ("front", w.transform.forward), ("back", -w.transform.forward), ("side", w.transform.right) })
                mags[$"LOD{lod}-{v.Item1}"] = Shoot(cam, new Vector3(0, 0.95f, 0), v.Item2, 3.6f, $"render-LOD{lod}-{v.Item1}.png", 0.05f, 800, 800, tiles);
        }
        Sheet("sheet-lods.png", 3, tiles, 800, 800);
        lg.ForceLOD(0);

        // poses de cada clipe (LOD0), frente e lado
        Vector3 center = new Vector3(0, 0.95f, 0);
        (string clip, float[] fracs)[] sets = {
            ("Idle", new[] { 0.0f, 0.5f }), ("Walk", new[] { 0.0f, 0.5f }), ("Run", new[] { 0.0f, 0.5f }),
            ("Attack", new[] { 0.15f, 0.3f, 0.45f, 0.6f, 0.75f, 0.9f }), ("Heal", new[] { 0.2f, 0.4f, 0.6f, 0.8f }),
            ("Hit", new[] { 0.2f, 0.5f }), ("Death", new[] { 0.15f, 0.4f, 0.7f, 1.0f }) };
        foreach (var set in sets)
        {
            AnimationClip clip = LoadClip(set.clip);
            List<Texture2D> front = new List<Texture2D>(), side = new List<Texture2D>();
            foreach (float f in set.fracs)
            {
                PoseAt(anim, clip, clip.length * f);
                Bounds b = RendBounds(w, r => !(r.name == "TeamRing"));
                Vector3 c = set.clip == "Death" ? new Vector3(0, 0.6f, 0) : center;
                float dist = 4.2f;
                mags[$"pose-{set.clip}-{f:F2}-front"] = Shoot(cam, c, w.transform.forward, dist, "", 0.05f, 400, 560, front, false);
                mags[$"pose-{set.clip}-{f:F2}-side"] = Shoot(cam, c, w.transform.right, dist, "", 0.05f, 400, 560, side, false);
            }
            int cols = set.fracs.Length;
            List<Texture2D> all = new List<Texture2D>(); all.AddRange(front); all.AddRange(side);
            Sheet($"sheet-pose-{set.clip}.png", cols, all, 400, 560);
        }
        PoseAt(anim, idle, 0.5f);
        // closeups: espada e escudo na pose Idle
        Transform sword = w.GetComponentsInChildren<Transform>(true).First(t => t.name.ToLower().Contains("sword"));
        Transform shield = w.GetComponentsInChildren<Transform>(true).First(t => t.name.ToLower().Contains("shield"));
        List<Texture2D> closes = new List<Texture2D>();
        foreach (var part in new[] { ("sword", sword), ("shield", shield) })
        {
            Bounds pb = RendBounds(part.Item2.gameObject);
            Bounds ctx = RendBounds(w, r => !(r.name == "TeamRing"));
            // vista aproximada de frente, direita e costas ao redor da mao/antebraco
            Vector3 cc = pb.center;
            foreach (var v in new[] { ("front", w.transform.forward), ("right", w.transform.right), ("left", -w.transform.right), ("back", -w.transform.forward) })
                mags[$"close-{part.Item1}-{v.Item1}"] = Shoot(cam, cc, v.Item2, 1.6f, "", 0.1f, 500, 500, closes, false);
        }
        Sheet("sheet-closeups.png", 4, closes, 500, 500);
        // pose de ataque: espada perto da mao no meio do golpe
        UnityEngine.Object.DestroyImmediate(w);
        foreach (var kv in mags) Ok("13.m", kv.Value < 0.0005f, $"{kv.Key}: pixels magenta {(kv.Value * 100f):F4}%");
        Info("13.m", "magenta: maior fracao " + (mags.Values.Max() * 100f).ToString("F4") + "% em " + mags.OrderByDescending(k => k.Value).First().Key);
        UnityEngine.Object.DestroyImmediate(cam.gameObject);
    }

    // ------------------------------------------------------------------ 12b (geometria de poses: deformacao, espada, escudo)
    private static void Section12_PoseGeometry()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject w = InstantiateWrapper();
        foreach (NavMeshAgent a in w.GetComponentsInChildren<NavMeshAgent>()) a.enabled = false;
        foreach (CharacterBT bt in w.GetComponentsInChildren<CharacterBT>()) bt.enabled = false;
        Animator anim = w.GetComponentInChildren<Animator>();
        LODGroup lg = w.GetComponentInChildren<LODGroup>();
        SkinnedMeshRenderer[] smrs = w.GetComponentsInChildren<SkinnedMeshRenderer>();
        SkinnedMeshRenderer lod0 = smrs.OrderByDescending(s => Tris(s.sharedMesh)).First();
        SkinnedMeshRenderer lod1 = smrs.OrderByDescending(s => Tris(s.sharedMesh)).Skip(1).First();
        SkinnedMeshRenderer lod2 = smrs.OrderByDescending(s => Tris(s.sharedMesh)).Skip(2).First();
        Info("12b.0", "SMRs: " + string.Join(", ", smrs.Select(s => s.name + "=" + Tris(s.sharedMesh))));
        foreach (SkinnedMeshRenderer s in smrs) s.updateWhenOffscreen = true;

        // arestas do LOD de cada malha (para medir estiramento)
        Func<SkinnedMeshRenderer, (int[], int[])> edges = s =>
        {
            Mesh m = s.sharedMesh; int[] tr = m.triangles; HashSet<long> set = new HashSet<long>(); List<int> a = new List<int>(), b = new List<int>();
            for (int i = 0; i < tr.Length; i += 3)
                for (int k = 0; k < 3; k++)
                {
                    int u = tr[i + k], v = tr[i + (k + 1) % 3]; int lo = Math.Min(u, v), hi = Math.Max(u, v);
                    if (set.Add(((long)lo << 32) | (uint)hi)) { a.Add(lo); b.Add(hi); }
                }
            return (a.ToArray(), b.ToArray());
        };
        var ed0 = edges(lod0);
        Mesh baked = new Mesh();
        PoseAt(anim, LoadClip("Idle"), 0f);
        // pose de ligacao (referencia): usa o mesh sem skin (bindpose): vertices locais do sharedMesh
        Vector3[] bind = lod0.sharedMesh.vertices;
        float[] bindLen = new float[ed0.Item1.Length];
        for (int i = 0; i < bindLen.Length; i++) bindLen[i] = Vector3.Distance(bind[ed0.Item1[i]], bind[ed0.Item2[i]]);

        Transform wm = FindSocket(w, "WeaponMount");
        Transform sword = w.GetComponentsInChildren<Transform>(true).First(t => t.name.ToLower().Contains("sword"));
        Transform shield = w.GetComponentsInChildren<Transform>(true).First(t => t.name.ToLower().Contains("shield"));
        Renderer[] swr = sword.GetComponentsInChildren<Renderer>();
        // ponta da espada: vertice mais distante do cabo (no espaco local da espada)
        MeshFilter swf = sword.GetComponentsInChildren<MeshFilter>().OrderByDescending(f => f.sharedMesh.bounds.size.magnitude).First();
        Vector3 tipLocal = Vector3.zero; float maxd = 0; Vector3 gripLocal = swf.transform.InverseTransformPoint(wm.position);
        foreach (Vector3 v in swf.sharedMesh.vertices) { float d = Vector3.Distance(v, gripLocal); if (d > maxd) { maxd = d; tipLocal = v; } }
        Info("12b.1", $"espada: ponta a {maxd * swf.transform.lossyScale.x:F2} m do ponto de cabo (WeaponMount)");
        Transform sTip = swf.transform;

        Transform rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        Transform leftHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
        MeshFilter shf = shield.GetComponentsInChildren<MeshFilter>().OrderByDescending(f => f.sharedMesh.bounds.size.magnitude).First();

        foreach (string cname in Clips)
        {
            AnimationClip clip = LoadClip(cname);
            int samples = 24;
            List<float> allRatios = new List<float>();
            float worstMax = 0, worstMin = 10; int over2 = 0, over3 = 0, under04 = 0, spikes = 0, spikesMax = 0; float longest = 0; string worstAt = "";
            double hairp99Sum = 0;
            float minTipY = 9, maxTipAboveHead = -9, minGripDist = 9, maxGripDist = 0, minTipToBody = 9, maxShieldPen = 0;
            List<Vector3> tips = new List<Vector3>(); List<float> times = new List<float>();
            for (int s = 0; s <= samples; s++)
            {
                float t = clip.length * s / samples;
                PoseAt(anim, clip, t);
                lod0.BakeMesh(baked, false);
                Vector3[] v = baked.vertices;
                float[] r = new float[bindLen.Length];
                for (int i = 0; i < r.Length; i++)
                {
                    float L = bindLen[i]; if (L < 1e-4f) { r[i] = 1; continue; }
                    r[i] = Vector3.Distance(v[ed0.Item1[i]], v[ed0.Item2[i]]) / L;
                }
                float[] sorted = (float[])r.Clone(); Array.Sort(sorted);
                float p99 = sorted[(int)(sorted.Length * 0.99f)], p999 = sorted[(int)(sorted.Length * 0.999f)], mx = sorted[sorted.Length - 1], mn = sorted[0];
                hairp99Sum += p99;
                int c2 = 0, c3 = 0, cu = 0, sp = 0; for (int i = 0; i < r.Length; i++) { float x = r[i]; if (x > 2f) c2++; if (x > 3f) c3++; if (x < 0.4f) cu++; float pl = x * bindLen[i]; if (pl > longest) longest = pl; if (pl > 0.10f && bindLen[i] < 0.05f) sp++; } spikesMax = Math.Max(spikesMax, sp);
                over2 = Math.Max(over2, c2); over3 = Math.Max(over3, c3); under04 = Math.Max(under04, cu);
                if (mx > worstMax) { worstMax = mx; worstAt = $"t={t:F2}s"; }
                worstMin = Math.Min(worstMin, mn);
                // geometria da espada
                Vector3 tip = sTip.TransformPoint(tipLocal);
                Vector3 grip = wm.position;
                minTipY = Mathf.Min(minTipY, tip.y);
                maxTipAboveHead = Mathf.Max(maxTipAboveHead, tip.y - head.position.y);
                float gd = Vector3.Distance(grip, rightHand.position);
                minGripDist = Mathf.Min(minGripDist, gd); maxGripDist = Mathf.Max(maxGripDist, gd);
                tips.Add(tip); times.Add(t);
                // ponta da espada x corpo: distancia minima da lamina (pontos alem de 0.30 m do cabo) aos vertices do corpo
                Matrix4x4 l2w = lod0.transform.localToWorldMatrix;
                float minD = 9;
                for (int k = 1; k <= 8; k++)
                {
                    Vector3 pt = Vector3.Lerp(grip, tip, 0.3f + 0.7f * k / 8f);
                    for (int i = 0; i < v.Length; i += 7)
                    {
                        float d = (l2w.MultiplyPoint3x4(v[i]) - pt).sqrMagnitude;
                        if (d < minD) minD = d;
                    }
                }
                minTipToBody = Mathf.Min(minTipToBody, Mathf.Sqrt(minD));
                // escudo: vertices do corpo dentro da caixa (OBB) do escudo, encolhida
                Bounds sb = shf.sharedMesh.bounds; Vector3 ext = sb.extents * 0.9f;
                Matrix4x4 w2s = shf.transform.worldToLocalMatrix;
                int inside = 0;
                for (int i = 0; i < v.Length; i += 3)
                {
                    Vector3 lp = w2s.MultiplyPoint3x4(l2w.MultiplyPoint3x4(v[i])) - sb.center;
                    if (Mathf.Abs(lp.x) < ext.x && Mathf.Abs(lp.y) < ext.y && Mathf.Abs(lp.z) < ext.z) inside++;
                }
                maxShieldPen = Mathf.Max(maxShieldPen, inside);
            }
            // velocidade da ponta -> instante do golpe
            float peakSpeed = 0, peakT = 0; for (int i = 1; i < tips.Count; i++) { float sp = Vector3.Distance(tips[i], tips[i - 1]) / (times[i] - times[i - 1]); if (sp > peakSpeed) { peakSpeed = sp; peakT = (times[i] + times[i - 1]) / 2; } }
            Info("12b." + cname, $"{cname}: aresta/bind p99 medio {hairp99Sum / (samples + 1):F2}; max {worstMax:F2} ({worstAt}); min {worstMin:F2}; #arestas>2.0 (max) {over2}, >3.0 {over3}, <0.4 {under04} de {bindLen.Length}");
            Info("12b." + cname, $"{cname}: espada: cabo-mao {minGripDist:F3}..{maxGripDist:F3} m; ponta y min {minTipY:F2} m; ponta acima da cabeca max {maxTipAboveHead:F2} m; lamina x corpo dist min {minTipToBody:F3} m; pico de velocidade da ponta {peakSpeed:F1} m/s em t={peakT:F2}s de {clip.length:F2}s; escudo: vertices do corpo dentro da caixa do escudo (max, amostra 1/3) {maxShieldPen}");
            Info("12b." + cname, $"{cname}: aresta mais longa em pose {longest:F3} m; arestas \"espeto\" (> 10 cm em pose, < 5 cm no repouso), max por amostra: {spikesMax}");
            Ok("12b.d", longest < 0.20f && spikesMax < 20, $"{cname}: deformacao: aresta mais longa {longest:F3} m (limite 0.20), arestas-espeto (>10 cm vindas de < 5 cm) {spikesMax} (limite 20)");
            Ok("12b.s", maxGripDist < 0.15f, $"{cname}: espada permanece na mao (cabo-mao max {maxGripDist:F3} m)");
            Ok("12b.g", minTipY > -0.05f || cname == "Death", $"{cname}: ponta da espada nao atravessa o chao (y min {minTipY:F2} m)");
        }
        UnityEngine.Object.DestroyImmediate(w);
        UnityEngine.Object.DestroyImmediate(baked);
    }

    // ------------------------------------------------------------------ 9b (renders do efeito de cura em escala de jogo)
    private static void Section9b_EffectRenders()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeLight();
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.transform.localScale = Vector3.one * 3;
        floor.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.35f, 0.42f, 0.30f) };
        GameObject w = InstantiateWrapper();
        foreach (NavMeshAgent a in w.GetComponentsInChildren<NavMeshAgent>()) a.enabled = false;
        foreach (CharacterBT bt in w.GetComponentsInChildren<CharacterBT>()) bt.enabled = false;
        w.GetComponentInChildren<Animator>().Rebind();
        foreach (Renderer r in w.GetComponentsInChildren<Renderer>()) if (r.name == "TeamRing" || r.name == "SelectionCircle") r.enabled = false;
        PoseAt(w.GetComponentInChildren<Animator>(), LoadClip("Idle"), 0.5f);
        PaladinHealerContract contract = AssetDatabase.LoadAssetAtPath<PaladinHealerContract>(ContractPath);
        GameObject fxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath);
        Transform origin = FindSocket(w, "HealAuraCenter");
        // aliados de referencia a distancias conhecidas
        float[] dists = { 2.0f, 3.5f, 5.0f, 7.0f };
        List<GameObject> dummies = new List<GameObject>();
        for (int i = 0; i < dists.Length; i++)
        {
            GameObject d = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            d.transform.position = new Vector3(Mathf.Cos(i * 1.4f) * dists[i], 0.9f, Mathf.Sin(i * 1.4f) * dists[i]);
            d.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = i < 3 ? new Color(0.2f, 0.4f, 0.9f) : new Color(0.9f, 0.3f, 0.2f) };
            UnityEngine.Object.DestroyImmediate(d.GetComponent<Collider>());
            dummies.Add(d);
        }
        Camera cam = MakeCamera(new Color(0.55f, 0.6f, 0.65f));
        cam.fieldOfView = 40;
        Dictionary<string, float> mags = new Dictionary<string, float>();
        foreach (ImperialEra era in new[] { ImperialEra.Ancient, ImperialEra.Imperial })
        {
            contract.TryGetTier(era, out IEraTier tier);
            float r = ((PaladinEraTier)tier).healRadius;
            GameObject fx = (GameObject)PrefabUtility.InstantiatePrefab(fxPrefab);
            fx.transform.position = origin.position; fx.transform.rotation = Quaternion.identity;
            fx.transform.localScale = new Vector3(r * 2f, 1f, r * 2f);   // identico ao PaladinHealBehaviour.PlayFeedback
            PaladinHealAuraEffect comp = fx.GetComponent<PaladinHealAuraEffect>();
            List<Texture2D> tiles = new List<Texture2D>();
            foreach (float t in new[] { 0.2f, 0.45f, 0.75f, 1.15f })
            {
                comp.Evaluate(t);
                foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>()) ps.Simulate(t, true, true);
                mags[$"fx-{era}-{t:F2}-oblique"] = Shoot(cam, new Vector3(0, 0.6f, 0), new Vector3(0.0f, 0.8f, -1f), r * 3.4f, "", 0.0f, 500, 500, tiles, false);
            }
            foreach (float t in new[] { 0.45f })
            {
                comp.Evaluate(t);
                foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>()) ps.Simulate(t, true, true);
                mags[$"fx-{era}-{t:F2}-side"] = Shoot(cam, new Vector3(0, 1.0f, 0), Vector3.right, r * 3.0f, "", 0.02f, 500, 500, tiles, false);
                mags[$"fx-{era}-{t:F2}-top"] = Shoot(cam, new Vector3(0, 0.0f, 0), Vector3.up + new Vector3(0, 0, -0.01f), r * 3.0f, "", 0.0f, 500, 500, tiles, false);
            }
            Sheet($"sheet-heal-effect-{era}.png", 3, tiles, 500, 500);
            // extensao real das particulas (mundo) em varios instantes
            float maxHoriz = 0, maxH = -9, minH = 9; int sampled = 0;
            foreach (float t in new[] { 0.3f, 0.6f, 0.9f, 1.2f })
            {
                foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
                {
                    ps.Simulate(t, true, true);
                    ParticleSystem.Particle[] arr = new ParticleSystem.Particle[ps.main.maxParticles];
                    int n = ps.GetParticles(arr);
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 pw = ps.main.simulationSpace == ParticleSystemSimulationSpace.World ? arr[i].position : ps.transform.TransformPoint(arr[i].position);
                        float hz = Mathf.Sqrt(pw.x * pw.x + pw.z * pw.z); maxHoriz = Mathf.Max(maxHoriz, hz); maxH = Mathf.Max(maxH, pw.y); minH = Mathf.Min(minH, pw.y); sampled++;
                    }
                }
            }
            Info("9b." + era, $"raio de cura {r:F1} m: particulas vivas amostradas {sampled}; alcance horizontal max {maxHoriz:F2} m ({(r > 0 ? maxHoriz / r : 0):P0} do raio); altura {minH:F2}..{maxH:F2} m; efeito instanciado em y={origin.position.y:F2} m (HealAuraCenter)");
            Ok("9b.1", sampled == 0 || maxHoriz <= r * 1.1f, $"{era}: particulas ficam dentro do raio de cura (max {maxHoriz:F2} m <= {r * 1.1f:F2})");
            comp.Evaluate(comp.expandTime);
            Bounds rb = comp.ring.bounds;
            Ok("9b.2", Mathf.Abs(rb.extents.x - r) < r * 0.1f, $"{era}: anel expandido tem raio {rb.extents.x:F2} m = raio de cura {r:F2} m");
            UnityEngine.Object.DestroyImmediate(fx);
        }
        foreach (var kv in mags) Ok("9b.m", kv.Value < 0.0005f, $"{kv.Key}: pixels magenta {(kv.Value * 100f):F4}%");
        UnityEngine.Object.DestroyImmediate(cam.gameObject);
        UnityEngine.Object.DestroyImmediate(w);
    }

    // ===================================================================== PLAY MODE

    private static readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>();
    private static double _wakeAt;
    private static bool _started;
    private static Vector3 _spawnBase;
    private static string _map = "";

    static PaladinHealerIndependentAudit()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    public static void RunPlay()
    {
        Begin(PlayReport);
        SessionState.SetBool(Key + "Running", true);
        SessionState.SetInt(Key + "Errors", 0);
        SessionState.SetInt(Key + "Warnings", 0);
        SessionState.SetInt(Key + "Pass", 0);
        SessionState.SetInt(Key + "Fail", 0);
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 3300f);
        SessionState.SetString(Key + "DataPath", Path.GetFullPath("TestResults/PHIndepData-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void IsolateSaves()
    {
        if (SessionState.GetBool(Key + "Running", false))
            BinarySerializable.DATA_DIRECTORY = SessionState.GetString(Key + "DataPath", "TestResults/PHIndepData");
    }

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

    /// <summary>Espera em tempo de JOGO (Time.time): o Play Mode em batch pode rodar a poucos quadros por segundo.</summary>
    private static IEnumerator GT(double seconds)
    {
        float end = Time.time + (float)seconds;
        while (Time.time < end) yield return 0.0;
    }

    private static IEnumerator Flow()
    {
        for (int cycle = 0; cycle < 2; cycle++)
        {
            string mapName = cycle == 0 ? "Map1" : "Map2";
            _map = mapName;
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
            P(mapName + ".1", Globals.CHARACTER_DATA.ContainsKey(Code), "Globals.CHARACTER_DATA contem paladin_healer no " + mapName);
            Transform spawnpoints = GameObject.Find("Spawnpoints").transform;
            NavMesh.SamplePosition(spawnpoints.GetChild(0).position, out NavMeshHit start, 40, NavMesh.AllAreas);
            _spawnBase = start.position;
            yield return TestPaladin(mapName);
            yield return 1.0;
            EventManager.TriggerEvent("PausedGame");
            CoreBooter.instance.LoadMenu();
            yield return WaitUntil(() => !SceneManager.GetSceneByName("GameScene").isLoaded && SceneManager.GetSceneByName("MainMenu").isLoaded, 90, "volta ao menu");
            yield return 2.0;
        }
    }

    private static Character Spawn(string code, int owner, Vector3 position, bool keepBt = false)
    {
        Character c = new Character(Globals.CHARACTER_DATA[code], owner);
        // dummies do auditor (soldier = 30 PV) precisam sobreviver a golpes de 31 e curas acumuladas sem cortar o teto de vida
        c.SetEraStats(500, c.AttackDamage, c.AttackRange);
        c.ComputeProduction();
        NavMeshAgent agent = c.Transform.GetComponent<NavMeshAgent>();
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 6f, NavMesh.AllAreas)) position = hit.position;
        agent.Warp(position);
        if (!keepBt) { CharacterBT bt = c.Transform.GetComponent<CharacterBT>(); if (bt != null) bt.enabled = false; agent.isStopped = true; }
        return c;
    }

    private static int EnemyOwner()
    {
        int mine = GameManager.instance.gamePlayersParameters.myPlayerId;
        return GameManager.instance.gamePlayersParameters.players.Length > 1 ? (mine == 0 ? 1 : 0) : mine + 1;
    }

    private static string AnimState(Animator a, int layer = 0)
    {
        var info = a.GetCurrentAnimatorClipInfo(layer);
        if (info.Length == 0) return "?"; AnimatorClipInfo top = info[0]; foreach (AnimatorClipInfo c in info) if (c.weight > top.weight) top = c; return top.clip.name;
    }

    private static float Flat(Vector3 a, Vector3 b) { return AdvancedCombat.FlatDistance(a, b); }

    private static void Kill(params Character[] cs)
    {
        foreach (Character c in cs) if (c != null && c.Transform != null) UnityEngine.Object.Destroy(c.Transform.gameObject);
    }

    private static List<GameObject> FindEffects()
    {
        return UnityEngine.Object.FindObjectsByType<PaladinHealAuraEffect>(FindObjectsSortMode.None).Select(e => e.gameObject).ToList();
    }

    // -------------------------------------------------------------- captures
    private static float ShootPlay(Camera cam, string file, int w = 1024, int h = 768)
    {
        Texture2D t = Render(cam, w, h);
        File.WriteAllBytes(Path.Combine(OutDir, file), t.EncodeToPNG());
        float f = MagentaFraction(t);
        UnityEngine.Object.DestroyImmediate(t);
        return f;
    }

    private static Camera PlayCam()
    {
        GameObject g = new GameObject("PHAuditCam");
        Camera c = g.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.45f, 0.5f, 0.55f);
        c.fieldOfView = 30; c.nearClipPlane = 0.1f; c.farClipPlane = 300; c.cullingMask = ~0;
        return c;
    }

    private static Bounds VisBounds(GameObject go)
    {
        Bounds b = default; bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (r.name == "TeamRing" || r.name == "MinimapIcon" || r.name == "SelectionCircle" || r.name.ToLower().Contains("fov") || r is ParticleSystemRenderer || r is TrailRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private static void CaptureViews(GameObject unit, string prefix)
    {
        foreach (LODGroup lod in unit.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
        Bounds b = VisBounds(unit);
        float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
        Camera cam = PlayCam();
        foreach (var v in new[] { ("front", unit.transform.forward), ("back", -unit.transform.forward), ("side", unit.transform.right) })
        {
            cam.transform.position = b.center + v.Item2 * size * 2.1f + Vector3.up * size * 0.15f;
            cam.transform.LookAt(b.center);
            float m = ShootPlay(cam, $"play-{_map}-{prefix}-{v.Item1}.png");
            P(_map + ".cap", m < 0.001f, $"{prefix}-{v.Item1}: pixels magenta {m * 100:F4}%");
        }
        UnityEngine.Object.Destroy(cam.gameObject);
        foreach (LODGroup lod in unit.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(-1);
    }

    private static void CaptureWide(Vector3 center, float radius, string file, float height = 1.0f)
    {
        Camera cam = PlayCam();
        cam.fieldOfView = 40;
        cam.transform.position = center + new Vector3(0f, radius * 1.15f + 3f, -radius * 1.3f - 2f);
        cam.transform.LookAt(center + Vector3.up * height * 0.3f);
        float m = ShootPlay(cam, file);
        P(_map + ".cap", m < 0.001f, $"{file}: pixels magenta {m * 100:F4}%");
        UnityEngine.Object.Destroy(cam.gameObject);
    }

    private static void CaptureNear(GameObject unit, Vector3 lookFrom, string file, float dist = 5f)
    {
        Bounds b = VisBounds(unit);
        Camera cam = PlayCam();
        cam.fieldOfView = 40;
        cam.transform.position = b.center + lookFrom.normalized * dist + Vector3.up * 1.2f;
        cam.transform.LookAt(b.center);
        float m = ShootPlay(cam, file);
        P(_map + ".cap", m < 0.001f, $"{file}: pixels magenta {m * 100:F4}%");
        UnityEngine.Object.Destroy(cam.gameObject);
    }

    // -------------------------------------------------------------- the test
    private static IEnumerator TestPaladin(string mapName)
    {
        string T = mapName;
        int mine = GameManager.instance.gamePlayersParameters.myPlayerId;
        int foeOwner = EnemyOwner();
        PaladinHealerContract contract = AssetDatabase.LoadAssetAtPath<PaladinHealerContract>(ContractPath);
        PI(T, $"meu jogador {mine}, jogador inimigo {foeOwner}, jogadores {GameManager.instance.gamePlayersParameters.players.Length}");
        float wallT0 = (float)EditorApplication.timeSinceStartup, gameT0 = Time.time; int frame0 = Time.frameCount;

        Character pal = Spawn(Code, mine, _spawnBase + new Vector3(2, 0, 2), true);
        GameObject go = pal.Transform.gameObject;
        yield return GT(1.5);
        UnitManager um = go.GetComponent<UnitManager>();
        CharacterBT bt = go.GetComponent<CharacterBT>();
        PaladinHealBehaviour heal = go.GetComponent<PaladinHealBehaviour>();
        PaladinMeleeAttackController melee = go.GetComponent<PaladinMeleeAttackController>();
        AdvancedUnitEraBinding bind = go.GetComponent<AdvancedUnitEraBinding>();
        Animator anim = go.GetComponentInChildren<Animator>();
        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        P(T + ".2", heal != null && melee != null && bind != null && anim != null && agent != null && agent.isOnNavMesh, "instancia pelo catalogo com PaladinHealBehaviour, PaladinMeleeAttackController, binding, animator e agente no NavMesh");
        P(T + ".3", bind.HasApplied && pal.HP == 70 && pal.MaxHP == 70, $"era Ancient aplicada: HP {pal.HP}/{pal.MaxHP}");
        P(T + ".3b", go.GetComponentsInChildren<SkinnedMeshRenderer>().Length >= 3 && go.GetComponentsInChildren<Renderer>().Any(r => r.name.ToLower().Contains("sword")) && go.GetComponentsInChildren<Renderer>().Any(r => r.name.ToLower().Contains("shield")), "visual do paladino com corpo (3 LODs), espada e escudo");
        PI(T + ".fps", $"taxa de quadros no batch: {(Time.frameCount - frame0) / Mathf.Max(0.01f, (float)EditorApplication.timeSinceStartup - wallT0):F1} fps; tempo de jogo/parede = {(Time.time - gameT0) / Mathf.Max(0.01f, (float)EditorApplication.timeSinceStartup - wallT0):F2}");
        P(T + ".3c", !go.GetComponentsInChildren<MonoBehaviour>(true).Any(m => m == null), "sem scripts ausentes na instancia");
        P(T + ".3d", go.GetComponent<NavMeshAgent>().speed > 0, $"agente: velocidade {agent.speed}, raio {agent.radius}");
        if (bt != null) bt.enabled = false; // testes de cura: sem a IA propria do paladino interferindo
        agent.isStopped = true;

        // ---- eras: vida maxima, dano e alcance seguem o contrato (e a ponte)
        foreach (ImperialEra era in ImperialEras.Ordered)
        {
            AdvancedEraState.Set(era);
            yield return GT(0.2);
            contract.TryGetTier(era, out IEraTier tier);
            PaladinEraTier t = (PaladinEraTier)tier;
            P(T + ".4", pal.MaxHP == t.healthpoints && pal.AttackDamage >= t.attackDamage && Mathf.Approximately(pal.AttackRange, t.attackRange) && bind.AppliedEra == era, $"{era}: vida max {pal.MaxHP}/{t.healthpoints}, dano {pal.AttackDamage}/{t.attackDamage}, alcance {pal.AttackRange}/{t.attackRange}");
            P(T + ".4b", bind.CurrentTier != null && bind.CurrentTier.Era == era, $"{era}: binding.CurrentTier = tier da era");
        }
        AdvancedEraState.Set(ImperialEra.Ancient);
        yield return GT(0.3);
        pal.HP = pal.MaxHP;

        // ---- movimento + animator
        Vector3 before = go.transform.position;
        Vector3 goal = before + go.transform.forward * 8f;
        if (NavMesh.SamplePosition(goal, out NavMeshHit gh, 8f, NavMesh.AllAreas)) goal = gh.position;
        agent.isStopped = false;
        CharacterManager cmgr = go.GetComponent<CharacterManager>();
        P(T + ".5", cmgr.MoveTo(goal, false), "ordem de movimento aceita");
        float tMove0 = Time.time; float maxSpeed = 0; bool sawWalkRun = false;
        while (Time.time - tMove0 < 5f)
        {
            maxSpeed = Mathf.Max(maxSpeed, agent.velocity.magnitude);
            string cn = AnimState(anim);
            if (cn == "Walk" || cn == "Run") sawWalkRun = true;
            yield return 0.0;
        }
        float moved = Flat(before, go.transform.position);
        P(T + ".5b", moved > 4f, $"moveu {moved:F1} m em 5 s (velocidade max {maxSpeed:F2} m/s)");
        P(T + ".5c", sawWalkRun, "Animator tocou Walk/Run durante o deslocamento");
        PI(T + ".5", "clipe ao chegar: " + AnimState(anim) + ", speed anim " + anim.GetFloat("Speed").ToString("F2"));
        yield return GT(1.0);
        P(T + ".5d", AnimState(anim) == "Idle", "voltou ao Idle ao parar (" + AnimState(anim) + ")");
        agent.isStopped = true;
        agent.ResetPath();

        // ---- capturas (3 vistas)
        CaptureViews(go, "paladin");
        yield return 0.1;

        // ---- CURA POR ERA
        Vector3 basePos = go.transform.position;
        Vector3 right = go.transform.right;
        foreach (ImperialEra era in ImperialEras.Ordered)
        {
            AdvancedEraState.Set(era);
            yield return GT(0.3);
            contract.TryGetTier(era, out IEraTier tier);
            PaladinEraTier t = (PaladinEraTier)tier;
            string E = T + "." + era;
            int pulses0 = heal.PulsesPerformed;
            Vector3 p = go.transform.position;
            Character inA = Spawn("soldier", mine, p + right * (t.healRadius - 0.6f));
            Character inB = Spawn("soldier", mine, p - right * 2.0f);
            Character outA = Spawn("soldier", mine, p + right * (t.healRadius + 0.7f));
            Character farA = Spawn("soldier", mine, p - right * (t.healRadius + 6f));
            Character foe = Spawn("soldier", foeOwner, p + go.transform.forward * 2.2f);
            yield return GT(0.2);
            // distancias REAIS apos o NavMesh
            float dInA = Flat(p, inA.Transform.position), dInB = Flat(p, inB.Transform.position), dOutA = Flat(p, outA.Transform.position), dFar = Flat(p, farA.Transform.position), dFoe = Flat(p, foe.Transform.position);
            PI(E, $"distancias reais ao paladino: aliadoIn {dInA:F2}, aliadoIn2 {dInB:F2}, aliadoFora(borda) {dOutA:F2}, aliadoLonge {dFar:F2}, inimigo {dFoe:F2}; raio {t.healRadius}");
            P(E + ".g", dInA < t.healRadius - 0.1f && dInB < t.healRadius - 0.1f && dOutA > t.healRadius + 0.1f && dFar > t.healRadius + 0.1f && dFoe < t.healRadius - 0.1f, "geometria do teste valida (dois aliados dentro, dois fora, inimigo dentro)");
            foreach (Character c in new[] { inA, inB, outA, farA, foe }) c.HP = 1;
            paladinSet(pal, pal.MaxHP - 4); // paladino ferido: nao deve ser curado por si
            int hpInA = inA.HP, hpInB = inB.HP, hpOut = outA.HP, hpFar = farA.HP, hpFoe = foe.HP, hpPal = pal.HP;
            float tStart = Time.time;
            while (heal.PulsesPerformed == pulses0 && Time.time - tStart < t.healCooldown + 4f) yield return 0.0;
            float tPulse = Time.time;
            P(E + ".1", heal.PulsesPerformed > pulses0, $"pulso de cura ocorreu (apos {tPulse - tStart:F2} s)");
            yield return 0.0;
            int dA = inA.HP - hpInA, dB = inB.HP - hpInB;
            P(E + ".2", dA == t.healAmount && dB == t.healAmount, $"aliados dentro do raio curados em exatamente {t.healAmount}: +{dA} e +{dB}");
            P(E + ".3", outA.HP == hpOut && farA.HP == hpFar, $"aliados fora do raio nao curados (fora {outA.HP - hpOut:+0;-0;0}, longe {farA.HP - hpFar:+0;-0;0})");
            P(E + ".4", foe.HP == hpFoe, $"inimigo (outro dono) dentro do raio NAO curado ({foe.HP - hpFoe:+0;-0;0})");
            P(E + ".5", pal.HP == hpPal, $"paladino nao se cura (HP {hpPal} -> {pal.HP})");
            P(E + ".6", heal.LastPulseTargets == 2, $"alvos no pulso = 2 (LastPulseTargets {heal.LastPulseTargets})");
            // efeito
            List<GameObject> fxs = FindEffects();
            P(E + ".7", fxs.Count >= 1, $"efeito de cura instanciado ({fxs.Count})");
            if (fxs.Count >= 1)
            {
                GameObject fx = fxs.OrderByDescending(f => f.GetComponent<PaladinHealAuraEffect>().Age == 0 ? 0 : 1).First();
                Vector3 s = fx.transform.lossyScale;
                P(E + ".8", Mathf.Abs(s.x - t.healRadius * 2f) < 0.02f && Mathf.Abs(s.z - t.healRadius * 2f) < 0.02f, $"efeito escalado ao raio: {s.x:F2} x {s.z:F2} (esperado {t.healRadius * 2f:F2})");
                PI(E + ".8", $"efeito em {fx.transform.position.ToString("F2")} (paladino em {go.transform.position.ToString("F2")}; altura do efeito sobre o paladino {(fx.transform.position.y - go.transform.position.y):F2} m)");
            }
            // animacao de cura
            yield return GT(0.25);
            string healLayerClip = anim.layerCount > 1 ? AnimState(anim, anim.layerCount - 1) : "?";
            P(E + ".9", healLayerClip == "Heal", $"camada de cura toca o clipe Heal ({healLayerClip}), camada base {AnimState(anim, 0)}");
            if (era == ImperialEra.Ancient || era == ImperialEra.Imperial)
            {
                CaptureWide(go.transform.position, t.healRadius, $"play-{T}-heal-{era}-a.png");
                yield return GT(0.35);
                CaptureWide(go.transform.position, t.healRadius, $"play-{T}-heal-{era}-b.png");
                if (era == ImperialEra.Ancient) CaptureNear(go, new Vector3(0.6f, 0, 1f), $"play-{T}-heal-{era}-near.png", 4.5f);
            }
            // cooldown entre pulsos (alvos continuam feridos mas limite por alvo pode bloquear -> mede contados)
            List<float> times = new List<float> { tPulse };
            int lastPulses = heal.PulsesPerformed;
            float tEnd = Time.time + t.healCooldown * 2.4f;
            while (Time.time < tEnd)
            {
                if (heal.PulsesPerformed > lastPulses) { times.Add(Time.time); lastPulses = heal.PulsesPerformed; }
                yield return 0.0;
            }
            string gaps = string.Join(", ", Enumerable.Range(1, times.Count - 1).Select(i => (times[i] - times[i - 1]).ToString("F2")));
            P(E + ".10", times.Count >= 2 && Enumerable.Range(1, times.Count - 1).All(i => times[i] - times[i - 1] >= t.healCooldown - 0.06f), $"intervalo entre pulsos >= cooldown {t.healCooldown} s (medidos: {gaps}; {times.Count} pulsos)");
            P(E + ".11", times.Count - 1 <= 3, $"nao ha mais pulsos que o cooldown permite em {t.healCooldown * 2.4f:F1} s");
            // limite por alvo: ate aqui (janela de 10 s) ninguem recebe mais que o teto
            int healedA = inA.HP - hpInA;
            P(E + ".12", healedA <= t.maxHealPerTarget, $"cura acumulada do alvo ({healedA}) <= limite por alvo {t.maxHealPerTarget} (janela {contract.healCapWindowSeconds} s)");
            // efeito desaparece
            yield return GT(3.2);
            P(E + ".13", FindEffects().Count == 0 || FindEffects().All(f => f.GetComponent<PaladinHealAuraEffect>().Age < 3.3f), "efeito de cura e destruido (nao vaza)");
            Kill(inA, inB, outA, farA, foe);
            yield return GT(0.2);
            pal.HP = pal.MaxHP;
        }
        AdvancedEraState.Set(ImperialEra.Ancient);
        yield return GT(0.3);

        // ---- LIMITE POR ALVO + janela (Ancient: 6/pulso, 3 s, teto 18 por 10 s)
        {
            contract.TryGetTier(ImperialEra.Ancient, out IEraTier tier);
            PaladinEraTier t = (PaladinEraTier)tier;
            Character ally = Spawn("soldier", mine, go.transform.position + go.transform.right * 2.5f);
            yield return GT(0.2);
            ally.HP = 1;
            List<(float time, int hp)> track = new List<(float, int)>();
            int lastHp = ally.HP; float t0 = Time.time;
            int pulses0 = heal.PulsesPerformed;
            while (Time.time - t0 < 24f)
            {
                if (ally.HP != lastHp) { track.Add((Time.time - t0, ally.HP - lastHp)); lastHp = ally.HP; }
                yield return 0.0;
            }
            string tl = string.Join(", ", track.Select(x => $"t={x.time:F1}s +{x.hp}"));
            PI(T + ".cap", "linha do tempo das curas: " + tl);
            int firstWindow = track.Count > 0 ? track.Where(x => x.time - track[0].time < 10f).Sum(x => x.hp) : 0;
            P(T + ".cap1", track.Count >= 3 && firstWindow == t.maxHealPerTarget, $"na 1a janela de {contract.healCapWindowSeconds} s o alvo recebeu {firstWindow} (teto {t.maxHealPerTarget} = {t.maxHealPerTarget / t.healAmount} curas de {t.healAmount})");
            P(T + ".cap2", track.Count > 0 && track.Any(x => x.time - track[0].time >= contract.healCapWindowSeconds - 0.1f), "a cura retoma depois que a janela expira");
            P(T + ".cap3", track.All(x => x.hp == t.healAmount), "cada cura individual = " + t.healAmount + " (" + string.Join(",", track.Select(x => x.hp)) + ")");
            // nunca excede a vida maxima
            ally.HP = ally.MaxHP - 2; int before2 = ally.HP;
            yield return GT(contract.healCapWindowSeconds + t.healCooldown + 1f); // garante janela nova
            P(T + ".cap4", ally.HP <= ally.MaxHP && ally.HP >= before2, $"cura nunca excede a vida maxima (HP {ally.HP}/{ally.MaxHP})");
            Kill(ally);
            yield return GT(0.2);
        }

        // ---- CURA CRUZADA: paladino inimigo nao cura meus aliados; cura os dele
        {
            Vector3 far = _spawnBase + new Vector3(30f, 0, 8f);
            Character myAlly = Spawn("soldier", mine, far);
            Character foePal = Spawn(Code, foeOwner, far + new Vector3(2.5f, 0, 0));
            Character foeAlly = Spawn("soldier", foeOwner, far + new Vector3(0f, 0, 2.5f));
            yield return GT(0.3);
            float dMine = Flat(foePal.Transform.position, myAlly.Transform.position), dFoeA = Flat(foePal.Transform.position, foeAlly.Transform.position);
            PI(T + ".x", $"paladino inimigo: aliado meu a {dMine:F2} m, aliado dele a {dFoeA:F2} m");
            myAlly.HP = 1; foeAlly.HP = 1;
            PaladinHealBehaviour fh = foePal.Transform.GetComponent<PaladinHealBehaviour>();
            yield return GT(7.5);
            P(T + ".x1", myAlly.HP == 1, $"paladino inimigo NAO curou unidade de outro dono (HP {myAlly.HP})");
            P(T + ".x2", foeAlly.HP > 1 && fh.PulsesPerformed >= 1, $"paladino inimigo curou o proprio aliado (HP {foeAlly.HP}, pulsos {fh.PulsesPerformed})");
            Kill(myAlly, foePal, foeAlly);
            yield return GT(0.2);
        }

        // ---- sem ferido: paladino nao pulsa nem gasta cooldown
        {
            int p0 = heal.PulsesPerformed;
            yield return GT(4.0);
            P(T + ".idle", heal.PulsesPerformed == p0, $"sem aliado ferido nao ha pulso (pulsos {p0} -> {heal.PulsesPerformed})");
        }

        // ---- MELEE (behavior tree): cadencia segue o cooldown da era, mesmo trocando a era depois do spawn
        {
            AdvancedEraState.Set(ImperialEra.Ancient);
            yield return GT(0.3);
            pal.HP = pal.MaxHP;
            go.transform.position = go.transform.position; // sem efeito, so legibilidade
            Character foe = Spawn("soldier", foeOwner, go.transform.position + go.transform.forward * 9f);
            Character ally = Spawn("soldier", mine, go.transform.position - go.transform.right * 2f);
            yield return GT(0.3);
            ally.HP = 1;
            if (bt != null) bt.enabled = true;
            agent.isStopped = false;
            int swings0 = melee.Swings, pulsesAtStart = heal.PulsesPerformed;
            List<(float time, int dmg, ImperialEra era)> hits = new List<(float, int, ImperialEra)>();
            int lastFoeHp = foe.HP; int attackEntries = 0; bool wasAttack = false; bool capturedAttack = false;
            float tFight0 = Time.time; float firstContact = -1;
            // fase 1: Ancient ate 6 golpes
            while (Time.time - tFight0 < 40f && hits.Count < 6)
            {
                if (foe.Transform != null && foe.HP < lastFoeHp)
                {
                    if (firstContact < 0) firstContact = Time.time - tFight0;
                    hits.Add((Time.time, lastFoeHp - foe.HP, ImperialEra.Ancient));
                    foe.HP = foe.MaxHP; lastFoeHp = foe.MaxHP;
                    if (!capturedAttack && hits.Count == 2) { capturedAttack = true; yield return GT(0.3); CaptureNear(go, new Vector3(0.7f, 0, -1f), $"play-{T}-attack-a.png", 4.5f); CaptureNear(go, new Vector3(1f, 0, 0.2f), $"play-{T}-attack-b.png", 4.5f); }
                }
                bool a = anim.GetCurrentAnimatorStateInfo(0).IsName("Attack");
                if (a && !wasAttack) attackEntries++;
                wasAttack = a;
                yield return 0.0;
            }
            float dist = foe.Transform != null ? Flat(go.transform.position, foe.Transform.position) : -1f;
            P(T + ".m1", hits.Count >= 6, $"BT: o paladino foi ate o inimigo (primeiro golpe em {firstContact:F1} s) e deu {hits.Count} golpes; distancia ao alvo {dist:F2} m");
            // troca de era com o combate em andamento
            AdvancedEraState.Set(ImperialEra.Imperial);
            int idxSwitch = hits.Count;
            float tSwitch = Time.time;
            while (Time.time - tSwitch < 30f && hits.Count < idxSwitch + 7)
            {
                if (foe.Transform != null && foe.HP < lastFoeHp)
                {
                    hits.Add((Time.time, lastFoeHp - foe.HP, ImperialEra.Imperial));
                    foe.HP = foe.MaxHP; lastFoeHp = foe.MaxHP;
                }
                bool a = anim.GetCurrentAnimatorStateInfo(0).IsName("Attack");
                if (a && !wasAttack) attackEntries++;
                wasAttack = a;
                yield return 0.0;
            }
            int idxSwitch2 = hits.Count;
            // volta para Classical (outra era, outro cooldown 1.7)
            AdvancedEraState.Set(ImperialEra.Classical);
            float tSwitch2 = Time.time;
            while (Time.time - tSwitch2 < 20f && hits.Count < idxSwitch2 + 5)
            {
                if (foe.Transform != null && foe.HP < lastFoeHp)
                {
                    hits.Add((Time.time, lastFoeHp - foe.HP, ImperialEra.Classical));
                    foe.HP = foe.MaxHP; lastFoeHp = foe.MaxHP;
                }
                yield return 0.0;
            }
            List<float> gapsA = new List<float>(), gapsI = new List<float>(), gapsC = new List<float>();
            for (int i = 1; i < hits.Count; i++)
            {
                float g = hits[i].time - hits[i - 1].time;
                if (hits[i].era == ImperialEra.Ancient && hits[i - 1].era == ImperialEra.Ancient) gapsA.Add(g);
                else if (hits[i].era == ImperialEra.Imperial && i - 1 >= idxSwitch) gapsI.Add(g);
                else if (hits[i].era == ImperialEra.Classical && i - 1 >= idxSwitch2) gapsC.Add(g);
            }
            PI(T + ".m", "golpes (tempo de jogo, dano, era): " + string.Join(" | ", hits.Select(h => $"{h.time - tFight0:F2}s/{h.dmg}/{h.era}")));
            PI(T + ".m", $"intervalos Ancient: {string.Join(", ", gapsA.Select(x => x.ToString("F2")))}; Imperial: {string.Join(", ", gapsI.Select(x => x.ToString("F2")))}; Classical: {string.Join(", ", gapsC.Select(x => x.ToString("F2")))}");
            // regra: intervalo >= cooldown da era (nunca mais rapido), media dentro de cooldown + poll(0.25) + folga
            Action<string, List<float>, float> cadence = (name, gaps, cdv) =>
            {
                if (gaps.Count == 0) { P(T + ".m2", false, name + ": sem intervalos medidos"); return; }
                float min = gaps.Min(), avg = gaps.Average(), max = gaps.Max();
                P(T + ".m2", min >= cdv - 0.06f, $"{name}: nenhum golpe mais rapido que o cooldown {cdv:F1} s (min {min:F2})");
                P(T + ".m3", avg <= cdv + 0.30f && avg >= cdv - 0.05f, $"{name}: cadencia media {avg:F2} s ~ cooldown {cdv:F1} s (+ sondagem 0.25 s); max {max:F2}");
            };
            contract.TryGetTier(ImperialEra.Ancient, out IEraTier ta); contract.TryGetTier(ImperialEra.Imperial, out IEraTier ti); contract.TryGetTier(ImperialEra.Classical, out IEraTier tc);
            cadence("Ancient", gapsA, ta.AttackCooldown);
            cadence("Imperial (trocada apos o spawn e em combate)", gapsI, ti.AttackCooldown);
            cadence("Classical (trocada de novo)", gapsC, tc.AttackCooldown);
            if (gapsA.Count > 0 && gapsI.Count > 0) P(T + ".m4", gapsI.Average() < gapsA.Average() - 0.15f, $"Imperial bate mais rapido que Ancient ({gapsI.Average():F2} < {gapsA.Average():F2})");
            // dano por golpe = AttackDamage da era
            foreach (ImperialEra era in new[] { ImperialEra.Ancient, ImperialEra.Imperial, ImperialEra.Classical })
            {
                contract.TryGetTier(era, out IEraTier tt);
                var mine2 = hits.Where(h => h.era == era).ToList();
                P(T + ".m5", mine2.Count > 0 && mine2.All(h => h.dmg == tt.AttackDamage), $"{era}: dano por golpe {string.Join(",", mine2.Select(h => h.dmg).Distinct())} = AttackDamage do contrato {tt.AttackDamage}");
            }
            int swingsDone = melee.Swings - swings0;
            P(T + ".m6", Mathf.Abs(swingsDone - hits.Count) <= 1, $"golpes do controlador ({swingsDone}) == golpes observados ({hits.Count})");
            P(T + ".m7", attackEntries >= hits.Count - 2 && attackEntries <= hits.Count + 1, $"entradas no estado Attack do Animator ({attackEntries}) acompanham os golpes ({hits.Count}); a animacao nao reinicia a cada sondagem de 0.25 s");
            // combinado: a cura continuou durante o combate
            P(T + ".m8", heal.PulsesPerformed > pulsesAtStart, $"cura continua funcionando durante o combate corpo a corpo (pulsos +{heal.PulsesPerformed - pulsesAtStart}); aliado ferido HP {ally.HP}");
            // BT: ataque so no alcance
            P(T + ".m9", dist >= 0 && dist < 3.5f, $"golpes ocorreram a curta distancia ({dist:F2} m; alcance {ta.AttackRange} + tamanho do alvo)");
            AdvancedEraState.Set(ImperialEra.Ancient);
            if (bt != null) bt.enabled = false;
            agent.isStopped = true; agent.ResetPath();
            Kill(foe, ally);
            yield return GT(0.3);
        }

        // ---- amigo nao e atacado: aliado dentro do FOV com BT ligado nao recebe dano
        {
            if (bt != null) bt.enabled = true;
            Character friend = Spawn("soldier", mine, go.transform.position + go.transform.forward * 3f);
            int hp0 = friend.HP;
            yield return GT(5.0);
            P(T + ".f1", friend.HP == hp0, $"o paladino nao ataca aliado do mesmo dono (HP {hp0} -> {friend.HP})");
            if (bt != null) bt.enabled = false;
            Kill(friend);
            yield return GT(0.2);
        }

        // ---- reacao a dano
        {
            int d0 = go.GetComponent<AdvancedUnitAnimatorDriver>().DamageReactions;
            pal.HP = pal.MaxHP;
            um.TakeHit(5);
            yield return GT(0.25);
            string st = AnimState(anim);
            P(T + ".d1", go.GetComponent<AdvancedUnitAnimatorDriver>().DamageReactions == d0 + 1, "AdvancedUnitAnimatorDriver registrou a reacao a dano");
            P(T + ".d2", st != "Idle" || anim.GetCurrentAnimatorStateInfo(0).IsName("HitReact") || anim.IsInTransition(0), $"Animator entrou na reacao a dano ({st})");
            yield return GT(1.5);
            pal.HP = pal.MaxHP;
        }

        // ---- morte
        {
            Vector3 deathPos = go.transform.position;
            um.TakeHit(9999);
            yield return GT(0.15);
            yield return 0.0;
            GameObject death = null;
            float tw = Time.time;
            while (Time.time - tw < 2f && death == null)
            {
                death = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Select(a => a.gameObject).FirstOrDefault(g => g.name.Contains("PaladinHealerDeath") || (g.transform.root.name.Contains("PaladinHealerDeath")));
                yield return 0.0;
            }
            P(T + ".k1", go == null, "unidade original destruida ao zerar a vida");
            P(T + ".k2", death != null, "prefab de morte instanciado (" + (death != null ? death.transform.root.name : "nenhum") + ")");
            if (death != null)
            {
                Animator da = death.GetComponentInChildren<Animator>();
                yield return GT(0.4);
                string dc = AnimState(da);
                P(T + ".k3", dc == "Death", $"prefab de morte toca o clipe Death sozinho ({dc})");
                float dd = Flat(death.transform.root.position, deathPos);
                P(T + ".k4", dd < 1f, $"prefab de morte aparece onde a unidade morreu (desvio {dd:F2} m)");
                Bounds b = VisBounds(death.transform.root.gameObject);
                Camera cam = PlayCam(); cam.transform.position = b.center + new Vector3(2.5f, 2.0f, -3.5f); cam.transform.LookAt(b.center);
                float m = ShootPlay(cam, $"play-{T}-death-a.png"); P(T + ".cap", m < 0.001f, $"death-a magenta {m * 100:F4}%");
                yield return GT(1.4);
                b = VisBounds(death.transform.root.gameObject);
                cam.transform.position = b.center + new Vector3(2.5f, 2.0f, -3.5f); cam.transform.LookAt(b.center);
                m = ShootPlay(cam, $"play-{T}-death-b.png"); P(T + ".cap", m < 0.001f, $"death-b magenta {m * 100:F4}%");
                UnityEngine.Object.Destroy(cam.gameObject);
                PI(T + ".k5", $"estado do clipe da morte aos 1.8 s: {AnimState(da)}; normalizedTime {da.GetCurrentAnimatorStateInfo(0).normalizedTime:F2}");
                float tl2 = Time.time;
                while (death != null && Time.time - tl2 < 30f) yield return 0.0;
                P(T + ".k6", death == null, $"prefab de morte e removido apos {Time.time - tl2 + 1.8f:F1} s");
            }
        }
        yield return GT(0.5);
        PI(T + ".end", "fim do teste do paladino no " + T);
    }

    private static void paladinSet(Character c, int hp) { c.HP = hp; }
}
