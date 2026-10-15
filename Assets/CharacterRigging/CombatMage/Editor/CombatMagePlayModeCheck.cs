using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Mage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Play Mode no jogo real (Core.unity -> menu -> Map1): cria um CombatMage pelo catalogo (Globals.CHARACTER_DATA),
/// confere a progressao nas 4 eras (AdvancedEraState), a locomocao pelo NavMesh e o ataque magico contra um inimigo parado:
/// alcance e recarga respeitados, esfera em voo, dano igual ao da era aplicado so na chegada do projetil, reacao a dano,
/// morte (prefab Death) e depois o ataque autonomo pelo behavior tree. Capturas (frente/costas/lado/ataque/projetil/impacto/morte),
/// contagem de pixels magenta = 0 e zero erros de console.
/// Batch: -executeMethod CombatMagePlayModeCheck.Run  (sai com 0 se passar). Resultado: TestResults/combatmage-playmode.txt
/// </summary>
[InitializeOnLoad]
public static class CombatMagePlayModeCheck
{
    private const string Prefix = "EraImperial.CombatMageCheck.";
    private static string ReportFile { get { return SessionState.GetString(Prefix + "Report", "TestResults/combatmage-playmode.txt"); } }
    private static string Suffix { get { return SessionState.GetString(Prefix + "Suffix", ""); } }
    private const string Shots = "Docs/CharacterMeshAudit/CombatMage/";
    private static double nextStep;
    private static Character mage;
    private static Character near, far;
    private static CharacterManager mageManager;
    private static ProjectileAttackController attack;
    private static AdvancedUnitAnimatorDriver driver;
    private static Camera shotCamera;
    private static Vector3 start, walkTarget;
    private static CombatMageContract contract;
    private static float stageStarted;
    private static readonly HashSet<string> Captured = new HashSet<string>();
    private static readonly HashSet<string> Passed = new HashSet<string>();
    private static int sub;
    private static float marker;
    private static int hpMark, shotsMark;
    private static readonly List<float> ShotTimes = new List<float>();
    private static int lastShots;
    private static float minDistance = 999f;
    private static int btShotsStart;

    static CombatMagePlayModeCheck()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    public static void Run() { RunMap("Map1", "TestResults/combatmage-playmode.txt", ""); }

    /// <summary>Mesma bateria no Map2 (terreno em outra altura); relatorio TestResults/combatmage-playmode-map2.txt.</summary>
    public static void RunMap2() { RunMap("Map2", "TestResults/combatmage-playmode-map2.txt", "-map2"); }

