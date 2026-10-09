using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using EraImperial.Units.Advanced;
using EraImperial.Units.Crossbow;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// AUDITORIA INDEPENDENTE da Balestra (crossbow). Escrita pelo auditor; nao reutiliza CrossbowAudit/CrossbowPlayModeCheck.
///   EditMode : -quit -executeMethod CrossbowIndependentAudit.RunEdit  -> Docs/AdvancedUnits/audit/crossbow/edit-checks.txt (+ renders)
///   Play Mode: -executeMethod CrossbowIndependentAudit.RunPlay         -> Docs/AdvancedUnits/audit/crossbow/play-checks.txt (+ renders), Map1 e Map2 reais
/// Somente leitura nos assets do jogo. Escreve apenas em Docs/AdvancedUnits/audit/crossbow e TestResults.
/// </summary>
[InitializeOnLoad]
public static class CrossbowIndependentAudit
{
    private const string OutDir = "Docs/AdvancedUnits/audit/crossbow";
    private const string EditReport = OutDir + "/edit-checks.txt";
    private const string PlayReport = OutDir + "/play-checks.txt";
    private const string Key = "CBIndep.";

    private const string Code = "crossbow";
    private const string Root = "Assets/CharacterRigging/Crossbow";
    private const string ContractPath = "Assets/Data/AdvancedUnits/Crossbow.asset";
    private const string BoltContractPath = "Assets/Data/AdvancedUnits/Projectiles/CrossbowBolt.asset";
    private const string WrapperPath = "Assets/Resources/Prefabs/Units/Characters/Crossbow.prefab";
    private const string CatalogPath = "Assets/Resources/ScriptableObjects/Units/Characters/Crossbow.asset";
    private const string BodyFbx = Root + "/Models/Crossbow.fbx";
    private const string BoltFbx = Root + "/Accessories/Crossbow_Bolt.fbx";
    private const string ModelsDir = Root + "/Models";
    private static readonly string[] ClipNames = { "Idle", "Move", "Reload", "Fire", "Hit", "Destroy" };

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

    private static string F(float v) { return v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }
    private static string V(Vector3 v) { return "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")"; }

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

    private static float Shoot(Camera cam, Vector3 center, Vector3 dir, float dist, string file, float lift = 0.15f, int w = 900, int h = 700)
    {
        cam.transform.position = center + dir.normalized * dist + Vector3.up * dist * lift;
        cam.transform.LookAt(center);
        Texture2D t = Render(cam, w, h);
        File.WriteAllBytes(Path.Combine(OutDir, file), t.EncodeToPNG());
        float f = MagentaFraction(t);
        UnityEngine.Object.DestroyImmediate(t);
        return f;
    }

