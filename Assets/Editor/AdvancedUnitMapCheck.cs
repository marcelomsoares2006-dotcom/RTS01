using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Paladin;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Verificacao de integracao das unidades avancadas no jogo real, em Play Mode, no Map1 e no Map2:
/// nova partida pelo menu, spawn pelo catalogo (Globals.CHARACTER_DATA), movimento no NavMesh, aplicacao das quatro eras,
/// combate/cura reais contra unidades inimigas, capturas de frente/costas/lado e contagem de pixels magenta.
/// So testa unidades presentes no catalogo (registradas somente quando o contrato esta valido e o visual e real).
/// Batch: -executeMethod AdvancedUnitMapCheck.Run   Resultado: TestResults/advanced-map-check.txt (+ PNGs em TestResults/advanced-map).
/// </summary>
[InitializeOnLoad]
public static class AdvancedUnitMapCheck
{
    private const string Key = "EraImperial.AdvancedMap.";
    private const string ReportPath = "TestResults/advanced-map-check.txt";
    private const string ImagesDir = "TestResults/advanced-map";

    private static readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>();
    private static double _wakeAt;
    private static bool _started;
    private static Vector3 _spawnBase;
    private static int _spawnIndex;

    static AdvancedUnitMapCheck()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    public static void Run()
    {
        Directory.CreateDirectory("TestResults");
        Directory.CreateDirectory(ImagesDir);
        File.WriteAllText(ReportPath, "Unity " + Application.unityVersion + "\n");
        SessionState.SetBool(Key + "Running", true);
        SessionState.SetInt(Key + "Errors", 0);
        SessionState.SetInt(Key + "Checks", 0);
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 900f);
        SessionState.SetString(Key + "DataPath", Path.GetFullPath("TestResults/AdvancedMapData-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void IsolateSaves()
    {
        if (SessionState.GetBool(Key + "Running", false))
            BinarySerializable.DATA_DIRECTORY = SessionState.GetString(Key + "DataPath", "TestResults/AdvancedMapData");
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Key + "Running", false)) return;
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        SessionState.SetInt(Key + "Errors", SessionState.GetInt(Key + "Errors", 0) + 1);
        File.AppendAllText(ReportPath, "ERROR: " + message + "\n" + stack + "\n");
    }

    private static void Record(string message)
    {
        File.AppendAllText(ReportPath, message + "\n");
        Debug.Log("[AdvancedMapCheck] " + message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        SessionState.SetInt(Key + "Checks", SessionState.GetInt(Key + "Checks", 0) + 1);
    }

    // ------------------------------------------------------------------ runner

    private static void Tick()
    {
        if (!SessionState.GetBool(Key + "Running", false)) return;
        try
        {
            if (SessionState.GetInt(Key + "Errors", 0) > 0) throw new InvalidOperationException("Erros de runtime foram registrados; abortando.");
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "Deadline", 0)) throw new TimeoutException("Excedeu 15 minutos.");
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
        catch (Exception exception)
        {
            Record("FAILED: " + exception);
            SessionState.SetBool(Key + "Running", false);
            EditorApplication.Exit(1);
        }
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
        Record($"ADVANCED_MAP_CHECK_PASSED: {SessionState.GetInt(Key + "Checks", 0)} verificacoes, 0 erros de runtime.");
        SessionState.SetBool(Key + "Running", false);
        EditorApplication.Exit(0);
    }

    private static IEnumerator WaitUntil(Func<bool> condition, double timeout, string what)
    {
        double end = EditorApplication.timeSinceStartup + timeout;
        while (!condition())
        {
            if (EditorApplication.timeSinceStartup > end) throw new TimeoutException("Tempo esgotado esperando: " + what);
            yield return 0.2;
        }
    }

    // ------------------------------------------------------------------ flow

