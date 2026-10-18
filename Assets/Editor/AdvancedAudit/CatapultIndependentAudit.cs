using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using EraImperial.Units.Advanced;
using EraImperial.Units.Siege;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// AUDITORIA INDEPENDENTE da Catapulta (catapult), parte EditMode. Escrita pelo auditor; nao reutiliza CatapultAudit.
///   -quit -executeMethod CatapultIndependentAudit.RunEdit  -> Docs/AdvancedUnits/audit/catapult/edit-checks.txt (+ renders)
/// Somente leitura nos assets do jogo; escreve apenas em Docs/AdvancedUnits/audit/catapult.
/// </summary>
public static class CatapultIndependentAudit
{
    public const string OutDir = "Docs/AdvancedUnits/audit/catapult";
    private const string EditReport = OutDir + "/edit-checks.txt";

    public const string Code = "catapult";
    public const string Root = "Assets/CharacterRigging/Catapult";
    public const string ContractPath = "Assets/Data/AdvancedUnits/Catapult.asset";
    public const string BoulderContractPath = "Assets/Data/AdvancedUnits/Projectiles/CatapultBoulder.asset";
    public const string WrapperPath = "Assets/Resources/Prefabs/Units/Characters/Catapult.prefab";
    public const string CatalogPath = "Assets/Resources/ScriptableObjects/Units/Characters/Catapult.asset";
    public const string BodyFbx = Root + "/Models/Catapult.fbx";
    public const string ModelsDir = Root + "/Models";
    public const string BoulderPrefabPath = Root + "/Prefabs/CatapultBoulder.prefab";
    public const string FirePrefabPath = Root + "/Prefabs/CatapultFire.prefab";
    public const string ImpactPrefabPath = Root + "/Prefabs/CatapultImpact.prefab";
    public const string DeathPrefabPath = Root + "/Prefabs/CatapultDestroyed.prefab";
    public const string DustPrefabPath = Root + "/Prefabs/CatapultCollapseDust.prefab";
    public const string VisualPrefabPath = Root + "/Prefabs/CatapultVisual.prefab";
    public static readonly string[] ClipNames = { "Idle", "Move", "Reload", "Fire", "Hit", "Destroy" };

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

    public static string F(float v) { return v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }
    public static string V(Vector3 v) { return "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")"; }

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

    public static Mesh MeshOf(Renderer r)
    {
        if (r is SkinnedMeshRenderer s) return s.sharedMesh;
        MeshFilter f = r.GetComponent<MeshFilter>();
        return f != null ? f.sharedMesh : null;
    }

    public static float MagentaFraction(Texture2D t)
    {
        Color32[] px = t.GetPixels32();
        int n = 0;
        foreach (Color32 p in px)
            if (p.r > 190 && p.b > 190 && p.g < 90 && p.r - p.g > 110 && p.b - p.g > 110) n++;
        return (float)n / px.Length;
    }

    public static Texture2D Render(Camera cam, int w, int h)
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

    /// <summary>Renderiza e grava PNG; devolve a fracao de pixels magenta.</summary>
    public static float Shoot(Camera cam, Vector3 center, Vector3 dir, float dist, string file, float lift = 0.2f, int w = 1000, int h = 750)
    {
        cam.transform.position = center + dir.normalized * dist + Vector3.up * dist * lift;
        cam.transform.LookAt(center);
        Texture2D t = Render(cam, w, h);
        File.WriteAllBytes(Path.Combine(OutDir, file), t.EncodeToPNG());
        float f = MagentaFraction(t);
        UnityEngine.Object.DestroyImmediate(t);
        return f;
    }

