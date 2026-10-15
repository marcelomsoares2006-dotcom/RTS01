using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Crossbow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Play Mode no jogo real (Core.unity -> menu -> Map1) com a Balestra criada pelo catalogo (Globals.CHARACTER_DATA).
/// Verifica: locomocao (Speed/Move), recusa de disparo alem do alcance maximo, cooldown, virote em voo reto saindo do
/// BoltSpawn, dano perfurante esperado (armadura 0), nenhum dano em aliados, efeitos de disparo e impacto, sequencia
/// Fire -> Reload -> Idle, reacao Damaged -> Hit, troca de LOD pelo zoom e destruicao com o prefab de morte.
/// Renders de frente/costas/lado/disparo/impacto sem pixels magenta e zero erros no Console.
/// Batch: -executeMethod CrossbowPlayModeCheck.Run (sai com 0 se passar). Resultado: TestResults/crossbow-playmode.txt
/// </summary>
[InitializeOnLoad]
public static class CrossbowPlayModeCheck
{
    private const string Prefix = "EraImperial.CrossbowCheck.";
    private const string ReportFile = "TestResults/crossbow-playmode.txt";
    private const string Shots = "Docs/CharacterMeshAudit/Crossbow/";
    private static IEnumerator routine;
    private static double nextStep;
    private static Camera shotCamera;
    private static Character crossbow;
    private static CharacterManager crossbowManager;
    private static ProjectileAttackController attack;
    private static Animator animator;
    private static Character enemy;
    private static Character ally;
    private static Character soldier;
    private static readonly HashSet<string> Captured = new HashSet<string>();
    private static int magentaTotal;