    private static IEnumerator Flow()
    {
        for (int cycle = 0; cycle < 2; cycle++)
        {
            string mapName = cycle == 0 ? "Map1" : "Map2";
            yield return WaitUntil(() => UnityEngine.Object.FindAnyObjectByType<MainMenuManager>() is MainMenuManager m && m.newGameScrollview.childCount > 0, 60, "menu");
            MainMenuManager menu = UnityEngine.Object.FindAnyObjectByType<MainMenuManager>();
            MapData map = Resources.Load<MapData>("ScriptableObjects/Maps/" + mapName);
            Check(map != null, "metadados do mapa ausentes: " + mapName);
            Transform picker = menu.newGameScrollview.Cast<Transform>().First(item => item.Find("Data/Name").GetComponent<Text>().text == map.mapName);
            picker.GetComponent<Button>().onClick.Invoke();
            menu.StartNewGame();
            Record($"== {mapName}: nova partida");
            yield return WaitUntil(() => GameManager.instance != null && Camera.main != null && Globals.NAV_MESH_SURFACE != null, 90, "partida pronta");
            yield return 4.0;

            AdvancedEraState.Reset();
            List<string> codes = AdvancedUnitCodes.All.Where(c => Globals.CHARACTER_DATA.ContainsKey(c)).ToList();
            Record($"{mapName}: unidades avancadas no catalogo: {(codes.Count == 0 ? "(nenhuma)" : string.Join(", ", codes))}");
            Check(codes.Count > 0, "nenhuma unidade avancada registrada no catalogo");

            Transform spawnpoints = GameObject.Find("Spawnpoints").transform;
            Check(NavMesh.SamplePosition(spawnpoints.GetChild(0).position, out NavMeshHit start, 30, NavMesh.AllAreas), "sem ponto andavel");
            _spawnBase = start.position;
            _spawnIndex = 0;

            foreach (string code in codes)
                yield return TestUnit(code, mapName);

            yield return 1.0;
            Record($"== {mapName}: OK");
            EventManager.TriggerEvent("PausedGame");
            CoreBooter.instance.LoadMenu();
            yield return WaitUntil(() => !SceneManager.GetSceneByName("GameScene").isLoaded && SceneManager.GetSceneByName("MainMenu").isLoaded, 60, "volta ao menu");
            yield return 2.0;
        }
    }

    private static Vector3 NextSlot()
    {
        Vector3 wanted = _spawnBase + new Vector3((_spawnIndex % 3) * 14f, 0f, (_spawnIndex / 3) * 14f);
        _spawnIndex++;
        return NavMesh.SamplePosition(wanted, out NavMeshHit hit, 25, NavMesh.AllAreas) ? hit.position : _spawnBase;
    }