    public static bool IsBodyRenderer(Renderer r)
    {
        if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) return false;
        string n = r.name;
        if (n.Contains("TeamRing") || n.Contains("Minimap") || n.Contains("FOV")) return false;
        return true;
    }

    public static Bounds BodyBounds(GameObject go, Func<Renderer, bool> filter = null)
    {
        Bounds b = default; bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !IsBodyRenderer(r)) continue;
            if (filter != null && !filter(r)) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    public static Camera MakeCamera(Color bg)
    {
        GameObject g = new GameObject("AuditCam");
        Camera c = g.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = bg;
        c.fieldOfView = 28; c.nearClipPlane = 0.05f; c.farClipPlane = 150;
        return c;
    }

    public static void MakeLight()
    {
        GameObject l = new GameObject("AuditLight");
        Light light = l.AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.15f; light.shadows = LightShadows.Soft;
        l.transform.rotation = Quaternion.Euler(48, 140, 0);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.52f, 0.54f, 0.6f);
    }

    public static GameObject MakeGround(float size = 40f)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Plane);
        g.name = "AuditGround";
        g.transform.localScale = Vector3.one * (size / 10f);
        Material m = new Material(Shader.Find("Standard"));
        m.color = new Color(0.36f, 0.43f, 0.27f);
        m.SetFloat("_Glossiness", 0f);
        g.GetComponent<Renderer>().sharedMaterial = m;
        UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    public static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            Transform r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }

    private static List<string> GuidsIn(string path)
    {
        List<string> list = new List<string>();
        if (!File.Exists(path)) return list;
        string text = File.ReadAllText(path);
        if (!text.StartsWith("%YAML")) return list;
        foreach (Match m in Regex.Matches(text, @"guid: ([0-9a-f]{32})"))
            list.Add(m.Groups[1].Value);
        return list;
    }

    private static AnimationClip LoadClip(string fbx)
    {
        return AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    // ===================================================================== EDIT MODE

    public static void RunEdit()
    {
        Begin(EditReport);
        try
        {
            AssetDatabase.Refresh();
            S1_Origin();
            S2_Rig();
            S3_Weights();
            S4_UvMaterials();
            S5_Lods();
            S6_Wrapper();
            S7_Sockets();
            S8_Animator();
            S9_Projectile();
            S10_ContractCatalog();
            S11_Tiers();
            S14_References();
            S13_Renders();
        }
        catch (Exception e)
        {
            _fail++;
            Line("FAIL [EXCEPTION] " + e);
        }
        Line("RESUMO EDIT: " + _pass + " PASS, " + _fail + " FAIL");
        EditorApplication.Exit(_fail == 0 ? 0 : 1);
    }

    // ---------------------------------------------------------------- 1
    private static void S1_Origin()
    {
        Ok("1.1", File.Exists(BodyFbx), "malha-fonte existe: " + BodyFbx + " (" + new FileInfo(BodyFbx).Length / 1024 + " KB)");
        string lic = "Docs/CharacterMeshAudit/Catapult/LICENSE.md";
        Ok("1.2", File.Exists(lic), "LICENSE.md existe");
        string text = File.Exists(lic) ? File.ReadAllText(lic) : "";
        Ok("1.3", Regex.IsMatch(text, "obra do proprio projeto", RegexOptions.IgnoreCase) && Regex.IsMatch(text, "Nenhum modelo, textura, animacao", RegexOptions.IgnoreCase), "LICENSE declara obra do projeto, sem terceiros");
        Ok("1.4", !Directory.Exists("Tools/CharacterRigging/Inputs/Catapult"), "nao existe Inputs/Catapult (coerente com modelagem procedural: nenhum arquivo de entrada de terceiros)");
        string py = File.ReadAllText("Tools/CharacterRigging/BuildCatapult.py");
        var imports = Regex.Matches(py, @"^\s*(?:import|from)\s+([A-Za-z0-9_\.]+)", RegexOptions.Multiline).Cast<Match>().Select(m => m.Groups[1].Value.Split('.')[0]).Distinct().OrderBy(s => s).ToList();
        Info("1.5", "imports de BuildCatapult.py: " + string.Join(", ", imports));
        string[] allowed = { "bpy", "bmesh", "mathutils", "math", "os", "sys", "json", "random", "numpy", "time", "argparse", "collections", "itertools", "struct", "zlib", "pathlib" };
        Ok("1.5b", imports.All(i => allowed.Contains(i)), "BuildCatapult.py so importa bpy/numpy/stdlib (sem rede/terceiros): " + string.Join(", ", imports.Where(i => !allowed.Contains(i))));
        Ok("1.6", !Regex.IsMatch(py + File.ReadAllText("Tools/CharacterRigging/BuildCatapultProps.py"), @"urllib|requests|http[s]?://|mixamo|meshy|sketchfab", RegexOptions.IgnoreCase), "scripts de build sem URL/rede/Mixamo/Meshy/Sketchfab");

        // segredos em todos os arquivos de texto da unidade
        List<string> files = new List<string>();
        foreach (string dir in new[] { Root, "Docs/CharacterMeshAudit/Catapult" })
            foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                if (new[] { ".cs", ".py", ".ps1", ".md", ".txt", ".json", ".prefab", ".asset", ".mat", ".controller", ".meta" }.Contains(Path.GetExtension(f).ToLowerInvariant())) files.Add(f);
        files.AddRange(new[] { "Tools/CharacterRigging/BuildCatapult.py", "Tools/CharacterRigging/BuildCatapultProps.py", "Tools/CharacterRigging/Build-Catapult.ps1", "Tools/CharacterRigging/RenderCatapult.py", ContractPath, BoulderContractPath, WrapperPath, CatalogPath });
        int secrets = 0;
        Regex secret = new Regex(@"(sk-[A-Za-z0-9]{20,})|(msy_[A-Za-z0-9]{10,})|(AKIA[0-9A-Z]{16})|(api[_-]?key\s*[:=]\s*['""]?[A-Za-z0-9_\-]{16,})|(Bearer\s+[A-Za-z0-9\-_\.]{20,})|(password\s*[:=]\s*\S+)", RegexOptions.IgnoreCase);
        foreach (string f in files.Where(File.Exists))
        {
            string t = File.ReadAllText(f);
            if (secret.IsMatch(t)) { secrets++; Line("  possivel segredo em " + f); }
        }
        Ok("1.7", secrets == 0, "0 segredos em " + files.Count + " arquivos de texto da unidade");
        // sem arquivos binarios de terceiros: extensoes das fontes
        var odd = Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta")).Select(f => Path.GetExtension(f).ToLowerInvariant()).Distinct().OrderBy(s => s).ToList();
        Info("1.8", "extensoes de arquivo em Assets/CharacterRigging/Catapult: " + string.Join(" ", odd));
    }

    // ---------------------------------------------------------------- 2
    private static void S2_Rig()
    {
        ModelImporter imp = (ModelImporter)AssetImporter.GetAtPath(BodyFbx);
        Ok("2.1", imp != null && imp.animationType == ModelImporterAnimationType.Generic, "Catapult.fbx importado como Generic (maquina; Avatar humanoide nao se aplica)");
        Avatar av = AssetDatabase.LoadAllAssetsAtPath(BodyFbx).OfType<Avatar>().FirstOrDefault();
        Ok("2.2", av != null && av.isValid && !av.isHuman, "Avatar generico valido (isValid=" + (av != null && av.isValid) + ", isHuman=" + (av != null && av.isHuman) + ")");
        GameObject body = AssetDatabase.LoadAssetAtPath<GameObject>(BodyFbx);
        SkinnedMeshRenderer[] smrs = body.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Ok("2.3", smrs.Length > 0, "FBX tem " + smrs.Length + " SkinnedMeshRenderer(s)");
        HashSet<string> boneNames = new HashSet<string>();
        foreach (SkinnedMeshRenderer s in smrs) foreach (Transform b in s.bones) if (b != null) boneNames.Add(b.name);
        Info("2.4", "ossos usados pelo skin: " + string.Join(", ", boneNames.OrderBy(s => s)));
        string[] expected = { "Root", "Arm", "Ammo", "Winch", "Rope", "Wheel_FL", "Wheel_FR", "Wheel_BL", "Wheel_BR" };
        Ok("2.5", expected.All(boneNames.Contains), "esqueleto contem Root, Arm, Ammo, Winch, Rope e as 4 Wheel_*: faltando=" + string.Join(",", expected.Where(e => !boneNames.Contains(e))));
        // hierarquia coerente: Arm filho de Root; Rodas sob Root
        Transform root = FindDeep(body.transform, "Root"), arm = FindDeep(body.transform, "Arm");
        Ok("2.6", root != null && arm != null && arm.IsChildOf(root), "hierarquia coerente: Arm descende de Root");

        // clipes
        foreach (string name in ClipNames)
        {
            string path = ModelsDir + "/Catapult@" + name + ".fbx";
            ModelImporter ci = AssetImporter.GetAtPath(path) as ModelImporter;
            AnimationClip clip = LoadClip(path);
            Ok("2.7." + name, ci != null && ci.animationType == ModelImporterAnimationType.Generic && clip != null && !clip.isHumanMotion && clip.length > 0.05f,
                "clipe " + name + ": importador Generic, clip real, humanMotion=false, duracao " + (clip != null ? F(clip.length) : "?") + " s, loop=" + (clip != null && clip.isLooping));
            if (clip == null) continue;
            var bindings = AnimationUtility.GetCurveBindings(clip);
            int bad = 0;
            HashSet<string> paths = new HashSet<string>();
            foreach (var b in bindings) paths.Add(b.path);
            foreach (string p in paths) if (!string.IsNullOrEmpty(p) && body.transform.Find(p) == null && body.transform.Find(p.Contains("/") ? p.Substring(p.IndexOf('/') + 1) : p) == null) bad++;
            Ok("2.8." + name, bindings.Length > 0 && bad == 0, "clipe " + name + ": " + bindings.Length + " curvas em " + paths.Count + " ossos; caminhos nao resolvidos no esqueleto=" + bad);
        }
    }

    // ---------------------------------------------------------------- 3
    private static void CheckWeights(string tag, Mesh m, int boneCount)
    {
        var bpv = m.GetBonesPerVertex();
        var bw = m.GetAllBoneWeights();
        int idx = 0, none = 0, over = 0, notNorm = 0, badIndex = 0, maxInf = 0;
        float worst = 0;
        for (int v = 0; v < m.vertexCount; v++)
        {
            int n = bpv.Length > v ? bpv[v] : 0;
            if (n == 0) none++;
            if (n > 4) over++;
            maxInf = Mathf.Max(maxInf, n);
            float sum = 0;
            for (int k = 0; k < n; k++)
            {
                sum += bw[idx + k].weight;
                if (bw[idx + k].boneIndex < 0 || bw[idx + k].boneIndex >= boneCount) badIndex++;
            }
            idx += n;
            if (n > 0) { worst = Mathf.Max(worst, Mathf.Abs(sum - 1f)); if (Mathf.Abs(sum - 1f) > 0.01f) notNorm++; }
        }
        Ok("3." + tag, none == 0 && over == 0 && notNorm == 0 && badIndex == 0 && m.bindposes.Length == boneCount,
            tag + ": " + m.vertexCount + " verts, sem peso=" + none + ", >4 influencias=" + over + ", soma!=1=" + notNorm + " (pior desvio " + worst.ToString("0.0000") + "), max influencias=" + maxInf + ", indices invalidos=" + badIndex + ", bindposes=" + m.bindposes.Length + "/ossos=" + boneCount);
    }

    private static void S3_Weights()
    {
        GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(wrapper);
        try
        {
            int n = 0;
            foreach (SkinnedMeshRenderer s in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (s.sharedMesh == null) { Ok("3.null", false, s.name + " sem malha"); continue; }
                CheckWeights(s.name + "(" + s.sharedMesh.name + ")", s.sharedMesh, s.bones.Length);
                n++;
            }
            Ok("3.count", n >= 3, n + " SkinnedMeshRenderers no wrapper (todos os LODs)");
            var rigid = inst.GetComponentsInChildren<MeshRenderer>(true).Where(IsBodyRenderer).ToList();
            Info("3.rigid", "MeshRenderers rigidos no wrapper (fora FX): " + rigid.Count + (rigid.Count > 0 ? " -> " + string.Join(", ", rigid.Select(r => r.name)) : ""));
        }
        finally { UnityEngine.Object.DestroyImmediate(inst); }
    }

    // ---------------------------------------------------------------- 4
    private static void S4_UvMaterials()
    {
        GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(wrapper);
        try
        {
            foreach (SkinnedMeshRenderer s in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh m = s.sharedMesh;
                if (m == null) continue;
                List<Vector2> uv = new List<Vector2>();
                m.GetUVs(0, uv);
                bool finite = uv.Count == m.vertexCount && uv.All(u => !float.IsNaN(u.x) && !float.IsNaN(u.y) && !float.IsInfinity(u.x) && !float.IsInfinity(u.y));
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
                foreach (Vector2 u in uv) { min = Vector2.Min(min, u); max = Vector2.Max(max, u); }
                // triangulos degenerados em UV
                var tri = m.triangles; int degenerate = 0;
                for (int i = 0; i + 2 < tri.Length; i += 3)
                {
                    Vector2 a = uv[tri[i]], b = uv[tri[i + 1]], c = uv[tri[i + 2]];
                    if (Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) < 1e-9f) degenerate++;
                }
                Ok("4.uv." + s.name, finite && max.x - min.x > 0.05f && max.y - min.y > 0.05f && degenerate < (tri.Length / 3) * 0.02f,
                    s.name + ": UV " + uv.Count + "/" + m.vertexCount + " finitas, faixa u[" + F(min.x) + "," + F(max.x) + "] v[" + F(min.y) + "," + F(max.y) + "], triangulos UV degenerados " + degenerate + "/" + tri.Length / 3);
            }
            HashSet<Material> mats = new HashSet<Material>();
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true)) foreach (Material m in r.sharedMaterials) mats.Add(m);
            foreach (string extra in new[] { BoulderPrefabPath, FirePrefabPath, ImpactPrefabPath, DeathPrefabPath, DustPrefabPath })
            {
                GameObject g = AssetDatabase.LoadAssetAtPath<GameObject>(extra);
                if (g == null) continue;
                foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true)) foreach (Material m in r.sharedMaterials) mats.Add(m);
            }
            foreach (Material m in mats)
            {
                if (m == null) { Ok("4.mat.null", false, "material nulo em algum renderer"); continue; }
                bool supported = m.shader != null && m.shader.isSupported && m.shader.name != "Hidden/InternalErrorShader";
                string texInfo = "";
                bool texOk = true;
                if (m.HasProperty("_MainTex"))
                {
                    Texture t = m.GetTexture("_MainTex");
                    texInfo = t != null ? " tex=" + t.name + " " + t.width + "x" + t.height : " SEM textura (cor " + m.color + ")";
                    string path = AssetDatabase.GetAssetPath(m);
                    bool isFx = m.name.StartsWith("Fx_");
                    if (t == null && !isFx && m.name != "FOV") texOk = false;
                }
                Ok("4.mat." + m.name, supported && texOk, m.name + ": shader " + (m.shader != null ? m.shader.name : "null") + " isSupported=" + supported + texInfo);
            }
            // amostragem de pixels das texturas
            foreach (string tex in Directory.GetFiles(Root + "/Textures", "*.png"))
            {
                Texture2D t = new Texture2D(2, 2);
                t.LoadImage(File.ReadAllBytes(tex));
                float mag = MagentaFraction(t);
                Color32[] px = t.GetPixels32();
                float mean = 0, var = 0;
                foreach (Color32 p in px) mean += (p.r + p.g + p.b) / 3f;
                mean /= px.Length;
                foreach (Color32 p in px) { float d = (p.r + p.g + p.b) / 3f - mean; var += d * d; }
                var = Mathf.Sqrt(var / px.Length);
                if (Path.GetFileName(tex).StartsWith("Fx_")) { float am = 0, av = 0; foreach (Color32 p in px) am += p.a; am /= px.Length; foreach (Color32 p in px) av += (p.a - am) * (p.a - am); var = Mathf.Max(var, Mathf.Sqrt(av / px.Length)); }
                Ok("4.tex." + Path.GetFileName(tex), mag < 0.001f && var > 3f, Path.GetFileName(tex) + " " + t.width + "x" + t.height + ": magenta=" + mag.ToString("P3") + ", brilho medio " + F(mean) + ", desvio " + F(var) + " (nao uniforme)");
                UnityEngine.Object.DestroyImmediate(t);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(inst); }
    }

    // ---------------------------------------------------------------- 5
    private static void S5_Lods()
    {
        GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(wrapper);
        try
        {
            LODGroup[] groups = inst.GetComponentsInChildren<LODGroup>(true);
            Ok("5.1", groups.Length >= 1, "LODGroups no wrapper: " + groups.Length);
            LODGroup g = groups.OrderByDescending(x => x.lodCount).First();
            LOD[] lods = g.GetLODs();
            Ok("5.2", lods.Length == 3, "LODGroup com " + lods.Length + " niveis");
            List<int> tris = new List<int>();
            List<float> heights = new List<float>();
            for (int i = 0; i < lods.Length; i++)
            {
                int t = lods[i].renderers.Where(r => r != null).Sum(r => Tris(MeshOf(r)));
                tris.Add(t);
                heights.Add(lods[i].screenRelativeTransitionHeight);
                Info("5.lod" + i, "LOD" + i + ": " + t + " triangulos, " + lods[i].renderers.Length + " renderers, limiar " + F(lods[i].screenRelativeTransitionHeight));
            }
            bool strictly = true;
            for (int i = 1; i < tris.Count; i++) if (tris[i] >= tris[i - 1]) strictly = false;
            Ok("5.3", strictly, "triangulos estritamente decrescentes: " + string.Join(" > ", tris));
            Ok("5.4", tris[0] <= 8000 && tris[1] <= 3000 && tris[2] <= 1000, "limites da especificacao (props <= 8k/3k/1k): " + string.Join("/", tris));
            bool desc = true;
            for (int i = 1; i < heights.Count; i++) if (heights[i] >= heights[i - 1]) desc = false;
            Ok("5.5", desc && heights[0] < 1f && heights[0] > 0.05f, "limiares de transicao decrescentes e coerentes: " + string.Join(" > ", heights.Select(F)));
            // renderers fora do LODGroup
            HashSet<Renderer> inLod = new HashSet<Renderer>(lods.SelectMany(l => l.renderers).Where(r => r != null));
            var outside = inst.GetComponentsInChildren<Renderer>(true).Where(r => IsBodyRenderer(r) && !inLod.Contains(r)).ToList();
            Ok("5.6", outside.Count == 0, "renderers de corpo fora do LODGroup: " + outside.Count + (outside.Count > 0 ? " (" + string.Join(", ", outside.Select(r => r.name)) + ")" : ""));
        }
        finally { UnityEngine.Object.DestroyImmediate(inst); }
    }

    // ---------------------------------------------------------------- 6
    private static int MissingScripts(GameObject root)
    {
        int n = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            n += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
        return n;
    }

    private static void S6_Wrapper()
    {
        GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        Ok("6.0", wrapper != null, "wrapper existe");
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(wrapper);
        try
        {
            Ok("6.1", inst.GetComponent("CharacterManager") != null, "CharacterManager na raiz");
            NavMeshAgent agent = inst.GetComponent<NavMeshAgent>();
            Ok("6.2", agent != null, "NavMeshAgent: " + (agent != null ? "vel " + F(agent.speed) + ", raio " + F(agent.radius) + ", altura " + F(agent.height) + ", acel " + F(agent.acceleration) + ", giro " + F(agent.angularSpeed) : "ausente"));
            BoxCollider box = inst.GetComponent<BoxCollider>();
            Bounds bb = BodyBounds(inst, r => !(r is SkinnedMeshRenderer s2) || true);
            // bounds do LOD0 apenas
            LODGroup g = inst.GetComponentsInChildren<LODGroup>(true).First();
            Bounds lod0 = default; bool any = false;
            foreach (Renderer r in g.GetLODs()[0].renderers) { if (!any) { lod0 = r.bounds; any = true; } else lod0.Encapsulate(r.bounds); }
            Vector3 local = inst.transform.InverseTransformVector(lod0.size);
            Info("6.3a", "LOD0 bounds tamanho " + V(lod0.size) + " centro " + V(lod0.center) + "; collider centro " + (box ? V(box.center) : "-") + " tamanho " + (box ? V(box.size) : "-"));
            bool coherent = box != null && box.size.x >= lod0.size.x * 0.8f && box.size.x <= lod0.size.x * 1.4f && box.size.z >= lod0.size.z * 0.8f && box.size.z <= lod0.size.z * 1.4f && box.size.y >= lod0.size.y * 0.7f && box.size.y <= lod0.size.y * 1.6f;
            Ok("6.3", coherent, "BoxCollider coerente com o modelo (largura/altura/comprimento dentro de 0,8..1,4x)");
            GameObject soldierRef = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Units/Characters/Soldier.prefab");
            BoxCollider soldierBox = soldierRef != null ? soldierRef.GetComponent<BoxCollider>() : null;
            Ok("6.3b", box != null && soldierBox != null && box.isTrigger == soldierBox.isTrigger, "BoxCollider.isTrigger=" + (box != null && box.isTrigger) + " igual ao do Soldier (" + (soldierBox != null && soldierBox.isTrigger) + "): padrao do jogo (selecao por OnMouseDown)");
            AdvancedUnitEraBinding binding = inst.GetComponent<AdvancedUnitEraBinding>();
            CatapultContract contract = AssetDatabase.LoadAssetAtPath<CatapultContract>(ContractPath);
            Ok("6.4", binding != null && binding.contract == contract && contract != null && contract.code == Code, "AdvancedUnitEraBinding -> Catapult.asset (code=" + (binding != null && binding.contract != null ? binding.contract.code : "?") + ")");
            Ok("6.5", inst.GetComponent<AdvancedUnitAnimatorDriver>() != null, "AdvancedUnitAnimatorDriver");
            CatapultCombatController cc = inst.GetComponent<CatapultCombatController>();
            Ok("6.6", cc != null && cc.contract == contract && cc.projectileSpawn != null && cc.animator != null, "CatapultCombatController: contrato ok, projectileSpawn=" + (cc != null && cc.projectileSpawn != null ? RelPath(inst.transform, cc.projectileSpawn) : "null") + ", animator=" + (cc != null && cc.animator != null) + ", releaseDelay=" + (cc != null ? F(cc.releaseDelay) : "?") + ", turnSpeed=" + (cc != null ? F(cc.turnSpeed) : "?"));
            AdvancedUnitDeathEffect de = inst.GetComponent<AdvancedUnitDeathEffect>();
            Ok("6.7", de != null && de.deathPrefab != null && de.deathPrefab == AssetDatabase.LoadAssetAtPath<GameObject>(DeathPrefabPath), "AdvancedUnitDeathEffect -> " + (de != null && de.deathPrefab != null ? de.deathPrefab.name : "null") + " (vida " + (de != null ? F(de.lifetime) : "?") + " s)");
            int missing = MissingScripts(inst);
            Ok("6.8", missing == 0, "scripts ausentes (Missing) no wrapper: " + missing);
            foreach (string p in new[] { VisualPrefabPath, BoulderPrefabPath, FirePrefabPath, ImpactPrefabPath, DeathPrefabPath, DustPrefabPath })
            {
                GameObject pg = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                Ok("6.8b." + Path.GetFileNameWithoutExtension(p), pg != null && MissingScripts(pg) == 0, Path.GetFileName(p) + ": existe e sem scripts ausentes");
            }
            // compara com o Soldier: componentes que o Soldier tem e o wrapper nao
            GameObject soldier = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Units/Characters/Soldier.prefab");
            if (soldier != null)
            {
                var sc = soldier.GetComponents<Component>().Select(c => c.GetType().Name).Distinct().ToList();
                var wc = inst.GetComponents<Component>().Select(c => c.GetType().Name).Distinct().ToList();
                Info("6.9", "componentes da raiz do Soldier ausentes no wrapper: " + string.Join(",", sc.Where(s => !wc.Contains(s))) + " | extras no wrapper: " + string.Join(",", wc.Where(s => !sc.Contains(s))));
            }
            Ok("6.10", inst.GetComponents<Animator>().Length == 0 && inst.GetComponentsInChildren<Animator>(true).Length == 1, "exatamente 1 Animator (no visual): " + inst.GetComponentsInChildren<Animator>(true).Length);
            Component cm = inst.GetComponent("CharacterManager");
            var so = new SerializedObject(cm);
            var anim = so.FindProperty("animator");
            Ok("6.11", anim != null && anim.objectReferenceValue == inst.GetComponentInChildren<Animator>(), "UnitManager.animator aponta para o Animator do visual");
            var mr = so.FindProperty("meshRenderer");
            Info("6.12", "UnitManager.meshRenderer=" + (mr != null && mr.objectReferenceValue != null ? ((Renderer)mr.objectReferenceValue).name + " bounds " + V(((Renderer)mr.objectReferenceValue).bounds.size) : "null") + " (MeshSize usado no alcance efetivo)");
            Transform ring = FindDeep(inst.transform, "TeamRing");
            Info("6.13", "TeamRing: " + (ring != null ? "presente, escala " + V(ring.localScale) + (ring.GetComponent<MeshFilter>() != null && ring.GetComponent<MeshFilter>().sharedMesh != null ? ", malha " + ring.GetComponent<MeshFilter>().sharedMesh.name + " " + V(ring.GetComponent<MeshFilter>().sharedMesh.bounds.size) : "") : "ausente"));
            var layer = LayerMask.LayerToName(inst.layer);
            Info("6.14", "layer do wrapper: " + layer + "; layer do Soldier: " + (soldier != null ? LayerMask.LayerToName(soldier.layer) : "?"));
            Ok("6.15", soldier == null || inst.layer == soldier.layer, "layer igual ao do Soldier");
        }
        finally { UnityEngine.Object.DestroyImmediate(inst); }
    }

    // ---------------------------------------------------------------- 7
    private static void S7_Sockets()
    {
        CatapultContract contract = AssetDatabase.LoadAssetAtPath<CatapultContract>(ContractPath);
        GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(wrapper);
        try
        {
            foreach (SocketRequirement s in contract.visual.sockets)
            {
                Transform t = inst.transform.Find(s.expectedTransformPath);
                Ok("7.find." + s.socketName, t != null, "socket " + s.socketName + " em '" + s.expectedTransformPath + "': " + (t != null ? "local " + V(inst.transform.InverseTransformPoint(t.position)) : "AUSENTE"));
            }
            Transform spawn = FindDeep(inst.transform, "ProjectileSpawn");
            Transform arm = FindDeep(inst.transform, "Arm");
            Animator animator = inst.GetComponentInChildren<Animator>();
            // modelo de LOD0: bounds
            LODGroup g = inst.GetComponentsInChildren<LODGroup>(true).First();
            foreach (LOD l in g.GetLODs()) { }
            Bounds b = BodyBounds(inst, r => g.GetLODs()[0].renderers.Contains(r));
            Vector3 spawnLocal = inst.transform.InverseTransformPoint(spawn.position);
            Info("7.1", "ProjectileSpawn em repouso (local do wrapper) " + V(spawnLocal) + "; corpo bounds local min/max: " + V(inst.transform.InverseTransformPoint(b.min)) + " .. " + V(inst.transform.InverseTransformPoint(b.max)));
            Ok("7.2", spawn.IsChildOf(arm) , "ProjectileSpawn e filho do osso Arm (acompanha o braco)");
            Ok("7.3", Mathf.Abs(spawnLocal.x) < 0.3f && spawnLocal.y > 0.5f && spawnLocal.y < 2.5f, "ProjectileSpawn centrado em X (|x|<0,3) e a altura plausivel em repouso (" + F(spawnLocal.y) + " m)");

            // amostra o clipe Fire: o socket deve subir/ir a frente e ficar acima da catapulta na soltura
            AnimationClip fire = LoadClip(ModelsDir + "/Catapult@Fire.fbx");
            GameObject animRoot = animator.gameObject;
            List<string> track = new List<string>();
            float[] ts = { 0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.7f, 1.0f };
            Vector3 atRelease = Vector3.zero;
            float minLen = float.MaxValue, maxLen = 0;
            Vector3 pRest = Vector3.zero;
            foreach (float t in ts)
            {
                fire.SampleAnimation(animRoot, Mathf.Min(t, fire.length));
                Vector3 p = inst.transform.InverseTransformPoint(spawn.position);
                float d = (spawn.position - arm.position).magnitude;
                minLen = Mathf.Min(minLen, d); maxLen = Mathf.Max(maxLen, d);
                track.Add("t=" + F(t) + ":" + V(p));
                if (Mathf.Approximately(t, 0.4f)) atRelease = p;
                if (t == 0f) pRest = p;
            }
            Info("7.4", "trajetoria do socket no clipe Fire (local do wrapper): " + string.Join(" ", track));
            Ok("7.5", atRelease.y > pRest.y + 0.8f, "na soltura (t=0,4 s) o socket esta bem acima da posicao armada (" + F(atRelease.y) + " > " + F(pRest.y) + " + 0,8)");
            Ok("7.6", atRelease.z > pRest.z + 0.3f || atRelease.y > 2.2f, "na soltura o socket avancou/subiu (z " + F(pRest.z) + "->" + F(atRelease.z) + ", y " + F(pRest.y) + "->" + F(atRelease.y) + "): pedra sai a frente da catapulta");
            Ok("7.7", maxLen - minLen < 0.02f, "distancia socket-pivo do braco constante (" + F(minLen) + ".." + F(maxLen) + "): rigidamente preso ao braco");
            Ok("7.8", atRelease.z > -0.5f, "origem do projetil na soltura nao esta atras do chassi (z=" + F(atRelease.z) + ")");
            // restaura pose
            AnimationClip idle = LoadClip(ModelsDir + "/Catapult@Idle.fbx");
            idle.SampleAnimation(animRoot, 0f);
        }
        finally { UnityEngine.Object.DestroyImmediate(inst); }
    }

    // ---------------------------------------------------------------- 8
    private static void S8_Animator()
    {
        CatapultContract contract = AssetDatabase.LoadAssetAtPath<CatapultContract>(ContractPath);
        GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(wrapper);
        try
        {
            foreach (AnimationRequirement a in contract.visual.animations)
            {
                bool ok = a.clip != null && a.clip.length > 0.05f && AnimationUtility.GetCurveBindings(a.clip).Length > 0;
                Ok("8.contract." + a.state, ok, "contrato: " + a.state + " (param " + a.animatorParameter + ") -> " + (a.clip != null ? a.clip.name + " " + F(a.clip.length) + " s, " + AnimationUtility.GetCurveBindings(a.clip).Length + " curvas" : "SEM CLIPE"));
            }
            // clipes reais para Idle/Move/Fire/Reload do contrato apontam para os FBX @Clip
            foreach (AnimationRequirement a in contract.visual.animations)
            {
                string p = a.clip != null ? AssetDatabase.GetAssetPath(a.clip) : "";
                Ok("8.src." + a.state, p.EndsWith("Catapult@" + a.state + ".fbx"), a.state + " vem de " + p);
            }

            Animator animator = inst.GetComponentInChildren<Animator>();
            AnimatorController ctrl = animator.runtimeAnimatorController as AnimatorController;
            if (ctrl == null) ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController));
            Ok("8.ctrl", ctrl != null, "AnimatorController: " + (ctrl != null ? AssetDatabase.GetAssetPath(ctrl) : "null"));
            var pars = ctrl.parameters.ToDictionary(p => p.name, p => p.type);
            string[] needed = { "Speed", "Fire", "Hit", "Damaged", "Running", "Attacking" };
            Ok("8.params", needed.All(pars.ContainsKey), "parametros: " + string.Join(", ", pars.Select(p => p.Key + ":" + p.Value)));
            Ok("8.paramtypes", pars["Speed"] == AnimatorControllerParameterType.Float && pars["Fire"] == AnimatorControllerParameterType.Trigger && pars["Hit"] == AnimatorControllerParameterType.Trigger && pars["Damaged"] == AnimatorControllerParameterType.Trigger && pars["Running"] == AnimatorControllerParameterType.Bool && pars["Attacking"] == AnimatorControllerParameterType.Bool, "tipos dos parametros corretos (Speed float, Fire/Hit/Damaged trigger, Running/Attacking bool)");

            // dump de estados e transicoes
            List<string> dump = new List<string>();
            Dictionary<string, AnimatorState> states = new Dictionary<string, AnimatorState>();
            for (int li = 0; li < ctrl.layers.Length; li++)
            {
                var layer = ctrl.layers[li];
                dump.Add("layer " + li + " '" + layer.name + "' peso " + F(layer.defaultWeight) + " mask=" + (layer.avatarMask != null ? layer.avatarMask.name : "-") + " default=" + (layer.stateMachine.defaultState != null ? layer.stateMachine.defaultState.name : "-"));
                foreach (var cs in layer.stateMachine.states)
                {
                    AnimatorState s = cs.state;
                    states[li + ":" + s.name] = s;
                    dump.Add("  estado " + s.name + " motion=" + (s.motion != null ? s.motion.name : "NULL") + " speed=" + F(s.speed) + (string.IsNullOrEmpty(s.speedParameter) ? "" : " speedParam=" + s.speedParameter + (s.speedParameterActive ? "(ativo)" : "")));
                    foreach (var tr in s.transitions)
                        dump.Add("    -> " + (tr.destinationState != null ? tr.destinationState.name : "(exit)") + " exitTime=" + (tr.hasExitTime ? F(tr.exitTime) : "-") + " dur=" + F(tr.duration) + " cond=[" + string.Join(" & ", tr.conditions.Select(c => c.parameter + " " + c.mode + " " + F(c.threshold))) + "]");
                }
                foreach (var t in layer.stateMachine.anyStateTransitions)
                    dump.Add("  AnyState -> " + (t.destinationState != null ? t.destinationState.name : "?") + " dur=" + F(t.duration) + " cond=[" + string.Join(" & ", t.conditions.Select(c => c.parameter + " " + c.mode + " " + F(c.threshold))) + "]");
            }
            foreach (string d in dump) Line("      " + d);
            string[] required = { "Idle", "Move", "Fire", "Reload", "Hit" };
            foreach (string r in required)
            {
                var s = states.Where(kv => kv.Key.EndsWith(":" + r)).Select(kv => kv.Value).FirstOrDefault();
                Ok("8.state." + r, s != null && s.motion is AnimationClip c && c.length > 0.05f, "estado " + r + " com AnimationClip real" + (s != null && s.motion != null ? " (" + s.motion.name + ")" : ""));
            }
            // todas as transicoes referem parametros existentes
            bool paramsFine = true;
            foreach (var kv in states) foreach (var tr in kv.Value.transitions) foreach (var c in tr.conditions) if (!pars.ContainsKey(c.parameter)) paramsFine = false;
            Ok("8.transparams", paramsFine, "todas as condicoes de transicao usam parametros declarados");
            // Idle<->Move, Idle->Fire etc
            bool idleToMove = states.Values.Any(s => s.name == "Idle" && s.transitions.Any(t => t.destinationState != null && t.destinationState.name == "Move" && t.conditions.Any(c => c.parameter == "Speed" && c.mode == AnimatorConditionMode.Greater)));
            bool moveToIdle = states.Values.Any(s => s.name == "Move" && s.transitions.Any(t => t.destinationState != null && t.destinationState.name == "Idle" && t.conditions.Any(c => c.parameter == "Speed" && c.mode == AnimatorConditionMode.Less)));
            Ok("8.idlemove", idleToMove && moveToIdle, "Idle->Move por Speed>x e Move->Idle por Speed<x");
            bool fireTrig = states.Values.Any(s => s.transitions.Any(t => t.destinationState != null && t.destinationState.name == "Fire" && t.conditions.Any(c => c.parameter == "Fire"))) || ctrl.layers.Any(l => l.stateMachine.anyStateTransitions.Any(t => t.destinationState != null && t.destinationState.name == "Fire" && t.conditions.Any(c => c.parameter == "Fire")));
            Ok("8.fire", fireTrig, "trigger Fire leva ao estado Fire");
            bool fireReload = states.Values.Any(s => s.name == "Fire" && s.transitions.Any(t => t.destinationState != null && t.destinationState.name == "Reload"));
            bool reloadIdle = states.Values.Any(s => s.name == "Reload" && s.transitions.Any(t => t.destinationState != null && t.destinationState.name == "Idle"));
            Ok("8.firereload", fireReload && reloadIdle, "Fire -> Reload -> Idle");
            bool hitDamaged = states.Values.Any(s => s.transitions.Any(t => t.destinationState != null && t.destinationState.name == "Hit" && t.conditions.Any(c => c.parameter == "Damaged"))) || ctrl.layers.Any(l => l.stateMachine.anyStateTransitions.Any(t => t.destinationState != null && t.destinationState.name == "Hit" && t.conditions.Any(c => c.parameter == "Damaged")));
            Ok("8.hit", hitDamaged, "trigger Damaged leva ao estado Hit");
            // duracao do ciclo Fire+Reload vs cooldown minimo
            AnimationClip fireClip = LoadClip(ModelsDir + "/Catapult@Fire.fbx"), reloadClip = LoadClip(ModelsDir + "/Catapult@Reload.fbx");
            CatapultContract c2 = contract;
            float minCd = c2.tiers.Min(t => t.attackCooldown);
            Ok("8.cycle", fireClip.length + reloadClip.length < minCd, "ciclo Fire (" + F(fireClip.length) + ") + Reload (" + F(reloadClip.length) + ") = " + F(fireClip.length + reloadClip.length) + " s < menor cooldown (" + F(minCd) + " s)");
            // evento de soltura
            var evs = AnimationUtility.GetAnimationEvents(fireClip);
            Info("8.events", "eventos do clipe Fire: " + string.Join("; ", evs.Select(e => e.functionName + "@" + F(e.time))));
            CatapultCombatController cc = inst.GetComponent<CatapultCombatController>();
            Ok("8.release", evs.Any(e => e.functionName == "CatapultRelease" && Mathf.Abs(e.time - cc.releaseDelay) < 0.03f), "evento CatapultRelease em t = releaseDelay (" + F(cc.releaseDelay) + " s)");
            var ae = inst.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(m => m != null && m.GetType().Name == "CatapultAnimationEvents");
            Ok("8.eventsink", ae != null && ae.gameObject == animator.gameObject, "CatapultAnimationEvents no GameObject do Animator (recebe o evento)");
            if (ae != null)
            {
                var so = new SerializedObject(ae);
                Ok("8.eventfields", so.FindProperty("projectileSpawn").objectReferenceValue != null && so.FindProperty("fireEffectPrefab").objectReferenceValue == AssetDatabase.LoadAssetAtPath<GameObject>(FirePrefabPath), "CatapultAnimationEvents: projectileSpawn e fireEffectPrefab ligados (efeito de disparo = CatapultFire)");
            }
            // clipes amostrados mudam a pose (nao vazios de fato)
            foreach (string n in ClipNames)
            {
                AnimationClip c = LoadClip(ModelsDir + "/Catapult@" + n + ".fbx");
                GameObject animRoot = animator.gameObject;
                Transform arm = FindDeep(inst.transform, "Arm"), wheel = FindDeep(inst.transform, "Wheel_FL"), root = FindDeep(inst.transform, "Root");
                float deltaArm = 0, deltaWheel = 0, deltaRoot = 0;
                c.SampleAnimation(animRoot, 0f);
                Quaternion armA = arm.localRotation, whA = wheel.localRotation, rootA = root.localRotation; Vector3 rootPA = root.localPosition;
                for (float t = c.length * 0.1f; t <= c.length; t += c.length * 0.1f)
                {
                    c.SampleAnimation(animRoot, t);
                    deltaArm = Mathf.Max(deltaArm, Quaternion.Angle(armA, arm.localRotation));
                    deltaWheel = Mathf.Max(deltaWheel, Quaternion.Angle(whA, wheel.localRotation));
                    deltaRoot = Mathf.Max(deltaRoot, Quaternion.Angle(rootA, root.localRotation) + (rootPA - root.localPosition).magnitude * 100f);
                }
                float thr = n == "Idle" ? 0.2f : 1f; bool moves = deltaArm > thr || deltaWheel > thr || deltaRoot > thr;
                Ok("8.sample." + n, moves, "clipe " + n + " muda a pose (maior variacao: braco " + F(deltaArm) + " graus, roda " + F(deltaWheel) + " graus, raiz " + F(deltaRoot) + ")");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(inst); }

        // controller de morte
        GameObject death = AssetDatabase.LoadAssetAtPath<GameObject>(DeathPrefabPath);
        Animator da = death.GetComponentInChildren<Animator>();
        AnimatorController dc = da != null ? AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(da.runtimeAnimatorController)) : null;
        Ok("8.death", dc != null && dc.layers[0].stateMachine.defaultState != null && dc.layers[0].stateMachine.defaultState.motion != null && dc.layers[0].stateMachine.defaultState.motion.name.Contains("Destroy"), "prefab de morte: controller com estado padrao = clipe Destroy (" + (dc != null && dc.layers[0].stateMachine.defaultState != null && dc.layers[0].stateMachine.defaultState.motion != null ? dc.layers[0].stateMachine.defaultState.motion.name : "?") + ")");
        AnimationClip destroy = LoadClip(ModelsDir + "/Catapult@Destroy.fbx");
        Info("8.destroyloop", "Destroy loopTime=" + destroy.isLooping + " (esperado false: permanece desabado)");
        Ok("8.destroynoloop", !destroy.isLooping, "Destroy nao e laco");
    }

    // ---------------------------------------------------------------- 9
    private static void S9_Projectile()
    {
        GameObject boulder = AssetDatabase.LoadAssetAtPath<GameObject>(BoulderPrefabPath);
        ProjectileContract pc = AssetDatabase.LoadAssetAtPath<ProjectileContract>(BoulderContractPath);
        Ok("9.1", boulder != null && pc != null, "prefab da pedra e contrato do projetil existem");
        MeshFilter mf = boulder.GetComponentInChildren<MeshFilter>();
        Ok("9.2", mf != null && mf.sharedMesh != null && Tris(mf.sharedMesh) > 50, "pedra com malha propria: " + (mf != null ? mf.sharedMesh.name + " " + Tris(mf.sharedMesh) + " tris, tamanho " + V(mf.sharedMesh.bounds.size) : "sem MeshFilter"));
        Ok("9.3", boulder.GetComponentsInChildren<Collider>().Length == 0 && boulder.GetComponentsInChildren<Rigidbody>().Length == 0, "pedra sem collider/rigidbody (voo controlado por AdvancedProjectile)");
        Ok("9.4", pc.prefab.asset == boulder, "contrato do projetil -> prefab da pedra");
        Ok("9.5", pc.impactEffect.asset == AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPrefabPath), "contrato do projetil -> CatapultImpact");
        Ok("9.6", pc.ballistic && pc.arcHeight > 0 && pc.damageType == DamageType.Siege && pc.speed > 0, "contrato balistico: ballistic=" + pc.ballistic + ", arco " + F(pc.arcHeight) + " m, vel " + F(pc.speed) + " m/s, tipo " + pc.damageType + ", vida max " + F(pc.maxLifetime) + " s");
        Ok("9.7", pc.Validate().IsValid && pc.EvaluateVisuals().IsReady, "ProjectileContract: Validate().IsValid e EvaluateVisuals().IsReady");
        GameObject impact = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPrefabPath);
        int ps = impact.GetComponentsInChildren<ParticleSystem>(true).Length;
        Ok("9.8", ps >= 2, "efeito de impacto real com " + ps + " sistemas de particulas: " + string.Join(", ", impact.GetComponentsInChildren<ParticleSystem>(true).Select(p => p.name)));
        GameObject fx = AssetDatabase.LoadAssetAtPath<GameObject>(FirePrefabPath);
        int psf = fx.GetComponentsInChildren<ParticleSystem>(true).Length;
        Ok("9.9", psf >= 1, "efeito de disparo real com " + psf + " sistemas de particulas: " + string.Join(", ", fx.GetComponentsInChildren<ParticleSystem>(true).Select(p => p.name)));
        Ok("9.10", boulder.GetComponent<AdvancedProjectile>() == null, "AdvancedProjectile e adicionado em runtime (prefab nao o carrega): ok");
        CatapultContract contract = AssetDatabase.LoadAssetAtPath<CatapultContract>(ContractPath);
        Ok("9.11", contract.Projectile == pc, "contrato da catapulta aponta para o contrato do projetil separado");
    }

    // ---------------------------------------------------------------- 10
    private static void S10_ContractCatalog()
    {
        CatapultContract contract = AssetDatabase.LoadAssetAtPath<CatapultContract>(ContractPath);
        ValidationReport vr = contract.Validate();
        Ok("10.1", vr.IsValid, "Validate().IsValid " + vr);
        VisualReadinessReport rr = contract.EvaluateVisuals();
        Ok("10.2", rr.IsReady, "IsVisualReady (unidade + projetil): " + rr);
        GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        Ok("10.3", contract.visual.prefab.asset == wrapper, "contrato.visual.prefab = wrapper Resources/Prefabs/Units/Characters/Catapult.prefab");
        foreach (var m in contract.visual.meshes) Ok("10.mesh." + m.role, m.asset != null, "malha '" + m.role + "' -> " + (m.asset != null ? AssetDatabase.GetAssetPath(m.asset) : "NULA"));
        foreach (var e in contract.visual.effects) Ok("10.fx." + e.role, e.asset != null, "efeito '" + e.role + "' -> " + (e.asset != null ? AssetDatabase.GetAssetPath(e.asset) : "NULO"));
        Ok("10.4", !contract.buildSettingsApproved, "buildSettingsApproved=false (nao aprovado para Build Settings)");
        Ok("10.5", contract.dataKind == UnitDataKind.Character, "dataKind = Character (unidade movel)");

        CharacterData cd = AssetDatabase.LoadAssetAtPath<CharacterData>(CatalogPath);
        Ok("10.6", cd != null && cd.code == Code && cd.prefab == wrapper, "CharacterData catapult -> mesmo wrapper");
        CatapultEraTier a = contract.tiers.First(t => t.era == ImperialEra.Ancient);
        Ok("10.7", cd.healthpoints == a.healthpoints && cd.attackDamage == a.attackDamage && Mathf.Approximately(cd.attackRange, a.attackRange) && Mathf.Approximately(cd.fieldOfView, contract.fieldOfView),
            "CharacterData Ancient: vida " + cd.healthpoints + "/" + a.healthpoints + ", dano " + cd.attackDamage + "/" + a.attackDamage + ", alcance " + F(cd.attackRange) + "/" + F(a.attackRange) + ", FOV " + F(cd.fieldOfView) + ", attackRate " + F(cd.attackRate) + " (poll do BT; esperado " + AdvancedUnitDataBridge.AttackPollSeconds + ")");
        Ok("10.8", Mathf.Approximately(cd.attackRate, AdvancedUnitDataBridge.AttackPollSeconds), "attackRate do CharacterData = AttackPollSeconds (0,25) -- cadencia real e do controlador");
        Ok("10.9", cd.cost.Count == contract.cost.Length && contract.cost.All(c => cd.cost.Any(v => v.code.ToString() == c.resource && v.amount == c.amount)), "custo do CharacterData = custo do contrato: " + string.Join(", ", cd.cost.Select(c => c.code + " " + c.amount)));
        // duplicatas por codigo
        var all = Resources.LoadAll<UnitData>("");
        var dup = all.Where(u => u != null && u.code == Code).ToList();
        Ok("10.10", dup.Count == 1, "UnitData com code 'catapult' em Resources: " + dup.Count + " (" + string.Join(", ", dup.Select(u => AssetDatabase.GetAssetPath(u))) + ")");
        var codes = all.Where(u => u != null).GroupBy(u => u.code).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Info("10.11", "codigos duplicados no catalogo inteiro: " + (codes.Count == 0 ? "nenhum" : string.Join(", ", codes)));
        // Build Settings
        var scenes = EditorBuildSettings.scenes.Select(s => s.path).ToList();
        Ok("10.12", !scenes.Any(s => s.ToLowerInvariant().Contains("catapult")), "Build Settings nao menciona catapulta: " + string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension)));
    }

    // ---------------------------------------------------------------- 11
    private static void S11_Tiers()
    {
        CatapultContract c = AssetDatabase.LoadAssetAtPath<CatapultContract>(ContractPath);
        var t = ImperialEras.Ordered.Select(e => c.tiers.First(x => x.era == e)).ToList();
        Line("      eras: " + string.Join(" | ", t.Select(x => x.era + " vida " + x.healthpoints + " dano " + x.attackDamage + " radial " + x.radialDamage + " raio " + F(x.blastRadius) + " alcance " + F(x.minimumRange) + ".." + F(x.attackRange) + " recarga " + F(x.attackCooldown))));
        Ok("11.1", t[0].healthpoints == 60 && t[0].attackDamage == 45 && t[0].attackRange == 20f && t[0].minimumRange == 8f && t[0].attackCooldown == 6f, "Ancient: vida 60, dano 45, alcance 8..20 m, recarga 6 s (especificacao)");
        bool vida = true, dano = true, alc = true, rec = true, rad = true;
        for (int i = 1; i < 4; i++)
        {
            vida &= t[i].healthpoints > t[i - 1].healthpoints; dano &= t[i].attackDamage > t[i - 1].attackDamage; alc &= t[i].attackRange > t[i - 1].attackRange;
            rec &= t[i].attackCooldown < t[i - 1].attackCooldown; rad &= t[i].radialDamage > t[i - 1].radialDamage && t[i].blastRadius >= t[i - 1].blastRadius;
        }
        Ok("11.2", vida && dano && alc && rec && rad, "progressao estritamente crescente em vida/dano/alcance/dano radial, raio nao decrescente, recarga estritamente decrescente");
        Ok("11.3", t.All(x => x.radialDamage < x.attackDamage && x.minimumRange < x.attackRange), "radial < direto e minimo < maximo em todas as eras");
        Ok("11.4", c.Validate().IsValid, "EraProgressionRules (via Validate) sem problemas");
    }

    // ---------------------------------------------------------------- 14
    private static void S14_References()
    {
        string[] roots =
        {
            WrapperPath, ContractPath, BoulderContractPath, CatalogPath,
        };
        List<string> assetFiles = new List<string>(roots);
        foreach (string f in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
        {
            string e = Path.GetExtension(f).ToLowerInvariant();
            if (e == ".prefab" || e == ".controller" || e == ".mat" || e == ".asset" || e == ".anim") assetFiles.Add(f.Replace('\\', '/'));
        }
        int missingGuid = 0, total = 0;
        foreach (string f in assetFiles)
            foreach (string guid in GuidsIn(f).Distinct())
            {
                total++;
                if (guid == "00000000000000000000000000000000" || guid == "0000000000000000e000000000000000" || guid == "0000000000000000f000000000000000") continue;
                if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid))) { missingGuid++; Line("  GUID sem destino " + guid + " em " + f); }
            }
        Ok("14.1", missingGuid == 0, "GUIDs referenciados por " + assetFiles.Count + " arquivos da unidade: " + total + " referencias distintas, " + missingGuid + " sem destino");

        // referencias nulas "quebradas" em objetos serializados
        int broken = 0;
        foreach (string f in assetFiles.Where(f => f.EndsWith(".prefab") || f.EndsWith(".asset") || f.EndsWith(".controller")))
        {
            foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(f))
            {
                if (o == null) continue;
                SerializedObject so;
                try { so = new SerializedObject(o); } catch { continue; }
                SerializedProperty it = so.GetIterator();
                while (it.NextVisible(true))
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null && !it.objectReferenceEntityIdValue.Equals(default(EntityId)))
                    {
                        broken++;
                        Line("  referencia quebrada: " + f + " :: " + o.name + " :: " + it.propertyPath);
                    }
            }
        }
        Ok("14.2", broken == 0, "referencias de objeto quebradas (instanceID != 0 e nulo) nos assets da unidade: " + broken);

        // todo arquivo com .meta
        int noMeta = 0;
        foreach (string f in Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta")))
            if (!File.Exists(f + ".meta")) { noMeta++; Line("  sem .meta: " + f); }
        Ok("14.3", noMeta == 0, "arquivos sem .meta: " + noMeta);

        // nomes de tipo duplicados
        Dictionary<string, List<string>> types = new Dictionary<string, List<string>>();
        foreach (string f in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories))
        {
            string t = File.ReadAllText(f);
            foreach (Match m in Regex.Matches(t, @"^\s*(?:public |internal |sealed |static |abstract |partial )*(?:class|struct|enum|interface)\s+([A-Za-z0-9_]+)", RegexOptions.Multiline))
            {
                string n = m.Groups[1].Value;
                if (!types.ContainsKey(n)) types[n] = new List<string>();
                types[n].Add(f.Replace('\\', '/'));
            }
        }
        var dups = types.Where(kv => kv.Value.Count > 1 && kv.Key.ToLowerInvariant().Contains("catapult")).ToList();
        Ok("14.4", dups.Count == 0, "tipos 'Catapult*' declarados mais de uma vez: " + (dups.Count == 0 ? "nenhum" : string.Join("; ", dups.Select(d => d.Key + " em " + string.Join(",", d.Value)))));
        var allDups = types.Where(kv => kv.Value.Count > 1 && kv.Value.Any(f => f.Contains("/Catapult/") || f.Contains("CatapultIndependent") || f.ToLower().Contains("catapult"))).ToList();
        Info("14.4b", "tipos duplicados envolvendo arquivos da catapulta: " + (allDups.Count == 0 ? "nenhum" : string.Join("; ", allDups.Select(d => d.Key))));
        var files = Directory.GetFiles("Assets", "*atapult*.cs", SearchOption.AllDirectories).GroupBy(f => Path.GetFileName(f)).Where(g => g.Count() > 1).ToList();
        Ok("14.5", files.Count == 0, "arquivos .cs da catapulta com nome duplicado: " + files.Count);
        // dependencia de outras unidades
        int cross = 0;
        foreach (string f in assetFiles.Where(f => f.EndsWith(".prefab")))
            foreach (string guid in GuidsIn(f).Distinct())
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Regex.IsMatch(p, @"CharacterRigging/(Archer|CombatMage|Crossbow|PaladinHealer)/|Characters/(Archer|Soldier|Worker|Spearman|HeraldicKnight|DarkKnight|CombatMage|Crossbow|PaladinHealer)")) { cross++; Line("  depende de outra unidade: " + f + " -> " + p); }
            }
        Ok("14.6", cross == 0, "prefabs da catapulta sem dependencia de assets de outras unidades: " + cross);
    }

    // ---------------------------------------------------------------- 13
    // O skinning do SkinnedMeshRenderer nao e atualizado entre Camera.Render consecutivos em EditMode/batch; por isso cada
    // pose e "assada" (BakeMesh) em malhas rigidas temporarias antes da captura.
    public sealed class Baked
    {
        public readonly List<GameObject> Proxies = new List<GameObject>();
        public readonly List<SkinnedMeshRenderer> Hidden = new List<SkinnedMeshRenderer>();
        public Bounds Bounds;
        public void Dispose()
        {
            foreach (GameObject g in Proxies) { if (g != null) { Mesh m = g.GetComponent<MeshFilter>().sharedMesh; UnityEngine.Object.DestroyImmediate(g); UnityEngine.Object.DestroyImmediate(m); } }
            foreach (SkinnedMeshRenderer s in Hidden) if (s != null) s.enabled = true;
            Proxies.Clear(); Hidden.Clear();
        }
    }

    public static Baked Bake(GameObject root, Func<SkinnedMeshRenderer, bool> filter)
    {
        Baked b = new Baked();
        bool any = false;
        foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!filter(smr) || smr.sharedMesh == null) continue;
            Mesh m = new Mesh();
            smr.BakeMesh(m, true);
            GameObject p = new GameObject("proxy_" + smr.name);
            p.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
            p.AddComponent<MeshFilter>().sharedMesh = m;
            MeshRenderer mr = p.AddComponent<MeshRenderer>();
            mr.sharedMaterials = smr.sharedMaterials;
            mr.shadowCastingMode = ShadowCastingMode.On; mr.receiveShadows = true;
            b.Proxies.Add(p);
            if (smr.enabled) { smr.enabled = false; b.Hidden.Add(smr); }
            if (!any) { b.Bounds = mr.bounds; any = true; } else b.Bounds.Encapsulate(mr.bounds);
        }
        return b;
    }

    private static string T2(float t) { return t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); }

    private static void S13_Renders()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeLight();
        GameObject ground = MakeGround();
        GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        GameObject unit = (GameObject)PrefabUtility.InstantiatePrefab(wrapper);
        unit.transform.position = Vector3.zero;
        unit.transform.rotation = Quaternion.identity;
        foreach (var rc in unit.GetComponentsInChildren<Canvas>(true)) rc.gameObject.SetActive(false);
        Camera cam = MakeCamera(new Color(0.62f, 0.7f, 0.78f));
        LODGroup lod = unit.GetComponentInChildren<LODGroup>();
        lod.enabled = false; // controle manual: so o LOD escolhido e desenhado (via proxies)
        Animator animator = unit.GetComponentInChildren<Animator>();
        animator.enabled = false;
        GameObject animRoot = animator.gameObject;
        LOD[] lods = lod.GetLODs();

        // escala: capsula humana de 1,8 m (o prefab do Soldier e uma caixa branca, sem uso como referencia)
        GameObject human = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        human.name = "Human1.8m";
        human.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
        human.transform.position = new Vector3(2.7f, 0.9f, 1.3f);
        Material hm = new Material(Shader.Find("Standard")); hm.color = new Color(0.85f, 0.4f, 0.3f); human.GetComponent<Renderer>().sharedMaterial = hm;
        UnityEngine.Object.DestroyImmediate(human.GetComponent<Collider>());
        human.SetActive(false);

        // os SMRs reais ficam sempre desligados; so os proxies assados sao desenhados
        foreach (SkinnedMeshRenderer s in unit.GetComponentsInChildren<SkinnedMeshRenderer>(true)) s.enabled = false;
        Func<int, Func<SkinnedMeshRenderer, bool>> lodFilter = idx => smr => lods[idx].renderers.Contains(smr);
        AnimationClip idle = LoadClip(ModelsDir + "/Catapult@Idle.fbx");
        idle.SampleAnimation(animRoot, 0f);
        Baked b0 = Bake(unit, lodFilter(0));
        Bounds bounds = b0.Bounds;
        b0.Dispose();
        Line("      bounds LOD0 em repouso (BakeMesh): tamanho " + V(bounds.size) + " centro " + V(bounds.center));
        float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        Vector3 c = bounds.center;
        Vector3 fwd = unit.transform.forward, right = unit.transform.right;
        Dictionary<string, float> mag = new Dictionary<string, float>();
        float d = size * 2.1f;

        Action<string, int, Vector3, float, float, Vector3?> snap = (name, lodIdx, dir, dist, lift, center) =>
        {
            Baked b = Bake(unit, lodFilter(lodIdx));
            mag[name] = Shoot(cam, center ?? c, dir, dist, "render-" + name + ".png", lift);
            b.Dispose();
        };

        snap("LOD0-front", 0, fwd, d, 0.2f, null);
        snap("LOD0-back", 0, -fwd, d, 0.2f, null);
        snap("LOD0-side", 0, right, d, 0.2f, null);
        snap("LOD0-side-left", 0, -right, d, 0.2f, null);
        snap("LOD0-q-front", 0, fwd + right, d, 0.35f, null);
        snap("LOD0-q-rear", 0, -fwd - right, d, 0.35f, null);
        snap("LOD0-q-front-left", 0, fwd - right, d, 0.35f, null);
        snap("LOD0-top", 0, fwd * 0.05f + Vector3.up, d * 0.95f, 0f, null);
        snap("LOD0-iso", 0, fwd + right, d * 1.1f, 0.9f, null);
        // com humano de 1,8 m ao lado
        human.SetActive(true);
        snap("scale-human", 0, fwd + right * 0.7f, 9.5f, 0.25f, new Vector3(1.2f, 1.1f, 0.5f));
        human.SetActive(false);
        // LODs
        for (int i = 0; i < 3; i++) snap("LOD" + i + "-q-front", i, fwd + right, d, 0.35f, null);

        // poses
        foreach (var pose in new (string clip, float[] times)[]
        {
            ("Fire", new[] { 0f, 0.2f, 0.3f, 0.4f, 0.5f, 0.7f, 1.0f }),
            ("Reload", new[] { 0.3f, 1.0f, 1.8f, 2.5f }),
            ("Hit", new[] { 0.15f, 0.3f }),
            ("Move", new[] { 0.3f, 0.8f }),
        })
        {
            AnimationClip clip = LoadClip(ModelsDir + "/Catapult@" + pose.clip + ".fbx");
            foreach (float t in pose.times)
            {
                clip.SampleAnimation(animRoot, Mathf.Min(t, clip.length));
                string tag = pose.clip + "-" + T2(t);
                Vector3 cc = c + Vector3.up * 0.4f;
                snap("pose-" + tag + "-side", 0, right, size * 2.5f, 0.1f, cc);
                snap("pose-" + tag + "-q", 0, fwd + right, size * 2.5f, 0.3f, cc);
            }
        }
        idle.SampleAnimation(animRoot, 0f);
        UnityEngine.Object.DestroyImmediate(human);
        UnityEngine.Object.DestroyImmediate(unit);

        // pedra
        GameObject boulder = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BoulderPrefabPath));
        boulder.transform.position = new Vector3(0, 1f, 0);
        Bounds bb = BodyBounds(boulder);
        mag["boulder"] = Shoot(cam, bb.center, new Vector3(1, 0.3f, 1), Mathf.Max(0.8f, bb.size.magnitude * 3.2f), "render-boulder.png", 0.2f, 600, 450);
        Line("      pedra: tamanho " + V(bb.size));
        UnityEngine.Object.DestroyImmediate(boulder);

        // efeitos de disparo e impacto (particulas simuladas)
        foreach (var fx in new[] { ("fire", FirePrefabPath, new[] { 0.1f, 0.3f, 0.7f }), ("impact", ImpactPrefabPath, new[] { 0.08f, 0.25f, 0.6f, 1.2f }) })
        {
            foreach (float t in fx.Item3)
            {
                GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fx.Item2));
                g.transform.position = new Vector3(0, 0.5f, 0);
                foreach (ParticleSystem p in g.GetComponentsInChildren<ParticleSystem>(true)) { p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
                ParticleSystem rootPs = g.GetComponentInChildren<ParticleSystem>(true);
                rootPs.Simulate(t, true, true);
                mag[fx.Item1 + "-" + T2(t)] = Shoot(cam, new Vector3(0, 1.2f, 0), new Vector3(1, 0.1f, 1.2f), 10f, "render-fx-" + fx.Item1 + "-" + T2(t) + ".png", 0.15f, 800, 600);
                UnityEngine.Object.DestroyImmediate(g);
            }
        }

        // destruicao (prefab de morte: amostra o clipe Destroy)
        GameObject death = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DeathPrefabPath));
        death.transform.position = Vector3.zero;
        Animator da = death.GetComponentInChildren<Animator>(); da.enabled = false;
        AnimationClip dclip = LoadClip(ModelsDir + "/Catapult@Destroy.fbx");
        LODGroup dl = death.GetComponentInChildren<LODGroup>();
        if (dl != null) dl.enabled = false;
        LOD[] lodsD = dl != null ? dl.GetLODs() : null;
        var dsmrs = death.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Line("      prefab de morte: " + dsmrs.Length + " SkinnedMeshRenderers: " + string.Join(", ", dsmrs.Select(x => x.name)) + "; LODGroup=" + (dl != null));
        foreach (SkinnedMeshRenderer s in dsmrs) s.enabled = false;
        foreach (float t in new[] { 0f, 0.5f, 1.0f, 1.6f, dclip.length })
        {
            dclip.SampleAnimation(da.gameObject, t);
            Baked bd = Bake(death, smr => lodsD == null || lodsD[0].renderers.Contains(smr));
            string tag = T2(t);
            Vector3 cc = bd.Bounds.center + Vector3.up * 0.3f;
            mag["destroy-" + tag] = Shoot(cam, cc, fwd + right, size * 2.4f, "render-destroy-" + tag + ".png", 0.3f);
            mag["destroy-" + tag + "-side"] = Shoot(cam, cc, right, size * 2.4f, "render-destroy-" + tag + "-side.png", 0.1f);
            Line("      Destroy t=" + F(t) + " bounds " + V(bd.Bounds.size) + " centro " + V(bd.Bounds.center));
            bd.Dispose();
        }
        UnityEngine.Object.DestroyImmediate(death);

        UnityEngine.Object.DestroyImmediate(cam.gameObject);
        UnityEngine.Object.DestroyImmediate(ground);

        float worst = mag.Values.Max();
        Ok("13.1", worst < 0.001f, mag.Count + " renders proprios (frente/costas/lados/3-4/topo/iso/LODs/poses/pedra/efeitos/destruicao): pior fracao magenta = " + worst.ToString("P4"));
        foreach (var kv in mag.Where(k => k.Value > 0)) Line("  magenta em " + kv.Key + ": " + kv.Value.ToString("P4"));
    }
}
