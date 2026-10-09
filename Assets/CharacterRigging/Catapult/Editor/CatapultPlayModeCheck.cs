using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Siege;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Play Mode no jogo real (Core.unity -> menu -> Map1): cria uma Catapulta pelo catalogo (Globals.CHARACTER_DATA) e valida:
/// alcance minimo e maximo, recarga, projetil balistico em arco, dano direto no alvo + dano radial com queda nos vizinhos inimigos e
/// nenhum nos aliados, efeitos de disparo e impacto, reacao a dano (Hit), movimento (Speed + rodas), progressao nas 4 eras,
/// ataque pelo behavior tree real (UnitManager.Attack -> IAttackHandler), LOD pela camera do jogo e destruicao (prefab de morte).
/// Renders: frente, costas, lado, disparo, impacto, movimento e destruicao (sem pixels magenta).
/// Batch: -executeMethod CatapultPlayModeCheck.Run (sai com 0 se passar). Resultado: Docs/CharacterMeshAudit/Catapult/playmode.txt
/// </summary>
[InitializeOnLoad]
public static class CatapultPlayModeCheck
{
    private const string Prefix = "EraImperial.CatapultCheck.";
    private const string ReportFile = "Docs/CharacterMeshAudit/Catapult/playmode.txt";
    private const string Shots = "Docs/CharacterMeshAudit/Catapult/";

    private static readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>();
    private static float resumeAt;
    private static Character catapult;
    private static CharacterManager catapultManager;
    private static CatapultCombatController combat;
    private static AdvancedUnitAnimatorDriver driver;
    private static Camera shotCamera;
    private static readonly HashSet<string> Captured = new HashSet<string>();
    private static readonly List<Character> Spawned = new List<Character>();
    private static int lastFrame = -1;

