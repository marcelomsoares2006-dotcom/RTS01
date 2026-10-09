using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BehaviorTree;
using EraImperial.Units.Advanced;
using EraImperial.Units.Crossbow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// AUDITORIA INDEPENDENTE DA BALESTRA em Play Mode, no Map1 e no Map2 reais (Core -> menu -> nova partida).
/// Escrita pelo auditor, no padrao de Assets/Editor/AdvancedUnitMapCheck.cs (sem edita-lo).
/// Batch: -executeMethod CrossbowIndependentPlay.Run   Resultado: Docs/AdvancedUnits/audit/crossbow/play-checks.txt (+ PNGs).
/// Falhas sao acumuladas (nao abortam); excecoes do fluxo abortam. Saida: 0 = tudo PASS, 2 = ha FAIL, 1 = excecao/timeout.
/// </summary>
[InitializeOnLoad]
public static class CrossbowIndependentPlay
{
    private const string Key = "CBPlay.";
    private const string OutDir = "Docs/AdvancedUnits/audit/crossbow";
    private const string ReportPath = OutDir + "/play-checks.txt";
    private const string Code = "crossbow";

    private static readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>();
    private static double _wakeAt;
    private static bool _started;
    private static Vector3 _spawnBase;
    private static readonly HashSet<string> SeenWarnings = new HashSet<string>();

    static CrossbowIndependentPlay()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    public static void Run()
    {
        Directory.CreateDirectory(OutDir);
        File.WriteAllText(ReportPath, "Unity " + Application.unityVersion + " - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n");
        SessionState.SetBool(Key + "Running", true);
        SessionState.SetInt(Key + "Errors", 0);
        SessionState.SetInt(Key + "Pass", 0);
        SessionState.SetInt(Key + "Fail", 0);
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 1500f);
        SessionState.SetString(Key + "DataPath", Path.GetFullPath("TestResults/CBIndepData-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void IsolateSaves()
    {
        if (SessionState.GetBool(Key + "Running", false))
            BinarySerializable.DATA_DIRECTORY = SessionState.GetString(Key + "DataPath", "TestResults/CBIndepData");
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Key + "Running", false)) return;
        if (type == LogType.Warning)
        {
            string head = message.Length > 160 ? message.Substring(0, 160) : message;
            if (SeenWarnings.Add(head)) File.AppendAllText(ReportPath, "CONSOLE Warning: " + head.Replace("\n", " ") + "\n");
            return;
        }
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        SessionState.SetInt(Key + "Errors", SessionState.GetInt(Key + "Errors", 0) + 1);
        File.AppendAllText(ReportPath, "CONSOLE ERROR: " + message + "\n" + stack + "\n");
    }

    private static void Record(string message)
    {
        File.AppendAllText(ReportPath, message + "\n");
        Debug.Log("[CBPlay] " + message);
    }

    private static void Ok(string id, bool cond, string msg)
    {
        SessionState.SetInt(Key + (cond ? "Pass" : "Fail"), SessionState.GetInt(Key + (cond ? "Pass" : "Fail"), 0) + 1);
        Record((cond ? "PASS" : "FAIL") + " [" + id + "] " + msg);
    }

    private static void Info(string id, string msg) { Record("INFO [" + id + "] " + msg); }
    private static string F(float v) { return v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }
    private static string V(Vector3 v) { return "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")"; }

    // ------------------------------------------------------------------ runner