    static CrossbowPlayModeCheck()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    public static void Run()
    {
        Directory.CreateDirectory("TestResults");
        Directory.CreateDirectory(Shots);
        File.WriteAllText(ReportFile, "Unity " + Application.unityVersion + "\n");
        SessionState.SetBool(Prefix + "Running", true);
        SessionState.SetInt(Prefix + "Errors", 0);
        SessionState.SetFloat(Prefix + "Deadline", (float)EditorApplication.timeSinceStartup + 480f);
        SessionState.SetString(Prefix + "DataPath", Path.GetFullPath("TestResults/CrossbowData-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void IsolateSaves()
    {
        if (SessionState.GetBool(Prefix + "Running", false))
            BinarySerializable.DATA_DIRECTORY = SessionState.GetString(Prefix + "DataPath", "TestResults/CrossbowData");
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
        Debug.Log("[Crossbow check] " + message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FALHOU: " + message);
        Record("PASS " + message);
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Prefix + "Running", false)) return;
        try
        {
            if (SessionState.GetInt(Prefix + "Errors", 0) > 0) throw new InvalidOperationException("Erros de runtime registrados; abortando.");
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Prefix + "Deadline", 0)) throw new TimeoutException("Excedeu o tempo limite.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (EditorApplication.timeSinceStartup < nextStep) return;
            IsolateSaves();
            if (routine == null) routine = Scenario();
            if (!routine.MoveNext()) { routine = null; return; }
            nextStep = EditorApplication.timeSinceStartup + Convert.ToDouble(routine.Current);
        }
        catch (Exception exception)
        {
            Record("FAILED: " + exception);
            SessionState.SetBool(Prefix + "Running", false);
            EditorApplication.Exit(1);
        }
    }

    // ----------------------------------------------------------------------------------------------- helpers
    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    private static Vector3 PositionAhead(Vector3 origin, Vector3 forward, float distance, out bool ok)
    {
        foreach (float angle in new[] { 0f, 25f, -25f, 50f, -50f, 90f, -90f, 140f, 180f })
        {
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
            if (NavMesh.SamplePosition(origin + dir * distance, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)
                && Mathf.Abs(Vector3.Distance(Flat(hit.position), Flat(origin)) - distance) < 1f)
            {
                ok = true;
                return hit.position;
            }
        }
        ok = false;
        return origin;
    }

    private static Character SpawnDummy(string code, int owner, Vector3 position, int hp)
    {
        Character c = new Character(Globals.CHARACTER_DATA[code], owner);
        CharacterManager m = c.Transform.GetComponent<CharacterManager>();
        Require(m.agent.Warp(position), $"{code} posicionado no NavMesh");
        c.Transform.GetComponent<CharacterBT>().enabled = false;
        m.agent.isStopped = true;
        c.HP = hp;
        return c;
    }

    private static string StateName(Animator a, int layer = 0)
    {
        AnimatorStateInfo info = a.GetCurrentAnimatorStateInfo(layer);
        foreach (string name in new[] { "Idle", "Move", "Fire", "Reload", "Hit", "Empty" })
            if (info.IsName(name)) return name;
        return "?";
    }

    private static void Capture(string name, Vector3 focusOffset, Vector3 direction, float distance)
    {
        if (!Captured.Add(name) || crossbow == null || crossbow.Transform == null) return;
        Vector3 focus = crossbow.Transform.position + focusOffset;
        Vector3 dir = crossbow.Transform.TransformDirection(direction).normalized;
        shotCamera.transform.position = focus + dir * distance;
        shotCamera.transform.rotation = Quaternion.LookRotation(focus - shotCamera.transform.position);
        RenderTexture target = new RenderTexture(960, 720, 24);
        Texture2D texture = new Texture2D(960, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        List<Renderer> hidden = HideOccluders(shotCamera.transform.position, focus);
        try
        {
            shotCamera.targetTexture = target;
            shotCamera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Shots + "playmode-" + name + ".png", texture.EncodeToPNG());
            int magenta = texture.GetPixels32().Count(p => p.r > 220 && p.g < 40 && p.b > 220);
            magentaTotal += magenta;
            Record($"Captura {name}: {magenta} pixels magenta");
            if (magenta != 0) throw new InvalidOperationException("Pixels magenta (shader com erro) em " + name + ": " + magenta);
        }
        finally
        {
            foreach (Renderer r in hidden) if (r != null) r.enabled = true;
            foreach (Terrain t in Terrain.activeTerrains) t.drawTreesAndFoliage = true;
            shotCamera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    // esconde, so durante a captura, arvores/folhagem do terreno e renderers que ficam entre a camera e a unidade
    private static List<Renderer> HideOccluders(Vector3 camera, Vector3 focus)
    {
        foreach (Terrain t in Terrain.activeTerrains) t.drawTreesAndFoliage = false;
        List<Renderer> hidden = new List<Renderer>();
        foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled || r is ParticleSystemRenderer || r.GetComponentInParent<UnitManager>() != null) continue;
            Bounds b = r.bounds;
            if (b.size.y < 0.4f || Vector3.Distance(b.center, focus) > 14f) continue;
            if (b.IntersectRay(new Ray(camera, (focus - camera).normalized), out float dist) && dist < Vector3.Distance(camera, focus) - 0.5f)
            {
                r.enabled = false;
                hidden.Add(r);
            }
        }
        return hidden;
    }

    private static void Shoot(string tag)
    {
        // quatro vistas: frente, costas, lado (esquerda do jogador) e tres-quartos; relativas a unidade (+Z = frente)
        Capture(tag + "-front", new Vector3(0f, 0.8f, 0f), new Vector3(0.15f, 0.25f, 1f), 7f);
    }

    // ----------------------------------------------------------------------------------------------- cenario
    private static IEnumerator Scenario()
    {
        // 1. abrir partida
        MainMenuManager menu = null;
        for (int i = 0; i < 400 && (menu == null || menu.newGameScrollview.childCount == 0); i++)
        {
            menu = UnityEngine.Object.FindAnyObjectByType<MainMenuManager>();
            yield return 0.05f;
        }
        Require(menu != null, "Menu principal carregado");
        MapData map = Resources.Load<MapData>("ScriptableObjects/Maps/Map1");
        Require(map != null, "Map1 encontrado");
        Transform picker = menu.newGameScrollview.Cast<Transform>().First(item => item.Find("Data/Name").GetComponent<Text>().text == map.mapName);
        picker.GetComponent<Button>().onClick.Invoke();
        menu.StartNewGame();
        Record("Partida nova em Map1 iniciada");
        yield return 8f;
        for (int i = 0; i < 200 && (GameManager.instance == null || Camera.main == null); i++) yield return 0.1f;

        // 2. criar a Balestra pelo catalogo
        Require(Globals.CHARACTER_DATA.ContainsKey("crossbow"), "Globals.CHARACTER_DATA contem 'crossbow'");
        Transform spawnpoints = GameObject.Find("Spawnpoints").transform;
        Require(NavMesh.SamplePosition(spawnpoints.GetChild(0).position, out NavMeshHit startHit, 20, NavMesh.AllAreas), "Ponto inicial no NavMesh");
        int me = GameManager.instance.gamePlayersParameters.myPlayerId;
        int other = me == 0 ? 1 : 0;
        crossbow = new Character(Globals.CHARACTER_DATA["crossbow"], me);
        crossbowManager = crossbow.Transform.GetComponent<CharacterManager>();
        attack = crossbow.Transform.GetComponent<ProjectileAttackController>();
        Require(attack != null && crossbowManager != null, "Balestra instanciada com CharacterManager e ProjectileAttackController");
        Require(crossbowManager.agent.Warp(startHit.position), "Balestra posicionada no NavMesh");
        animator = attack.animator;
        Require(animator != null && crossbow.Transform.Find("Mesh/CrossbowVisual") != null, "Visual da Balestra presente sob Mesh");
        Require(crossbow.HP == 28 && crossbow.AttackDamage == 22, $"Estatisticas da era Ancient aplicadas (PV {crossbow.HP}, dano {crossbow.AttackDamage})");
        CrossbowFireEffects fx = crossbow.Transform.GetComponentInChildren<CrossbowFireEffects>();
        Require(fx != null && fx.muzzle != null, "CrossbowFireEffects com efeito de disparo");

        shotCamera = new GameObject("CrossbowShotCamera").AddComponent<Camera>();
        shotCamera.fieldOfView = 32f;
        shotCamera.clearFlags = CameraClearFlags.Skybox;
        shotCamera.enabled = false;
        yield return 0.5f;

        // 3. locomocao
        Vector3 start = crossbow.Transform.position;
        Vector3 direction = Flat(spawnpoints.GetChild(1).position - start).normalized;
        Vector3 walkTarget = PositionAhead(start, direction, 10f, out bool walkOk);
        Require(walkOk && crossbowManager.MoveTo(walkTarget, false), "Comando de movimento aceito");
        float seenSpeed = 0f; bool seenMove = false; Quaternion wheelStart = Quaternion.identity; float maxWheelTurn = 0f;
        Transform wheel = crossbow.Transform.GetComponentsInChildren<Transform>().First(t => t.name == "Wheel_L");
        wheelStart = wheel.localRotation;
        float t0 = Time.time;
        while (Time.time - t0 < 4f)
        {
            seenSpeed = Mathf.Max(seenSpeed, animator.GetFloat("Speed"));
            seenMove |= StateName(animator) == "Move";
            maxWheelTurn = Mathf.Max(maxWheelTurn, Quaternion.Angle(wheelStart, wheel.localRotation));
            yield return 0f;
        }
        Require(Vector3.Distance(start, crossbow.Transform.position) > 3f, $"Balestra andou {Vector3.Distance(start, crossbow.Transform.position):F1} m pelo NavMesh");
        Require(seenSpeed > 1.0f && seenMove, $"Speed do Animator = {seenSpeed:F2} e estado Move ativo");
        Require(maxWheelTurn > 30f, $"Rodas giram durante o movimento ({maxWheelTurn:F0} graus)");
        Capture("move", new Vector3(0f, 0.8f, 0f), new Vector3(-1f, 0.35f, 0.8f), 7f);
        for (int i = 0; i < 100 && crossbowManager.agent.remainingDistance > 0.6f; i++) yield return 0.1f;
        crossbowManager.agent.isStopped = true;
        crossbow.Transform.GetComponent<CharacterBT>().enabled = false;   // a partir daqui o ataque e comandado diretamente
        yield return 1.0f;
        Require(StateName(animator) == "Idle", "Parada: estado Idle (corda armada, virote carregado)");

        // soldado de referencia de escala (parado, sem BT) ao lado
        bool refOk;
        Vector3 refPos = PositionAhead(crossbow.Transform.position + crossbow.Transform.right * 2.2f, crossbow.Transform.right, 0.01f, out refOk);
        if (!refOk) refPos = crossbow.Transform.position + crossbow.Transform.right * 2.2f;
        soldier = SpawnDummy("soldier", me, refPos, 100);
        yield return 0.3f;
        Capture("front", new Vector3(0f, 0.8f, 0f), new Vector3(0.12f, 0.45f, 1f), 7.5f);
        Capture("back", new Vector3(0f, 0.8f, 0f), new Vector3(-0.12f, 0.45f, -1f), 7.5f);
        Capture("side", new Vector3(0f, 0.8f, 0f), new Vector3(-1f, 0.2f, 0f), 7.5f);
        Capture("iso", new Vector3(0f, 0.7f, 0f), new Vector3(-0.8f, 0.7f, 0.9f), 7.5f);
        Require(soldier.Transform.GetComponentInChildren<Renderer>() != null, "Soldado de referencia de escala visivel nos renders");

        // 4. nao dispara alem do alcance maximo
        Require(attack.contract.TryGetTier(EraImperial.Units.Advanced.ImperialEra.Ancient, out IEraTier tier), "Tier Ancient do contrato");
        float range = tier.AttackRange, cooldown = tier.AttackCooldown;
        Vector3 forward = Flat(crossbow.Transform.forward).normalized;
        Vector3 farPos = PositionAhead(crossbow.Transform.position, forward, range + 3.5f, out bool farOk);
        Require(farOk, $"Posicao alvo alem do alcance ({range + 3.5f:F1} m) disponivel no NavMesh");
        enemy = SpawnDummy("worker", other, farPos, 1000);
        UnitManager enemyManager = enemy.Transform.GetComponent<UnitManager>();
        float distance = AdvancedCombat.FlatDistance(crossbow.Transform.position, enemy.Transform.position);
        float effective = distance - AdvancedCombat.TargetSize(enemyManager);
        Require(effective > range, $"Alvo a {distance:F1} m (efetivo {effective:F1}) acima do alcance maximo {range:F1}");
        attack.TryHandleAttack(enemy.Transform);
        yield return 1.5f;
        Require(attack.ShotsFired == 0 && !attack.IsFiring && AdvancedProjectile.ActiveCount == 0 && enemy.HP == 1000 && StateName(animator) != "Fire",
            "Alem do alcance maximo: nenhum disparo, nenhum virote, nenhum dano, sem animacao Fire");

        // 5. dentro do alcance: dispara apos releaseDelay; efeitos; voo reto; dano
        bool nearOk;
        Vector3 nearPos = PositionAhead(crossbow.Transform.position, forward, 9f, out nearOk);
        Require(nearOk, "Posicao dentro do alcance disponivel");
        enemy.Transform.GetComponent<CharacterManager>().agent.Warp(nearPos);
        Vector3 allyPos = Vector3.Lerp(crossbow.Transform.position, nearPos, 0.5f) + crossbow.Transform.right * 0.0f;
        if (NavMesh.SamplePosition(allyPos, out NavMeshHit allyHit, 2f, NavMesh.AllAreas)) allyPos = allyHit.position;
        ally = SpawnDummy("worker", me, allyPos, 400);       // aliado na linha de tiro
        int allyHp = ally.HP;
        yield return 0.3f;
        int hpBefore = enemy.HP;
        List<string> trace = new List<string>();
        float shotStart = Time.time;
        int releasesBefore = fx.Releases;
        int impactsBefore = AdvancedProjectile.ImpactCount;
        attack.TryHandleAttack(enemy.Transform);
        Require(attack.IsFiring, "Em alcance e recarregada: o disparo foi iniciado");
        List<Vector3> flight = new List<Vector3>();
        List<float> flightTimes = new List<float>();
        float releaseSeenAt = -1f, shotCountAt = -1f;
        Vector3 spawnWorld = attack.projectileSpawn.position;
        bool muzzleSeen = false, impactSeen = false, damagedDuringWindup = false;
        while (Time.time - shotStart < 2.7f)
        {
            string s = StateName(animator);
            if (trace.Count == 0 || trace[trace.Count - 1] != s) trace.Add(s);
            if (Time.time - shotStart < attack.releaseDelay - 0.05f && enemy.HP != hpBefore) damagedDuringWindup = true;
            if (fx.Releases > releasesBefore && releaseSeenAt < 0f) releaseSeenAt = Time.time;
            if (attack.ShotsFired > 0 && shotCountAt < 0f) shotCountAt = Time.time;
            if (fx.muzzle != null && (fx.muzzle.isPlaying || fx.muzzle.particleCount > 0) && fx.Releases > releasesBefore) muzzleSeen = true;
            AdvancedProjectile p = UnityEngine.Object.FindAnyObjectByType<AdvancedProjectile>();
            if (p != null) { flight.Add(p.transform.position); flightTimes.Add(Time.time); if (flight.Count == 3) Capture("fire", new Vector3(0f, 0.8f, 0f), new Vector3(-0.9f, 0.45f, 0.5f), 6.5f); }
            if (AdvancedProjectile.ImpactCount > impactsBefore && !impactSeen)
            {
                impactSeen = true;
                yield return 0.12f;
                GameObject impactFx = GameObject.Find("CrossbowImpact(Clone)");
                Require(impactFx != null && impactFx.GetComponentsInChildren<ParticleSystem>().Any(ps => ps.particleCount > 0), "Efeito de impacto apareceu com particulas vivas");
                Capture("impact", new Vector3(0f, 1.0f, 0f), new Vector3(-0.9f, 0.25f, 0.6f), 6.5f);
            }
            yield return 0f;
        }
        Require(!damagedDuringWindup, "Sem dano antes da soltura da corda (dano nao e instantaneo)");
        Require(attack.ShotsFired == 1, "Exatamente um disparo no ciclo (" + attack.ShotsFired + ")");
        Require(Mathf.Abs(shotCountAt - shotStart - attack.releaseDelay) < 0.15f, $"Virote sai apos o releaseDelay ({shotCountAt - shotStart:F2} s)");
        Require(releaseSeenAt > 0f && Mathf.Abs(releaseSeenAt - shotCountAt) < 0.2f, $"Evento de animacao CrossbowRelease coincide com a saida do virote ({releaseSeenAt - shotCountAt:+0.00;-0.00} s)");
        Require(muzzleSeen, "Efeito de disparo (fumaca/poeira) visivel apos a soltura");
        Require(flight.Count >= 5, $"Virote observado em voo ({flight.Count} amostras)");
        Vector3 a0 = flight.First(), a1 = flight.Last();
        float maxDeviation = flight.Max(pt => Vector3.Cross((pt - a0).normalized, (a1 - a0)).magnitude);
        Require(maxDeviation < 0.12f, $"Virote voa em linha reta (desvio maximo {maxDeviation:F3} m)");
        Require(Vector3.Distance(a0, spawnWorld) < 1.2f, $"Virote nasce na ponta do trilho (BoltSpawn a {Vector3.Distance(a0, spawnWorld):F2} m)");
        Require(Vector3.Dot((a1 - a0).normalized, Flat(nearPos - a0).normalized) > 0.9f, "Virote voa em direcao ao alvo");
        float speed = Vector3.Distance(a0, a1) / (flightTimes.Last() - flightTimes.First() + 1e-4f);
        Require(speed > 15f && speed < 60f, $"Velocidade do virote ~{speed:F0} m/s (contrato 30)");
        int expected = CrossbowAttackProfile.ComputeDamage((CrossbowEraTier)tier, 0f);
        Require(enemy.HP == hpBefore - expected && attack.LastDamage == expected, $"Dano perfurante esperado (armadura 0): {expected}; HP {hpBefore} -> {enemy.HP}");
        Require(ally.HP == allyHp, $"Aliado na linha de tiro nao sofreu dano (HP {ally.HP})");
        Require(impactSeen, "Impacto do virote ocorreu");
        Record("Sequencia de animacao: " + string.Join(" > ", trace));
        Require(trace.Contains("Fire") && trace.Contains("Reload") && trace.IndexOf("Fire") < trace.IndexOf("Reload") && trace.Last() == "Idle" && trace.IndexOf("Reload") < trace.LastIndexOf("Idle"),
            "Animator: Fire > Reload > Idle");

        // 6. cooldown
        float sinceShot = attack.SecondsSinceLastShot;
        Record($"Segundos desde o disparo: {sinceShot:F2}; cooldown {cooldown:F1}");
        if (sinceShot < cooldown - 0.2f)
        {
            attack.TryHandleAttack(enemy.Transform);
            yield return 0.4f;
            Require(attack.ShotsFired == 1 && !attack.IsFiring, "Em recarga (cooldown): tentativa de disparo recusada");
        }
        while (attack.SecondsSinceLastShot < cooldown - 0.6f) yield return 0.05f;
        attack.TryHandleAttack(enemy.Transform);
        Require(attack.ShotsFired == 1, "Ainda em cooldown 0,3 s antes do fim: recusado");
        while (attack.SecondsSinceLastShot < cooldown + 0.05f) yield return 0.05f;
        float secondGap = attack.SecondsSinceLastShot;
        attack.TryHandleAttack(enemy.Transform);
        Require(attack.IsFiring, $"Cooldown cumprido ({secondGap:F2} s >= {cooldown:F1} s): novo disparo aceito");
        yield return attack.releaseDelay + 1.2f;
        Require(attack.ShotsFired == 2 && enemy.HP == hpBefore - 2 * expected, $"Segundo virote: dano acumulado {2 * expected} (HP {enemy.HP})");

        // 7. disparo contra aliado nao causa dano
        while (attack.SecondsSinceLastShot < cooldown + 0.05f) yield return 0.05f;
        attack.TryHandleAttack(ally.Transform);
        yield return attack.releaseDelay + 1.5f;
        Require(attack.ShotsFired == 3 && ally.HP == allyHp, $"Virote contra aliado: nenhum dano (HP {ally.HP}/{allyHp})");

        // 8. reacao a dano: Damaged -> Hit (camada 1)
        while (StateName(animator) != "Idle") yield return 0.1f;
        int hpNow = crossbow.HP;
        crossbowManager.TakeHit(2);
        bool hitSeen = false;
        for (int i = 0; i < 20 && !hitSeen; i++) { hitSeen = StateName(animator, 1) == "Hit"; yield return 0.02f; }
        Require(crossbow.HP == hpNow - 2 && hitSeen, "Dano na balestra aciona Damaged e a camada Hit (tremida)");

        // 9. LOD pelo zoom da camera do jogo (ortografica, 8..26)
        IEnumerator lodStage = StageLod();
        while (lodStage.MoveNext()) yield return lodStage.Current;

        // 10. destruicao
        Vector3 deathPos = crossbow.Transform.position;
        int deathsBefore = AdvancedUnitDeathEffect.DeathsSpawned;
        crossbowManager.TakeHit(100000);
        yield return 0.4f;
        Require(AdvancedUnitDeathEffect.DeathsSpawned == deathsBefore + 1, "AdvancedUnitDeathEffect instanciou o prefab de morte");
        GameObject death = GameObject.Find("CrossbowDeath(Clone)");
        Require(death != null && Vector3.Distance(death.transform.position, deathPos) < 0.5f, "CrossbowDeath(Clone) na posicao da unidade");
        Animator da = death.GetComponent<Animator>();
        float n0 = da.GetCurrentAnimatorStateInfo(0).normalizedTime;
        Require(da.GetCurrentAnimatorStateInfo(0).IsName("Destroy"), "Prefab de morte toca o estado Destroy");
        yield return 0.5f;
        float n1 = da.GetCurrentAnimatorStateInfo(0).normalizedTime;
        Require(n1 > n0 + 0.1f, $"Animacao Destroy avanca ({n0:F2} -> {n1:F2})");
        Require(death.GetComponentsInChildren<ParticleSystem>().Any(ps => ps.isPlaying || ps.particleCount > 0), "Poeira da queda tocando");
        shotCamera.transform.position = deathPos + new Vector3(-4.2f, 3.2f, 5.0f);
        shotCamera.transform.rotation = Quaternion.LookRotation(deathPos + Vector3.up * 0.4f - shotCamera.transform.position);
        yield return 1.0f;
        CaptureAt("destroy", deathPos);
        yield return 1.0f;
        Require(SessionState.GetInt(Prefix + "Errors", 0) == 0, "0 erros de runtime no Console");
        Require(magentaTotal == 0, "Pixels magenta em todas as capturas = 0");
        Record("CROSSBOW_PLAYMODE_PASSED");
        SessionState.SetBool(Prefix + "Running", false);
        EditorApplication.Exit(0);
    }

    private static void CaptureAt(string name, Vector3 focusPoint)
    {
        RenderTexture target = new RenderTexture(960, 720, 24);
        Texture2D texture = new Texture2D(960, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            shotCamera.targetTexture = target;
            shotCamera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
            File.WriteAllBytes(Shots + "playmode-" + name + ".png", texture.EncodeToPNG());
            int magenta = texture.GetPixels32().Count(p => p.r > 220 && p.g < 40 && p.b > 220);
            magentaTotal += magenta;
            Record($"Captura {name}: {magenta} pixels magenta");
        }
        finally
        {
            shotCamera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    // cada LOD recebe uma cor chapada; a camera de teste renderiza em 3 zooms e a cor dominante diz qual nivel o motor desenhou
    private static IEnumerator StageLod()
    {
        LODGroup group = crossbow.Transform.GetComponentInChildren<LODGroup>();
        Require(group != null && group.lodCount == 3, "LODGroup presente na unidade em jogo");
        Renderer[][] levels = group.GetLODs().Select(l => l.renderers).ToArray();
        Require(levels.All(l => l.Length > 0) && levels[0].Length >= 8 && levels[1].Length == 1 && levels[2].Length == 1, $"Renderers por LOD: {levels[0].Length}/{levels[1].Length}/{levels[2].Length}");
        Color[] colours = { new Color(1f, 1f, 0f), new Color(0f, 1f, 0f), new Color(1f, 0f, 1f) };
        for (int i = 0; i < 3; i++)
            foreach (Renderer r in levels[i])
                r.sharedMaterials = r.sharedMaterials.Select(_ => new Material(Shader.Find("Unlit/Color")) { color = colours[i] }).ToArray();
        shotCamera.clearFlags = CameraClearFlags.SolidColor;
        shotCamera.backgroundColor = Color.black;
        shotCamera.orthographic = true;
        shotCamera.nearClipPlane = 0.1f;
        shotCamera.farClipPlane = 200f;
        Vector3 focus = crossbow.Transform.position + Vector3.up * 0.75f;
        List<int> seen = new List<int>();
        foreach (float ortho in new[] { 8f, 18f, 26f })
        {
            shotCamera.orthographicSize = ortho;
            shotCamera.transform.position = focus + new Vector3(0.3f, 0.2f, -1f).normalized * 40f;
            shotCamera.transform.rotation = Quaternion.LookRotation(focus - shotCamera.transform.position);
            // Keep the unit large enough for the three LOD colors to be
            // measured reliably at the far zoom; the game camera remains
            // unchanged. This avoids classifying a visible 1-4 pixel island
            // as "no LOD" because of the old low-resolution audit buffer.
            const int renderWidth = 960;
            const int renderHeight = 720;
            RenderTexture target = new RenderTexture(renderWidth, renderHeight, 24);
            Texture2D texture = new Texture2D(renderWidth, renderHeight, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            shotCamera.targetTexture = target;
            shotCamera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, renderWidth, renderHeight), 0, 0);
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
            int best = Array.IndexOf(counts, counts.Max());
            if (counts.Max() < 5) best = -1;
            Record($"LOD em jogo, zoom ortografico {ortho:F0}: pixels LOD0/1/2 = {counts[0]}/{counts[1]}/{counts[2]} -> LOD{best}");
            seen.Add(best);
            yield return 0.05f;
        }
        Require(seen[0] == 0 && seen[1] == 1 && seen[2] == 2, "Troca de LOD pelo zoom da camera do jogo: 8 > LOD0, 18 > LOD1, 26 > LOD2");
        shotCamera.orthographic = false;
        shotCamera.clearFlags = CameraClearFlags.Skybox;
        shotCamera.fieldOfView = 32f;
    }
}
