using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EraImperial.Units.Advanced;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Play Mode no jogo real (Core.unity -> menu -> Map1): cria um Paladino pelo catalogo (Globals.CHARACTER_DATA), anda pelo
/// NavMesh e cerca de aliados feridos dentro e fora do raio, um inimigo (outro dono) ferido dentro do raio e valida:
/// cura so os aliados no raio (nunca o inimigo), cooldown, limite por alvo, efeito de cura na cena, camada Heal do Animator,
/// raio maior na era Imperial, ataque corpo a corpo pelo behavior tree (causa dano), capturas frente/costas/lado/cura,
/// troca de LOD pelo zoom da camera, prefab de morte e zero erros no console.
/// Batch: -executeMethod PaladinHealerPlayModeCheck.Run (sai com 0 se passar). Resultado: TestResults/paladin-playmode.txt
/// </summary>
[InitializeOnLoad]
public static class PaladinHealerPlayModeCheck
{
    private const string Prefix = "EraImperial.PaladinCheck.";
    private const string ReportFile = "TestResults/paladin-playmode.txt";
    private const string Shots = "Docs/CharacterMeshAudit/PaladinHealer/";

    private static double nextStep;
    private static Character paladin, allyInside, allyEdge, allyOutside, enemyInside, enemyMelee;
    private static CharacterManager paladinManager;
    private static PaladinHealBehaviour heal;
    private static Camera shotCamera;
    private static Vector3 start, walkTarget;
    private static float stageStarted;
    private static readonly HashSet<string> Captured = new HashSet<string>();
    private static readonly List<float> PulseTimes = new List<float>();
    private static readonly List<string> StateTrace = new List<string>();
    private static readonly List<string> HealLayerTrace = new List<string>();
    private static int lastPulses, initialInside, initialEdge;
    private static float firstPulseTime = -1f, captureHealAt = -1f;
    private static bool effectChecked, eraSet, meleeSetup;
    private static int lastEnemyHp, hitsSeen, initialMeleeHp;
    private static float minMeleeDistance = 999f;
    private static int deathsBefore;
    private static double deathAt;
    private static double diagAt;
    private static double stageEditorStarted;

    private const int WoundedHp = 4;     // vida de partida dos feridos (soldier tem 30)
    private const float InsideDistance = 2.0f, EdgeDistance = 3.6f, OutsideDistance = 5.0f; // raio Ancient = 4, Imperial = 6