    private static bool IsUnitBodyRenderer(Renderer r)
    {
        if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) return false;
        string n = r.name;
        if (n.Contains("TeamRing") || n.Contains("Minimap") || n.Contains("FOV")) return false;
        return true;
    }

    private static Bounds RendBounds(GameObject go, Func<Renderer, bool> filter = null)
    {
        Bounds b = default; bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !IsUnitBodyRenderer(r)) continue;
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

    private static Transform FindDeep(Transform root, string name)
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
            Section11_Eras();
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
        long bodySize = File.Exists(BodyFbx) ? new FileInfo(BodyFbx).Length : 0;
        Ok("1.1", bodySize > 50000, "FBX da balestra existe (" + bodySize + " bytes)");
        Ok("1.2", File.Exists(BoltFbx), "FBX do virote existe");
        foreach (string c in ClipNames)
            Ok("1.3", File.Exists($"{ModelsDir}/Crossbow@{c}.fbx"), "clipe Crossbow@" + c + ".fbx presente");
        Ok("1.4", !Directory.Exists("Tools/CharacterRigging/Inputs/Crossbow"), "nao ha pasta de entradas de terceiros (Inputs/Crossbow) - coerente com 'procedural, sem terceiros'");
        string py = File.ReadAllText("Tools/CharacterRigging/BuildCrossbow.py");
        Ok("1.5", !Regex.IsMatch(py, @"import_scene|bpy\.ops\.wm\.(obj|gltf|fbx)_import|urlopen|requests\.|http://|https://"), "BuildCrossbow.py nao importa malhas externas nem acessa a rede");
        Ok("1.6", !Regex.IsMatch(py, @"mixamo|meshy", RegexOptions.IgnoreCase), "BuildCrossbow.py nao referencia Mixamo/Meshy");
        string lic = File.ReadAllText("Docs/CharacterMeshAudit/Crossbow/LICENSE.md");
        Ok("1.7", lic.Contains("proceduralmente") && lic.Contains("sem terceiros"), "LICENSE.md declara obra procedural sem terceiros");
        // segredos
        Regex secret = new Regex(@"msy_[A-Za-z0-9]{10,}|sk-[A-Za-z0-9]{20,}|AKIA[0-9A-Z]{16}|(api[_-]?key|token|secret|password)\s*[:=]\s*['""][^'""]{12,}|Bearer\s+[A-Za-z0-9\-_\.]{20,}", RegexOptions.IgnoreCase);
        List<string> files = new List<string>();
        files.AddRange(Directory.GetFiles("Docs/CharacterMeshAudit/Crossbow", "*.*", SearchOption.TopDirectoryOnly).Where(p => !p.EndsWith(".png")));
        files.AddRange(Directory.GetFiles("Tools/CharacterRigging", "*Crossbow*", SearchOption.TopDirectoryOnly));
        files.AddRange(Directory.GetFiles(Root, "*.cs", SearchOption.AllDirectories));
        files.AddRange(Directory.GetFiles(Root, "*.json", SearchOption.AllDirectories));
        files.Add(ContractPath); files.Add(BoltContractPath); files.Add(WrapperPath); files.Add(CatalogPath);
        files.AddRange(Directory.GetFiles("Tools/CharacterRigging/Working/Crossbow", "*.json", SearchOption.AllDirectories));
        int hits = 0;
        foreach (string f in files)
        {
            if (!File.Exists(f)) continue;
            string text = File.ReadAllText(f);
            if (secret.IsMatch(text)) { hits++; Info("1.8", "possivel segredo em " + f + ": " + secret.Match(text).Value.Substring(0, Math.Min(12, secret.Match(text).Value.Length)) + "..."); }
        }
        Ok("1.8", hits == 0, $"nenhum segredo/chave nos {files.Count} arquivos de texto da unidade");
        // Mixamo / terceiros: nomes de arquivos no diretorio da unidade
        string[] all = Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta")).ToArray();
        Ok("1.9", !all.Any(p => Regex.IsMatch(Path.GetFileName(p), "mixamo|meshy|sketchfab|turbosquid", RegexOptions.IgnoreCase)), $"nenhum arquivo de terceiros pelo nome entre os {all.Length} arquivos da unidade");
    }

    // ------------------------------------------------------------------ 2
    private static GameObject LoadBody() { return AssetDatabase.LoadAssetAtPath<GameObject>(BodyFbx); }

    private static void Section2_Rig()
    {
        string[] fbxs = Directory.GetFiles(ModelsDir, "*.fbx");
        foreach (string p in fbxs)
        {
            ModelImporter mi = AssetImporter.GetAtPath(p.Replace('\\', '/')) as ModelImporter;
            Ok("2.1", mi != null && mi.animationType == ModelImporterAnimationType.Generic, Path.GetFileName(p) + " importado como Generic (" + (mi != null ? mi.animationType.ToString() : "?") + ")");
        }
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(BodyFbx).OfType<Avatar>().FirstOrDefault();
        Ok("2.2", avatar != null && avatar.isValid && !avatar.isHuman, "Avatar do corpo valido e generico (isValid=" + (avatar != null && avatar.isValid) + ", isHuman=" + (avatar != null && avatar.isHuman) + ") - maquina, nao humanoide: coerente");
        GameObject body = LoadBody();
        string[] bones = { "Root", "Wheel_L", "Wheel_R", "LimbL", "LimbL_Tip", "LimbR", "LimbR_Tip", "String", "Bolt", "Winch", "Trigger" };
        foreach (string b in bones)
            Ok("2.3", FindDeep(body.transform, b) != null, "osso '" + b + "' existe na hierarquia importada");
        SkinnedMeshRenderer[] smrs = body.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Ok("2.4", smrs.Length > 0 && smrs.All(s => s.rootBone != null && s.bones.Length > 0 && s.bones.All(b => b != null)), "todos os SkinnedMeshRenderer tem rootBone e ossos validos (" + smrs.Length + " skinned)");
        Info("2.5", "SkinnedMeshRenderers: " + string.Join(", ", smrs.Select(s => s.name + "[" + s.bones.Length + " ossos]")));
        // eixo/escala: o corpo tem de estar em metros e com frente +Z
        Bounds b0 = RendBounds(body, r => r.name.EndsWith("LOD0") || r.name.Contains("LOD0"));
        Info("2.6", "bounds LOD0 do FBX (local): centro " + V(b0.center) + " tamanho " + V(b0.size));
        Ok("2.6", b0.size.z > 1.5f && b0.size.z < 4f && b0.size.y > 0.8f && b0.size.y < 2.5f, "escala em metros plausivel para balista de carreta (" + V(b0.size) + ")");
        // clipes importados
        foreach (string c in ClipNames)
        {
            string p = $"{ModelsDir}/Crossbow@{c}.fbx";
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview__"));
            Ok("2.7", clip != null && clip.length > 0.05f && !clip.legacy && !clip.humanMotion, c + ": AnimationClip Generic real (" + (clip != null ? clip.name + ", " + F(clip.length) + " s, humanMotion=" + clip.humanMotion : "ausente") + ")");
            ModelImporter mi = AssetImporter.GetAtPath(p) as ModelImporter;
            Info("2.7", c + ": importer avatarSetup=" + mi.avatarSetup + " sourceAvatar=" + (mi.sourceAvatar != null ? mi.sourceAvatar.name : "(nenhum)") + " loopTime=" + (mi.clipAnimations.Length > 0 ? mi.clipAnimations[0].loopTime.ToString() : "default"));
        }
    }

    // ------------------------------------------------------------------ 3
    private static void Section3_Weights_Uvs_Lods()
    {
        GameObject visual = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CrossbowVisual.prefab");
        Ok("3.0", visual != null, "CrossbowVisual.prefab carregado");
        LODGroup group = visual.GetComponentInChildren<LODGroup>(true);
        Ok("5.1", group != null && group.lodCount == 3, "LODGroup com 3 niveis no prefab visual");
        LOD[] lods = group.GetLODs();
        int[] tris = new int[lods.Length];
        for (int i = 0; i < lods.Length; i++)
        {
            string names = string.Join(",", lods[i].renderers.Select(r => r.name));
            Info("5.2", $"LOD{i}: limiar {F(lods[i].screenRelativeTransitionHeight)}, {lods[i].renderers.Length} renderers: {names}");
            foreach (Renderer r in lods[i].renderers)
            {
                Mesh m = MeshOf(r);
                Ok("3.1", m != null, $"LOD{i}/{r.name}: malha atribuida");
                if (m == null) continue;
                tris[i] += Tris(m);
                Vector2[] uv = m.uv;
                bool uvOk = uv.Length == m.vertexCount && uv.All(u => !float.IsNaN(u.x) && !float.IsNaN(u.y) && !float.IsInfinity(u.x) && !float.IsInfinity(u.y));
                int[] idx = m.triangles; int degenerate = 0;
                for (int k = 0; k + 2 < idx.Length; k += 3)
                {
                    Vector2 a = uv[idx[k]], b = uv[idx[k + 1]], c = uv[idx[k + 2]];
                    if (Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) < 1e-9f) degenerate++;
                }
                float outside = uv.Length == 0 ? 1 : (float)uv.Count(u => u.x < -0.01f || u.x > 1.01f || u.y < -0.01f || u.y > 1.01f) / uv.Length;
                float degFrac = idx.Length > 2 ? (float)degenerate / (idx.Length / 3) : 0;
                Ok("3.2", uvOk && degFrac < 0.05f, $"LOD{i}/{r.name}: UVs finitas ({uv.Length}/{m.vertexCount}), degenerados {degFrac:P1}, fora de [0,1] {outside:P1}");
                if (r is SkinnedMeshRenderer s)
                {
                    BoneWeight[] w = m.boneWeights;
                    int bad = 0, unw = 0, over4 = 0, maxInf = 0; float maxDev = 0;
                    var allWeights = m.GetAllBoneWeights(); var perVert = m.GetBonesPerVertex();
                    foreach (byte c in perVert) { if (c > 4) over4++; if (c > maxInf) maxInf = c; }
                    for (int v = 0; v < w.Length; v++)
                    {
                        float sum = w[v].weight0 + w[v].weight1 + w[v].weight2 + w[v].weight3;
                        if (sum <= 0f) unw++;
                        float dev = Mathf.Abs(sum - 1f); if (dev > maxDev) maxDev = dev;
                        if (dev > 0.01f) bad++;
                    }
                    Ok("3.3", w.Length == m.vertexCount && unw == 0 && bad == 0 && over4 == 0 && s.bones.Length == m.bindposes.Length,
                        $"LOD{i}/{r.name}: pesos ok (vertices {w.Length}, sem peso {unw}, soma != 1 em {bad} (max desvio {maxDev:0.0000}), max influencias {maxInf}, ossos {s.bones.Length}/bindposes {m.bindposes.Length})");
                }
                else Info("3.3", $"LOD{i}/{r.name}: MeshRenderer estatico (sem pesos) - peca rigida");
            }
        }
        Ok("5.3", tris[0] > tris[1] && tris[1] > tris[2] && tris[2] > 0, $"triangulos estritamente decrescentes: LOD0={tris[0]} > LOD1={tris[1]} > LOD2={tris[2]}");
        Ok("5.4", tris[0] <= 8000 && tris[1] <= 3000 && tris[2] <= 1000, "orcamento de props (<= 8000 / 3000 / 1000)");
        Ok("5.5", lods[0].screenRelativeTransitionHeight > lods[1].screenRelativeTransitionHeight && lods[1].screenRelativeTransitionHeight > lods[2].screenRelativeTransitionHeight && lods[2].screenRelativeTransitionHeight > 0 && lods[0].screenRelativeTransitionHeight < 1,
            $"limiares do LODGroup decrescentes e coerentes ({F(lods[0].screenRelativeTransitionHeight)} > {F(lods[1].screenRelativeTransitionHeight)} > {F(lods[2].screenRelativeTransitionHeight)})");
        // renderers fora de qualquer LOD (apareceriam sempre)
        HashSet<Renderer> inLod = new HashSet<Renderer>(lods.SelectMany(l => l.renderers));
        Renderer[] stray = visual.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer) && !inLod.Contains(r)).ToArray();
        Ok("5.6", stray.Length == 0, "nenhum renderer de malha fora do LODGroup" + (stray.Length > 0 ? ": " + string.Join(",", stray.Select(r => r.name)) : ""));
        // o contrato lista a malha do corpo: confirmar que o LOD0 do prefab usa as malhas do FBX do contrato
        Mesh[] fbxMeshes = AssetDatabase.LoadAllAssetsAtPath(BodyFbx).OfType<Mesh>().ToArray();
        Ok("5.7", lods[0].renderers.All(r => fbxMeshes.Contains(MeshOf(r))), "malhas do LOD0 do prefab sao sub-assets do FBX da balestra");
    }

    // ------------------------------------------------------------------ 4
    private static void Section4_Materials()
    {
        GameObject visual = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CrossbowVisual.prefab");
        HashSet<Material> mats = new HashSet<Material>();
        foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
            foreach (Material m in r.sharedMaterials) mats.Add(m);
        foreach (string path in new[] { Root + "/Prefabs/CrossbowBolt.prefab", Root + "/Prefabs/CrossbowImpact.prefab", Root + "/Prefabs/CrossbowMuzzle.prefab", Root + "/Prefabs/CrossbowDeath.prefab", WrapperPath })
        {
            GameObject g = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials) mats.Add(m);
        }
        foreach (Material m in mats)
        {
            string id = m != null ? m.name : "(nulo)";
            Ok("4.1", m != null && m.shader != null && m.shader.isSupported && !m.shader.name.Contains("InternalErrorShader") && m.shader.name != "Hidden/InternalErrorShader",
                $"material {id}: shader '{(m != null && m.shader != null ? m.shader.name : "?")}' suportado");
            if (m == null) continue;
            bool particle = m.shader.name.Contains("Particles") || m.shader.name.Contains("Unlit");
            if (!particle && m.shader.name == "Standard")
                Ok("4.2", m.mainTexture != null, $"material {id}: textura principal atribuida ({(m.mainTexture != null ? m.mainTexture.name + " " + m.mainTexture.width + "x" + m.mainTexture.height : "NENHUMA")})");
            else
            {
                string texture = m.shader != null && m.HasProperty("_MainTex") && m.mainTexture != null ? m.mainTexture.name : "(sem)";
                string color = m.shader != null && m.HasProperty("_Color") ? m.color.ToString() : "(n/a)";
                Info("4.2", $"material {id}: shader {m.shader.name}, textura {texture}, cor {color}");
            }
        }
        foreach (string t in Directory.GetFiles(Root + "/Textures", "*.png"))
        {
            long len = new FileInfo(t).Length;
            Ok("4.3", len < 4 * 1024 * 1024, Path.GetFileName(t) + " tamanho " + len / 1024 + " KB");
        }
    }

    // ------------------------------------------------------------------ 6
    private static void Section6_Wrapper()
    {
        GameObject w = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        Ok("6.0", w != null, "wrapper Crossbow.prefab carregado");
        List<string> comps = w.GetComponents<Component>().Select(c => c == null ? "MISSING" : c.GetType().Name).ToList();
        Info("6.1", "componentes da raiz: " + string.Join(", ", comps));
        foreach (string n in new[] { "CharacterManager", "NavMeshAgent", "BoxCollider", "AdvancedUnitEraBinding", "AdvancedUnitAnimatorDriver", "ProjectileAttackController", "AdvancedUnitDeathEffect", "CharacterBT" })
            Ok("6.1", comps.Contains(n), "raiz tem " + n);
        int missing = 0;
        foreach (Transform t in w.GetComponentsInChildren<Transform>(true))
            missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
        Ok("6.2", missing == 0 && !comps.Contains("MISSING"), "nenhum script ausente (Missing) em toda a hierarquia do wrapper");

        // comparar com o prefab do Soldier (base clonada)
        GameObject soldier = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Units/Characters/Soldier.prefab");
        if (soldier != null)
        {
            string[] sc = soldier.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name).ToArray();
            string[] miss = sc.Where(n => !comps.Contains(n)).ToArray();
            Ok("6.3", miss.Length == 0, "wrapper contem todos os componentes da raiz do Soldier" + (miss.Length > 0 ? " (faltam: " + string.Join(",", miss) + ")" : ""));
            Ok("6.3b", w.layer == soldier.layer && w.tag == soldier.tag, $"layer/tag iguais ao Soldier ({LayerMask.LayerToName(w.layer)}/{w.tag})");
        }

        BoxCollider box = w.GetComponent<BoxCollider>();
        GameObject visual = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CrossbowVisual.prefab");
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(w);
        inst.transform.position = Vector3.zero; inst.transform.rotation = Quaternion.identity;
        Bounds b = RendBounds(inst);
        Info("6.4", "bounds visual (mundo, wrapper na origem): centro " + V(b.center) + " tamanho " + V(b.size));
        Info("6.4", "BoxCollider: centro " + V(box.center) + " tamanho " + V(box.size));
        Bounds cb = new Bounds(box.center, box.size);
        Ok("6.4", Mathf.Abs(cb.size.z - b.size.z) < 0.6f && Mathf.Abs(cb.size.x - b.size.x) < 0.6f && Mathf.Abs(cb.size.y - b.size.y) < 0.6f,
            $"BoxCollider ({V(cb.size)}) coerente com o modelo ({V(b.size)})");
        Ok("6.4b", cb.Contains(b.center) && Mathf.Abs(cb.center.y - (b.center.y)) < 0.5f, "centro do BoxCollider cobre o centro do modelo");

        NavMeshAgent agent = w.GetComponent<NavMeshAgent>();
        Info("6.5", $"NavMeshAgent: velocidade {agent.speed}, raio {agent.radius}, altura {agent.height}, aceleracao {agent.acceleration}, angularSpeed {agent.angularSpeed}");
        Ok("6.5", agent.radius > 0.3f && agent.radius <= 1.5f && agent.speed > 0.5f && agent.speed < 8f, "NavMeshAgent plausivel (raio " + agent.radius + ", velocidade " + agent.speed + ")");

        AdvancedUnitEraBinding binding = w.GetComponent<AdvancedUnitEraBinding>();
        CrossbowContract contract = AssetDatabase.LoadAssetAtPath<CrossbowContract>(ContractPath);
        Ok("6.6", binding.contract == contract && contract.code == Code, "AdvancedUnitEraBinding aponta para o contrato Crossbow.asset");
        ProjectileAttackController atk = w.GetComponent<ProjectileAttackController>();
        Ok("6.7", atk.contract == contract, "ProjectileAttackController.contract = contrato Crossbow");
        Ok("6.7b", atk.animator != null && atk.animator.gameObject.transform.IsChildOf(w.transform), "ProjectileAttackController.animator referencia o Animator do visual");
        Ok("6.7c", atk.projectileSpawn != null && RelPath(w.transform, atk.projectileSpawn) == "Mesh/CrossbowVisual/CrossbowRig/Root/Sockets/BoltSpawn", "projectileSpawn = socket BoltSpawn (" + (atk.projectileSpawn != null ? RelPath(w.transform, atk.projectileSpawn) : "nulo") + ")");
        AdvancedUnitAnimatorDriver drv = w.GetComponent<AdvancedUnitAnimatorDriver>();
        Ok("6.8", drv.animator == atk.animator || drv.animator == null, "AnimatorDriver usa o mesmo Animator (ou o encontra por filho)");
        AdvancedUnitDeathEffect death = w.GetComponent<AdvancedUnitDeathEffect>();
        GameObject deathPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CrossbowDeath.prefab");
        Ok("6.9", death.deathPrefab == deathPrefab && deathPrefab != null, "AdvancedUnitDeathEffect.deathPrefab = CrossbowDeath.prefab");
        Info("6.10", $"releaseDelay={F(atk.releaseDelay)} sourceHeight={F(atk.sourceHeight)} turnSpeed={F(atk.turnSpeed)}");

        // unit manager: anel de time
        UnitManager um = w.GetComponent<UnitManager>();
        Info("6.11", "UnitManager: " + (um != null ? "ok" : "ausente"));
        Animator anim = atk.animator;
        Ok("6.12", anim.runtimeAnimatorController == AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "/Animation/Crossbow.controller"), "Animator usa Crossbow.controller");
        Ok("6.13", anim.avatar != null && anim.avatar.isValid, "Animator tem Avatar valido");
        Ok("6.14", !anim.applyRootMotion, "Animator sem root motion (a unidade e movida pelo NavMeshAgent)");
        // visual presente e dentro do 'Mesh'
        Ok("6.15", w.transform.Find("Mesh/CrossbowVisual") != null, "visual esta em Mesh/CrossbowVisual");
        // a unidade nao deve ter mais de um Animator ativo nem Rigidbody
        Ok("6.16", w.GetComponentsInChildren<Animator>(true).Length == 1, "exatamente um Animator no wrapper (" + w.GetComponentsInChildren<Animator>(true).Length + ")");
        // renderers listados
        foreach (Renderer r in inst.GetComponentsInChildren<Renderer>())
            Info("6.17", "renderer " + RelPath(inst.transform, r.transform) + " (" + r.GetType().Name + ") enabled=" + r.enabled + " layer=" + LayerMask.LayerToName(r.gameObject.layer));
        UnityEngine.Object.DestroyImmediate(inst);
    }

    // ------------------------------------------------------------------ 7
    private static void Section7_Sockets()
    {
        GameObject w = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        CrossbowContract contract = AssetDatabase.LoadAssetAtPath<CrossbowContract>(ContractPath);
        foreach (SocketRequirement s in contract.visual.sockets)
            Ok("7.1", w.transform.Find(s.expectedTransformPath) != null, $"socket {s.socketName}: Transform.Find('{s.expectedTransformPath}') a partir do wrapper");

        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(w);
        inst.transform.position = Vector3.zero; inst.transform.rotation = Quaternion.identity;
        Animator anim = inst.GetComponentInChildren<Animator>();
        AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath($"{ModelsDir}/Crossbow@Idle.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        idle.SampleAnimation(anim.gameObject, 0f);
        Transform spawn = inst.transform.Find("Mesh/CrossbowVisual/CrossbowRig/Root/Sockets/BoltSpawn");
        Transform mount = inst.transform.Find("Mesh/CrossbowVisual/CrossbowRig/Root/Sockets/WeaponMount");
        Bounds body = RendBounds(inst, r => r.name.Contains("LOD0") && !r.name.Contains("Bolt") && !r.name.Contains("String"));
        Info("7.2", "corpo LOD0 (sem virote/corda): " + V(body.min) + " .. " + V(body.max));
        Info("7.2", "BoltSpawn mundo " + V(spawn.position) + " | WeaponMount " + V(mount.position));
        Bounds head = RendBounds(inst, r => r.name.Contains("LOD0"));
        Ok("7.2", Mathf.Abs(spawn.position.x) < 0.15f, "BoltSpawn centrado no eixo X (" + F(spawn.position.x) + ")");
        Ok("7.3", spawn.position.y > 0.5f && spawn.position.y < body.max.y + 0.1f, "BoltSpawn a altura plausivel do trilho (" + F(spawn.position.y) + " m)");
        // o bolt carregado: ponta do virote
        Renderer boltR = inst.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name == "Bolt_LOD0");
        if (boltR != null)
        {
            Bounds bb = boltR.bounds;
            Info("7.4", "virote carregado (Bolt_LOD0): " + V(bb.min) + " .. " + V(bb.max));
            float dTip = Vector3.Distance(spawn.position, new Vector3(bb.center.x, bb.center.y, bb.max.z));
            Ok("7.4", dTip < 0.35f, $"BoltSpawn fica na ponta frontal do virote carregado (distancia {F(dTip)} m)");
            Ok("7.5", spawn.position.z >= bb.center.z, "BoltSpawn a frente do centro do virote (z " + F(spawn.position.z) + " >= " + F(bb.center.z) + ")");
        }
        else Ok("7.4", false, "Bolt_LOD0 nao encontrado para comparar com BoltSpawn");
        Ok("7.6", Vector3.Distance(mount.position, head.center) < 1.2f && Mathf.Abs(mount.position.x) < 0.2f, "WeaponMount proximo do corpo/cabecote (distancia ao centro " + F(Vector3.Distance(mount.position, head.center)) + " m)");
        // frente: BoltSpawn deve ser o ponto mais a frente do corpo, nao o rabo do carrinho
        Ok("7.7", spawn.position.z > head.center.z, "BoltSpawn na metade dianteira (+Z) do modelo (z " + F(spawn.position.z) + " > centro " + F(head.center.z) + ")");
        // direcao: o virote aponta para +Z local? (eixo do socket)
        Info("7.8", "BoltSpawn forward (mundo) = " + V(spawn.forward));
        UnityEngine.Object.DestroyImmediate(inst);
    }

    // ------------------------------------------------------------------ 8
    private static AnimationClip ClipOf(string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath($"{ModelsDir}/Crossbow@{name}.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
    }

    private static void Section8_Animator()
    {
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "/Animation/Crossbow.controller");
        CrossbowContract contract = AssetDatabase.LoadAssetAtPath<CrossbowContract>(ContractPath);
        Ok("8.0", ctrl != null, "Crossbow.controller carregado");
        Dictionary<string, AnimatorControllerParameterType> ps = ctrl.parameters.ToDictionary(p => p.name, p => p.type);
        Info("8.1", "parametros: " + string.Join(", ", ps.Select(kv => kv.Key + ":" + kv.Value)));
        (string, AnimatorControllerParameterType)[] want =
        {
            ("Speed", AnimatorControllerParameterType.Float), ("Fire", AnimatorControllerParameterType.Trigger), ("Damaged", AnimatorControllerParameterType.Trigger),
            ("Running", AnimatorControllerParameterType.Bool), ("Attacking", AnimatorControllerParameterType.Bool), ("Hit", AnimatorControllerParameterType.Trigger)
        };
        foreach (var (n, t) in want) Ok("8.1", ps.TryGetValue(n, out var got) && got == t, $"parametro {n} ({t})");

        // dump de estados/transicoes
        foreach (AnimatorControllerLayer layer in ctrl.layers)
        {
            Info("8.2", $"camada '{layer.name}' peso {F(layer.defaultWeight)} modo {layer.blendingMode} mascara {(layer.avatarMask != null ? layer.avatarMask.name : "-")} default={layer.stateMachine.defaultState?.name}");
            foreach (ChildAnimatorState cs in layer.stateMachine.states)
            {
                AnimationClip mc = cs.state.motion as AnimationClip;
                Info("8.2", $"  estado {cs.state.name}: clip {(mc != null ? mc.name + " " + F(mc.length) + "s" : "(nenhum)")} speed {F(cs.state.speed)} param {(cs.state.speedParameterActive ? cs.state.speedParameter : "-")}");
                foreach (AnimatorStateTransition tr in cs.state.transitions)
                    Info("8.2", $"    -> {tr.destinationState?.name} exit={tr.hasExitTime}@{F(tr.exitTime)} dur={F(tr.duration)} cond=[{string.Join(";", tr.conditions.Select(c => c.parameter + " " + c.mode + " " + F(c.threshold)))}]");
            }
            foreach (AnimatorStateTransition tr in layer.stateMachine.anyStateTransitions)
                Info("8.2", $"  AnyState -> {tr.destinationState?.name} cond=[{string.Join(";", tr.conditions.Select(c => c.parameter + " " + c.mode))}]");
        }
        // contrato x controller: cada estado exigido usa o clip do contrato
        foreach (AnimationRequirement a in contract.visual.animations)
        {
            if (a.state == "Destroy") continue; // fica no controller de morte
            AnimatorState st = ctrl.layers.SelectMany(l => l.stateMachine.states).Select(c => c.state).FirstOrDefault(s => s.name == a.state);
            Ok("8.3", st != null && st.motion == a.clip && a.clip != null, $"estado '{a.state}' existe no controller e usa o clip do contrato ({(a.clip != null ? a.clip.name : "nulo")})");
        }
        AnimatorController dc = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "/Animation/CrossbowDeath.controller");
        AnimationRequirement destroy = contract.visual.animations.First(a => a.state == "Destroy");
        Ok("8.4", dc != null && dc.layers[0].stateMachine.states.Any(s => s.state.motion == destroy.clip), "CrossbowDeath.controller toca o clip Destroy do contrato");

        // transicoes coerentes (estrutura)
        AnimatorStateMachine m0 = ctrl.layers[0].stateMachine;
        AnimatorState Idle = m0.states.First(s => s.state.name == "Idle").state, Move = m0.states.First(s => s.state.name == "Move").state,
            Fire = m0.states.First(s => s.state.name == "Fire").state, Reload = m0.states.First(s => s.state.name == "Reload").state;
        Ok("8.5", Idle.transitions.Any(t => t.destinationState == Move && t.conditions.Any(c => c.parameter == "Speed" && c.mode == AnimatorConditionMode.Greater)), "Idle -> Move por Speed > limiar");
        Ok("8.5b", Move.transitions.Any(t => t.destinationState == Idle && t.conditions.Any(c => c.parameter == "Speed" && c.mode == AnimatorConditionMode.Less)), "Move -> Idle por Speed < limiar");
        Ok("8.5c", Idle.transitions.Any(t => t.destinationState == Fire && t.conditions.Any(c => c.parameter == "Fire")) && Move.transitions.Any(t => t.destinationState == Fire && t.conditions.Any(c => c.parameter == "Fire")), "Idle/Move -> Fire pelo trigger Fire");
        Ok("8.5d", Fire.transitions.Any(t => t.destinationState == Reload && t.hasExitTime) && Reload.transitions.Any(t => t.destinationState == Idle && t.hasExitTime), "Fire -> Reload -> Idle por tempo de saida");
        float cycle = Fire.motion.averageDuration + Reload.motion.averageDuration;
        foreach (CrossbowEraTier t in contract.tiers)
            Ok("8.6", cycle < t.attackCooldown, $"ciclo de animacao Fire+Reload ({F(cycle)} s) cabe no cooldown da era {t.era} ({F(t.attackCooldown)} s; folga {F(t.attackCooldown - cycle)} s)");

        // clipes: curvas reais, sem NaN, com movimento
        foreach (string name in ClipNames)
        {
            AnimationClip c = ClipOf(name);
            EditorCurveBinding[] bind = AnimationUtility.GetCurveBindings(c);
            int animated = 0, nan = 0, keys = 0;
            foreach (EditorCurveBinding bd in bind)
            {
                AnimationCurve cv = AnimationUtility.GetEditorCurve(c, bd);
                keys += cv.length;
                float min = float.MaxValue, max = float.MinValue;
                foreach (Keyframe k in cv.keys) { if (float.IsNaN(k.value) || float.IsInfinity(k.value)) nan++; min = Mathf.Min(min, k.value); max = Mathf.Max(max, k.value); }
                if (max - min > 1e-4f) animated++;
            }
            bool shouldLoop = name == "Idle" || name == "Move";
            Ok("8.7", c.length > 0.2f && bind.Length > 0 && animated > 0 && nan == 0, $"{name}: {F(c.length)} s, {bind.Length} curvas, {animated} variam, {keys} chaves, NaN {nan}, frameRate {c.frameRate}, loop={c.isLooping}");
            if (shouldLoop) Ok("8.8", c.isLooping, name + ": clipe em loop");
            if (c.events.Length > 0) Info("8.9", name + ": eventos " + string.Join(", ", c.events.Select(e => e.functionName + "@" + F(e.time) + "s")));
            // quais ossos se mexem
            List<string> movers = new List<string>();
            foreach (var g in bind.GroupBy(b => b.path))
            {
                bool var = false;
                foreach (EditorCurveBinding bd in g)
                {
                    AnimationCurve cv = AnimationUtility.GetEditorCurve(c, bd);
                    float mn = cv.keys.Min(k => k.value), mx = cv.keys.Max(k => k.value);
                    if (mx - mn > 1e-4f) var = true;
                }
                if (var) movers.Add(g.Key.Split('/').Last());
            }
            Info("8.7", name + ": ossos animados -> " + string.Join(",", movers.Distinct()));
        }
        AnimationClip fire = ClipOf("Fire");
        Ok("8.10", fire.events.Any(e => e.functionName == "CrossbowRelease"), "Fire tem evento CrossbowRelease");
        GameObject w = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        ProjectileAttackController atk = w.GetComponent<ProjectileAttackController>();
        AnimationEvent rel = fire.events.FirstOrDefault(e => e.functionName == "CrossbowRelease");
        if (rel != null) Ok("8.11", Mathf.Abs(rel.time - atk.releaseDelay) < 0.06f, $"releaseDelay do controlador ({F(atk.releaseDelay)} s) coincide com o evento de soltura ({F(rel.time)} s)");
        // evento chama funcao existente no Animator GameObject?
        GameObject visual = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CrossbowVisual.prefab");
        Ok("8.12", visual.GetComponentsInChildren<CrossbowFireEffects>(true).Length == 1 && visual.GetComponentInChildren<CrossbowFireEffects>(true).gameObject == visual.GetComponentInChildren<Animator>(true).gameObject, "CrossbowFireEffects no mesmo GameObject do Animator (recebe o evento)");

        // amostragem de pose: deformacao real
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(w);
        Animator anim = inst.GetComponentInChildren<Animator>();
        GameObject ag = anim.gameObject;
        Transform tipL = FindDeep(ag.transform, "LimbL_Tip"), tipR = FindDeep(ag.transform, "LimbR_Tip"), str = FindDeep(ag.transform, "String"), bolt = FindDeep(ag.transform, "Bolt"), winch = FindDeep(ag.transform, "Winch"), whl = FindDeep(ag.transform, "Wheel_L"), root = FindDeep(ag.transform, "Root");
        Renderer boltRend = inst.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name == "Bolt_LOD0");
        Func<string> boltInfo = () => boltRend != null ? $"bolt.bounds.center={V(boltRend.bounds.center)} size={V(boltRend.bounds.size)} scale={V(bolt.lossyScale)}" : "(sem renderer Bolt_LOD0)";
        ClipOf("Idle").SampleAnimation(ag, 0f);
        Vector3 idleTip = tipL.position, idleStr = str.position, idleBolt = boltRend != null ? boltRend.bounds.center : Vector3.zero;
        Vector3 idleBoltScale = bolt.lossyScale;
        Info("8.13", "Idle t=0: " + boltInfo() + " tipL=" + V(tipL.position) + " string=" + V(str.position));
        foreach (float t in new[] { 0f, 0.1f, 0.2f, 0.25f, 0.3f, 0.5f, 0.8f })
        {
            fire.SampleAnimation(ag, Mathf.Min(t, fire.length));
            Info("8.13", $"Fire t={F(t)}: tipL={V(tipL.position)} string={V(str.position)} boltScale={V(bolt.lossyScale)} {boltInfo()}");
        }
        fire.SampleAnimation(ag, 0.05f); Vector3 sBefore = str.position; Vector3 tBefore = tipL.position;
        fire.SampleAnimation(ag, 0.3f); Vector3 sAfter = str.position; Vector3 tAfter = tipL.position;
        Ok("8.14", Vector3.Distance(sBefore, sAfter) > 0.05f && Vector3.Distance(tBefore, tAfter) > 0.02f, $"Fire: corda move {F(Vector3.Distance(sBefore, sAfter))} m, ponta do braco {F(Vector3.Distance(tBefore, tAfter))} m entre 0,05 s e 0,3 s");
        Renderer br = boltRend;
        fire.SampleAnimation(ag, 0.05f);
        bool boltVisibleBefore = br != null && IsBoltVisible(bolt, br);
        fire.SampleAnimation(ag, 0.4f);
        bool boltVisibleAfter = br != null && IsBoltVisible(bolt, br);
        Ok("8.15", boltVisibleBefore && !boltVisibleAfter, $"Fire: virote visivel antes da soltura ({boltVisibleBefore}) e oculto apos ({!boltVisibleAfter})");
        AnimationClip reload = ClipOf("Reload");
        reload.SampleAnimation(ag, 0f); bool rl0 = IsBoltVisible(bolt, br); Vector3 rStr0 = str.position;
        reload.SampleAnimation(ag, reload.length * 0.5f); Vector3 rStrMid = str.position; bool rlMid = IsBoltVisible(bolt, br);
        reload.SampleAnimation(ag, reload.length); bool rl1 = IsBoltVisible(bolt, br); Vector3 rStr1 = str.position;
        Ok("8.16", !rl0 && rl1 && Vector3.Distance(rStr0, rStrMid) > 0.03f, $"Reload: virote some no inicio ({!rl0}) e reaparece no fim ({rl1}); corda se move {F(Vector3.Distance(rStr0, rStrMid))} m");
        // continuidade
        ClipOf("Idle").SampleAnimation(ag, 0f); Vector3 sIdle = str.position; Vector3 tIdle = tipL.position;
        Ok("8.17", Vector3.Distance(rStr1, sIdle) < 0.03f, $"fim de Reload ~ Idle (corda diferenca {F(Vector3.Distance(rStr1, sIdle))} m)");
        fire.SampleAnimation(ag, fire.length); Vector3 fEnd = str.position;
        reload.SampleAnimation(ag, 0f); Vector3 rStart = str.position;
        Ok("8.17b", Vector3.Distance(fEnd, rStart) < 0.03f, $"fim de Fire ~ inicio de Reload (corda diferenca {F(Vector3.Distance(fEnd, rStart))} m)");
        // Move: roda gira
        AnimationClip move = ClipOf("Move");
        move.SampleAnimation(ag, 0f); Quaternion q0 = whl.localRotation;
        move.SampleAnimation(ag, move.length * 0.25f); Quaternion q1 = whl.localRotation;
        Ok("8.18", Quaternion.Angle(q0, q1) > 20f, $"Move: roda gira {F(Quaternion.Angle(q0, q1))} graus em 1/4 do clipe");
        // Destroy: deslocamento do corpo
        AnimationClip dest = ClipOf("Destroy");
        dest.SampleAnimation(ag, 0f); Vector3 d0 = root.position; Quaternion dr0 = root.rotation;
        dest.SampleAnimation(ag, dest.length); Vector3 d1 = root.position; Quaternion dr1 = root.rotation;
        Bounds endB = RendBounds(inst);
        Ok("8.19", Quaternion.Angle(dr0, dr1) > 15f || Vector3.Distance(d0, d1) > 0.1f, $"Destroy: raiz muda (rotacao {F(Quaternion.Angle(dr0, dr1))} graus, deslocamento {F(Vector3.Distance(d0, d1))} m); bounds final {V(endB.size)}");
        // nenhum vertice vai para o infinito / abaixo do chao demais
        foreach (string name in ClipNames)
        {
            AnimationClip c = ClipOf(name);
            float minY = float.MaxValue; bool finite = true; float maxExtent = 0;
            for (int i = 0; i <= 12; i++)
            {
                c.SampleAnimation(ag, c.length * i / 12f);
                Bounds bb = RendBounds(inst);
                if (float.IsNaN(bb.size.x) || float.IsInfinity(bb.size.x)) finite = false;
                minY = Mathf.Min(minY, bb.min.y); maxExtent = Mathf.Max(maxExtent, bb.size.magnitude);
            }
            Ok("8.20", finite && minY > -0.8f && maxExtent < 8f, $"{name}: pose finita, ponto mais baixo y={F(minY)} m, extensao max {F(maxExtent)} m");
        }
        UnityEngine.Object.DestroyImmediate(inst);
    }

    private static bool IsBoltVisible(Transform bolt, Renderer r)
    {
        // o virote some por escala ~0 ou por tamanho de bounds quase nulo
        return r.bounds.size.magnitude > 0.2f && bolt.lossyScale.x > 0.05f;
    }

    // ------------------------------------------------------------------ 9
    private static void Section9_Projectile()
    {
        ProjectileContract pc = AssetDatabase.LoadAssetAtPath<ProjectileContract>(BoltContractPath);
        Ok("9.0", pc != null && pc.code == "crossbow_bolt", "ProjectileContract CrossbowBolt carregado");
        GameObject prefab = pc.prefab.asset as GameObject;
        Ok("9.1", prefab != null && AssetDatabase.GetAssetPath(prefab) == Root + "/Prefabs/CrossbowBolt.prefab", "prefab do virote e real e e o esperado");
        Ok("9.2", pc.damageType == DamageType.Piercing && !pc.ballistic && pc.speed > 10f && pc.maxLifetime >= 1f, $"contrato: dano {pc.damageType}, reto={!pc.ballistic}, velocidade {pc.speed} m/s, vida {pc.maxLifetime} s");
        Renderer[] rs = prefab.GetComponentsInChildren<Renderer>(true);
        Ok("9.3", rs.Length > 0 && rs.All(r => MeshOf(r) != null && Tris(MeshOf(r)) > 4), $"virote tem malha real ({string.Join(",", rs.Select(r => r.name + ":" + Tris(MeshOf(r)) + "tris"))})");
        Bounds b = default; bool any = false;
        foreach (Renderer r in rs) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
        Info("9.4", "bounds do virote (prefab em origem): " + V(b.min) + ".." + V(b.max));
        Ok("9.4", b.size.z > 0.4f && b.size.z < 1.0f && b.size.x < 0.4f, $"virote 0,4-1 m de comprimento ao longo de +Z (tamanho {V(b.size)})");
        Ok("9.5", prefab.GetComponentsInChildren<Collider>(true).Length == 0 && prefab.GetComponentInChildren<Rigidbody>(true) == null, "virote sem collider/rigidbody (a trajetoria e do AdvancedProjectile)");
        GameObject impact = pc.impactEffect.asset as GameObject;
        Ok("9.6", impact != null && impact.GetComponentsInChildren<ParticleSystem>(true).Length > 0, "efeito de impacto real com particulas (" + (impact != null ? impact.GetComponentsInChildren<ParticleSystem>(true).Length : 0) + " sistemas)");
        foreach (ParticleSystem ps in impact.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            Info("9.6", $"impacto/{ps.name}: loop={main.loop} duracao={F(main.duration)} vida={F(main.startLifetime.constantMax)} max={main.maxParticles}");
        }
        GameObject muzzle = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CrossbowMuzzle.prefab");
        Ok("9.7", muzzle != null && muzzle.GetComponentsInChildren<ParticleSystem>(true).Length > 0, "efeito de disparo real");
        Ok("9.8", AdvancedUnitCodes.All.Contains(Code), "codigo 'crossbow' registrado em AdvancedUnitCodes");
    }

    // ------------------------------------------------------------------ 10
    private static void Section10_Contract()
    {
        CrossbowContract c = AssetDatabase.LoadAssetAtPath<CrossbowContract>(ContractPath);
        ValidationReport v = c.Validate();
        Ok("10.1", v.IsValid, "contrato Validate().IsValid" + (v.IsValid ? "" : ": " + v));
        VisualReadinessReport vr = c.EvaluateVisuals();
        Ok("10.2", c.IsVisualReady && vr.IsReady, "contrato IsVisualReady" + (vr.IsReady ? "" : ": " + vr));
        Ok("10.3", c.visual.prefab.asset == AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath) && c.visual.prefab.expectedAssetPath == WrapperPath, "contrato.visual.prefab = wrapper Resources");
        foreach (VisualAssetReference r in c.visual.meshes.Concat(c.visual.effects))
            Ok("10.4", r.asset != null && AssetDatabase.GetAssetPath(r.asset) == r.expectedAssetPath || (r.asset != null && AssetDatabase.GetAssetPath(r.asset).StartsWith(Root)), $"referencia '{r.role}' real em {(r.asset != null ? AssetDatabase.GetAssetPath(r.asset) : "NULA")}");
        foreach (AnimationRequirement ar in c.visual.animations)
        {
            string p = ar.clip != null ? AssetDatabase.GetAssetPath(ar.clip) : "";
            Ok("10.5", ar.clip != null && p == $"{ModelsDir}/Crossbow@{ar.state}.fbx" && ar.clip.length > 0, $"animacao '{ar.state}' -> {p} ({(ar.clip != null ? F(ar.clip.length) + " s" : "nula")})");
        }
        Ok("10.6", c.Projectile != null && c.Projectile == AssetDatabase.LoadAssetAtPath<ProjectileContract>(BoltContractPath), "contrato aponta para ProjectileContract CrossbowBolt");
        Ok("10.7", !c.buildSettingsApproved && !c.CanBeAddedToBuildSettings, "buildSettingsApproved = false (nao aprovada para Build Settings)");

        // catalogo
        CharacterData cd = AssetDatabase.LoadAssetAtPath<CharacterData>(CatalogPath);
        Ok("10.8", cd != null && cd.code == Code, "CharacterData do catalogo carregado");
        GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        Ok("10.9", cd.prefab == wrapper, "CharacterData.prefab = mesmo wrapper do contrato");
        CrossbowEraTier a = c.tiers.First(t => t.era == ImperialEra.Ancient);
        Ok("10.10", cd.healthpoints == a.healthpoints && cd.attackDamage == a.attackDamage && Mathf.Approximately(cd.attackRange, a.attackRange) && Mathf.Approximately(cd.attackRate, a.attackCooldown) && Mathf.Approximately(cd.fieldOfView, c.fieldOfView),
            $"CharacterData = era Ancient (PV {cd.healthpoints}, dano {cd.attackDamage}, alcance {cd.attackRange}, ataque {cd.attackRate}, FOV {cd.fieldOfView})");
        Ok("10.11", cd.cost != null && cd.cost.Count == c.cost.Length, "custo do CharacterData tem as mesmas entradas do contrato (" + string.Join(",", cd.cost.Select(x => x.code + ":" + x.amount)) + " vs " + string.Join(",", c.cost.Select(x => x.resource + ":" + x.amount)) + ")");
        string[] guids = AssetDatabase.FindAssets("t:CharacterData");
        List<string> same = new List<string>();
        foreach (string g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            CharacterData x = AssetDatabase.LoadAssetAtPath<CharacterData>(p);
            if (x != null && x.code == Code) same.Add(p);
        }
        Ok("10.12", same.Count == 1, "exatamente um CharacterData com codigo 'crossbow' no projeto: " + string.Join(", ", same));
        // todos os CharacterData em Resources/ScriptableObjects/Units/Characters: codigos unicos
        CharacterData[] all = Resources.LoadAll<CharacterData>("ScriptableObjects/Units/Characters");
        Ok("10.13", all.Select(x => x.code).Distinct().Count() == all.Length, "codigos unicos no catalogo de unidades (" + string.Join(",", all.Select(x => x.code)) + ")");
        Ok("10.14", AssetDatabase.FindAssets("t:BuildingData").Select(g => AssetDatabase.LoadAssetAtPath<BuildingData>(AssetDatabase.GUIDToAssetPath(g))).All(x => x == null || x.code != Code), "nenhum BuildingData duplica o codigo crossbow");
        // Build Settings
        string cbGuid = AssetDatabase.AssetPathToGUID(WrapperPath);
        Ok("10.15", EditorBuildSettings.scenes.All(s => !s.path.Contains("rossbow")), "nenhuma cena de balestra no Build Settings");
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (File.Exists(s.path) && File.ReadAllText(s.path).Contains(cbGuid)) Ok("10.16", false, "cena " + s.path + " referencia o wrapper da balestra");
    }

    // ------------------------------------------------------------------ 11 (dados)
    private static void Section11_Eras()
    {
        CrossbowContract c = AssetDatabase.LoadAssetAtPath<CrossbowContract>(ContractPath);
        CrossbowEraTier[] t = c.tiers;
        Ok("11.1", t.Length == 4 && t[0].era == ImperialEra.Ancient && t[1].era == ImperialEra.Classical && t[2].era == ImperialEra.Medieval && t[3].era == ImperialEra.Imperial, "quatro eras na ordem Ancient, Classical, Medieval, Imperial");
        for (int i = 1; i < t.Length; i++)
        {
            Ok("11.2", t[i].healthpoints > t[i - 1].healthpoints && t[i].attackDamage > t[i - 1].attackDamage && t[i].attackRange > t[i - 1].attackRange && t[i].armorPenetration > t[i - 1].armorPenetration && t[i].attackCooldown < t[i - 1].attackCooldown,
                $"{t[i - 1].era} -> {t[i].era}: vida {t[i - 1].healthpoints}->{t[i].healthpoints}, dano {t[i - 1].attackDamage}->{t[i].attackDamage}, alcance {F(t[i - 1].attackRange)}->{F(t[i].attackRange)}, cooldown {F(t[i - 1].attackCooldown)}->{F(t[i].attackCooldown)}, penetracao {F(t[i - 1].armorPenetration)}->{F(t[i].armorPenetration)} (tudo melhora)");
        }
        Ok("11.3", Mathf.Approximately(t[0].attackRange, 14f), "alcance Ancient = 14 m");
        foreach (CrossbowEraTier tier in t)
        {
            Ok("11.4", CrossbowAttackProfile.CanFire(tier, tier.attackRange, tier.attackCooldown) && !CrossbowAttackProfile.CanFire(tier, tier.attackRange + 0.01f, tier.attackCooldown) && !CrossbowAttackProfile.CanFire(tier, 5f, tier.attackCooldown - 0.01f) && CrossbowAttackProfile.CanFire(tier, 0f, tier.attackCooldown),
                $"{tier.era}: CanFire respeita alcance maximo ({F(tier.attackRange)}) e cooldown ({F(tier.attackCooldown)})");
            int d0 = CrossbowAttackProfile.ComputeDamage(tier, 0f), d10 = CrossbowAttackProfile.ComputeDamage(tier, 10f);
            Info("11.5", $"{tier.era}: dano contra armadura 0 = {d0}, armadura 10 = {d10} (penetracao {F(tier.armorPenetration)})");
        }
        // perfurante: ignora parte da armadura (compara com um golpe sem penetracao)
        CrossbowEraTier none = t[0]; none.armorPenetration = 0f;
        Ok("11.6", CrossbowAttackProfile.ComputeDamage(t[3], 20f) > CrossbowAttackProfile.ComputeDamage(none, 20f) - 0 && CrossbowAttackProfile.ComputeDamage(t[0], 20f) > CrossbowAttackProfile.ComputeDamage(none, 20f), "penetracao reduz a armadura efetiva (dano perfurante maior contra armadura 20 do que sem penetracao)");
        Ok("11.7", c.Projectile.damageType == DamageType.Piercing, "projetil causa dano Piercing");
    }

    // ------------------------------------------------------------------ 14
    private static void Section14_References()
    {
        List<string> yaml = new List<string>();
        yaml.AddRange(Directory.GetFiles(Root, "*.*", SearchOption.AllDirectories).Where(p => p.EndsWith(".prefab") || p.EndsWith(".mat") || p.EndsWith(".controller") || p.EndsWith(".mask") || p.EndsWith(".asset") || p.EndsWith(".anim")));
        yaml.Add(WrapperPath); yaml.Add(CatalogPath); yaml.Add(ContractPath); yaml.Add(BoltContractPath);
        int total = 0, broken = 0;
        foreach (string f in yaml)
        {
            foreach (string guid in GuidsIn(f).Distinct())
            {
                if (guid.StartsWith("0000000000000000")) continue; // embutidos
                total++;
                if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid))) { broken++; Info("14.1", "GUID ausente " + guid + " em " + f); }
            }
        }
        Ok("14.1", broken == 0, $"referencias por GUID resolvidas: {total} verificadas em {yaml.Count} assets, {broken} quebradas");
        // arquivos .meta presentes
        int noMeta = Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Count(p => !p.EndsWith(".meta") && !File.Exists(p + ".meta"));
        Ok("14.2", noMeta == 0, "todo arquivo da unidade tem .meta (" + noMeta + " sem)");
        // tipos duplicados: classes C# definidas na pasta da unidade
        List<string> defined = new List<string>();
        foreach (string cs in Directory.GetFiles(Root, "*.cs", SearchOption.AllDirectories))
            foreach (Match m in Regex.Matches(File.ReadAllText(cs), @"(?:class|struct|enum|interface)\s+([A-Za-z_][A-Za-z0-9_]*)"))
                defined.Add(m.Groups[1].Value);
        string[] allCs = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories).Where(p => !p.Replace('\\', '/').StartsWith(Root)).ToArray();
        int dup = 0;
        foreach (string type in defined.Distinct())
            foreach (string other in allCs)
                if (Regex.IsMatch(File.ReadAllText(other), @"(?:class|struct|enum|interface)\s+" + type + @"\b")) { dup++; Info("14.3", "tipo duplicado " + type + " em " + other); }
        Ok("14.3", dup == 0, $"nenhum nome de tipo da unidade duplicado em outro arquivo ({defined.Distinct().Count()} tipos: {string.Join(",", defined.Distinct())})");
        // nomes de arquivo duplicados (basename igual) entre assets da unidade e o resto
        string[] unitFiles = Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta")).ToArray();
        string[] others = Directory.GetFiles("Assets", "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta") && !p.Replace('\\', '/').StartsWith(Root)).ToArray();
        HashSet<string> otherNames = new HashSet<string>(others.Select(Path.GetFileName));
        string[] clash = unitFiles.Select(Path.GetFileName).Where(otherNames.Contains).ToArray();
        Ok("14.4", clash.Length == 0, "nomes de arquivo da unidade nao colidem com outros assets" + (clash.Length > 0 ? ": " + string.Join(",", clash) : ""));
        // dependencias do wrapper: tudo dentro da unidade ou de assets compartilhados esperados
        string[] deps = AssetDatabase.GetDependencies(WrapperPath, true);
        string[] foreign = deps.Where(d => !d.StartsWith(Root) && !d.StartsWith("Assets/Resources/Prefabs/Units/Characters/Crossbow.prefab") && !d.EndsWith(".cs") && !d.StartsWith("Assets/Data/AdvancedUnits")).ToArray();
        Info("14.5", "dependencias do wrapper fora da unidade (nao .cs): " + (foreign.Length == 0 ? "(nenhuma)" : string.Join(", ", foreign)));
        // dependencias que apontam para outra unidade?
        string[] otherUnits = deps.Where(d => Regex.IsMatch(d, "Archer|Soldier|Spearman|Heraldic|DarkKnight|Worker|CombatMage|Paladin|Catapult") && !d.EndsWith(".cs")).ToArray();
        Ok("14.5", otherUnits.Length == 0, "wrapper nao depende de assets de outras unidades" + (otherUnits.Length > 0 ? ": " + string.Join(", ", otherUnits) : ""));
    }

    // ------------------------------------------------------------------ 13
    private static void Section13_Renders()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeLight();
        Camera cam = MakeCamera(new Color(0.46f, 0.52f, 0.58f));
        GameObject visual = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CrossbowVisual.prefab");
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(visual);
        inst.transform.position = Vector3.zero;
        // chao
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.transform.localScale = new Vector3(2, 1, 2);
        floor.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.4f, 0.55f, 0.3f) };
        // referencia de escala: cilindro 1,8 m (humano)
        GameObject human = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        human.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
        human.transform.position = new Vector3(2.0f, 0.9f, 0f);
        human.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.8f, 0.2f, 0.2f) };
        Animator anim = inst.GetComponentInChildren<Animator>();
        LODGroup group = inst.GetComponentInChildren<LODGroup>();
        ClipOf("Idle").SampleAnimation(anim.gameObject, 0f);

        float worst = 0;
        Func<string, float, string, bool> cap = (name, mag, what) => { worst = Mathf.Max(worst, mag); Ok("13", mag < 0.001f, $"render {name}: magenta {mag:P3} ({what})"); return true; };
        for (int lod = 0; lod < 3; lod++)
        {
            group.ForceLOD(lod);
            Bounds b = RendBounds(inst, r => group.GetLODs()[lod].renderers.Contains(r));
            Vector3 c = b.center; float s = Mathf.Max(b.size.x, b.size.y, b.size.z);
            cap($"LOD{lod}-front", Shoot(cam, c, Vector3.forward, s * 2.1f, $"render-LOD{lod}-front.png", 0.18f), "frente");
            cap($"LOD{lod}-back", Shoot(cam, c, Vector3.back, s * 2.1f, $"render-LOD{lod}-back.png", 0.18f), "costas");
            cap($"LOD{lod}-side", Shoot(cam, c, Vector3.right, s * 2.1f, $"render-LOD{lod}-side.png", 0.12f), "lado");
            cap($"LOD{lod}-iso", Shoot(cam, c, new Vector3(-0.8f, 0.6f, 0.9f), s * 2.1f, $"render-LOD{lod}-iso.png", 0.0f), "3/4");
            if (lod == 0) cap("LOD0-top", Shoot(cam, c, new Vector3(0.001f, 1f, 0.001f), s * 1.9f, "render-LOD0-top.png", 0.0f), "topo");
        }
        group.ForceLOD(0);
        // com referencia humana (escala)
        {
            Bounds b = RendBounds(inst);
            Vector3 c = (b.center + human.transform.position) * 0.5f;
            cap("scale-iso", Shoot(cam, c, new Vector3(-0.5f, 0.45f, 1f), 7.5f, "render-scale-iso.png", 0f), "escala vs humano 1,8 m");
        }
        human.SetActive(false);
        // poses
        AnimationClip fire = ClipOf("Fire"), reload = ClipOf("Reload"), dest = ClipOf("Destroy"), hit = ClipOf("Hit"), move = ClipOf("Move");
        Bounds ab = RendBounds(inst); Vector3 ac = ab.center;
        (string, AnimationClip, float)[] poses =
        {
            ("Idle", ClipOf("Idle"), 0f), ("Fire-0.00", fire, 0f), ("Fire-0.20", fire, 0.2f), ("Fire-0.30", fire, 0.3f), ("Fire-0.80", fire, 0.8f),
            ("Reload-0.35", reload, 0.35f), ("Reload-0.70", reload, 0.7f), ("Reload-1.40", reload, 1.399f),
            ("Hit-0.15", hit, 0.15f), ("Move-1.0", move, 1.0f),
            ("Destroy-0.50", dest, 0.5f), ("Destroy-1.00", dest, 1.0f), ("Destroy-1.50", dest, 1.5f), ("Destroy-2.00", dest, 1.999f)
        };
        foreach (var (name, clip, t) in poses)
        {
            clip.SampleAnimation(anim.gameObject, t);
            cap("pose-" + name + "-iso", Shoot(cam, ac, new Vector3(-0.8f, 0.6f, 0.9f), 6.5f, $"render-pose-{name}-iso.png", 0.0f, 800, 600), "pose");
            cap("pose-" + name + "-side", Shoot(cam, ac, Vector3.right, 6.5f, $"render-pose-{name}-side.png", 0.1f, 800, 600), "pose");
        }
        // projetil e efeitos
        ClipOf("Idle").SampleAnimation(anim.gameObject, 0f);
        GameObject bolt = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CrossbowBolt.prefab"));
        bolt.transform.position = new Vector3(0f, 1f, 3f); bolt.transform.rotation = Quaternion.identity;
        Bounds bb = RendBounds(bolt);
        cap("bolt-close", Shoot(cam, bb.center, new Vector3(-0.8f, 0.4f, 0.6f), 1.4f, "render-bolt-close.png", 0f, 700, 500), "virote");
        cap("bolt-side", Shoot(cam, bb.center, Vector3.right, 1.4f, "render-bolt-side.png", 0f, 700, 500), "virote lado");
        UnityEngine.Object.DestroyImmediate(bolt);
        UnityEngine.Object.DestroyImmediate(cam.gameObject);
        Line("render: pior fracao magenta = " + worst.ToString("P4"));
    }
}