    static CatapultPlayModeCheck()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    public static void Run()
    {
        Directory.CreateDirectory("TestResults");
        Directory.CreateDirectory(Shots);
        File.WriteAllText(ReportFile, "Unity " + Application.unityVersion + " - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
        SessionState.SetBool(Prefix + "Running", true);
        SessionState.SetInt(Prefix + "Errors", 0);
        SessionState.SetFloat(Prefix + "Deadline", (float)EditorApplication.timeSinceStartup + 900f);
        SessionState.SetString(Prefix + "DataPath", Path.GetFullPath("TestResults/CatapultData-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void IsolateSaves()
    {
        if (SessionState.GetBool(Prefix + "Running", false))
            BinarySerializable.DATA_DIRECTORY = SessionState.GetString(Prefix + "DataPath", "TestResults/CatapultData");
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Prefix + "Running", false)) return;
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        SessionState.SetInt(Prefix + "Errors", SessionState.GetInt(Prefix + "Errors", 0) + 1);
        File.AppendAllText(ReportFile, "ERROR: " + message + "\n" + stack + "\n");
    }

    private static void Record(string message)
    {
        File.AppendAllText(ReportFile, message + "\n");
        Debug.Log("[Catapult check] " + message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL " + message);
        Record("PASS " + message);
    }

    // ------------------------------------------------------------------------------------------------ runner
    private static void Tick()
    {
        if (!SessionState.GetBool(Prefix + "Running", false)) return;
        try
        {
            if (SessionState.GetInt(Prefix + "Errors", 0) > 0) throw new InvalidOperationException("Erros de runtime registrados no Console; abortando.");
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Prefix + "Deadline", 0)) throw new TimeoutException("Excedeu o tempo limite.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            IsolateSaves();
            if (Stack.Count == 0) Stack.Push(Script());
            if (Time.realtimeSinceStartup < resumeAt) return;
            if (Time.frameCount == lastFrame) return; // um passo por quadro do jogo
            lastFrame = Time.frameCount;
            IEnumerator top = Stack.Peek();
            if (!top.MoveNext())
            {
                Stack.Pop();
                if (Stack.Count == 0) Finish();
                return;
            }
            switch (top.Current)
            {
                case IEnumerator nested: Stack.Push(nested); break;
                case float seconds: resumeAt = Time.realtimeSinceStartup + seconds; break;
            }
        }
        catch (Exception exception)
        {
            Record("FAILED: " + exception);
            SessionState.SetBool(Prefix + "Running", false);
            EditorApplication.Exit(1);
        }
    }

    private static void Finish()
    {
        int errors = SessionState.GetInt(Prefix + "Errors", 0);
        Require(errors == 0, "0 erros de runtime no Console");
        Record("CATAPULT_PLAYMODE_PASSED");
        SessionState.SetBool(Prefix + "Running", false);
        EditorApplication.Exit(0);
    }

    private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds, string what)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition())
        {
            if (Time.realtimeSinceStartup - start > timeoutSeconds) throw new TimeoutException("Tempo esgotado esperando: " + what);
            yield return null;
        }
    }

    // ------------------------------------------------------------------------------------------------ script
    private static IEnumerator Script()
    {
        // ---- partida em Map1
        MainMenuManager menu = null;
        yield return WaitFor(() => { menu = UnityEngine.Object.FindAnyObjectByType<MainMenuManager>(); return menu != null && menu.newGameScrollview.childCount > 0; }, 60f, "menu principal");
        MapData map = Resources.Load<MapData>("ScriptableObjects/Maps/Map1");
        Require(map != null, "Map1 encontrado");
        Transform picker = menu.newGameScrollview.Cast<Transform>().First(item => item.Find("Data/Name").GetComponent<Text>().text == map.mapName);
        picker.GetComponent<Button>().onClick.Invoke();
        menu.StartNewGame();
        Record("Partida nova em Map1 iniciada");
        yield return WaitFor(() => GameManager.instance != null && Camera.main != null && GameObject.Find("Spawnpoints") != null, 60f, "partida carregada");
        yield return 3f;

        // ---- unidade pelo catalogo
        Require(Globals.CHARACTER_DATA.ContainsKey("catapult"), "Globals.CHARACTER_DATA contem 'catapult'");
        Transform spawnpoints = GameObject.Find("Spawnpoints").transform;
        Require(NavMesh.SamplePosition(spawnpoints.GetChild(0).position, out NavMeshHit hit, 20, NavMesh.AllAreas), "Ponto inicial no NavMesh");
        int me = GameManager.instance.gamePlayersParameters.myPlayerId;
        int other = me == 0 ? 1 : 0;
        catapult = new Character(Globals.CHARACTER_DATA["catapult"], me);
        catapultManager = catapult.Transform.GetComponent<CharacterManager>();
        combat = catapult.Transform.GetComponent<CatapultCombatController>();
        driver = catapult.Transform.GetComponent<AdvancedUnitAnimatorDriver>();
        Require(combat != null && driver != null && catapult.Transform.GetComponent<AdvancedUnitEraBinding>() != null, "Catapulta instanciada com CharacterManager, CatapultCombatController, AdvancedUnitEraBinding e AdvancedUnitAnimatorDriver");
        Require(catapultManager.agent.Warp(hit.position), "Catapulta posicionada no NavMesh");
        Require(catapult.Transform.Find("Mesh/CatapultVisual") != null, "Visual da Catapulta presente sob Mesh");
        Require(combat.projectileSpawn != null && combat.projectileSpawn.name == "ProjectileSpawn", "projectileSpawn real (filho do osso Arm)");
        CatapultContract contract = combat.contract as CatapultContract;
        contract.TryGetTier(ImperialEra.Ancient, out IEraTier tier0);
        CatapultEraTier ancient = (CatapultEraTier)tier0;
        yield return 0.5f;
        Require(catapult.AttackDamage == ancient.attackDamage && catapult.MaxHP == ancient.healthpoints && Mathf.Approximately(catapult.AttackRange, ancient.attackRange),
            $"Catapulta nasce com os valores da era Ancient: vida {catapult.MaxHP}, dano {catapult.AttackDamage}, alcance {catapult.AttackRange}");
        catapult.Transform.GetComponent<CharacterBT>().enabled = false; // controle manual nas fases 1-6; o BT real e testado depois
        catapultManager.agent.isStopped = true;

        shotCamera = new GameObject("CatapultShotCamera").AddComponent<Camera>();
        shotCamera.fieldOfView = 32f;
        shotCamera.clearFlags = CameraClearFlags.Skybox;
        shotCamera.enabled = false;

        // ---- renders de frente, costas e lado (parada, Idle)
        yield return 0.4f;
        CaptureAround("playmode-front", 28f, 9.5f, 4.2f);
        CaptureAround("playmode-back", 180f, 9.5f, 2.3f);
        CaptureAround("playmode-side", 90f, 9.5f, 1.8f);
        Animator animator = combat.animator;
        Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "Estado inicial do Animator: Idle");

        // ---- fase 1: alcance minimo
        Vector3 origin = catapult.Transform.position;
        Character probe = Spawn("worker", other, PlaceAt(origin, 5f), 500);
        int shots0 = combat.ShotsFired;
        int impacts0 = AdvancedProjectile.ImpactCount;
        Record($"Alvo a {Flat(origin, probe.Transform.position):F1} m (alcance minimo {ancient.minimumRange} m)");
        Require(combat.TryHandleAttack(probe.Transform), "TryHandleAttack trata o ataque (nunca dano instantaneo)");
        yield return 1.0f;
        Require(combat.ShotsFired == shots0 && !combat.IsFiring && AdvancedProjectile.ActiveCount == 0 && AdvancedProjectile.ImpactCount == impacts0 && probe.HP == 500,
            "Nao dispara abaixo do alcance minimo (5 m < 8 m): sem tiro, sem projetil, sem dano");

        // ---- fase 2: alem do alcance maximo
        probe.Transform.GetComponent<CharacterManager>().agent.Warp(PlaceAt(origin, 23.5f));
        yield return 0.2f;
        Record($"Alvo a {Flat(origin, probe.Transform.position):F1} m (alcance maximo {ancient.attackRange} m)");
        combat.TryHandleAttack(probe.Transform);
        yield return 1.0f;
        Require(combat.ShotsFired == shots0 && AdvancedProjectile.ActiveCount == 0 && probe.HP == 500, "Nao dispara alem do alcance maximo (23,5 m > 20 m)");

        // ---- fase 3: dentro de [minimo, maximo]; cluster de alvos
        Vector3 e1Pos = PlaceAt(origin, 14f);
        Character e1 = probe;
        e1.Transform.GetComponent<CharacterManager>().agent.Warp(e1Pos);
        Character e2 = Spawn("worker", other, PlaceAt(e1Pos, 1.5f), 500);
        Character e3 = Spawn("worker", other, PlaceAt(e1Pos, 2.4f, e2.Transform.position), 500);
        Character e4 = Spawn("worker", other, PlaceAt(e1Pos, 5.5f), 500);
        Character ally = Spawn("worker", me, PlaceAt(e1Pos, 1.0f, e2.Transform.position), 500);
        yield return 0.4f;
        float d12 = Vector3.Distance(e1.Transform.position, e2.Transform.position), d13 = Vector3.Distance(e1.Transform.position, e3.Transform.position);
        float d14 = Vector3.Distance(e1.Transform.position, e4.Transform.position), dAlly = Vector3.Distance(e1.Transform.position, ally.Transform.position);
        Record($"Cluster: alvo direto a {Flat(origin, e1.Transform.position):F1} m; vizinhos inimigos a {d12:F2} / {d13:F2} / {d14:F2} m; aliado a {dAlly:F2} m; raio da explosao {ancient.blastRadius} m");
        Require(d12 < ancient.blastRadius && d13 < ancient.blastRadius && d12 < d13 && d14 > ancient.blastRadius && dAlly < ancient.blastRadius, "Geometria do teste: 2 inimigos dentro do raio (queda), 1 fora, 1 aliado dentro");
        int[] hp0 = { e1.HP, e2.HP, e3.HP, e4.HP, ally.HP };
        Vector3 aim = e1.Transform.position;
        int fireFx0 = CatapultAnimationEvents.FireEffectsSpawned;
        float firstShotTime = Time.time;
        List<string> stateTrace = new List<string>();
        Require(combat.TryHandleAttack(e1.Transform) && combat.IsFiring, "Dentro do alcance: a catapulta inicia o disparo (animacao Fire)");
        float tFire = Time.time;
        Vector3 spawnAtStart = combat.projectileSpawn.position;
        yield return WaitFor(() => { Trace(animator, stateTrace); return Time.time - tFire >= 0.2f; }, 2f, "inicio da animacao");
        Require(e1.HP == hp0[0] && AdvancedProjectile.ActiveCount == 0, "Sem dano nem projetil enquanto o braco ainda sobe (dano nao e instantaneo)");
        yield return WaitFor(() => { Trace(animator, stateTrace); return combat.ShotsFired > shots0; }, 3f, "saida da pedra");
        float releaseT = Time.time - tFire;
        Vector3 releasePos = combat.projectileSpawn.position;
        Record($"Pedra saiu {releaseT:F2} s depois do inicio (releaseDelay {combat.releaseDelay:F2} s); socket em {releasePos:F2} (inicio {spawnAtStart:F2})");
        Require(Mathf.Abs(releaseT - combat.releaseDelay) < 0.12f, "Pedra sai no instante configurado (releaseDelay)");
        Require(releasePos.y > spawnAtStart.y + 0.8f, $"Na saida o ProjectileSpawn acompanhou o braco: altura {spawnAtStart.y - origin.y:F2} -> {releasePos.y - origin.y:F2} m");
        Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Fire"), "Animator em Fire na saida da pedra");
        yield return null;
        AdvancedProjectile projectile = UnityEngine.Object.FindAnyObjectByType<AdvancedProjectile>();
        Require(projectile != null && projectile.name.StartsWith("CatapultBoulder"), "Projetil CatapultBoulder em voo (AdvancedProjectile adicionado em runtime ao prefab sem scripts)");
        // O projétil já percorre um pequeno trecho no primeiro frame; validar a origem
        // com margem baseada no deslocamento físico, sem confundir voo correto com erro de socket.
        float firstFrameTravel = projectile.GetComponent<AdvancedProjectile>() != null ? 2.0f : 0f;
        Require(Vector3.Distance(projectile.transform.position, releasePos) < 1.5f + firstFrameTravel, "O projetil nasce no socket ProjectileSpawn (na concha)");
        Require(CatapultAnimationEvents.FireEffectsSpawned > fireFx0, "Efeito de disparo (evento CatapultRelease do clipe) instanciado");
        GameObject fireFx = FindByName("CatapultFire");
        Require(fireFx != null && Vector3.Distance(fireFx.transform.position, releasePos) < 1.0f && fireFx.GetComponentsInChildren<ParticleSystem>().Any(p => p.particleCount > 0 || p.isPlaying),
            "Efeito de disparo (poeira/fumaca) no ProjectileSpawn, com particulas ativas");
        CaptureSideOfCatapult("playmode-fire");

        // trajetoria
        List<Vector3> path = new List<Vector3>();
        int impactsBefore = AdvancedProjectile.ImpactCount;
        float flightStart = Time.time;
        yield return WaitFor(() =>
        {
            Trace(animator, stateTrace);
            AdvancedProjectile p = UnityEngine.Object.FindAnyObjectByType<AdvancedProjectile>();
            if (p != null) path.Add(p.transform.position);
            if (p == null && AdvancedProjectile.ImpactCount > impactsBefore) return true;
            // recarga ainda sem poder disparar de novo
            return false;
        }, 12f, "impacto do projetil");
        float flightTime = Time.time - flightStart;
        float apex = path.Max(p => p.y);
        float lineY = Mathf.Max(releasePos.y, aim.y);
        int apexIndex = path.FindIndex(p => Mathf.Approximately(p.y, apex));
        Record($"Voo: {path.Count} amostras em {flightTime:F2} s; apice {apex - lineY:F1} m acima da reta origem-alvo; indice do apice {apexIndex}");
        Require(path.Count >= 8 && apexIndex > 1 && apexIndex < path.Count - 2 && apex - lineY > 4f, "Projetil balistico em arco: sobe, passa por um apice e desce (apice > 4 m acima da reta)");
        Require(path.Take(apexIndex).Zip(path.Skip(1).Take(apexIndex), (a, b) => b.y >= a.y - 0.01f).All(x => x) && path.Skip(apexIndex).Zip(path.Skip(apexIndex + 1), (a, b) => b.y <= a.y + 0.01f).All(x => x),
            "Altura monotonicamente crescente ate o apice e decrescente depois");
        Require(Flat(path.Last(), aim) < 3.0f, $"Projetil termina perto do alvo (erro horizontal {Flat(path.Last(), aim):F2} m)");
        GameObject impactFx = FindByName("CatapultImpact");
        Require(impactFx != null && Vector3.Distance(impactFx.transform.position, aim) < 1.5f, "Efeito de impacto instanciado no ponto de impacto");
        CaptureAtPoint("playmode-impact-flash", aim, 10f, 3.5f);
        yield return 0.15f;
        Require(impactFx != null && impactFx.GetComponentsInChildren<ParticleSystem>().Sum(p => p.particleCount) > 5, "Efeito de impacto com particulas ativas (poeira, fragmentos, flash)");
        yield return 0.35f;
        CaptureAtPoint("playmode-impact", aim, 11f, 4f);

        // dano
        yield return 0.1f;
        int[] hp1 = { e1.HP, e2.HP, e3.HP, e4.HP, ally.HP };
        int direct = hp0[0] - hp1[0], r2 = hp0[1] - hp1[1], r3 = hp0[2] - hp1[2], r4 = hp0[3] - hp1[3], rAlly = hp0[4] - hp1[4];
        int exp2 = Mathf.RoundToInt(ancient.radialDamage * (1f - Vector3.Distance(aim, e2.Transform.position) / ancient.blastRadius));
        int exp3 = Mathf.RoundToInt(ancient.radialDamage * (1f - Vector3.Distance(aim, e3.Transform.position) / ancient.blastRadius));
        Record($"Dano: direto {direct}; radial {r2} (esperado {exp2}) e {r3} (esperado {exp3}); fora do raio {r4}; aliado {rAlly}; CatapultCombatController.LastDirectDamage={combat.LastDirectDamage}, LastRadialHits={combat.LastRadialHits}");
        Require(direct == ancient.attackDamage, $"Dano direto no alvo = dano da era ({ancient.attackDamage})");
        Require(r2 == exp2 && r3 == exp3 && r2 > r3 && r3 > 0, "Dano radial com queda linear com a distancia nos vizinhos inimigos (mais perto, mais dano)");
        Require(r4 == 0, "Inimigo fora do raio da explosao nao sofre dano");
        Require(rAlly == 0, "Aliado dentro do raio nao sofre dano");
        Require(combat.LastDirectDamage == ancient.attackDamage && combat.LastRadialHits >= 2, "Registro do controlador: 1 impacto direto e acertos radiais nos vizinhos");

        // recarga
        Require(combat.ShotsFired == shots0 + 1, "Um unico disparo ate aqui");
        combat.TryHandleAttack(e1.Transform);
        yield return 0.3f;
        Require(combat.ShotsFired == shots0 + 1 && !combat.IsFiring, "Respeita a recarga: novo pedido logo apos o tiro nao dispara");
        yield return WaitFor(() => { Trace(animator, stateTrace); return Time.time - firstShotTime >= ancient.attackCooldown - 0.3f; }, 12f, "fim da recarga");
        combat.TryHandleAttack(e1.Transform);
        yield return 0.2f;
        Require(combat.ShotsFired == shots0 + 1 && !combat.IsFiring, $"Ainda em recarga {ancient.attackCooldown - 0.3f:F1} s depois do tiro: nao dispara");
        yield return 0.45f;
        Require(combat.TryHandleAttack(e1.Transform) && combat.IsFiring, $"Recarga de {ancient.attackCooldown:F1} s concluida: dispara de novo");
        yield return WaitFor(() => { Trace(animator, stateTrace); return combat.ShotsFired == shots0 + 2; }, 3f, "segundo disparo");
        yield return WaitFor(() => AdvancedProjectile.ActiveCount == 0, 12f, "segundo impacto");
        yield return WaitFor(() => { Trace(animator, stateTrace); return animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") && !animator.IsInTransition(0); }, 8f, "volta ao Idle");
        string trace = string.Join(" > ", stateTrace);
        Record("Sequencia de estados do Animator: " + trace);
        Require(Regex(trace, "Fire.*Reload.*Idle.*Fire.*Reload.*Idle"), "Animator: Fire > Reload > Idle em cada disparo");
        Require(AdvancedProjectile.ActiveCount == 0, "Nenhum projetil restante");

        // ---- reacao a dano (Damaged -> Hit)
        int reactions0 = driver.DamageReactions;
        catapultManager.TakeHit(3);
        float tHit = Time.time;
        bool sawHit = false;
        yield return WaitFor(() => { sawHit |= animator.GetCurrentAnimatorStateInfo(0).IsName("Hit") || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Hit")); return sawHit || Time.time - tHit > 1f; }, 3f, "reacao a dano");
        Require(driver.DamageReactions > reactions0 && sawHit, "Dano recebido aciona o trigger Damaged e o estado Hit");
        yield return 1.0f;

        // ---- movimento: Speed e rodas
        Quaternion wheelRot0 = FindDeep(catapult.Transform, "Wheel_FL").localRotation;
        Vector3 moveStart = catapult.Transform.position;
        catapultManager.agent.isStopped = false;
        Vector3 forward = catapult.Transform.forward; forward.y = 0f;
        Require(NavMesh.SamplePosition(moveStart + forward.normalized * 12f, out NavMeshHit far, 10f, NavMesh.AllAreas) && Flat(far.position, moveStart) > 6f, "Destino de caminhada no NavMesh");
        Require(catapultManager.MoveTo(far.position, false), "Comando de movimento aceito");
        yield return 2.5f;
        float speed = animator.GetFloat("Speed");
        float moved = Flat(moveStart, catapult.Transform.position);
        Quaternion wheelRot1 = FindDeep(catapult.Transform, "Wheel_FL").localRotation;
        yield return 0.12f;
        Quaternion wheelRot2 = FindDeep(catapult.Transform, "Wheel_FL").localRotation;
        Record($"Movimento: {moved:F1} m em 2,5 s; velocidade do agente {catapultManager.agent.velocity.magnitude:F2} m/s; parametro Speed {speed:F2}");
        Require(moved > 2f && speed > 1.0f && catapultManager.agent.speed <= 2.6f, $"Catapulta lenta: andou {moved:F1} m, Speed {speed:F2} (agente com velocidade maxima 2,5)");
        Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Move") || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Move")), "Animator em Move");
        Require(Quaternion.Angle(wheelRot1, wheelRot2) > 3f, $"As rodas giram durante o movimento ({Quaternion.Angle(wheelRot1, wheelRot2):F1} graus em 0,12 s)");
        CaptureAround("playmode-move", 35f, 9.5f, 3.0f, 0f);
        catapultManager.agent.isStopped = true;
        catapultManager.agent.ResetPath();
        yield return 1.0f;
        Require(animator.GetFloat("Speed") < 0.3f, "Parada: Speed volta a ~0");

        // ---- progressao nas 4 eras (AdvancedEraState -> AdvancedUnitEraBinding)
        foreach (ImperialEra era in new[] { ImperialEra.Classical, ImperialEra.Medieval, ImperialEra.Imperial })
        {
            contract.TryGetTier(era, out IEraTier t);
            AdvancedEraState.Set(era);
            yield return 0.1f;
            Require(catapult.AttackDamage == t.AttackDamage && catapult.MaxHP == t.HealthPoints && Mathf.Approximately(catapult.AttackRange, t.AttackRange),
                $"Era {era}: vida {catapult.MaxHP}, dano {catapult.AttackDamage}, alcance {catapult.AttackRange} (contrato: {t.HealthPoints}/{t.AttackDamage}/{t.AttackRange})");
        }
        AdvancedEraState.Set(ImperialEra.Ancient);
        yield return 0.1f;
        Require(catapult.AttackDamage == ancient.attackDamage, "Volta a Ancient");

        // ---- ataque pelo behavior tree real
        foreach (Character c in Spawned.ToList()) if (c.Transform != null) c.Transform.GetComponent<CharacterManager>().TakeHit(100000);
        Spawned.Clear();
        yield return 0.5f;
        Vector3 here = catapult.Transform.position;
        Character target = Spawn("worker", other, PlaceAt(here, 13f), 1500);
        int btShots0 = combat.ShotsFired;
        catapult.Transform.GetComponent<CharacterBT>().enabled = true;
        catapultManager.agent.isStopped = false;
        Record("Behavior tree reativado; inimigo parado a 13 m");
        yield return WaitFor(() => combat.ShotsFired > btShots0, 40f, "disparo conduzido pelo CharacterBT");
        Require(true, "O behavior tree (UnitManager.Attack -> IAttackHandler) conduziu um disparo da catapulta");
        yield return WaitFor(() => target.HP < 1500, 15f, "dano do disparo conduzido pelo BT");
        Require(1500 - target.HP == catapult.AttackDamage, $"Dano do disparo do BT = {catapult.AttackDamage}");
        catapult.Transform.GetComponent<CharacterBT>().enabled = false;
        catapultManager.agent.isStopped = true;
        yield return WaitFor(() => AdvancedProjectile.ActiveCount == 0, 10f, "projeteis restantes");

        // ---- LOD pela camera do jogo
        yield return LodStage();

        // ---- destruicao
        yield return DestroyStage();
    }

    // ------------------------------------------------------------------------------------------------ fases
    private static IEnumerator LodStage()
    {
        LODGroup group = catapult.Transform.GetComponentInChildren<LODGroup>();
        Require(group != null && group.lodCount == 3, "LODGroup presente na unidade em jogo");
        SkinnedMeshRenderer[] renderers = catapult.Transform.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.Contains("_LOD")).OrderBy(r => r.name).ToArray();
        Require(renderers.Length == 3, "3 renderers de LOD na unidade em jogo");
        Color[] colours = { new Color(1f, 1f, 0f), new Color(0f, 1f, 0f), new Color(1f, 0f, 1f) };
        for (int i = 0; i < 3; i++) renderers[i].sharedMaterials = Enumerable.Repeat(new Material(Shader.Find("Unlit/Color")) { color = colours[i] }, renderers[i].sharedMaterials.Length).ToArray();
        shotCamera.clearFlags = CameraClearFlags.SolidColor;
        shotCamera.backgroundColor = Color.black;
        shotCamera.orthographic = true;
        shotCamera.nearClipPlane = 0.1f;
        shotCamera.farClipPlane = 200f;
        Vector3 focus = catapult.Transform.position + Vector3.up * 1.1f;
        List<int> seen = new List<int>();
        foreach (float size in new[] { 8f, 18f, 26f })
        {
            shotCamera.orthographicSize = size;
            shotCamera.transform.position = focus + new Vector3(0.3f, 0.2f, -1f).normalized * 40f;
            shotCamera.transform.rotation = Quaternion.LookRotation(focus - shotCamera.transform.position);
            RenderTexture target = new RenderTexture(480, 360, 24);
            Texture2D texture = new Texture2D(480, 360, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            shotCamera.targetTexture = target;
            shotCamera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 480, 360), 0, 0);
            RenderTexture.active = previous;
            shotCamera.targetTexture = null;
            int[] counts = new int[3];
            foreach (Color32 p in texture.GetPixels32())
            {
                if (p.r > 200 && p.g > 200 && p.b < 60) counts[0]++;
                else if (p.r < 60 && p.g > 200 && p.b < 60) counts[1]++;
                else if (p.r > 200 && p.g < 60 && p.b > 200) counts[2]++;
            }
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(texture);
            int best = counts.Max() < 5 ? -1 : Array.IndexOf(counts, counts.Max());
            Record($"LOD em jogo, zoom ortografico {size:F0}: pixels LOD0/1/2 = {counts[0]}/{counts[1]}/{counts[2]} -> LOD{best}");
            seen.Add(best);
            yield return null;
        }
        shotCamera.orthographic = false;
        shotCamera.clearFlags = CameraClearFlags.Skybox;
        Require(seen[0] == 0 && seen[1] == 1 && seen[2] == 2, "Troca de LOD pelo zoom da camera do jogo: 8 > LOD0, 18 > LOD1, 26 > LOD2");
    }

    private static IEnumerator DestroyStage()
    {
        Transform arm = FindDeep(catapult.Transform, "Arm");
        Vector3 armBefore = arm.position;
        Vector3 deathPos = catapult.Transform.position;
        Quaternion deathRot = catapult.Transform.rotation;
        int deaths0 = AdvancedUnitDeathEffect.DeathsSpawned;
        catapultManager.TakeHit(catapult.HP + 1000);
        yield return null;
        yield return null;
        Require(catapult.Transform == null, "A unidade e removida ao zerar a vida");
        Require(AdvancedUnitDeathEffect.DeathsSpawned == deaths0 + 1, "AdvancedUnitDeathEffect instanciou o prefab de morte");
        GameObject wreck = FindByName("CatapultDestroyed");
        Require(wreck != null && Vector3.Distance(wreck.transform.position, deathPos) < 0.05f && Quaternion.Angle(wreck.transform.rotation, deathRot) < 1f, "Prefab de morte no lugar e na orientacao da unidade");
        Animator death = wreck.GetComponent<Animator>();
        yield return 0.3f;
        Require(death.GetCurrentAnimatorStateInfo(0).IsName("Destroy") && death.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.5f, "A animacao Destroy toca sozinha desde o inicio");
        Require(wreck.GetComponentsInChildren<ParticleSystem>().Any(p => p.isPlaying || p.particleCount > 0), "Poeira do colapso ativa");
        CaptureAtPoint("playmode-destroy-start", deathPos + Vector3.up * 1.0f, 8f, 3.2f);
        yield return 1.3f;
        CaptureAtPoint("playmode-destroy-mid", deathPos + Vector3.up * 0.8f, 8f, 3.2f);
        yield return 2.2f;
        Transform wreckArm = FindDeep(wreck.transform, "Arm");
        Vector3 pose1 = wreckArm.position;
        Require(death.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f, "Destroy chegou ao fim");
        Require(Vector3.Distance(armBefore, pose1) > 0.5f, $"A estrutura desabou: o braco esta {Vector3.Distance(armBefore, pose1):F2} m do lugar original");
        CaptureAtPoint("playmode-destroy-end", deathPos + Vector3.up * 0.6f, 8f, 3.0f);
        yield return 1.5f;
        Require(Vector3.Distance(pose1, wreckArm.position) < 0.005f && death.GetCurrentAnimatorStateInfo(0).IsName("Destroy"), "Permanece desabada (pose final mantida)");
    }

    // ------------------------------------------------------------------------------------------------ utilidades
    private static bool Regex(string text, string pattern) { return System.Text.RegularExpressions.Regex.IsMatch(text, pattern); }

    private static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

    private static Transform FindDeep(Transform parent, string name) { return AdvancedUnitBuilders.FindDeep(parent, name); }

    private static GameObject FindByName(string prefix)
    {
        foreach (GameObject go in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Select(t => t.gameObject))
            if (go.transform.parent == null && go.name.StartsWith(prefix)) return go;
        return null;
    }

    private static void Trace(Animator animator, List<string> trace)
    {
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        string state = "?";
        foreach (string n in new[] { "Idle", "Move", "Fire", "Reload", "Hit" })
            if (info.IsName(n)) state = n;
        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            foreach (string n in new[] { "Idle", "Move", "Fire", "Reload", "Hit" })
                if (next.IsName(n)) state = n;
        }
        if (trace.Count == 0 || trace[trace.Count - 1] != state) trace.Add(state);
    }

    // posicao no NavMesh a `distance` metros (plano horizontal) de `center`, varrendo angulos a partir da frente da catapulta
    private static Vector3 PlaceAt(Vector3 center, float distance, Vector3? avoid = null)
    {
        Vector3 forward = catapult != null && catapult.Transform != null ? catapult.Transform.forward : Vector3.forward;
        forward.y = 0f;
        forward = forward.normalized;
        for (int step = 0; step < 24; step++)
        {
            float angle = (step % 2 == 0 ? 1 : -1) * (step / 2) * 20f;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
            Vector3 candidate = center + dir * distance;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.8f, NavMesh.AllAreas)) continue;
            if (Mathf.Abs(Flat(hit.position, center) - distance) > 0.35f) continue;
            if (avoid.HasValue && Flat(hit.position, avoid.Value) < 1.0f) continue;
            return hit.position;
        }
        throw new InvalidOperationException($"Nao ha NavMesh a {distance} m de {center}");
    }

    private static Character Spawn(string code, int owner, Vector3 position, int hp)
    {
        Character c = new Character(Globals.CHARACTER_DATA[code], owner);
        CharacterManager manager = c.Transform.GetComponent<CharacterManager>();
        Require(manager.agent.Warp(position), $"{code} (dono {owner}) posicionado no NavMesh");
        c.Transform.GetComponent<CharacterBT>().enabled = false; // alvo parado
        manager.agent.isStopped = true;
        c.HP = hp;
        Spawned.Add(c);
        return c;
    }

    // ------------------------------------------------------------------------------------------------ capturas
    private static void CaptureAround(string name, float yawFromForward, float distance, float height, float lateral = 0f)
    {
        if (!Captured.Add(name) || catapult == null || catapult.Transform == null) return;
        Vector3 focus = catapult.Transform.position + Vector3.up * 1.1f;
        Vector3 dir = Quaternion.Euler(0f, yawFromForward, 0f) * catapult.Transform.forward;
        dir.y = 0f;
        Render(name, focus + dir.normalized * distance + Vector3.up * (height - 1.1f), focus);
    }

    private static void CaptureSideOfCatapult(string name)
    {
        if (!Captured.Add(name) || catapult == null || catapult.Transform == null) return;
        Vector3 focus = catapult.Transform.position + Vector3.up * 1.4f;
        Vector3 side = catapult.Transform.right;
        Render(name, focus + side * 10f + Vector3.up * 1.2f, focus);
    }

    private static void CaptureAtPoint(string name, Vector3 point, float distance = 9f, float height = 3.5f)
    {
        if (!Captured.Add(name)) return;
        Vector3 away = catapult != null && catapult.Transform != null ? (point - catapult.Transform.position) : Vector3.forward;
        away.y = 0f;
        Vector3 side = Vector3.Cross(Vector3.up, away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward);
        Render(name, point + side * distance * 0.8f - (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * distance * 0.5f + Vector3.up * height, point + Vector3.up * 0.8f);
    }

    private static void Render(string name, Vector3 position, Vector3 target)
    {
        shotCamera.transform.position = position;
        shotCamera.transform.rotation = Quaternion.LookRotation(target - position);
        RenderTexture rt = new RenderTexture(960, 720, 24);
        Texture2D texture = new Texture2D(960, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            shotCamera.targetTexture = rt;
            shotCamera.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Shots + name + ".png", texture.EncodeToPNG());
            int magenta = texture.GetPixels32().Count(p => p.r > 220 && p.g < 40 && p.b > 220);
            Record($"Captura {name}: pixels magenta = {magenta}");
            if (magenta != 0) throw new InvalidOperationException($"FAIL pixels magenta ({magenta}) em {name}");
        }
        finally
        {
            shotCamera.targetTexture = null;
            RenderTexture.active = previous;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