    static PaladinHealerPlayModeCheck()
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
        SessionState.SetInt(Prefix + "Stage", 0);
        SessionState.SetInt(Prefix + "Errors", 0);
        SessionState.SetFloat(Prefix + "Deadline", (float)EditorApplication.timeSinceStartup + 420f);
        SessionState.SetString(Prefix + "DataPath", Path.GetFullPath("TestResults/PaladinData-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void IsolateSaves()
    {
        if (SessionState.GetBool(Prefix + "Running", false))
            BinarySerializable.DATA_DIRECTORY = SessionState.GetString(Prefix + "DataPath", "TestResults/PaladinData");
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
        Debug.Log("[Paladin check] " + message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Record("PASS " + message);
    }

    private static void RequireOnce(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        if (Captured.Add("req:" + message)) Record("PASS " + message);
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
            switch (SessionState.GetInt(Prefix + "Stage", 0))
            {
                case 0: StageOpenMatch(); break;
                case 1: StageSpawn(); break;
                case 2: StageWalking(); break;
                case 3: StageSpawnWounded(); break;
                case 4: StageHeal(); break;
                case 5: StageEra(); break;
                case 6: StageMeleeSetup(); break;
                case 7: StageMelee(); break;
                case 8: StageViews(); break;
                case 9: StageLod(); break;
                case 10: StageDeath(); break;
                case 11: StageDeathCheck(); break;
            }
        }
        catch (Exception exception)
        {
            Record("FAILED: " + exception);
            File.AppendAllText(ReportFile, "ESTADOS: " + string.Join(" > ", StateTrace) + "\n");
            SessionState.SetBool(Prefix + "Running", false);
            AdvancedEraState.Reset();
            EditorApplication.Exit(1);
        }
    }

    private static void Next(int stage, double delay)
    {
        SessionState.SetInt(Prefix + "Stage", stage);
        nextStep = EditorApplication.timeSinceStartup + delay;
        stageStarted = Time.time;
    }

    private static Vector3 OnNavMesh(Vector3 position, float maxDistance = 6f)
    {
        Require(NavMesh.SamplePosition(position, out NavMeshHit hit, maxDistance, NavMesh.AllAreas), "Posicao no NavMesh perto de " + position);
        return hit.position;
    }

    private static Character SpawnStatic(string code, int owner, Vector3 position, int hp)
    {
        var unit = new Character(Globals.CHARACTER_DATA[code], owner);
        var manager = unit.Transform.GetComponent<CharacterManager>();
        Require(manager.agent.Warp(OnNavMesh(position)), $"{code} (dono {owner}) posicionado no NavMesh");
        unit.Transform.GetComponent<CharacterBT>().enabled = false;   // alvo parado: sem behavior tree
        manager.agent.isStopped = true;
        unit.HP = hp;
        return unit;
    }

    private static float FlatDistance(Character a, Character b)
    {
        return AdvancedCombat.FlatDistance(a.Transform.position, b.Transform.position);
    }

    // ---------------------------------------------------------------------------------------------- stages
    private static void StageOpenMatch()
    {
        MainMenuManager menu = UnityEngine.Object.FindAnyObjectByType<MainMenuManager>();
        if (menu == null || menu.newGameScrollview.childCount == 0) return;
        MapData map = Resources.Load<MapData>("ScriptableObjects/Maps/Map1");
        Require(map != null, "Map1 encontrado");
        Transform picker = menu.newGameScrollview.Cast<Transform>().First(item => item.Find("Data/Name").GetComponent<Text>().text == map.mapName);
        picker.GetComponent<Button>().onClick.Invoke();
        menu.StartNewGame();
        Record("Partida nova em Map1 iniciada");
        Next(1, 8);
    }

    private static void StageSpawn()
    {
        if (GameManager.instance == null || Camera.main == null) return;
        Require(Globals.CHARACTER_DATA.ContainsKey("paladin_healer"), "Globals.CHARACTER_DATA contem 'paladin_healer'");
        Require(Globals.CHARACTER_DATA.ContainsKey("soldier"), "Globals.CHARACTER_DATA contem 'soldier' (aliados e inimigos de teste)");
        var spawnpoints = GameObject.Find("Spawnpoints").transform;
        start = OnNavMesh(spawnpoints.GetChild(0).position, 20f);
        int me = GameManager.instance.gamePlayersParameters.myPlayerId;
        paladin = new Character(Globals.CHARACTER_DATA["paladin_healer"], me);
        paladinManager = paladin.Transform.GetComponent<CharacterManager>();
        heal = paladin.Transform.GetComponent<PaladinHealBehaviour>();
        Require(heal != null && paladinManager != null && paladin.Transform.GetComponent<AdvancedUnitEraBinding>() != null, "Paladino instanciado com CharacterManager, AdvancedUnitEraBinding e PaladinHealBehaviour");
        Require(paladinManager.agent.Warp(start), "Paladino posicionado no NavMesh");
        Require(paladin.Transform.Find("Mesh/PaladinHealerVisual") != null, "Visual do Paladino presente sob Mesh");
        Require(AdvancedUnitBuilders_Find(paladin.Transform, "WeaponMount") != null && AdvancedUnitBuilders_Find(paladin.Transform, "ShieldMount") != null &&
                AdvancedUnitBuilders_Find(paladin.Transform, "HealAuraCenter") != null, "Sockets WeaponMount, ShieldMount e HealAuraCenter na unidade em jogo");
        Require(paladin.MaxHP == 70 && paladin.HP == 70 && paladin.AttackDamage == 16, $"Era Ancient aplicada: vida {paladin.HP}/{paladin.MaxHP}, dano {paladin.AttackDamage}");

        shotCamera = new GameObject("PaladinShotCamera").AddComponent<Camera>();
        shotCamera.fieldOfView = 35f;
        shotCamera.clearFlags = CameraClearFlags.Skybox;
        shotCamera.enabled = false;

        Vector3 direction = (spawnpoints.GetChild(1).position - start).normalized; direction.y = 0f;
        walkTarget = OnNavMesh(start + direction * 10f, 15f);
        Require(paladinManager.MoveTo(walkTarget, false), "Comando de movimento aceito");
        Next(2, 3);
    }

    private static Transform AdvancedUnitBuilders_Find(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            var found = AdvancedUnitBuilders_Find(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static void StageWalking()
    {
        Animator animator = heal.animator;
        float moved = Vector3.Distance(start, paladin.Transform.position);
        float speed = animator.GetFloat(AdvancedUnitAnimatorDriver.SpeedHash);
        Require(moved > 2f, $"Paladino andou {moved:F1} m pelo NavMesh");
        Require(speed > 1.0f, $"Parametro Speed do Animator = {speed:F2} (locomocao ativa)");
        Capture("playmode-walk", paladin.Transform.position + Vector3.up * 1f, paladin.Transform.forward, 3.4f);
        if (paladinManager.agent.remainingDistance > 1f && Time.time - stageStarted < 8f) { nextStep = EditorApplication.timeSinceStartup + 1; return; }
        paladinManager.agent.isStopped = true;
        Next(3, 1.0);
    }

    private static void StageSpawnWounded()
    {
        paladin.Transform.GetComponent<CharacterBT>().enabled = false;   // sem combate durante o teste de cura
        paladinManager.agent.ResetPath();
        int me = GameManager.instance.gamePlayersParameters.myPlayerId;
        int other = me == 0 ? 1 : 0;
        Vector3 p = paladin.Transform.position;
        allyInside = SpawnStatic("soldier", me, p + new Vector3(InsideDistance, 0f, 0f), WoundedHp);
        allyEdge = SpawnStatic("soldier", me, p + new Vector3(0f, 0f, EdgeDistance), WoundedHp);
        allyOutside = SpawnStatic("soldier", me, p + new Vector3(-OutsideDistance, 0f, 0f), WoundedHp);
        enemyInside = SpawnStatic("soldier", other, p + new Vector3(-1.5f, 0f, -1.5f), WoundedHp);
        initialInside = allyInside.HP; initialEdge = allyEdge.HP;
        Record($"Distancias planas: aliado dentro {FlatDistance(paladin, allyInside):F2} m, aliado na borda {FlatDistance(paladin, allyEdge):F2} m, aliado fora {FlatDistance(paladin, allyOutside):F2} m, inimigo {FlatDistance(paladin, enemyInside):F2} m (raio Ancient 4,0)");
        Require(FlatDistance(paladin, allyInside) < 4f && FlatDistance(paladin, allyEdge) < 4f && FlatDistance(paladin, allyOutside) > 4.2f && FlatDistance(paladin, allyOutside) < 6f && FlatDistance(paladin, enemyInside) < 4f,
            "Cenario: 2 aliados dentro do raio, 1 aliado fora (no alcance da era Imperial), 1 inimigo ferido dentro do raio");
        Require(allyInside.Owner == paladin.Owner && enemyInside.Owner != paladin.Owner, "Donos: aliados = dono do paladino; inimigo = outro dono");
        lastPulses = heal.PulsesPerformed;
        stageEditorStarted = EditorApplication.timeSinceStartup;
        Next(4, 0.2);
    }

    private static void StageHeal()
    {
        if (EditorApplication.timeSinceStartup > diagAt) { diagAt = EditorApplication.timeSinceStartup + 10; Record($"[diag] editor={EditorApplication.timeSinceStartup:F0}s jogo={Time.time:F1}s quadro={Time.frameCount} dt={Time.deltaTime:F3} escala={Time.timeScale}"); }
        if (enemyInside == null || enemyInside.Transform == null) throw new InvalidOperationException("O inimigo de teste desapareceu.");
        // nunca cura o inimigo nem quem esta fora do raio
        RequireOnce(enemyInside.HP == WoundedHp, "Inimigo ferido dentro do raio nunca e curado");
        RequireOnce(allyOutside.HP == WoundedHp, "Aliado fora do raio (Ancient 4 m) nao e curado");
        RequireOnce(paladin.HP == paladin.MaxHP, "O paladino nao se cura (canHealSelf desligado)");

        var layer = heal.animator.GetCurrentAnimatorStateInfo(1);
        string layerState = heal.animator.IsInTransition(1) ? "(" + (heal.animator.GetNextAnimatorStateInfo(1).IsName("Heal") ? "Heal" : "?") + ")" : (layer.IsName("Heal") ? "Heal" : "Empty");
        if (HealLayerTrace.Count == 0 || HealLayerTrace.Last() != layerState) HealLayerTrace.Add(layerState);

        if (heal.PulsesPerformed != lastPulses)
        {
            lastPulses = heal.PulsesPerformed;
            PulseTimes.Add(Time.time);
            if (firstPulseTime < 0f) { firstPulseTime = Time.time; captureHealAt = Time.time + 0.25f; }
            Record($"Pulso {heal.PulsesPerformed} em t={Time.time:F2}: alvos={heal.LastPulseTargets}, curado total={heal.TotalHealed}, aliado dentro HP={allyInside.HP}, borda HP={allyEdge.HP}");
            RequireOnce(heal.LastPulseTargets >= 1 && heal.LastPulseTargets <= 2, "Cada pulso cura no maximo os 2 aliados dentro do raio");
            var effects = UnityEngine.Object.FindObjectsByType<PaladinHealAuraEffect>(FindObjectsSortMode.None);
            if (!effectChecked)
            {
                effectChecked = true;
                Require(effects.Length >= 1, "Efeito de cura instanciado na cena (" + effects.Length + ")");
                Vector3 s = effects[0].transform.localScale;
                Require(Mathf.Abs(s.x - 8f) < 0.01f && Mathf.Abs(s.z - 8f) < 0.01f && Mathf.Abs(s.y - 1f) < 0.01f, $"Efeito escalado para o raio da era Ancient: (2r,1,2r) = {s}");
                Require(effects[0].GetComponentInChildren<ParticleSystem>() != null, "Efeito com particulas");
            }
        }
        if (captureHealAt > 0f && Time.time >= captureHealAt)
        {
            captureHealAt = -1f;
            Capture("playmode-heal", paladin.Transform.position + Vector3.up * 0.4f, (new Vector3(1f, 0f, -1f)).normalized, 7.5f, 1.6f);
        }

        // ledger: nunca passa do limite por alvo (18 em 10 s na era Ancient)
        RequireOnce(allyInside.HP - initialInside <= 18 && allyEdge.HP - initialEdge <= 18, "Limite de cura por alvo respeitado (<= 18 em 10 s)");
        if (firstPulseTime > 0f && Time.time - firstPulseTime > 9.0f)
        {
            Require(PulseTimes.Count >= 3, $"Pelo menos 3 pulsos em ~9 s ({PulseTimes.Count})");
            for (int i = 1; i < PulseTimes.Count; i++)
                Require(PulseTimes[i] - PulseTimes[i - 1] >= 2.6f, $"Cooldown de cura respeitado entre pulsos {i} e {i + 1}: {PulseTimes[i] - PulseTimes[i - 1]:F2} s (Ancient 3,0 s; o instante e lido por polling do editor, tolerancia 0,4 s)");
            Require((PulseTimes[PulseTimes.Count - 1] - PulseTimes[0]) / (PulseTimes.Count - 1) >= 2.85f, $"Periodo medio entre pulsos {(PulseTimes[PulseTimes.Count - 1] - PulseTimes[0]) / (PulseTimes.Count - 1):F2} s >= 2,85 s");
            Require(allyInside.HP - initialInside == 18 && allyEdge.HP - initialEdge == 18, $"Aliados dentro do raio curaram exatamente o limite por alvo: +{allyInside.HP - initialInside} / +{allyEdge.HP - initialEdge}");
            Require(heal.TotalHealed == 36, $"Total curado = 36 (2 alvos x 18), obtido {heal.TotalHealed}");
            Require(enemyInside.HP == WoundedHp && allyOutside.HP == WoundedHp, "Fim da fase Ancient: inimigo e aliado fora do raio continuam com HP " + WoundedHp);
            Require(HealLayerTrace.Contains("Heal") || HealLayerTrace.Contains("(Heal)"), "Camada de cura do Animator tocou o estado Heal (" + string.Join(" > ", HealLayerTrace) + ")");
            Next(5, 0.1);
            return;
        }
        if (EditorApplication.timeSinceStartup - stageEditorStarted > 40.0) throw new TimeoutException("Nenhum pulso de cura em 40 s.");
        nextStep = EditorApplication.timeSinceStartup + 0.05;
    }

    private static void StageEra()
    {
        // era Imperial: raio 6 m (o aliado a 5 m passa a ser curado), cura 18, vida maxima 145
        if (!eraSet)
        {
            eraSet = true;
            AdvancedEraState.Set(ImperialEra.Imperial);
            Require(paladin.MaxHP == 145 && paladin.AttackDamage == 31, $"Era Imperial aplicada ao paladino: vida maxima {paladin.MaxHP}, dano {paladin.AttackDamage}");
            stageStarted = Time.time;
            lastEnemyHp = allyOutside.HP;
            lastPulses = heal.PulsesPerformed;
            Capture("playmode-heal-imperial-start", paladin.Transform.position + Vector3.up * 0.4f, (new Vector3(1f, 0f, -1f)).normalized, 9.5f, 2.0f);
        }
        if (heal.PulsesPerformed != lastPulses)
        {
            lastPulses = heal.PulsesPerformed;
            Capture("playmode-heal-imperial", paladin.Transform.position + Vector3.up * 0.4f, (new Vector3(1f, 0f, -1f)).normalized, 9.5f, 2.0f);
        }
        if (allyOutside.HP > WoundedHp)
        {
            Require(allyOutside.HP - WoundedHp <= 18, $"Aliado a 5 m curado na era Imperial (+{allyOutside.HP - WoundedHp}, cura 18 por pulso)");
            Require(enemyInside.HP == WoundedHp, "Inimigo continua sem cura na era Imperial");
            Next(6, 0.1);
            return;
        }
        if (Time.time - stageStarted > 25f) throw new TimeoutException("Aliado a 5 m nao foi curado na era Imperial.");
        nextStep = EditorApplication.timeSinceStartup + 0.1;
    }

    private static void StageMeleeSetup()
    {
        foreach (var unit in new[] { allyInside, allyEdge, allyOutside, enemyInside })
            if (unit != null && unit.Transform != null) UnityEngine.Object.Destroy(unit.Transform.gameObject);
        int me = GameManager.instance.gamePlayersParameters.myPlayerId;
        int other = me == 0 ? 1 : 0;
        Vector3 forward = paladin.Transform.forward; forward.y = 0f; forward = forward.sqrMagnitude > 0.1f ? forward.normalized : Vector3.forward;
        enemyMelee = SpawnStatic("soldier", other, paladin.Transform.position + forward * 6f, 500);
        initialMeleeHp = enemyMelee.HP;
        lastEnemyHp = initialMeleeHp;
        paladin.Transform.GetComponent<CharacterBT>().enabled = true;
        paladinManager.agent.isStopped = false;
        Record($"Inimigo de combate a {FlatDistance(paladin, enemyMelee):F1} m com HP {initialMeleeHp}; alcance do paladino {paladin.AttackRange} m");
        StateTrace.Clear();
        Next(7, 0.3);
    }

    private static string BaseState(Animator animator)
    {
        var info = animator.GetCurrentAnimatorStateInfo(0);
        foreach (string name in new[] { "Locomotion", "Attack", "HitReact" })
        {
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(name)) return "(" + name + ")";
            if (info.IsName(name)) return name;
        }
        return "?";
    }

    private static void StageMelee()
    {
        if (enemyMelee == null || enemyMelee.Transform == null) throw new InvalidOperationException("O inimigo de combate desapareceu.");
        string state = BaseState(heal.animator);
        if (StateTrace.Count == 0 || StateTrace.Last() != state) StateTrace.Add(state);
        float distance = FlatDistance(paladin, enemyMelee);
        int hp = enemyMelee.HP;
        if (hp < lastEnemyHp)
        {
            int damage = lastEnemyHp - hp;
            hitsSeen++;
            minMeleeDistance = Mathf.Min(minMeleeDistance, distance);
            Record($"Golpe {hitsSeen}: dano {damage} (HP {lastEnemyHp} -> {hp}) a {distance:F2} m");
            Require(damage == paladin.AttackDamage, $"Dano do golpe = AttackDamage do paladino ({paladin.AttackDamage})");
            Require(distance < 3.2f, $"Golpe corpo a corpo a {distance:F2} m");
            lastEnemyHp = hp;
            if (hitsSeen == 1) Capture("playmode-attack", paladin.Transform.position + Vector3.up * 1f, Vector3.Cross(Vector3.up, (enemyMelee.Transform.position - paladin.Transform.position).normalized), 4.2f);
        }
        if (hitsSeen >= 2)
        {
            Record("Estados do Animator: " + string.Join(" > ", StateTrace));
            Require(StateTrace.Any(s => s.Contains("Attack")), "Animator entrou no estado Attack (trigger Hit do behavior tree)");
            Require(initialMeleeHp - enemyMelee.HP == 2 * paladin.AttackDamage || hitsSeen > 2, "Dano total acumulado coerente com os golpes");
            Require(SessionState.GetInt(Prefix + "Errors", 0) == 0, "0 erros de runtime no Console ate aqui");
            UnityEngine.Object.Destroy(enemyMelee.Transform.gameObject);
            paladin.Transform.GetComponent<CharacterBT>().enabled = false;
            paladinManager.agent.ResetPath();
            Next(8, 1.2);
            return;
        }
        if (Time.time - stageStarted > 60f) throw new TimeoutException("Ataque corpo a corpo nao concluiu em 60 s. Estados: " + string.Join(" > ", StateTrace) + $"; golpes={hitsSeen}; HP={enemyMelee.HP}");
        nextStep = EditorApplication.timeSinceStartup + 0.05;
    }

    private static void StageViews()
    {
        Vector3 focus = paladin.Transform.position + Vector3.up * 1.0f;
        Vector3 forward = paladin.Transform.forward; forward.y = 0f; forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Capture("playmode-front", focus, forward, 4.2f, 0.2f, true);
        Capture("playmode-back", focus, -forward, 4.2f, 0.2f, true);
        Capture("playmode-side", focus, right, 4.2f, 0.2f, true);
        Next(9, 0.2);
    }

    // LOD em jogo: cada LOD recebe uma cor chapada e a camera ortografica (mesma projecao do jogo, zoom 8..26) diz qual foi desenhado
    private static void StageLod()
    {
        var group = paladin.Transform.GetComponentInChildren<LODGroup>();
        Require(group != null && group.lodCount == 3, "LODGroup presente na unidade em jogo");
        var renderers = paladin.Transform.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.Contains("_LOD")).OrderBy(r => r.name).ToArray();
        Require(renderers.Length == 3, "3 renderers de LOD na unidade em jogo");
        var colours = new[] { new Color(1f, 1f, 0f), new Color(0f, 1f, 0f), new Color(1f, 0f, 1f) };
        var originals = renderers.Select(r => r.sharedMaterial).ToArray();
        for (int i = 0; i < 3; i++) renderers[i].sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = colours[i] };
        foreach (var other in paladin.Transform.GetComponentsInChildren<Renderer>(true))
            if (!renderers.Contains(other as SkinnedMeshRenderer) && other.name != "TeamRing") other.enabled = false;   // pecas e anel fora da contagem
        shotCamera.clearFlags = CameraClearFlags.SolidColor;
        shotCamera.backgroundColor = Color.black;
        shotCamera.orthographic = true;
        shotCamera.nearClipPlane = 0.1f;
        shotCamera.farClipPlane = 200f;
        Vector3 focus = paladin.Transform.position + Vector3.up * 0.9f;
        var seen = new List<int>();
        foreach (float size in new[] { 8f, 18f, 26f })
        {
            shotCamera.orthographicSize = size;
            shotCamera.transform.position = focus + new Vector3(0.3f, 0.2f, -1f).normalized * 40f;
            shotCamera.transform.rotation = Quaternion.LookRotation(focus - shotCamera.transform.position);
            var target = new RenderTexture(480, 360, 24);
            var texture = new Texture2D(480, 360, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            shotCamera.targetTexture = target;
            shotCamera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 480, 360), 0, 0);
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
            if (counts.Max() < 5) best = -1;
            Record($"LOD em jogo, zoom ortografico {size:F0}: pixels LOD0/1/2 = {counts[0]}/{counts[1]}/{counts[2]} -> LOD{best}");
            seen.Add(best);
        }
        Require(seen[0] == 0 && seen[1] == 1 && seen[2] == 2, "Troca de LOD pelo zoom da camera do jogo: 8 > LOD0, 18 > LOD1, 26 > LOD2");
        for (int i = 0; i < 3; i++) renderers[i].sharedMaterial = originals[i];
        foreach (var other in paladin.Transform.GetComponentsInChildren<Renderer>(true)) other.enabled = true;
        shotCamera.orthographic = false;
        shotCamera.clearFlags = CameraClearFlags.Skybox;
        Next(10, 0.2);
    }