    private static Character Spawn(string code, int owner, Vector3 position)
    {
        Character c = new Character(Globals.CHARACTER_DATA[code], owner);
        c.ComputeProduction();
        NavMeshAgent agent = c.Transform.GetComponent<NavMeshAgent>();
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 6f, NavMesh.AllAreas)) position = hit.position;
        Check(agent.Warp(position), $"{code}: Warp falhou");
        return c;
    }

    private static int EnemyOwner()
    {
        int mine = GameManager.instance.gamePlayersParameters.myPlayerId;
        return GameManager.instance.gamePlayersParameters.players.Length > 1 ? (mine == 0 ? 1 : 0) : mine + 1;
    }

    private static IEnumerator TestUnit(string code, string mapName)
    {
        int mine = GameManager.instance.gamePlayersParameters.myPlayerId;
        int enemyOwner = EnemyOwner();
        Vector3 slot = NextSlot();
        Character unit = Spawn(code, mine, slot);
        GameObject go = unit.Transform.gameObject;
        yield return 1.5;
        string tag = $"{mapName}/{code}";

        // --- componentes e vinculo com o contrato
        Check(go.GetComponent("CharacterManager") != null, tag + ": CharacterManager ausente");
        Check(go.GetComponent<NavMeshAgent>() != null, tag + ": NavMeshAgent ausente");
        Animator animator = go.GetComponentInChildren<Animator>();
        Check(animator != null && animator.runtimeAnimatorController != null, tag + ": Animator/controller ausente");
        AdvancedUnitEraBinding binding = go.GetComponent<AdvancedUnitEraBinding>();
        Check(binding != null && binding.contract != null && binding.contract.code == code, tag + ": AdvancedUnitEraBinding/contrato invalido");
        Check(binding.HasApplied, tag + ": era nao aplicada");

        // --- as quatro eras: vida maxima, dano e alcance seguem o contrato
        foreach (ImperialEra era in ImperialEras.Ordered)
        {
            AdvancedEraState.Set(era);
            yield return 0.2;
            Check(binding.contract.TryGetTier(era, out IEraTier tier), tag + ": tier ausente " + era);
            Check(unit.MaxHP == tier.HealthPoints, $"{tag}: {era} vida maxima {unit.MaxHP} != {tier.HealthPoints}");
            Check(unit.AttackDamage >= tier.AttackDamage, $"{tag}: {era} dano {unit.AttackDamage} < {tier.AttackDamage}");
            Check(Mathf.Approximately(unit.AttackRange, tier.AttackRange), $"{tag}: {era} alcance {unit.AttackRange} != {tier.AttackRange}");
        }
        Record($"{tag}: progressao das 4 eras aplicada (vida/dano/alcance)");
        AdvancedEraState.Set(ImperialEra.Ancient);
        yield return 0.2;

        // --- movimento no NavMesh + animator
        CharacterManager manager = go.GetComponent<CharacterManager>();
        Vector3 before = go.transform.position;
        Vector3 goal = before + go.transform.forward * 7f;
        if (NavMesh.SamplePosition(goal, out NavMeshHit goalHit, 8f, NavMesh.AllAreas)) goal = goalHit.position;
        Check(manager.MoveTo(goal, false), tag + ": ordem de movimento recusada");
        yield return 4.0;
        Check(Vector3.Distance(before, go.transform.position) > 1.5f, tag + ": nao se moveu");
        yield return 2.0;

        // --- comportamento proprio de cada unidade
        if (code == AdvancedUnitCodes.PaladinHealer) yield return TestPaladin(unit, tag, mine, enemyOwner);
        else yield return TestAttacker(code, unit, tag, enemyOwner, mine);

        // --- imagens
        yield return Capture(go, $"{mapName}-{code}");
        UnityEngine.Object.Destroy(go);
        yield return 0.5;
    }

    private static IEnumerator TestAttacker(string code, Character unit, string tag, int enemyOwner, int mine)
    {
        GameObject go = unit.Transform.gameObject;
        binding_CurrentTier(go, out IEraTier tier);
        bool siege = code == AdvancedUnitCodes.Catapult;
        float distance = siege
            ? (((EraImperial.Units.Siege.CatapultEraTier)tier).minimumRange + tier.AttackRange) * 0.5f
            : tier.AttackRange * 0.6f;
        Vector3 at = go.transform.position + go.transform.forward * distance;
        Character enemy = Spawn("worker", enemyOwner, at);
        Character neighbor = null, ally = null;
        if (siege)
        {
            neighbor = Spawn("worker", enemyOwner, enemy.Transform.position + Vector3.right * 1.4f);
            ally = Spawn("worker", mine, enemy.Transform.position - Vector3.right * 1.4f);
        }
        yield return 0.5;
        // distancia real depois do Warp (o NavMesh pode ter deslocado o inimigo)
        float real = AdvancedCombat.FlatDistance(go.transform.position, enemy.Transform.position);
        Record($"{tag}: alvo a {real:F1} m (alcance max {tier.AttackRange:F1})");

        int enemyBefore = enemy.HP, neighborBefore = neighbor != null ? neighbor.HP : 0, allyBefore = ally != null ? ally.HP : 0;
        UnitManager attackerManager = go.GetComponent<UnitManager>();
        AdvancedAttackController controller = go.GetComponent<AdvancedAttackController>();
        Check(controller != null, tag + ": controlador de ataque ausente");
        double end = EditorApplication.timeSinceStartup + 14;
        while (EditorApplication.timeSinceStartup < end && enemy.Transform != null && enemy.HP >= enemyBefore)
        {
            attackerManager.Attack(enemy.Transform);
            yield return 0.4;
        }
        yield return 1.5;
        bool damaged = enemy.Transform == null || enemy.HP < enemyBefore;
        Check(damaged, tag + ": o alvo nao sofreu dano");
        Check(controller.ShotsFired >= 1, tag + ": nenhum disparo registrado");
        Record($"{tag}: dano aplicado ao alvo (HP {enemyBefore} -> {(enemy.Transform == null ? 0 : enemy.HP)}), disparos={controller.ShotsFired}");

        // recarga: tentativas seguidas nao podem gerar disparos extras
        int shots = controller.ShotsFired;
        for (int i = 0; i < 5; i++) { if (enemy.Transform != null) attackerManager.Attack(enemy.Transform); yield return 0.05; }
        Check(controller.ShotsFired <= shots + 1, tag + ": ignorou a recarga");

        if (siege)
        {
            Check(ally == null || ally.Transform == null || ally.HP == allyBefore, tag + ": dano em aliado (fogo amigo)");
            bool radial = neighbor == null || neighbor.Transform == null || neighbor.HP < neighborBefore;
            Check(radial, tag + ": sem dano radial no vizinho inimigo");
            Record($"{tag}: dano radial no vizinho inimigo; aliado intacto");
        }
        foreach (Character c in new[] { enemy, neighbor, ally })
            if (c != null && c.Transform != null) UnityEngine.Object.Destroy(c.Transform.gameObject);
    }

    private static void binding_CurrentTier(GameObject go, out IEraTier tier)
    {
        tier = go.GetComponent<AdvancedUnitEraBinding>().CurrentTier;
    }

    private static IEnumerator TestPaladin(Character unit, string tag, int mine, int enemyOwner)
    {
        GameObject go = unit.Transform.gameObject;
        binding_CurrentTier(go, out IEraTier tier);
        PaladinEraTier t = (PaladinEraTier)tier;
        PaladinHealBehaviour heal = go.GetComponent<PaladinHealBehaviour>();
        Check(heal != null, tag + ": PaladinHealBehaviour ausente");

        Vector3 p = go.transform.position;
        Character near = Spawn("worker", mine, p + go.transform.right * 2f);
        Character far = Spawn("worker", mine, p + go.transform.right * (t.healRadius + 6f));
        Character foe = Spawn("worker", enemyOwner, p - go.transform.right * 2f);
        yield return 0.5;
        near.HP = Mathf.Max(1, near.MaxHP / 2); far.HP = Mathf.Max(1, far.MaxHP / 2); foe.HP = Mathf.Max(1, foe.MaxHP / 2);
        int nearBefore = near.HP, farBefore = far.HP, foeBefore = foe.HP;

        yield return t.healCooldown + 1.5;
        Check(near.HP > nearBefore, tag + ": aliado ferido dentro do raio nao foi curado");
        Check(far.HP == farBefore, tag + ": aliado fora do raio foi curado");
        Check(foe.HP == foeBefore, tag + ": inimigo foi curado");
        Check(heal.PulsesPerformed >= 1, tag + ": nenhum pulso registrado");
        Record($"{tag}: curou aliado no raio ({nearBefore}->{near.HP}), poupou aliado distante e inimigo; pulsos={heal.PulsesPerformed}");

        // cooldown: em ~ (3 x cooldown) nao pode haver mais de 4 pulsos
        int pulses = heal.PulsesPerformed;
        near.HP = 1;
        yield return t.healCooldown * 3f;
        Check(heal.PulsesPerformed - pulses <= 4, tag + ": pulsos acima do cooldown");

        // ataque corpo a corpo comum do jogo
        Vector3 foePos = foe.Transform.position;
        go.GetComponent<NavMeshAgent>().Warp(foePos + go.transform.forward * 0.1f);
        int foeHp = foe.HP;
        go.GetComponent<UnitManager>().Attack(foe.Transform);
        yield return 0.5;
        Check(foe.Transform == null || foe.HP < foeHp, tag + ": ataque corpo a corpo nao causou dano");
        Record($"{tag}: ataque corpo a corpo causou dano");
        foreach (Character c in new[] { near, far, foe })
            if (c != null && c.Transform != null) UnityEngine.Object.Destroy(c.Transform.gameObject);
    }

    // ------------------------------------------------------------------ imagens

    private static IEnumerator Capture(GameObject unit, string prefix)
    {
        foreach (LODGroup lod in unit.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
        Bounds bounds = default;
        bool any = false;
        foreach (Renderer r in unit.GetComponentsInChildren<Renderer>())
        {
            if (r.name == "TeamRing" || r.name == "MinimapIcon" || r.gameObject.layer != unit.layer && r.name.Contains("FOV")) continue;
            if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
        }
        Check(any, prefix + ": sem renderers visiveis");
        float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        GameObject camGo = new GameObject("AdvancedMapCaptureCamera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 30f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.45f, 0.5f, 0.55f);
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f;
        cam.cullingMask = ~0;
        float distance = size * 1.9f;
        Vector3 center = bounds.center;
        (string name, Vector3 dir)[] views =
        {
            ("front", unit.transform.forward), ("back", -unit.transform.forward), ("side", unit.transform.right)
        };
        foreach (var view in views)
        {
            camGo.transform.position = center + view.dir * distance + Vector3.up * size * 0.15f;
            camGo.transform.LookAt(center);
            string path = $"{ImagesDir}/{prefix}-{view.name}.png";
            CaptureCamera(cam, path);
            yield return null;
        }
        UnityEngine.Object.Destroy(camGo);
    }

    private static void CaptureCamera(Camera camera, string path)
    {
        RenderTexture target = new RenderTexture(1024, 768, 24);
        Texture2D texture = new Texture2D(1024, 768, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            int magenta = texture.GetPixels32().Count(pixel => pixel.r > 220 && pixel.g < 40 && pixel.b > 220);
            float fraction = (float)magenta / (1024 * 768);
            Record($"{path}: pixels magenta={fraction:P3}");
            Check(fraction < 0.001f, "pixels magenta acima de 0,1% em " + path);
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