    private static void Tick()
    {
        if (!SessionState.GetBool(Key + "Running", false)) return;
        try
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "Deadline", 0)) throw new TimeoutException("Excedeu o tempo limite.");
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
            Record("FAILED (excecao do auditor/fluxo): " + exception);
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
        int pass = SessionState.GetInt(Key + "Pass", 0), fail = SessionState.GetInt(Key + "Fail", 0), errors = SessionState.GetInt(Key + "Errors", 0);
        Record($"RESUMO PLAY: {pass} PASS, {fail} FAIL, {errors} erros de console");
        SessionState.SetBool(Key + "Running", false);
        EditorApplication.Exit(fail == 0 && errors == 0 ? 0 : 2);
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
            Ok(mapName + ".0", map != null, "metadados do mapa " + mapName);
            Transform picker = menu.newGameScrollview.Cast<Transform>().First(item => item.Find("Data/Name").GetComponent<Text>().text == map.mapName);
            picker.GetComponent<Button>().onClick.Invoke();
            menu.StartNewGame();
            Info(mapName, "nova partida iniciada");
            yield return WaitUntil(() => GameManager.instance != null && Camera.main != null && Globals.NAV_MESH_SURFACE != null, 90, "partida pronta");
            yield return 4.0;

            AdvancedEraState.Reset();
            Ok(mapName + ".1", Globals.CHARACTER_DATA.ContainsKey(Code), "Globals.CHARACTER_DATA contem 'crossbow' em " + mapName);
            Transform spawnpoints = GameObject.Find("Spawnpoints").transform;
            Ok(mapName + ".2", NavMesh.SamplePosition(spawnpoints.GetChild(0).position, out NavMeshHit start, 30, NavMesh.AllAreas), "ponto inicial andavel");
            _spawnBase = start.position;

            yield return Scenario(mapName);

            yield return 1.0;
            Info(mapName, "cenario concluido");
            EventManager.TriggerEvent("PausedGame");
            CoreBooter.instance.LoadMenu();
            yield return WaitUntil(() => !SceneManager.GetSceneByName("GameScene").isLoaded && SceneManager.GetSceneByName("MainMenu").isLoaded, 60, "volta ao menu");
            yield return 2.0;
        }
    }

    // ------------------------------------------------------------------ helpers de cena

    private static int Mine() { return GameManager.instance.gamePlayersParameters.myPlayerId; }

    private static int EnemyOwner()
    {
        int mine = Mine();
        return GameManager.instance.gamePlayersParameters.players.Length > 1 ? (mine == 0 ? 1 : 0) : mine + 1;
    }

    private static float Flat(Vector3 a, Vector3 b) { return AdvancedCombat.FlatDistance(a, b); }

    private static Character Spawn(string code, int owner, Vector3 position)
    {
        Character c = new Character(Globals.CHARACTER_DATA[code], owner);
        c.ComputeProduction();
        NavMeshAgent agent = c.Transform.GetComponent<NavMeshAgent>();
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 6f, NavMesh.AllAreas)) position = hit.position;
        agent.Warp(position);
        return c;
    }

    private static Character Dummy(int owner, Vector3 position, int hp = 1000)
    {
        Character c = Spawn("worker", owner, position);
        c.Transform.GetComponent<CharacterBT>().enabled = false;
        c.Transform.GetComponent<NavMeshAgent>().isStopped = true;
        c.HP = hp;
        return c;
    }

    private static void Kill(params Character[] list)
    {
        foreach (Character c in list)
            if (c != null && c.Transform != null) UnityEngine.Object.Destroy(c.Transform.gameObject);
    }

    // acha um ponto andavel a `dist` m de `origin` com linha livre no NavMesh
    private static bool OpenLine(Vector3 origin, float dist, Vector3 baseDir, out Vector3 pos, float tol = 0.5f)
    {
        baseDir.y = 0; if (baseDir.sqrMagnitude < 1e-4f) baseDir = Vector3.forward; baseDir.Normalize();
        for (int k = 0; k < 19; k++)
        {
            float angle = (k % 2 == 0 ? 1 : -1) * ((k + 1) / 2) * 20f;
            Vector3 dir = Quaternion.Euler(0, angle, 0) * baseDir;
            Vector3 wanted = origin + dir * dist;
            if (NavMesh.SamplePosition(wanted, out NavMeshHit hit, 1.0f, NavMesh.AllAreas)
                && Mathf.Abs(Flat(hit.position, origin) - dist) < tol
                && !NavMesh.Raycast(origin, hit.position, out NavMeshHit _, NavMesh.AllAreas))
            {
                pos = hit.position;
                return true;
            }
        }
        pos = origin;
        return false;
    }

    /// <summary>Posiciona o atirador (Warp) de modo que a distancia efetiva (centro a centro menos o tamanho do alvo) fique em `effective`. Devolve a distancia efetiva real.</summary>
    private static float PlaceShooter(Character shooter, Character target, float effective, bool towardsFront = true)
    {
        UnitManager tm = target.Transform.GetComponent<UnitManager>();
        float size = AdvancedCombat.TargetSize(tm);
        float wanted = effective + size;
        if (!OpenLine(target.Transform.position, wanted, shooter.Transform.position - target.Transform.position, out Vector3 pos, 0.2f))
            return float.NaN;
        shooter.Transform.GetComponent<NavMeshAgent>().Warp(pos);
        Vector3 look = target.Transform.position - shooter.Transform.position; look.y = 0;
        if (look.sqrMagnitude > 1e-4f) shooter.Transform.rotation = Quaternion.LookRotation(look);
        return Flat(shooter.Transform.position, target.Transform.position) - size;
    }

    private static string StateName(Animator a, int layer = 0)
    {
        AnimatorStateInfo info = a.GetCurrentAnimatorStateInfo(layer);
        foreach (string name in new[] { "Idle", "Move", "Fire", "Reload", "Hit", "Empty", "Destroy" })
            if (info.IsName(name)) return name;
        return "?";
    }

    // ------------------------------------------------------------------ cenario por mapa

    private static IEnumerator Scenario(string map)
    {
        int mine = Mine(), enemyOwner = EnemyOwner();
        string tag = map;

        // ------------------------------------------------ A. instancia, componentes, estatisticas Ancient
        Character cb = Spawn(Code, mine, _spawnBase);
        GameObject go = cb.Transform.gameObject;
        yield return 1.5;
        CharacterManager manager = go.GetComponent<CharacterManager>();
        ProjectileAttackController atk = go.GetComponent<ProjectileAttackController>();
        AdvancedUnitEraBinding binding = go.GetComponent<AdvancedUnitEraBinding>();
        Animator anim = atk != null ? atk.animator : null;
        Ok(tag + ".A1", manager != null && atk != null && binding != null && anim != null && go.GetComponent<NavMeshAgent>() != null && go.GetComponent<AdvancedUnitAnimatorDriver>() != null && go.GetComponent<AdvancedUnitDeathEffect>() != null && go.GetComponent<CharacterBT>() != null,
            "instancia pelo catalogo com CharacterManager, NavMeshAgent, BT, Binding, AnimatorDriver, controlador de ataque e efeito de morte");
        int missing = go.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        Ok(tag + ".A2", missing == 0, "sem scripts ausentes na instancia em jogo");
        Ok(tag + ".A3", manager.animator == anim && anim.runtimeAnimatorController != null && anim.isInitialized, "UnitManager.animator = Animator do visual (o BT escreve Running/Attacking/Hit nele) e esta inicializado");
        Ok(tag + ".A4", binding.HasApplied && cb.MaxHP == 28 && cb.HP == 28 && cb.AttackDamage == 22 && Mathf.Approximately(cb.AttackRange, 14f), $"era Ancient aplicada: PV {cb.HP}/{cb.MaxHP}, dano {cb.AttackDamage}, alcance {cb.AttackRange}");
        Ok(tag + ".A5", go.GetComponentInChildren<LODGroup>() != null && go.GetComponentsInChildren<Renderer>().Count(r => r.enabled && !(r is ParticleSystemRenderer)) > 3, "visual com LODGroup e renderers ativos");
        Ok(tag + ".A6", go.GetComponent<NavMeshAgent>().isOnNavMesh, "agente no NavMesh");

        // ------------------------------------------------ B. movimento + animator
        Vector3 before = go.transform.position;
        Vector3 goal = before; OpenLine(before, 8f, go.transform.forward, out goal);
        Transform wheel = go.GetComponentsInChildren<Transform>().First(t => t.name == "Wheel_L");
        Quaternion wheel0 = wheel.localRotation;
        float maxSpeed = 0, maxWheel = 0; bool sawMove = false;
        Ok(tag + ".B1", manager.MoveTo(goal, false), "ordem de movimento aceita");
        double end = EditorApplication.timeSinceStartup + 6;
        while (EditorApplication.timeSinceStartup < end)
        {
            maxSpeed = Mathf.Max(maxSpeed, anim.GetFloat("Speed"));
            sawMove |= StateName(anim) == "Move";
            maxWheel = Mathf.Max(maxWheel, Quaternion.Angle(wheel0, wheel.localRotation));
            yield return 0.0;
        }
        float moved = Flat(before, go.transform.position);
        Ok(tag + ".B2", moved > 4f, $"moveu {F(moved)} m pelo NavMesh");
        Ok(tag + ".B3", maxSpeed > 1f && sawMove, $"Animator: Speed max {F(maxSpeed)}, estado Move visto={sawMove}");
        Ok(tag + ".B4", maxWheel > 30f, $"rodas giram em movimento ({F(maxWheel)} graus)");
        yield return 1.5;
        Ok(tag + ".B5", StateName(anim) == "Idle" && anim.GetFloat("Speed") < 0.15f, $"parada: estado {StateName(anim)}, Speed {F(anim.GetFloat("Speed"))}");

        // ------------------------------------------------ C. quatro eras (estatisticas no jogo)
        CrossbowContract contract = (CrossbowContract)binding.contract;
        foreach (ImperialEra era in ImperialEras.Ordered)
        {
            AdvancedEraState.Set(era); yield return 0.2;
            contract.TryGetTier(era, out IEraTier tier);
            Ok(tag + ".C." + era, cb.MaxHP == tier.HealthPoints && cb.AttackDamage >= tier.AttackDamage && Mathf.Approximately(cb.AttackRange, tier.AttackRange),
                $"{era}: PV max {cb.MaxHP}/{tier.HealthPoints}, dano {cb.AttackDamage}/{tier.AttackDamage}, alcance {F(cb.AttackRange)}/{F(tier.AttackRange)}");
        }
        AdvancedEraState.Set(ImperialEra.Ancient); yield return 0.2;
        Ok(tag + ".C5", cb.MaxHP == 28 && cb.HP > 0, $"volta a Ancient: PV max {cb.MaxHP}");
        Kill(cb);
        yield return 0.5;

        // ------------------------------------------------ E. engajamento autonomo pelo behavior tree (Ancient)
        yield return BtEngage(tag, mine, enemyOwner);

        // ------------------------------------------------ F. ordem de ataque explicita fora do FOV
        yield return BtOrder(tag, mine, enemyOwner);

        // ------------------------------------------------ N. era alterada depois do spawn x BT
        yield return BtEraDynamics(tag, mine, enemyOwner);

        // ------------------------------------------------ G. alcance, cooldown, projetil, dano, aliados, por era (controlador direto)
        yield return ControllerEras(tag, mine, enemyOwner);

        // ------------------------------------------------ edificio inimigo (se existir)
        yield return BuildingTarget(tag, mine);

        // ------------------------------------------------ K. capturas, LOD, reacao, morte
        yield return VisualsAndDeath(tag, mine, enemyOwner);
    }

    // ------------------------------------------------------------------ E
    private static IEnumerator BtEngage(string tag, int mine, int enemyOwner)
    {
        AdvancedEraState.Set(ImperialEra.Ancient); yield return 0.2;
        Character cb = Spawn(Code, mine, _spawnBase);
        yield return 0.8;
        GameObject go = cb.Transform.gameObject;
        ProjectileAttackController atk = go.GetComponent<ProjectileAttackController>();
        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        Vector3 ePos;
        bool okSpot = OpenLine(go.transform.position, 18.5f, go.transform.forward, out ePos);
        if (!okSpot) { Info(tag + ".E", "sem linha livre de 18,5 m perto do ponto inicial: cenario E nao aplicavel neste mapa"); Kill(cb); yield break; }
        Character enemy = Dummy(enemyOwner, ePos);
        UnitManager em = enemy.Transform.GetComponent<UnitManager>();
        float size = AdvancedCombat.TargetSize(em);
        float eff0 = Flat(go.transform.position, enemy.Transform.position) - size;
        Info(tag + ".E", $"alvo inimigo a {F(Flat(go.transform.position, enemy.Transform.position))} m (efetivo {F(eff0)}; alcance 14; FOV 20; tamanho do alvo {F(size)})");
        Ok(tag + ".E1", eff0 > 14.5f, "alvo comeca fora do alcance de tiro (efetivo > 14,5)");

        int hp0 = enemy.HP;
        List<float> shotTimes = new List<float>();
        List<float> shotEff = new List<float>();
        List<float> shotAngle = new List<float>();
        List<int> hpAtShot = new List<int>();
        bool wasFiring = false; int lastShots = 0;
        float startTime = Time.time;
        bool shotBeforeInRange = false;
        double stop = EditorApplication.timeSinceStartup + 22;
        float firstApproach = -1;
        Vector3 startPos = go.transform.position;
        while (EditorApplication.timeSinceStartup < stop && shotTimes.Count < 4)
        {
            if (enemy.Transform == null) break;
            float eff = Flat(go.transform.position, enemy.Transform.position) - size;
            if (atk.IsFiring && !wasFiring)
            {
                shotTimes.Add(Time.time - startTime);
                shotEff.Add(eff);
                Vector3 to = enemy.Transform.position - go.transform.position; to.y = 0;
                shotAngle.Add(Vector3.Angle(go.transform.forward, to));
                if (eff > 14.0f + 0.05f) shotBeforeInRange = true;
            }
            wasFiring = atk.IsFiring;
            if (firstApproach < 0 && Flat(go.transform.position, startPos) > 1f) firstApproach = Time.time - startTime;
            yield return 0.0;
        }
        yield return 1.2;
        int hit = hp0 - enemy.HP;
        int shots = atk.ShotsFired;
        Info(tag + ".E", $"BT: comecou a andar em {F(firstApproach)} s; {shots} disparos; inicios de tiro em t = [{string.Join(", ", shotTimes.Select(F))}] s; distancia efetiva no inicio de cada tiro = [{string.Join(", ", shotEff.Select(F))}] m; angulo ate o alvo = [{string.Join(", ", shotAngle.Select(F))}] graus; HP {hp0}->{enemy.HP}");
        Ok(tag + ".E2", firstApproach > 0 && shots >= 2, $"o BT aproximou-se do alvo (FOV 20 > alcance 14) e disparou {shots} vezes sozinho");
        Ok(tag + ".E3", !shotBeforeInRange && shotEff.All(e => e <= 14.05f), "nenhum disparo iniciado alem do alcance maximo de 14 m (maior efetivo " + (shotEff.Count > 0 ? F(shotEff.Max()) : "-") + ")");
        Ok(tag + ".E4", hit == 22 * shots && hit > 0, $"cada virote causa 22 de dano (perfurante, armadura 0): {shots} disparos -> {hit} de dano");
        if (shotTimes.Count >= 2)
        {
            float minGap = float.MaxValue;
            for (int i = 1; i < shotTimes.Count; i++) minGap = Mathf.Min(minGap, shotTimes[i] - shotTimes[i - 1]);
            Ok(tag + ".E5", minGap >= 3.2f - 0.12f, $"intervalo minimo entre disparos pelo BT = {F(minGap)} s (cooldown 3,2 s)");
        }
        Ok(tag + ".E6", shotAngle.All(a => a < 25f), "a balestra esta virada para o alvo ao iniciar o disparo (angulos " + string.Join(", ", shotAngle.Select(F)) + ")");
        Ok(tag + ".E7", AdvancedProjectile.ActiveCount <= 1, "nenhum virote preso em voo (" + AdvancedProjectile.ActiveCount + ")");
        Kill(cb, enemy);
        yield return 0.8;
    }

    // ------------------------------------------------------------------ F
    private static IEnumerator BtOrder(string tag, int mine, int enemyOwner)
    {
        AdvancedEraState.Set(ImperialEra.Ancient); yield return 0.2;
        Character cb = Spawn(Code, mine, _spawnBase);
        yield return 0.8;
        GameObject go = cb.Transform.gameObject;
        ProjectileAttackController atk = go.GetComponent<ProjectileAttackController>();
        CharacterBT bt = go.GetComponent<CharacterBT>();
        Vector3 ePos = default; bool found = false;
        foreach (float d in new[] { 30f, 27f, 24f })
            if (OpenLine(go.transform.position, d, go.transform.forward, out ePos)) { found = true; break; }
        if (!found) { Info(tag + ".F", "sem linha livre de 24 m+ perto do ponto inicial: ordem fora do FOV nao aplicavel neste mapa"); Kill(cb); yield break; }
        Character enemy = Dummy(enemyOwner, ePos);
        float size = AdvancedCombat.TargetSize(enemy.Transform.GetComponent<UnitManager>());
        float dist0 = Flat(go.transform.position, enemy.Transform.position);
        Info(tag + ".F", $"alvo a {F(dist0)} m (fora do FOV 20)");
        Vector3 p0 = go.transform.position;
        yield return 2.0;
        Ok(tag + ".F1", Flat(p0, go.transform.position) < 0.5f && atk.ShotsFired == 0, "fora do FOV e sem ordem: a unidade fica parada e nao atira");
        bt.Root.SetData("currentTarget", enemy.Transform);
        bt.Root.SetData("currentTargetOffset", Vector2.zero);
        go.GetComponent<UnitManager>().SetAnimatorBoolVariable("Running", true);
        int hp0 = enemy.HP;
        double stop = EditorApplication.timeSinceStartup + 25;
        float effAtFire = -1; bool wasFiring = false;
        while (EditorApplication.timeSinceStartup < stop && atk.ShotsFired < 1)
        {
            if (atk.IsFiring && !wasFiring) effAtFire = Flat(go.transform.position, enemy.Transform.position) - size;
            wasFiring = atk.IsFiring;
            yield return 0.0;
        }
        yield return 1.5;
        Ok(tag + ".F2", atk.ShotsFired >= 1 && effAtFire > 0 && effAtFire <= 14.05f, $"ordem de ataque: andou ate o alcance e disparou (efetivo no disparo {F(effAtFire)} m)");
        Ok(tag + ".F3", enemy.HP == hp0 - 22 * atk.ShotsFired, $"dano aplicado ao alvo (HP {hp0} -> {enemy.HP}, {atk.ShotsFired} disparos)");
        Kill(cb, enemy);
        yield return 0.8;
    }

    // ------------------------------------------------------------------ N
    private static IEnumerator BtEraDynamics(string tag, int mine, int enemyOwner)
    {
        // (a) unidade criada em Ancient; a era muda DEPOIS do spawn; alvo a ~16,5 m (dentro do alcance Imperial 18,5, fora do Ancient 14)
        AdvancedEraState.Set(ImperialEra.Ancient); yield return 0.2;
        Character cb = Spawn(Code, mine, _spawnBase);
        yield return 0.8;
        GameObject go = cb.Transform.gameObject;
        ProjectileAttackController atk = go.GetComponent<ProjectileAttackController>();
        AdvancedEraState.Set(ImperialEra.Imperial); yield return 0.3;
        Ok(tag + ".N0", cb.AttackRange == 18.5f && cb.AttackDamage == 47, $"apos mudar para Imperial: alcance {cb.AttackRange}, dano {cb.AttackDamage}");
        Vector3 ePos;
        if (!OpenLine(go.transform.position, 17f, go.transform.forward, out ePos)) { Info(tag + ".N", "sem linha de 17 m: cenario N nao aplicavel"); AdvancedEraState.Set(ImperialEra.Ancient); Kill(cb); yield break; }
        Character enemy = Dummy(enemyOwner, ePos);
        float size = AdvancedCombat.TargetSize(enemy.Transform.GetComponent<UnitManager>());
        float eff0 = Flat(go.transform.position, enemy.Transform.position) - size;
        Info(tag + ".N", $"(a) unidade nascida em Ancient, era virou Imperial; alvo a efetivo {F(eff0)} m (alcance Imperial 18,5; BT leu 14 ao nascer)");
        int hp0 = enemy.HP;
        yield return 11.0;
        float effNow = enemy.Transform != null ? Flat(go.transform.position, enemy.Transform.position) - size : -1;
        bool fired = atk.ShotsFired > 0;
        Info(tag + ".N", $"(a) apos 11 s: disparos {atk.ShotsFired}, HP {hp0}->{enemy.HP}, distancia efetiva atual {F(effNow)} m");
        Ok(tag + ".N1", fired, "(a) unidade criada em Ancient com a era mudada depois: ainda engaja e dispara (comportamento do BT com alcance antigo)");
        Kill(cb, enemy);
        yield return 0.6;

        // (b) unidade criada JA em Imperial: BT deve usar o alcance Imperial e (contrato) cooldown 2,3 s
        Character cb2 = Spawn(Code, mine, _spawnBase);
        yield return 0.8;
        GameObject go2 = cb2.Transform.gameObject;
        ProjectileAttackController atk2 = go2.GetComponent<ProjectileAttackController>();
        Ok(tag + ".N2", cb2.AttackRange == 18.5f, "(b) unidade nascida em Imperial: alcance 18,5");
        Vector3 ePos2;
        if (!OpenLine(go2.transform.position, 17.5f, go2.transform.forward, out ePos2)) { Info(tag + ".N", "sem linha de 17,5 m: cenario N(b) nao aplicavel"); AdvancedEraState.Set(ImperialEra.Ancient); Kill(cb2); yield break; }
        Character enemy2 = Dummy(enemyOwner, ePos2);
        float size2 = AdvancedCombat.TargetSize(enemy2.Transform.GetComponent<UnitManager>());
        Info(tag + ".N", $"(b) alvo a efetivo {F(Flat(go2.transform.position, enemy2.Transform.position) - size2)} m");
        List<float> times = new List<float>(); bool wasFiring = false; float t0 = Time.time;
        List<float> eff = new List<float>();
        double stop = EditorApplication.timeSinceStartup + 17;
        while (EditorApplication.timeSinceStartup < stop && times.Count < 4 && enemy2.Transform != null)
        {
            if (atk2.IsFiring && !wasFiring) { times.Add(Time.time - t0); eff.Add(Flat(go2.transform.position, enemy2.Transform.position) - size2); }
            wasFiring = atk2.IsFiring;
            yield return 0.0;
        }
        yield return 1.0;
        float minGap = float.MaxValue;
        for (int i = 1; i < times.Count; i++) minGap = Mathf.Min(minGap, times[i] - times[i - 1]);
        Info(tag + ".N", $"(b) Imperial via BT: tiros em t=[{string.Join(", ", times.Select(F))}] s, efetivo no inicio [{string.Join(", ", eff.Select(F))}] m, intervalo minimo {(times.Count > 1 ? F(minGap) : "-")} s (contrato Imperial: cooldown 2,3 s; BT usa CharacterData.attackRate = 3,2)");
        Ok(tag + ".N3", times.Count >= 2, "(b) unidade Imperial dispara pelo BT");
        Ok(tag + ".N4", eff.Count > 0 && eff.Max() <= 18.55f && eff.Max() > 14.2f, $"(b) Imperial atira alem do alcance Ancient (maior efetivo {(eff.Count > 0 ? F(eff.Max()) : "-")} m, limite 18,5)");
        if (times.Count > 1)
            Ok(tag + ".N5", minGap < 3.0f, $"(b) cadencia real via BT = {F(minGap)} s; o contrato da era Imperial promete 2,3 s (CharacterData.attackRate = 3,2 alimenta o Timer do BT)");
        AdvancedEraState.Set(ImperialEra.Ancient);
        Kill(cb2, enemy2);
        yield return 0.8;
    }

    // ------------------------------------------------------------------ G
    private static IEnumerator ControllerEras(string tag, int mine, int enemyOwner)
    {
        AdvancedEraState.Set(ImperialEra.Ancient); yield return 0.2;
        Character cb = Spawn(Code, mine, _spawnBase);
        yield return 0.8;
        GameObject go = cb.Transform.gameObject;
        go.GetComponent<CharacterBT>().enabled = false;
        go.GetComponent<NavMeshAgent>().isStopped = true;
        ProjectileAttackController atk = go.GetComponent<ProjectileAttackController>();
        Animator anim = atk.animator;
        AdvancedUnitEraBinding binding = go.GetComponent<AdvancedUnitEraBinding>();
        UnitManager um = go.GetComponent<UnitManager>();
        CrossbowContract contract = (CrossbowContract)binding.contract;

        Vector3 enemyPos;
        if (!OpenLine(_spawnBase, 3f, Vector3.forward, out enemyPos)) enemyPos = _spawnBase + new Vector3(3f, 0, 0);
        Character enemy = Dummy(enemyOwner, enemyPos);
        Character ally = Dummy(mine, enemyPos + Vector3.right * 2f, 400);
        UnitManager enemyM = enemy.Transform.GetComponent<UnitManager>();
        CrossbowFireEffects fx = go.GetComponentInChildren<CrossbowFireEffects>();
        float lastFireTime = -100;

        foreach (ImperialEra era in ImperialEras.Ordered)
        {
            AdvancedEraState.Set(era); yield return 0.3;
            contract.TryGetTier(era, out IEraTier tier);
            CrossbowEraTier ct = (CrossbowEraTier)tier;
            float R = tier.AttackRange, cd = tier.AttackCooldown;
            string et = tag + ".G." + era;

            // espera cooldown anterior
            while (atk.SecondsSinceLastShot < cd + 0.05f) yield return 0.05;

            // --- fora do alcance (R + 0.6)
            float effOut = PlaceShooter(cb, enemy, R + 0.6f);
            if (float.IsNaN(effOut)) { Info(et, "sem posicao andavel para " + F(R + 0.6f) + " m: era nao testada neste mapa"); continue; }
            yield return 0.2;
            effOut = Flat(go.transform.position, enemy.Transform.position) - AdvancedCombat.TargetSize(enemyM);
            int shots0 = atk.ShotsFired, hp0 = enemy.HP;
            um.Attack(enemy.Transform);        // caminho do jogo: UnitManager.Attack -> IAttackHandler
            yield return 0.7;
            bool fireState = StateName(anim) == "Fire";
            Ok(et + ".1", effOut > R && atk.ShotsFired == shots0 && !atk.IsFiring && AdvancedProjectile.ActiveCount == 0 && enemy.HP == hp0 && !fireState,
                $"alcance maximo {F(R)}: alvo a efetivo {F(effOut)} m -> nenhum disparo, nenhum virote, nenhum dano, sem animacao Fire");

            // --- dentro do alcance (R - 0.4)
            float effIn = PlaceShooter(cb, enemy, R - 0.5f);
            yield return 0.2;
            effIn = Flat(go.transform.position, enemy.Transform.position) - AdvancedCombat.TargetSize(enemyM);
            int hpBefore = enemy.HP; shots0 = atk.ShotsFired;
            float tFire = Time.time;
            List<string> trace = new List<string>();
            List<Vector3> flight = new List<Vector3>(); List<float> flightT = new List<float>(); List<Vector3> flightFwd = new List<Vector3>(); List<Vector3> boltCenter = new List<Vector3>();
            Vector3 spawnWorld = atk.projectileSpawn.position;
            um.Attack(enemy.Transform);
            Ok(et + ".2", atk.IsFiring && effIn <= R, $"alvo a efetivo {F(effIn)} m (<= {F(R)}): disparo aceito");
            float releaseAt = -1; bool muzzle = false; bool windupDamage = false; int impactsBefore = AdvancedProjectile.ImpactCount; int relBefore = fx != null ? fx.Releases : 0;
            Vector3 spawnAtRelease = spawnWorld; bool impactSeen = false;
            while (Time.time - tFire < Mathf.Min(cd - 0.35f, 2.6f))
            {
                string s = StateName(anim);
                if (trace.Count == 0 || trace[trace.Count - 1] != s) trace.Add(s);
                if (Time.time - tFire < atk.releaseDelay - 0.05f && enemy.HP != hpBefore) windupDamage = true;
                if (atk.ShotsFired > shots0 && releaseAt < 0) { releaseAt = Time.time - tFire; spawnAtRelease = atk.projectileSpawn.position; }
                if (fx != null && fx.muzzle != null && fx.Releases > relBefore && (fx.muzzle.isPlaying || fx.muzzle.particleCount > 0)) muzzle = true;
                AdvancedProjectile p = UnityEngine.Object.FindAnyObjectByType<AdvancedProjectile>();
                if (p != null)
                {
                    flight.Add(p.transform.position); flightT.Add(Time.time); flightFwd.Add(p.transform.forward);
                    Renderer r = p.GetComponentInChildren<Renderer>();
                    boltCenter.Add(r != null ? r.bounds.center : p.transform.position);
                }
                if (AdvancedProjectile.ImpactCount > impactsBefore && !impactSeen) impactSeen = true;
                yield return 0.0;
            }
            yield return 0.3;
            int expected = tier.AttackDamage;
            Ok(et + ".3", !windupDamage && releaseAt > 0 && Mathf.Abs(releaseAt - atk.releaseDelay) < 0.12f, $"dano nao e instantaneo: virote sai {F(releaseAt)} s apos o comando (releaseDelay {F(atk.releaseDelay)} s)");
            Ok(et + ".4", enemy.HP == hpBefore - expected && atk.LastDamage == expected, $"dano perfurante da era = {expected} (armadura 0): HP {hpBefore} -> {enemy.HP}");
            Ok(et + ".5", impactSeen && flight.Count >= 4, $"virote observado em voo ({flight.Count} amostras) e impacto ocorreu");
            if (flight.Count >= 4)
            {
                Vector3 a0 = flight.First(), a1 = flight.Last();
                float dev = flight.Max(pt => Vector3.Cross((pt - a0).normalized, (a1 - a0).normalized).magnitude * Vector3.Distance(pt, a0));
                float speed = Vector3.Distance(a0, a1) / (flightT.Last() - flightT.First() + 1e-4f);
                float dirDot = flightFwd.Select((f, i) => i == 0 ? 1f : Vector3.Dot(f, (flight[i] - flight[i - 1]).normalized)).Min();
                Vector3 dirFlight = (a1 - a0).normalized;
                float tipLead = boltCenter.Select((c, i) => Vector3.Dot(c - flight[i], dirFlight)).Average();
                Info(et, $"voo: origem {V(a0)} (BoltSpawn na soltura {V(spawnAtRelease)}, distancia {F(Vector3.Distance(a0, spawnAtRelease))} m), desvio maximo da reta {F(dev)} m, velocidade {F(speed)} m/s, alinhamento do virote com a trajetoria (min cos) {F(dirDot)}, centro da malha em relacao ao pivo ao longo do voo {F(tipLead)} m");
                Ok(et + ".6", dev < 0.12f, $"voo reto: desvio maximo da reta {F(dev)} m");
                Ok(et + ".7", Vector3.Distance(a0, spawnAtRelease) < 1.3f, $"virote nasce perto do BoltSpawn (primeira amostra a {F(Vector3.Distance(a0, spawnAtRelease))} m; 1 frame de voo = {F(30f / 60f)} m)");
                Ok(et + ".8", speed > 18f && speed < 45f, $"velocidade ~{F(speed)} m/s (contrato 30)");
                Ok(et + ".9", dirDot > 0.98f, $"virote alinhado com a direcao do voo (cos min {F(dirDot)})");
                Ok(et + ".9b", tipLead < 0.05f, $"ponta do virote a frente (corpo da malha atras do pivo: {F(tipLead)} m)");
            }
            Ok(et + ".10", muzzle, "efeito de disparo (fumaca/poeira) acionado pelo evento de animacao");
            if (era == ImperialEra.Ancient)
            {
                Info(et, "sequencia de animacao: " + string.Join(" > ", trace));
                Ok(et + ".11", trace.Contains("Fire") && trace.Contains("Reload") && trace.IndexOf("Fire") < trace.IndexOf("Reload"), "Animator Fire > Reload (sequencia " + string.Join(">", trace) + ")");
                // virote visivel de novo no fim?
            }
            else Info(et, "sequencia de animacao: " + string.Join(" > ", trace));

            // --- cooldown
            float since = atk.SecondsSinceLastShot;
            while (atk.SecondsSinceLastShot < cd - 0.3f) yield return 0.02;
            int sh = atk.ShotsFired;
            um.Attack(enemy.Transform);
            yield return 0.15;
            Ok(et + ".12", atk.ShotsFired == sh && !atk.IsFiring, $"cooldown {F(cd)} s: tentativa a {F(atk.SecondsSinceLastShot)} s recusada");
            while (atk.SecondsSinceLastShot < cd + 0.03f) yield return 0.02;
            float gap = atk.SecondsSinceLastShot;
            hpBefore = enemy.HP; sh = atk.ShotsFired;
            um.Attack(enemy.Transform);
            Ok(et + ".13", atk.IsFiring && gap >= cd, $"cooldown cumprido ({F(gap)} s >= {F(cd)} s): novo disparo aceito");
            yield return atk.releaseDelay + 1.2;
            Ok(et + ".14", atk.ShotsFired == sh + 1 && enemy.HP == hpBefore - expected, $"segundo disparo da era: dano {expected} (HP {hpBefore} -> {enemy.HP})");

            if (era == ImperialEra.Ancient)
            {
                // --- aliados: na linha de tiro, atras do alvo, e disparo direto contra aliado
                while (atk.SecondsSinceLastShot < cd + 0.05f) yield return 0.05;
                PlaceShooter(cb, enemy, 9f);
                Vector3 mid = Vector3.Lerp(go.transform.position, enemy.Transform.position, 0.5f);
                Vector3 behind = enemy.Transform.position + (enemy.Transform.position - go.transform.position).normalized * 1.2f;
                if (NavMesh.SamplePosition(mid, out NavMeshHit mh, 3f, NavMesh.AllAreas)) mid = mh.position;
                ally.Transform.GetComponent<NavMeshAgent>().Warp(mid);
                Character ally2 = Dummy(mine, behind, 400);
                yield return 0.3;
                int allyHp = ally.HP, ally2Hp = ally2.HP; hpBefore = enemy.HP; sh = atk.ShotsFired;
                um.Attack(enemy.Transform);
                yield return atk.releaseDelay + 1.2;
                Ok(et + ".15", atk.ShotsFired == sh + 1 && enemy.HP == hpBefore - expected && ally.HP == allyHp && ally2.HP == ally2Hp, $"aliado na linha de tiro (HP {ally.HP}/{allyHp}) e aliado atras do alvo (HP {ally2.HP}/{ally2Hp}) nao sofrem dano; inimigo sofre {hpBefore - enemy.HP}");
                while (atk.SecondsSinceLastShot < cd + 0.05f) yield return 0.05;
                sh = atk.ShotsFired;
                um.Attack(ally.Transform);   // ataque dirigido (hipotetico) a um aliado
                yield return atk.releaseDelay + 1.2;
                Ok(et + ".16", ally.HP == allyHp, $"disparo dirigido a um aliado: nenhum dano (HP {ally.HP}/{allyHp}; disparos {atk.ShotsFired - sh})");
                // alvo morre durante o preparo do disparo -> sem erro
                while (atk.SecondsSinceLastShot < cd + 0.05f) yield return 0.05;
                enemy.HP = 1;
                um.Attack(enemy.Transform);
                yield return 0.08;
                int errBefore = SessionState.GetInt(Key + "Errors", 0);
                enemyM.TakeHit(5);   // morre durante o windup
                yield return atk.releaseDelay + 1.4;
                Ok(et + ".17", SessionState.GetInt(Key + "Errors", 0) == errBefore && AdvancedProjectile.ActiveCount == 0, "alvo morre durante o preparo do disparo: sem erro de console e sem virote preso");
                Kill(ally2);
                enemy = Dummy(enemyOwner, enemyPos);
                enemyM = enemy.Transform.GetComponent<UnitManager>();
            }
        }
        AdvancedEraState.Set(ImperialEra.Ancient);
        Kill(cb, enemy, ally);
        yield return 0.8;
    }

    // ------------------------------------------------------------------ edificio
    private static IEnumerator BuildingTarget(string tag, int mine)
    {
        BuildingManager target = UnityEngine.Object.FindObjectsByType<BuildingManager>(FindObjectsSortMode.None)
            .FirstOrDefault(b => b.Unit != null && b.Unit.Owner != mine && b.Unit.HP > 0);
        if (target == null) { Info(tag + ".H", "nenhum edificio inimigo na partida inicial: teste contra edificio nao aplicavel"); yield break; }
        Character cb = Spawn(Code, mine, _spawnBase);
        yield return 0.8;
        GameObject go = cb.Transform.gameObject;
        go.GetComponent<CharacterBT>().enabled = false;
        go.GetComponent<NavMeshAgent>().isStopped = true;
        ProjectileAttackController atk = go.GetComponent<ProjectileAttackController>();
        float size = AdvancedCombat.TargetSize(target);
        Vector3 pos;
        if (!OpenLine(target.transform.position, size + 12f, _spawnBase - target.transform.position, out pos)) { Info(tag + ".H", "sem posicao andavel perto do edificio inimigo"); Kill(cb); yield break; }
        go.GetComponent<NavMeshAgent>().Warp(pos);
        int hp0 = target.Unit.HP;
        float eff = Flat(go.transform.position, target.transform.position) - size;
        go.GetComponent<UnitManager>().Attack(target.transform);
        yield return atk.releaseDelay + 1.5;
        Ok(tag + ".H1", eff <= 14f && atk.ShotsFired == 1 && target.Unit.HP == hp0 - 22, $"edificio inimigo ({target.Unit.Code}, tamanho {F(size)}) a efetivo {F(eff)} m: dano 22 (HP {hp0} -> {target.Unit.HP})");
        Kill(cb);
        yield return 0.5;
    }

    // ------------------------------------------------------------------ K
    private static IEnumerator VisualsAndDeath(string tag, int mine, int enemyOwner)
    {
        AdvancedEraState.Set(ImperialEra.Ancient); yield return 0.2;
        Character cb = Spawn(Code, mine, _spawnBase);
        yield return 1.0;
        GameObject go = cb.Transform.gameObject;
        go.GetComponent<CharacterBT>().enabled = false;
        go.GetComponent<NavMeshAgent>().isStopped = true;
        ProjectileAttackController atk = go.GetComponent<ProjectileAttackController>();
        Animator anim = atk.animator;
        UnitManager um = go.GetComponent<UnitManager>();
        Character soldier = Spawn("soldier", mine, go.transform.position + go.transform.right * 3.2f);
        soldier.Transform.GetComponent<CharacterBT>().enabled = false;
        Character enemy = null;
        Vector3 ep;
        if (OpenLine(go.transform.position, 11f, go.transform.forward, out ep)) enemy = Dummy(enemyOwner, ep);
        yield return 0.5;

        // --- capturas de frente/costas/lado/3-4 (perspectiva) e em escala de jogo (ortografica)
        foreach (LODGroup lod in go.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
        Bounds b = default; bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer || r.name == "TeamRing" || r.name.Contains("Minimap") || r.name.Contains("FOV")) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        Vector3 c = b.center;
        foreach (Terrain t in Terrain.activeTerrains) t.drawTreesAndFoliage = false;
        GameObject camGo = new GameObject("CBPlayCam");
        Camera cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 32f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 300f; cam.clearFlags = CameraClearFlags.Skybox; cam.cullingMask = ~0;
        (string, Vector3)[] views = { ("front", go.transform.forward + Vector3.up * 0.3f), ("back", -go.transform.forward + Vector3.up * 0.3f), ("side", go.transform.right + Vector3.up * 0.2f), ("iso", -go.transform.right * 0.8f + go.transform.forward * 0.9f + Vector3.up * 0.7f) };
        foreach (var (name, dir) in views)
        {
            cam.transform.position = c + dir.normalized * 7.5f;
            cam.transform.rotation = Quaternion.LookRotation(c - cam.transform.position);
            Capture(cam, $"play-{tag}-{name}.png", 1000, 750, tag + "." + name);
            yield return 0.0;
        }
        // escala de jogo: ortografica com a rotacao da camera do jogo
        Quaternion gameRot = Camera.main.transform.rotation;
        Info(tag + ".K", "camera do jogo: rotacao " + V(gameRot.eulerAngles) + ", ortografica=" + Camera.main.orthographic + ", tamanho " + F(Camera.main.orthographicSize));
        cam.orthographic = true;
        foreach (float ortho in new[] { 6f, 10f, 16f })
        {
            cam.orthographicSize = ortho;
            Vector3 focus = (c + soldier.Transform.position) * 0.5f;
            cam.transform.rotation = gameRot;
            cam.transform.position = focus - gameRot * Vector3.forward * 60f;
            Capture(cam, $"play-{tag}-gamescale-{(int)ortho}.png", 1280, 720, tag + ".gamescale" + ortho);
            yield return 0.0;
        }
        cam.orthographic = false;
        foreach (Terrain t in Terrain.activeTerrains) t.drawTreesAndFoliage = true;

        // --- LOD pelo zoom do jogo
        yield return LodByZoom(tag, go, cam, gameRot);

        // --- disparo e impacto (capturas)
        if (enemy != null)
        {
            PlaceShooter(cb, enemy, 9f);
            yield return 0.2;
            foreach (Terrain t in Terrain.activeTerrains) t.drawTreesAndFoliage = false;
            um.Attack(enemy.Transform);
            float t0 = Time.time; bool shotA = false, shotB = false, shotC = false;
            Vector3 f0 = go.transform.position + Vector3.up * 0.9f;
            while (Time.time - t0 < 1.6f)
            {
                float dt = Time.time - t0;
                if (!shotA && dt >= atk.releaseDelay - 0.02f) { shotA = true; CaptureAround(cam, go, new Vector3(-0.9f, 0.45f, 0.5f), 6f, $"play-{tag}-fire-release.png", tag + ".fire"); }
                if (!shotB && dt >= atk.releaseDelay + 0.25f) { shotB = true; CaptureAround(cam, go, new Vector3(-0.9f, 0.45f, 0.5f), 6f, $"play-{tag}-fire-flight.png", tag + ".flight"); }
                if (!shotC && AdvancedProjectile.ImpactCount > 0 && enemy.HP < enemy.MaxHP) { shotC = true; yield return 0.1; if (enemy.Transform != null) CaptureAround(cam, enemy.Transform.gameObject, new Vector3(-0.9f, 0.5f, 0.6f), 5f, $"play-{tag}-impact.png", tag + ".impact"); }
                yield return 0.0;
            }
            foreach (Terrain t in Terrain.activeTerrains) t.drawTreesAndFoliage = true;
            Ok(tag + ".K5", shotA && enemy.HP < 1000, "capturas do disparo e do impacto feitas (alvo sofreu dano)");
        }

        // --- reacao a dano
        yield return WaitUntil(() => StateName(anim) == "Idle", 6, "Idle");
        AdvancedUnitAnimatorDriver drv = go.GetComponent<AdvancedUnitAnimatorDriver>();
        int react0 = drv.DamageReactions; int hp0 = cb.HP;
        um.TakeHit(2);
        bool hitSeen = false;
        for (int i = 0; i < 25 && !hitSeen; i++) { hitSeen = StateName(anim, 1) == "Hit"; yield return 0.0; }
        Ok(tag + ".K6", cb.HP == hp0 - 2 && hitSeen && drv.DamageReactions == react0 + 1, $"dano na balestra: HP {hp0}->{cb.HP}, Damaged acionou a camada Hit ({hitSeen}), reacoes {drv.DamageReactions - react0}");
        yield return 1.0;
        Ok(tag + ".K7", StateName(anim, 1) != "Hit" && !anim.GetBool("Fire"), "camada Hit volta ao vazio e o trigger Fire nao fica preso");

        // --- morte com disparo em andamento (windup) -> sem erro
        Kill(enemy);
        Character foe = Dummy(enemyOwner, ep, 1000);
        PlaceShooter(cb, foe, 8f);
        yield return 0.3;
        while (atk.SecondsSinceLastShot < 3.3f) yield return 0.05;
        int err0 = SessionState.GetInt(Key + "Errors", 0);
        um.Attack(foe.Transform);
        yield return 0.06;
        Vector3 deathPos = go.transform.position; Quaternion deathRot = go.transform.rotation;
        int deaths0 = AdvancedUnitDeathEffect.DeathsSpawned;
        foreach (Terrain t in Terrain.activeTerrains) t.drawTreesAndFoliage = false;
        um.TakeHit(100000);
        yield return 0.4;
        Ok(tag + ".K8", AdvancedUnitDeathEffect.DeathsSpawned == deaths0 + 1, "AdvancedUnitDeathEffect instanciou o prefab de destruicao");
        GameObject death = GameObject.Find("CrossbowDeath(Clone)");
        Ok(tag + ".K9", death != null && Vector3.Distance(death.transform.position, deathPos) < 0.5f && Quaternion.Angle(death.transform.rotation, deathRot) < 5f, "CrossbowDeath(Clone) na posicao e orientacao da unidade");
        Ok(tag + ".K10", go == null, "a unidade original foi destruida (HP 0)");
        if (death != null)
        {
            Animator da = death.GetComponent<Animator>();
            float n0 = da.GetCurrentAnimatorStateInfo(0).normalizedTime;
            Ok(tag + ".K11", da.GetCurrentAnimatorStateInfo(0).IsName("Destroy"), "prefab de destruicao toca o estado Destroy");
            Vector3 cc = deathPos + Vector3.up * 0.5f;
            foreach (float wait in new[] { 0.3f, 0.7f, 0.8f })
            {
                yield return (double)wait;
                if (death == null) break;
                cam.transform.position = cc + new Vector3(-4.4f, 3.4f, 5.2f);
                cam.transform.rotation = Quaternion.LookRotation(cc - cam.transform.position);
                float n = da.GetCurrentAnimatorStateInfo(0).normalizedTime;
                Capture(cam, $"play-{tag}-destroy-{(int)(n * 100):D3}.png", 1000, 750, tag + ".destroy" + F(n));
            }
            float n1 = da.GetCurrentAnimatorStateInfo(0).normalizedTime;
            Ok(tag + ".K12", n1 > n0 + 0.3f, $"animacao Destroy avanca ({F(n0)} -> {F(n1)})");
            Ok(tag + ".K13", death.GetComponentsInChildren<ParticleSystem>().Any(ps => ps.isPlaying || ps.particleCount > 0) || n1 >= 1f, "poeira da queda ou animacao concluida");
        }
        foreach (Terrain t in Terrain.activeTerrains) t.drawTreesAndFoliage = true;
        yield return 1.5;
        Ok(tag + ".K14", SessionState.GetInt(Key + "Errors", 0) == err0 && AdvancedProjectile.ActiveCount == 0, "morte durante o preparo do disparo: sem erro de console e sem virote preso (o Invoke morre com o objeto)");
        yield return 8.2;
        Ok(tag + ".K15", GameObject.Find("CrossbowDeath(Clone)") == null, "prefab de destruicao e removido apos o lifetime (8 s)");
        UnityEngine.Object.Destroy(camGo);
        Kill(soldier, foe);
        AdvancedEraState.Set(ImperialEra.Ancient);
        yield return 0.5;
    }

    private static IEnumerator LodByZoom(string tag, GameObject go, Camera cam, Quaternion gameRot)
    {
        LODGroup group = go.GetComponentInChildren<LODGroup>();
        if (group == null || group.lodCount != 3) { Ok(tag + ".L0", false, "LODGroup ausente/diferente de 3 no jogo"); yield break; }
        foreach (LODGroup lg in go.GetComponentsInChildren<LODGroup>()) lg.ForceLOD(-1);
        LOD[] lods = group.GetLODs();
        Color[] colors = { new Color(1, 1, 0), new Color(0, 1, 0), new Color(1, 0, 1) };
        Shader flat = Shader.Find("Unlit/Color");
        for (int i = 0; i < 3; i++)
            foreach (Renderer r in lods[i].renderers)
                r.sharedMaterials = r.sharedMaterials.Select(_ => new Material(flat) { color = colors[i] }).ToArray();
        foreach (Terrain t in Terrain.activeTerrains) t.drawTreesAndFoliage = false;
        cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.cullingMask = 1 << go.layer | 1 << 0;
        List<int> seen = new List<int>();
        foreach (float ortho in new[] { 8f, 18f, 26f })
        {
            cam.orthographicSize = ortho;
            Vector3 focus = go.transform.position + Vector3.up * 0.75f;
            cam.transform.rotation = gameRot;
            cam.transform.position = focus - gameRot * Vector3.forward * 60f;
            RenderTexture rt = new RenderTexture(480, 360, 24);
            Texture2D tex = new Texture2D(480, 360, TextureFormat.RGB24, false);
            RenderTexture prev = RenderTexture.active;
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 480, 360), 0, 0);
            RenderTexture.active = prev; cam.targetTexture = null;
            int[] n = new int[3];
            foreach (Color32 p in tex.GetPixels32())
            {
                if (p.r > 200 && p.g > 200 && p.b < 60) n[0]++;
                else if (p.r < 60 && p.g > 200 && p.b < 60) n[1]++;
                else if (p.r > 200 && p.g < 60 && p.b > 200) n[2]++;
            }
            rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
            int best = n.Max() < 5 ? -1 : Array.IndexOf(n, n.Max());
            Info(tag + ".L", $"zoom ortografico {ortho}: pixels LOD0/1/2 = {n[0]}/{n[1]}/{n[2]} -> LOD{best}");
            seen.Add(best);
            yield return 0.0;
        }
        cam.orthographic = false; cam.clearFlags = CameraClearFlags.Skybox; cam.cullingMask = ~0;
        foreach (Terrain t in Terrain.activeTerrains) t.drawTreesAndFoliage = true;
        Ok(tag + ".L1", seen[0] == 0 && seen[1] == 1 && seen[2] == 2, $"LOD pelo zoom do jogo: 8 -> LOD{seen[0]}, 18 -> LOD{seen[1]}, 26 -> LOD{seen[2]}");
        // restaura materiais reais (a instancia sera destruida de qualquer forma)
        yield return 0.0;
    }

    private static void CaptureAround(Camera cam, GameObject go, Vector3 dirLocal, float dist, string file, string id)
    {
        Vector3 focus = go.transform.position + Vector3.up * 0.9f;
        Vector3 dir = go.transform.TransformDirection(dirLocal).normalized;
        cam.transform.position = focus + dir * dist;
        cam.transform.rotation = Quaternion.LookRotation(focus - cam.transform.position);
        Capture(cam, file, 1000, 750, id);
    }

    private static void Capture(Camera cam, string file, int w, int h, string id)
    {
        RenderTexture rt = new RenderTexture(w, h, 24);
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        RenderTexture prev = RenderTexture.active;
        try
        {
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(OutDir, file), tex.EncodeToPNG());
            Color32[] px = tex.GetPixels32();
            int mg = 0;
            foreach (Color32 p in px) if (p.r > 190 && p.b > 190 && p.g < 90 && p.r - p.g > 110 && p.b - p.g > 110) mg++;
            float frac = (float)mg / px.Length;
            Ok("render." + id, frac < 0.001f, $"{file}: pixels magenta {frac:P3}");
        }
        finally
        {
            cam.targetTexture = null; RenderTexture.active = prev;
            rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