    private static void StageDeath()
    {
        deathsBefore = AdvancedUnitDeathEffect.DeathsSpawned;
        paladinManager.TakeHit(99999);   // UnitManager destroi a unidade; AdvancedUnitDeathEffect coloca o prefab de morte no lugar
        Next(11, 0.6);
    }

    private static void StageDeathCheck()
    {
        Require(AdvancedUnitDeathEffect.DeathsSpawned == deathsBefore + 1, "Prefab de morte instanciado no lugar da unidade");
        var death = GameObject.Find("PaladinHealerDeath(Clone)");
        Require(death != null, "Objeto PaladinHealerDeath(Clone) na cena");
        var animator = death.GetComponent<Animator>();
        var info = animator.GetCurrentAnimatorStateInfo(0);
        Require(info.IsName("Death"), "Prefab de morte toca o estado Death sozinho (normalizedTime " + info.normalizedTime.ToString("F2") + ")");
        if (deathAt == 0) { deathAt = EditorApplication.timeSinceStartup; }
        if (!Captured.Contains("death1"))
        {
            Captured.Add("death1");
            Capture("playmode-death-start", death.transform.position + Vector3.up * 0.8f, Vector3.forward, 5.5f, 0.3f);
            nextStep = EditorApplication.timeSinceStartup + 2.2;
            return;
        }
        Require(info.normalizedTime > 0.35f, "Animacao de morte avancou (normalizedTime " + info.normalizedTime.ToString("F2") + ")");
        Capture("playmode-death", death.transform.position + Vector3.up * 0.3f, Vector3.forward, 5.5f, 1.8f);
        Require(SessionState.GetInt(Prefix + "Errors", 0) == 0, "0 erros de runtime no Console");
        Record("PALADINHEALER_PLAYMODE_PASSED");
        SessionState.SetBool(Prefix + "Running", false);
        AdvancedEraState.Reset();
        EditorApplication.Exit(0);
    }

    // --------------------------------------------------------------------------------------------- capture
    private static void Capture(string name, Vector3 focus, Vector3 direction, float distance, float height = 0.4f, bool force = false)
    {
        if (!Captured.Add(name) && !force) return;
        direction.y = 0f; direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, direction);
        shotCamera.transform.position = focus + direction * distance + Vector3.up * height + side * (name.Contains("walk") ? 1.2f : 0f);
        shotCamera.transform.rotation = Quaternion.LookRotation(focus - shotCamera.transform.position);
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
            if (magenta != 0) throw new InvalidOperationException("Pixels magenta (shader com erro) em " + name + ": " + magenta);
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