    private static void RunMap(string mapName, string report, string suffix)
    {
        SessionState.SetString(Prefix + "Map", mapName);
        SessionState.SetString(Prefix + "Report", report);
        SessionState.SetString(Prefix + "Suffix", suffix);
        Directory.CreateDirectory("TestResults");
        Directory.CreateDirectory(Shots);
        File.WriteAllText(ReportFile, "Unity " + Application.unityVersion + "\n");
        SessionState.SetBool(Prefix + "Running", true);
        SessionState.SetInt(Prefix + "Stage", 0);
        SessionState.SetInt(Prefix + "Errors", 0);
        SessionState.SetFloat(Prefix + "Deadline", (float)EditorApplication.timeSinceStartup + 600f);
        SessionState.SetString(Prefix + "DataPath", Path.GetFullPath("TestResults/CombatMageData-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void IsolateSaves()
    {
        if (SessionState.GetBool(Prefix + "Running", false))
            BinarySerializable.DATA_DIRECTORY = SessionState.GetString(Prefix + "DataPath", "TestResults/CombatMageData");
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
        Debug.Log("[CombatMage check] " + message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Record("PASS " + message);
    }

    private static void RequireOnce(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        if (Passed.Add(message)) Record("PASS " + message);
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
            int stage = SessionState.GetInt(Prefix + "Stage", 0);
            switch (stage)
            {
                case 0: StageOpenMatch(); break;
                case 1: StageSpawn(); break;
                case 2: StageEras(); break;
                case 3: StageWalking(); break;
                case 4: StageSpawnEnemies(); break;
                case 5: StageGates(); break;
                case 6: StageImperialShot(); break;
                case 7: StageBehaviourTree(); break;
                case 8: StageHitReaction(); break;
                case 9: StageDeath(); break;
                case 10: StageLod(); break;
            }
        }
        catch (Exception exception)
        {
            Record("FAILED: " + exception);
            SessionState.SetBool(Prefix + "Running", false);
            EditorApplication.Exit(1);
        }
    }

    private static void Next(int stage, double delay)
    {
        SessionState.SetInt(Prefix + "Stage", stage);
        nextStep = EditorApplication.timeSinceStartup + delay;
        stageStarted = Time.time;
        sub = 0;
    }

    private static CharacterBT BT(Character c) { return c.Transform.GetComponent<CharacterBT>(); }
    private static AnimatorStateInfo StateInfo { get { return attack.animator.GetCurrentAnimatorStateInfo(0); } }

    // ---------------------------------------------------------------------------------------------- stages
    private static void StageOpenMatch()
    {
        MainMenuManager menu = UnityEngine.Object.FindAnyObjectByType<MainMenuManager>();
        if (menu == null || menu.newGameScrollview.childCount == 0) return;
        MapData map = Resources.Load<MapData>("ScriptableObjects/Maps/" + SessionState.GetString(Prefix + "Map", "Map1"));
        Require(map != null, SessionState.GetString(Prefix + "Map", "Map1") + " encontrado");
        Transform picker = menu.newGameScrollview.Cast<Transform>().First(item => item.Find("Data/Name").GetComponent<Text>().text == map.mapName);
        picker.GetComponent<Button>().onClick.Invoke();
        menu.StartNewGame();
        Record("Partida nova iniciada: " + map.mapName);
        Next(1, 8);
    }

    private static void StageSpawn()
    {
        if (GameManager.instance == null || Camera.main == null) return;
        contract = AssetDatabase.LoadAssetAtPath<CombatMageContract>(AdvancedUnitBuilders.DataRoot + "/CombatMage.asset");
        Require(AdvancedEraState.Current == ImperialEra.Ancient, "Era inicial = Ancient");
        Require(Globals.CHARACTER_DATA.ContainsKey("combat_mage"), "Globals.CHARACTER_DATA contem 'combat_mage'");
        var spawnpoints = GameObject.Find("Spawnpoints").transform;
        Require(NavMesh.SamplePosition(spawnpoints.GetChild(0).position, out NavMeshHit hit, 20, NavMesh.AllAreas), "Ponto inicial no NavMesh");
        start = hit.position;
        int me = GameManager.instance.gamePlayersParameters.myPlayerId;
        mage = new Character(Globals.CHARACTER_DATA["combat_mage"], me);
        mageManager = mage.Transform.GetComponent<CharacterManager>();
        attack = mage.Transform.GetComponent<ProjectileAttackController>();
        driver = mage.Transform.GetComponent<AdvancedUnitAnimatorDriver>();
        Require(attack != null && driver != null && mageManager != null && mage.Transform.GetComponent<AdvancedUnitEraBinding>() != null &&
                mage.Transform.GetComponent<AdvancedUnitDeathEffect>() != null, "CombatMage instanciado com CharacterManager e componentes avancados");
        Require(mageManager.agent.Warp(start), "CombatMage posicionado no NavMesh");
        Require(mage.Transform.Find("Mesh/CombatMageVisual") != null, "Visual do CombatMage presente sob Mesh");
        Require(attack.projectileSpawn != null && attack.projectileSpawn.IsChildOf(mage.Transform), "projectileSpawn (CastPoint) pertence a unidade instanciada");

        shotCamera = new GameObject("CombatMageShotCamera").AddComponent<Camera>();
        shotCamera.fieldOfView = 35f;
        shotCamera.clearFlags = CameraClearFlags.Skybox;
        shotCamera.enabled = false;
        Next(2, 0.5);
    }

    // progressao nas 4 eras: troca AdvancedEraState e confere Unit (vida maxima, dano, alcance) contra o tier do contrato
    private static void StageEras()
    {
        var tiers = contract.GetTiers();
        BT(mage).enabled = false;
        mageManager.agent.isStopped = true;
        foreach (ImperialEra era in new[] { ImperialEra.Ancient, ImperialEra.Classical, ImperialEra.Medieval, ImperialEra.Imperial })
        {
            AdvancedEraState.Set(era);
            contract.TryGetTier(era, out IEraTier tier);
            Require(mage.MaxHP == tier.HealthPoints && mage.AttackDamage == tier.AttackDamage && Mathf.Approximately(mage.AttackRange, tier.AttackRange),
                $"Era {era}: Unit vida {mage.MaxHP}, dano {mage.AttackDamage}, alcance {mage.AttackRange} = tier ({tier.HealthPoints}/{tier.AttackDamage}/{tier.AttackRange})");
            Require(mage.Transform.GetComponent<AdvancedUnitEraBinding>().AppliedEra == era, $"Binding aplicou a era {era}");
        }
        AdvancedEraState.Set(ImperialEra.Ancient);
        Require(mage.MaxHP == 22 && mage.AttackDamage == 18, "Era restaurada para Ancient (vida 22, dano 18)");
        CaptureAngles("idle");
        Next(3, 0.2);
    }

    private static void StageWalking()
    {
        if (sub == 0)
        {
            mageManager.agent.isStopped = false;
            var spawnpoints = GameObject.Find("Spawnpoints").transform;
            Vector3 direction = (spawnpoints.GetChild(1).position - start).normalized; direction.y = 0f;
            Require(NavMesh.SamplePosition(start + direction * 10f, out NavMeshHit far10, 15, NavMesh.AllAreas), "Destino de caminhada no NavMesh");
            walkTarget = far10.position;
            Require(mageManager.MoveTo(walkTarget, false), "Comando de movimento aceito");
            sub = 1;
            nextStep = EditorApplication.timeSinceStartup + 3;
            return;
        }
        float moved = Vector3.Distance(start, mage.Transform.position);
        float speed = attack.animator.GetFloat(AdvancedUnitAnimatorDriver.SpeedHash);
        Require(moved > 2f, $"CombatMage andou {moved:F1} m pelo NavMesh");
        Require(speed > 0.8f, $"Parametro Speed do Animator = {speed:F2} (locomocao ativa)");
        Capture("playmode-walk");
        if (mageManager.agent.remainingDistance > 1f && Time.time - stageStarted < 8f) { nextStep = EditorApplication.timeSinceStartup + 1; return; }
        Next(4, 0.8);
    }

    private static void StageSpawnEnemies()
    {
        BT(mage).enabled = false;
        mageManager.agent.isStopped = true;
        mageManager.agent.ResetPath();
        Vector3 forward = mage.Transform.forward.sqrMagnitude > 0.1f ? mage.Transform.forward : Vector3.forward; forward.y = 0f; forward.Normalize();
        int me = GameManager.instance.gamePlayersParameters.myPlayerId;
        int other = me == 0 ? 1 : 0;
        near = SpawnEnemy(mage.Transform.position + Quaternion.Euler(0f, 35f, 0f) * forward * 6f, other, "perto");
        far = SpawnEnemy(mage.Transform.position - Quaternion.Euler(0f, 35f, 0f) * forward * 14f, other, "longe");
        float dn = Vector3.Distance(mage.Transform.position, near.Transform.position), df = Vector3.Distance(mage.Transform.position, far.Transform.position);
        Record($"Inimigo perto a {dn:F1} m, longe a {df:F1} m; alcance Ancient {mage.AttackRange} m; HP inimigos {near.HP}");
        Require(dn > 3f && dn < mage.AttackRange - 0.5f, "Inimigo proximo fora do corpo a corpo e dentro do alcance de conjuracao");
        Require(df > mage.AttackRange + 3f, "Inimigo distante fora do alcance de conjuracao");
        Next(5, 0.3);
    }

    private static Character SpawnEnemy(Vector3 desired, int owner, string label)
    {
        Require(NavMesh.SamplePosition(desired, out NavMeshHit hit, 12, NavMesh.AllAreas), "Posicao do inimigo (" + label + ") no NavMesh");
        var enemy = new Character(Globals.CHARACTER_DATA["worker"], owner);
        var manager = enemy.Transform.GetComponent<CharacterManager>();
        Require(manager.agent.Warp(hit.position), "Inimigo (" + label + ") posicionado no NavMesh");
        enemy.Transform.GetComponent<CharacterBT>().enabled = false;   // alvo parado
        manager.agent.isStopped = true;
        enemy.HP = 5000;
        return enemy;
    }

    private static void TrackShots()
    {
        if (attack.ShotsFired != lastShots)
        {
            lastShots = attack.ShotsFired;
            ShotTimes.Add(Time.time);
        }
    }

    // alcance e recarga: chamadas diretas ao handler (o mesmo que UnitManager.Attack usa), behavior tree desligado
    private static void StageGates()
    {
        TrackShots();
        switch (sub)
        {
            case 0: // alvo fora do alcance: nada acontece
                hpMark = far.HP; shotsMark = attack.ShotsFired;
                Require(attack.TryHandleAttack(far.Transform), "Handler assume o ataque (nunca dano instantaneo)");
                marker = Time.time; sub = 1; break;
            case 1:
                if (Time.time - marker < 1.4f) return;
                Require(attack.ShotsFired == shotsMark && !attack.IsFiring && AdvancedProjectile.ActiveCount == 0 && far.HP == hpMark,
                    "Fora do alcance: nenhuma conjuracao, nenhum projetil e nenhum dano");
                Require(!StateInfo.IsName("Cast"), "Fora do alcance: Animator nao entra em Cast");
                hpMark = near.HP; shotsMark = attack.ShotsFired;
                attack.TryHandleAttack(near.Transform);
                marker = Time.time; sub = 2; break;
            case 2: // durante a conjuracao: sem dano; animacao Cast; virado ao alvo
                RequireOnce(near.HP == hpMark, "Sem dano durante a conjuracao (dano nao e instantaneo)");
                if (StateInfo.IsName("Cast") || attack.animator.IsInTransition(0))
                {
                    RequireOnce(attack.IsFiring || attack.ShotsFired > shotsMark, "Controlador em disparo enquanto o Animator toca Cast");
                    if (StateInfo.IsName("Cast") && StateInfo.normalizedTime > 0.2f) Capture("playmode-cast");
                }
                if (attack.ShotsFired > shotsMark && sub == 2)
                {
                    float release = Time.time - marker;
                    Record($"Esfera liberada {release:F2} s apos o inicio (releaseDelay {attack.releaseDelay:F2} s)");
                    Require(Mathf.Abs(release - attack.releaseDelay) < 0.25f, "Instante do disparo = releaseDelay");
                    Vector3 flat = near.Transform.position - mage.Transform.position; flat.y = 0f;
                    float yaw = Vector3.Angle(mage.Transform.forward, flat);
                    Require(yaw < 25f, $"Mago virado para o alvo no disparo (erro {yaw:F1} graus)");
                    Require(AdvancedProjectile.ActiveCount > 0, "Projetil em voo apos o disparo");
                    Require(near.HP == hpMark, "Projetil ainda em voo: alvo sem dano");
                    marker = Time.time; sub = 3;
                }
                else if (Time.time - marker > 3f) throw new TimeoutException("Conjuracao nao liberou o projetil.");
                break;
            case 3: // voo + impacto
                if (AdvancedProjectile.ActiveCount > 0)
                {
                    Capture("playmode-bolt");
                    if (Time.time - marker > 5f) throw new TimeoutException("Projetil nao chegou.");
                    return;
                }
                RequireOnce(near.HP < hpMark, "Dano aplicado so na chegada do projetil");
                int damage = hpMark - near.HP;
                Require(damage == mage.AttackDamage && damage == 18, $"Dano da esfera = dano Ancient ({damage})");
                Capture("playmode-impact");
                hpMark = near.HP; marker = Time.time; sub = 4;
                // segunda ordem logo depois do disparo: recarga ainda ativa
                attack.TryHandleAttack(near.Transform);
                break;
            case 4:
                if (Time.time - marker < 1.0f) return;
                Require(attack.ShotsFired == shotsMark + 1 && !attack.IsFiring && near.HP == hpMark, "Recarga respeitada: ordem durante a recarga (2,4 s) nao dispara");
                marker = Time.time; sub = 5; break;
            case 5:
                if (Time.time - marker < 1.6f) return; // total ~2,6 s desde o disparo
                attack.TryHandleAttack(near.Transform);
                marker = Time.time; sub = 6; break;
            case 6:
                if (attack.ShotsFired == shotsMark + 2)
                {
                    float interval = ShotTimes.Count >= 2 ? ShotTimes[ShotTimes.Count - 1] - ShotTimes[ShotTimes.Count - 2] : 0f;
                    Require(interval >= 2.4f, $"Intervalo entre disparos {interval:F2} s >= recarga Ancient 2,4 s");
                    sub = 7; marker = Time.time; return;
                }
                if (Time.time - marker > 3f) throw new TimeoutException("Segundo disparo apos a recarga nao ocorreu.");
                break;
            case 7:
                if (AdvancedProjectile.ActiveCount > 0) return;
                Require(near.HP == hpMark - 18, $"Segundo disparo causou 18 de dano (HP {hpMark} -> {near.HP})");
                Next(6, 2.5);   // aguarda o fim da recarga e do clipe
                return;
        }
        nextStep = EditorApplication.timeSinceStartup + 0.03;
    }

    // outra era: dano, alcance e recarga do tier Imperial no mesmo fluxo
    private static void StageImperialShot()
    {
        TrackShots();
        if (sub == 0)
        {
            AdvancedEraState.Set(ImperialEra.Imperial);
            contract.TryGetTier(ImperialEra.Imperial, out IEraTier tier);
            Require(mage.AttackDamage == 40 && Mathf.Approximately(mage.AttackRange, 11f), "Era Imperial: dano 40 e alcance 11");
            hpMark = near.HP; shotsMark = attack.ShotsFired;
            attack.TryHandleAttack(near.Transform);
            marker = Time.time; sub = 1;
        }
        else if (sub == 1)
        {
            if (AdvancedProjectile.ActiveCount > 0 || (attack.IsFiring)) { nextStep = EditorApplication.timeSinceStartup + 0.05; return; }
            if (Time.time - marker < 1.2f) { nextStep = EditorApplication.timeSinceStartup + 0.05; return; }
            Require(attack.ShotsFired == shotsMark + 1 && hpMark - near.HP == 40, $"Era Imperial: esfera causou 40 de dano (HP {hpMark} -> {near.HP})");
            // a era Imperial alcanca o inimigo distante (14 m > 11 m? nao): continua fora do alcance
            shotsMark = attack.ShotsFired;
            float imperialRange = mage.AttackRange;
            float df = Vector3.Distance(mage.Transform.position, far.Transform.position);
            Require(df > imperialRange, $"Inimigo distante ({df:F1} m) segue fora do alcance Imperial ({imperialRange} m)");
            AdvancedEraState.Set(ImperialEra.Ancient);
            Require(mage.AttackDamage == 18, "Era devolvida a Ancient");
            Next(7, 2.0);
        }
    }

    // ataque autonomo pelo behavior tree: o mago encara o inimigo, conjura de longe e repete respeitando a recarga
    private static void StageBehaviourTree()
    {
        TrackShots();
        if (sub == 0)
        {
            UnityEngine.Object.Destroy(far.Transform.gameObject);   // so o alvo proximo participa do teste autonomo
            BT(mage).enabled = true;
            mageManager.agent.isStopped = false;
            btShotsStart = attack.ShotsFired;
            hpMark = near.HP;
            minDistance = 999f;
            Record("Behavior tree do mago religada; alvo parado a " + Vector3.Distance(mage.Transform.position, near.Transform.position).ToString("F1") + " m");
            sub = 1;
        }
        // o inimigo distante nao deve ser ignorado: remove-o para o BT focar o proximo
        minDistance = Mathf.Min(minDistance, Vector3.Distance(mage.Transform.position, near.Transform.position));
        if (attack.ShotsFired >= btShotsStart + 2 && AdvancedProjectile.ActiveCount == 0 && near.HP <= hpMark - 36)
        {
            Require(minDistance > 3f, $"O mago atacou a distancia pelo behavior tree (distancia minima {minDistance:F1} m)");
            Require(near.HP == hpMark - 18 * (attack.ShotsFired - btShotsStart), $"Cada esfera do BT causou 18 de dano ({attack.ShotsFired - btShotsStart} esferas, HP {hpMark} -> {near.HP})");
            for (int i = btShotsStart > 0 ? 1 : 0; i < ShotTimes.Count - 1; i++) { }
            float interval = ShotTimes[ShotTimes.Count - 1] - ShotTimes[ShotTimes.Count - 2];
            Require(interval >= 2.3f, $"Recarga respeitada no ataque autonomo (intervalo {interval:F2} s)");
            BT(mage).enabled = false;
            mageManager.agent.isStopped = true;
            Capture("playmode-bt-cast");
            Next(8, 1.0);
            return;
        }
        if (Time.time - stageStarted > 50f)
            throw new TimeoutException($"Ataque autonomo nao concluiu em 50 s: esferas={attack.ShotsFired - btShotsStart}, HP alvo {near.HP}, distancia {minDistance:F1}");
        if (attack.animator.GetCurrentAnimatorStateInfo(0).IsName("Cast") && attack.animator.GetCurrentAnimatorStateInfo(0).normalizedTime > 0.3f) Capture("playmode-bt-cast");
        nextStep = EditorApplication.timeSinceStartup + 0.05;
    }

    private static void StageHitReaction()
    {
        if (sub == 0)
        {
            Require(!attack.IsFiring, "Sem conjuracao em curso antes do teste de dano");
            if (!StateInfo.IsName("Locomotion") || attack.animator.IsInTransition(0)) { nextStep = EditorApplication.timeSinceStartup + 0.1; return; }
            int before = driver.DamageReactions;
            int hp = mage.HP;
            mageManager.TakeHit(3);
            Require(mage.HP == hp - 3, "Mago recebeu 3 de dano");
            marker = Time.time; sub = 1;
            shotsMark = before;
            nextStep = EditorApplication.timeSinceStartup + 0.25;
            return;
        }
        bool inHit = StateInfo.IsName("Hit") || (attack.animator.IsInTransition(0) && attack.animator.GetNextAnimatorStateInfo(0).IsName("Hit"));
        if (inHit)
        {
            Require(driver.DamageReactions == shotsMark + 1, "AdvancedUnitAnimatorDriver registrou a reacao ao dano (trigger Damaged)");
            Require(true, "Animator entrou no estado Hit apos o dano");
            Capture("playmode-hit");
            Next(9, 1.4);
            return;
        }
        if (Time.time - marker > 2f) throw new TimeoutException("Animator nao entrou em Hit apos o dano.");
        nextStep = EditorApplication.timeSinceStartup + 0.03;
    }

    private static void StageDeath()
    {
        if (sub == 0)
        {
            Require(AdvancedUnitDeathEffect.DeathsSpawned == 0, "Nenhuma morte registrada antes do teste");
            mageManager.TakeHit(mage.HP + 1);
            marker = Time.time; sub = 1;
            nextStep = EditorApplication.timeSinceStartup + 2.2;
            return;
        }
        var deathObject = GameObject.Find("CombatMageDeath(Clone)");
        Require(deathObject != null && AdvancedUnitDeathEffect.DeathsSpawned == 1, "Prefab de morte instanciado no lugar da unidade (DeathsSpawned = 1)");
        var animator = deathObject.GetComponent<Animator>();
        Require(animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName("Death"), "Prefab de morte toca o estado Death sozinho");
        mage = null;
        if (sub == 1)
        {
            CaptureAt("playmode-death", deathObject.transform.position + Vector3.up * 0.8f);
            sub = 2;
            nextStep = EditorApplication.timeSinceStartup + 3.3;   // fim do clipe (5,77 s); o prefab vive 6 s
            return;
        }
        // fim do clipe: o corpo deitado nao pode ficar abaixo do terreno visivel
        Require(animator.GetCurrentAnimatorStateInfo(0).normalizedTime > 0.9f, "Clipe Death chegou ao fim (normalizedTime " + animator.GetCurrentAnimatorStateInfo(0).normalizedTime.ToString("F2") + ")");
        var grounding = deathObject.GetComponent<CombatMageDeathGrounding>();
        Require(grounding != null, "Prefab de morte tem CombatMageDeathGrounding (ajuste ao terreno " + grounding.LastAdjust.ToString("F2") + " m)");
        var smr = deathObject.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.EndsWith("LOD0"));
        var baked = new Mesh();
        smr.BakeMesh(baked, true);
        var toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
        float lowest = 99f, highest = -99f;
        foreach (var v in baked.vertices) { float y = toWorld.MultiplyPoint3x4(v).y; lowest = Mathf.Min(lowest, y); highest = Mathf.Max(highest, y); }
        UnityEngine.Object.Destroy(baked);
        Require(Physics.Raycast(deathObject.transform.position + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 8f, (1 << 8) | (1 << 10)), "Terreno sob o corpo encontrado por raio");
        Record($"Corpo no fim do Death: y minimo {lowest:F3}, maximo {highest:F3}, terreno {ground.point.y:F3}, origem do prefab {deathObject.transform.position.y:F3}");
        Require(lowest >= ground.point.y - 0.03f, "Corpo morto deitado sobre o terreno (nenhum vertice abaixo do chao)");
        Require(highest - lowest < 1.0f, $"Corpo deitado (altura total {highest - lowest:F2} m)");
        CaptureAt("playmode-death-end", deathObject.transform.position + Vector3.up * 0.3f);
        Next(10, 0.3);
    }

    // LOD em runtime: cada LOD do mago de teste e pintado de uma cor chapada e renderizado em tres zooms da camera ortografica do jogo
    private static void StageLod()
    {
        int me = GameManager.instance.gamePlayersParameters.myPlayerId;
        var lodMage = new Character(Globals.CHARACTER_DATA["combat_mage"], me);
        lodMage.Transform.GetComponent<CharacterManager>().agent.Warp(near.Transform.position + Vector3.right * 4f);
        lodMage.Transform.GetComponent<CharacterBT>().enabled = false;
        var group = lodMage.Transform.GetComponentInChildren<LODGroup>();
        Require(group != null && group.lodCount == 3, "LODGroup presente na unidade em jogo");
        var renderers = lodMage.Transform.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.Contains("_LOD")).OrderBy(r => r.name).ToArray();
        Require(renderers.Length == 3, "3 renderers de LOD na unidade em jogo");
        var colours = new[] { new Color(1f, 1f, 0f), new Color(0f, 1f, 0f), new Color(1f, 0f, 1f) };
        for (int i = 0; i < 3; i++)
            renderers[i].sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = colours[i] };
        foreach (var other in lodMage.Transform.GetComponentsInChildren<MeshRenderer>(true)) other.enabled = false; // cajado/anel nao entram na contagem
        shotCamera.clearFlags = CameraClearFlags.SolidColor;
        shotCamera.backgroundColor = Color.black;
        shotCamera.orthographic = true;
        shotCamera.nearClipPlane = 0.1f;
        shotCamera.farClipPlane = 200f;
        Vector3 focus = lodMage.Transform.position + Vector3.up * 0.9f;
        var seen = new List<int>();
        foreach (float size in new[] { 8f, 18f, 26f })
        {
            shotCamera.orthographicSize = size;
            shotCamera.transform.position = focus + new Vector3(0.3f, 0.2f, -1f).normalized * 40f;
            shotCamera.transform.rotation = Quaternion.LookRotation(focus - shotCamera.transform.position);
            const int renderWidth = 1920;
            const int renderHeight = 1440;
            var target = new RenderTexture(renderWidth, renderHeight, 24);
            var texture = new Texture2D(renderWidth, renderHeight, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            shotCamera.targetTexture = target;
            shotCamera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, renderWidth, renderHeight), 0, 0);
            RenderTexture.active = previous;
            shotCamera.targetTexture = null;
            var counts = new int[3];
            foreach (var p in texture.GetPixels32())
            {
                if (p.r > 200 && p.g > 200 && p.b < 60) counts[0]++;
                else if (p.r < 60 && p.g > 200 && p.b < 60) counts[1]++;
                else if (p.r > 200 && p.g < 60 && p.b > 200) counts[2]++;
            }
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(texture);
            int best = Array.IndexOf(counts, counts.Max());
            if (counts.Max() < 1) best = -1;
            Record($"LOD em jogo, zoom ortografico {size:F0}: pixels LOD0/1/2 = {counts[0]}/{counts[1]}/{counts[2]} -> LOD{best}");
            seen.Add(best);
        }
        Require(seen[0] == 0 && seen[1] == 1 && seen[2] == 2, "Troca de LOD pelo zoom da camera do jogo: 8 > LOD0, 18 > LOD1, 26 > LOD2");
        Require(SessionState.GetInt(Prefix + "Errors", 0) == 0, "0 erros de runtime no Console");
        Record("COMBATMAGE_PLAYMODE_PASSED");
        SessionState.SetBool(Prefix + "Running", false);
        EditorApplication.Exit(0);
    }

    // --------------------------------------------------------------------------------------------- capture
    private static void CaptureAngles(string tag)
    {
        Vector3 focus = mage.Transform.position + Vector3.up * 1.0f;
        Vector3 f = mage.Transform.forward; f.y = 0f; f = f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, f);
        CaptureView($"playmode-{tag}-front", focus, ClearPosition(focus, f));
        CaptureView($"playmode-{tag}-back", focus, ClearPosition(focus, -f));
        CaptureView($"playmode-{tag}-side", focus, ClearPosition(focus, -right));
    }

    // posicao de camera sem obstaculo entre ela e o alvo: tenta 4 m, 2,5 m e por cima
    private static Vector3 ClearPosition(Vector3 focus, Vector3 direction)
    {
        foreach (float distance in new[] { 4.0f, 2.8f, 2.0f })
        {
            Vector3 position = focus + direction * distance + Vector3.up * 0.4f;
            if (!Physics.Linecast(focus, position)) return position;
        }
        return focus + direction * 1.2f + Vector3.up * 3.0f;
    }

    private static void Capture(string name)
    {
        if (mage == null || mage.Transform == null) return;
        Vector3 focus = mage.Transform.position + Vector3.up * 1.2f;
        Vector3 toEnemy = near != null && near.Transform != null ? near.Transform.position - mage.Transform.position : mage.Transform.forward;
        toEnemy.y = 0f; toEnemy = toEnemy.sqrMagnitude > 0.01f ? toEnemy.normalized : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, toEnemy);
        CaptureView(name, focus + toEnemy * 1.5f, focus - toEnemy * 3.2f + side * 2.4f + Vector3.up * 0.4f);
    }

    private static void CaptureAt(string name, Vector3 focus)
    {
        CaptureView(name, focus, focus + new Vector3(-2.5f, 1.6f, -3f));
    }

    private static void CaptureView(string name, Vector3 lookAt, Vector3 position)
    {
        name += Suffix;
        if (!Captured.Add(name)) return;
        shotCamera.orthographic = false;
        shotCamera.clearFlags = CameraClearFlags.Skybox;
        shotCamera.transform.position = position;
        shotCamera.transform.rotation = Quaternion.LookRotation(lookAt - position);
        var target = new RenderTexture(960, 720, 24);
        var texture = new Texture2D(960, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            shotCamera.targetTexture = target;
            shotCamera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Shots + name + ".png", texture.EncodeToPNG());
            int magenta = texture.GetPixels32().Count(p => p.r > 220 && p.g < 40 && p.b > 220);
            Record($"Captura {name}: pixels magenta = {magenta}");
            if (magenta != 0) throw new InvalidOperationException($"Pixels magenta ({magenta}) em {name}: shader com erro");
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
}
