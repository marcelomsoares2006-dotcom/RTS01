using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Play Mode no jogo real (Core.unity -> menu -> Map1): cria um Archer pelo catalogo (Globals.CHARACTER_DATA),
/// move pelo NavMesh (locomocao) e o coloca contra uma unidade inimiga parada para validar o ataque a distancia
/// pelo CharacterBT: Draw -> Aim -> Fire, flecha em voo, dano atrasado e aplicado pelo projetil.
/// Batch: -executeMethod ArcherPlayModeCheck.Run  (sai com 0 se passar). Resultado: TestResults/archer-playmode.txt
/// </summary>
[InitializeOnLoad]
public static class ArcherPlayModeCheck
{
    private const string Prefix = "EraImperial.ArcherCheck.";
    private const string ReportFile = "TestResults/archer-playmode.txt";
    private const string Shots = "Docs/CharacterMeshAudit/Archer/";
    private static double nextStep;
    private static Character archer;
    private static Character enemy;
    private static CharacterManager archerManager;
    private static ArcherCombatController combat;
    private static Camera shotCamera;
    private static Vector3 start, walkTarget, enemyPosition;
    private static readonly List<string> StateTrace = new List<string>();
    private static int initialEnemyHp;
    private static int hpBeforeFire;
    private static int lastHp;
    private static float minDistance = 999f;
    private static float stageStarted;
    private static readonly HashSet<string> Captured = new HashSet<string>();
    private static bool releaseSeen;
    private static float releaseYawError = -1f;

    static ArcherPlayModeCheck()
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
        SessionState.SetString(Prefix + "DataPath", Path.GetFullPath("TestResults/ArcherData-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void IsolateSaves()
    {
        if (SessionState.GetBool(Prefix + "Running", false))
            BinarySerializable.DATA_DIRECTORY = SessionState.GetString(Prefix + "DataPath", "TestResults/ArcherData");
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
        Debug.Log("[Archer check] " + message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
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
            int stage = SessionState.GetInt(Prefix + "Stage", 0);
            switch (stage)
            {
                case 0: StageOpenMatch(); break;
                case 1: StageSpawnArcher(); break;
                case 2: StageWalking(); break;
                case 3: StageSpawnEnemy(); break;
                case 4: StageCombat(); break;
                case 5: StageLod(); break;
            }
        }
        catch (Exception exception)
        {
            Record("FAILED: " + exception);
            File.AppendAllText(ReportFile, "STATE TRACE: " + string.Join(" > ", StateTrace) + "\n");
            SessionState.SetBool(Prefix + "Running", false);
            EditorApplication.Exit(1);
        }
    }

    private static void Next(int stage, double delay)
    {
        SessionState.SetInt(Prefix + "Stage", stage);
        nextStep = EditorApplication.timeSinceStartup + delay;
        stageStarted = Time.time;
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

    private static void StageSpawnArcher()
    {
        if (GameManager.instance == null || Camera.main == null) return;
        Require(Globals.CHARACTER_DATA.ContainsKey("archer"), "Globals.CHARACTER_DATA contem 'archer'");
        var spawnpoints = GameObject.Find("Spawnpoints").transform;
        Require(NavMesh.SamplePosition(spawnpoints.GetChild(0).position, out NavMeshHit hit, 20, NavMesh.AllAreas), "Ponto inicial no NavMesh");
        start = hit.position;
        int me = GameManager.instance.gamePlayersParameters.myPlayerId;
        archer = new Character(Globals.CHARACTER_DATA["archer"], me);
        archerManager = archer.Transform.GetComponent<CharacterManager>();
        combat = archer.Transform.GetComponent<ArcherCombatController>();
        Require(combat != null && archerManager != null, "Archer instanciado com CharacterManager e ArcherCombatController");
        Require(archerManager.agent.Warp(start), "Archer posicionado no NavMesh");
        Require(archer.Transform.Find("Mesh/ArcherVisual") != null, "Visual do Archer presente sob Mesh");

        shotCamera = new GameObject("ArcherShotCamera").AddComponent<Camera>();
        shotCamera.fieldOfView = 35f;
        shotCamera.clearFlags = CameraClearFlags.Skybox;
        shotCamera.enabled = false;

        // 10 m a frente em linha reta no NavMesh
        Vector3 direction = (spawnpoints.GetChild(1).position - start).normalized; direction.y = 0f;
        Require(NavMesh.SamplePosition(start + direction * 10f, out NavMeshHit far, 15, NavMesh.AllAreas), "Destino de caminhada no NavMesh");
        walkTarget = far.position;
        Require(archerManager.MoveTo(walkTarget, false), "Comando de movimento aceito");
        Next(2, 3);
    }

    private static void StageWalking()
    {
        Animator animator = combat.animator;
        float moved = Vector3.Distance(start, archer.Transform.position);
        float speed = animator.GetFloat(ArcherCombatController.SpeedHash);
        Require(moved > 2f, $"Archer andou {moved:F1} m pelo NavMesh");
        Require(speed > 0.8f, $"Parametro Speed do Animator = {speed:F2} (locomocao ativa)");
        Capture("playmode-walk");
        // keep the walk target; wait until it stops (or 8 s) before the combat test
        if (archerManager.agent.remainingDistance > 1f && Time.time - stageStarted < 8f) { nextStep = EditorApplication.timeSinceStartup + 1; return; }
        Next(3, 0.5);
    }

    private static void StageSpawnEnemy()
    {
        Vector3 position = archer.Transform.position;
        Vector3 away = (archer.Transform.forward.sqrMagnitude > 0.1f ? archer.Transform.forward : Vector3.forward); away.y = 0f;
        Require(NavMesh.SamplePosition(position + away.normalized * 9f, out NavMeshHit hit, 12, NavMesh.AllAreas), "Posicao do inimigo no NavMesh");
        enemyPosition = hit.position;
        int me = GameManager.instance.gamePlayersParameters.myPlayerId;
        int other = me == 0 ? 1 : 0;
        enemy = new Character(Globals.CHARACTER_DATA["worker"], other);
        var enemyManager = enemy.Transform.GetComponent<CharacterManager>();
        Require(enemyManager.agent.Warp(enemyPosition), "Inimigo posicionado no NavMesh");
        // stationary target: no behaviour tree, no movement
        enemy.Transform.GetComponent<CharacterBT>().enabled = false;
        enemyManager.agent.isStopped = true;
        enemy.HP = 500;
        initialEnemyHp = enemy.HP;
        lastHp = initialEnemyHp;
        float distance = Vector3.Distance(archer.Transform.position, enemy.Transform.position);
        Record($"Inimigo a {distance:F1} m; alcance do Archer {Globals.CHARACTER_DATA["archer"].attackRange} m; HP inimigo {initialEnemyHp}");
        Require(distance > 4f && distance <= Globals.CHARACTER_DATA["archer"].attackRange, "Inimigo fora do corpo a corpo e dentro do alcance de tiro");
        Next(4, 0.2);
    }

    private static void StageCombat()
    {
        if (enemy == null || enemy.Transform == null) throw new InvalidOperationException("O inimigo desapareceu antes do fim do teste.");
        Animator animator = combat.animator;
        var info = animator.GetCurrentAnimatorStateInfo(0);
        string state = animator.IsInTransition(0) ? "(" + StateName(animator.GetNextAnimatorStateInfo(0)) + ")" : StateName(info);
        if (StateTrace.Count == 0 || StateTrace.Last() != state) StateTrace.Add(state);

        float distance = Vector3.Distance(archer.Transform.position, enemy.Transform.position);
        minDistance = Mathf.Min(minDistance, distance);
        int hp = enemy.HP;

        bool inDraw = info.IsName("DrawString"), inAim = info.IsName("Aim"), inFire = info.IsName("Fire");
        if (inDraw || inAim)
            Require(hp == lastHp, "Sem dano durante " + (inDraw ? "DrawString" : "Aim") + " (dano nao e instantaneo)", once: true);
        if (inDraw && info.normalizedTime > 0.5f) Capture("playmode-draw");
        if (inAim) Capture("playmode-aim");
        if (inFire && info.normalizedTime > 0.2f && info.normalizedTime < 0.6f) Capture("playmode-fire");
        if (combat.ArrowsFired > 0 && !releaseSeen)
        {
            releaseSeen = true;
            hpBeforeFire = hp;
            Vector3 flat = enemy.Transform.position - archer.Transform.position; flat.y = 0f;
            releaseYawError = Vector3.Angle(archer.Transform.forward, flat);
            Record($"Flecha disparada: arqueiro virado ao alvo com erro de {releaseYawError:F1} graus; HP inimigo {hp}");
            Require(releaseYawError < 25f, "Arqueiro virado para o alvo no disparo");
        }
        if (hp < lastHp)
        {
            int dmg = lastHp - hp;
            Record($"Dano recebido: {dmg} (HP {lastHp} -> {hp})");
            Require(combat.ArrowsFired >= 1, "O dano so ocorre depois de uma flecha ser disparada");
            Require(dmg == archer.AttackDamage, $"Dano da flecha = AttackDamage do Archer ({archer.AttackDamage})");
            lastHp = hp;
            Capture("playmode-hit");
        }

        bool cycleDone = StateTrace.Contains("DrawString") && StateTrace.Contains("Aim") && StateTrace.Contains("Fire") &&
                         combat.ArrowsFired >= 2 && enemy.HP <= initialEnemyHp - 2 * archer.AttackDamage;
        if (cycleDone)
        {
            string trace = string.Join(" > ", StateTrace);
            Record("Sequencia de estados: " + trace);
            Require(trace.Contains("DrawString") && trace.IndexOf("DrawString", StringComparison.Ordinal) < trace.IndexOf("Aim", StringComparison.Ordinal) &&
                    trace.IndexOf("Aim", StringComparison.Ordinal) < trace.IndexOf("Fire", StringComparison.Ordinal), "Ordem Draw > Aim > Fire");
            Require(minDistance > 4f, $"Archer atacou a distancia (distancia minima {minDistance:F1} m)");
            Require(combat.ArrowsFired >= 2, $"Ciclo repetido: {combat.ArrowsFired} flechas");
            Require(SessionState.GetInt(Prefix + "Errors", 0) == 0, "0 erros de runtime no Console");
            Capture("playmode-final");
            Next(5, 1.5);
            return;
        }
        if (Time.time - stageStarted > 70f) throw new TimeoutException("Ataque nao concluiu em 70 s. Estados: " + string.Join(" > ", StateTrace) + $"; flechas={combat.ArrowsFired}; HP={enemy.HP}");
        nextStep = EditorApplication.timeSinceStartup + 0.05;
    }


    // LOD in runtime: each LOD renderer of this test instance is painted a distinct flat colour and rendered by the test camera
    // at three distances; the dominant colour tells which level the engine really drew (Renderer.isVisible is unreliable in batch mode).
    private static void StageLod()
    {
        var group = archer.Transform.GetComponentInChildren<LODGroup>();
        Require(group != null && group.lodCount == 3, "LODGroup presente na unidade em jogo");
        var renderers = archer.Transform.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.Contains("_LOD")).OrderBy(r => r.name).ToArray();
        Require(renderers.Length == 3, "3 renderers de LOD na unidade em jogo");
        var colours = new[] { new Color(1f, 1f, 0f), new Color(0f, 1f, 0f), new Color(1f, 0f, 1f) };
        for (int i = 0; i < 3; i++)
        {
            var material = new Material(Shader.Find("Unlit/Color")) { color = colours[i] };
            renderers[i].sharedMaterial = material;
        }
        shotCamera.clearFlags = CameraClearFlags.SolidColor;
        shotCamera.backgroundColor = Color.black;
        // same projection as the game camera (orthographic); zoom range of CameraManager is 8..26, default 18
        shotCamera.orthographic = true;
        shotCamera.nearClipPlane = 0.1f;
        shotCamera.farClipPlane = 200f;
        Vector3 focus = archer.Transform.position + Vector3.up * 0.9f;
        var seen = new List<int>();
        foreach (float distance in new[] { 8f, 18f, 26f })
        {
            shotCamera.orthographicSize = distance;
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
            var pixels = texture.GetPixels32();
            var counts = new int[3];
            foreach (var p in pixels)
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
            Record($"LOD em jogo, zoom ortografico {distance:F0}: pixels LOD0/1/2 = {counts[0]}/{counts[1]}/{counts[2]} -> LOD{best}");
            seen.Add(best);
        }
        Require(seen[0] == 0 && seen[1] == 1 && seen[2] == 2, "Troca de LOD pelo zoom da camera do jogo: 8 > LOD0, 18 > LOD1, 26 > LOD2");
        Record("ARCHER_PLAYMODE_PASSED");
        SessionState.SetBool(Prefix + "Running", false);
        EditorApplication.Exit(0);
    }

    private static void Require(bool condition, string message, bool once)
    {
        if (!condition) throw new InvalidOperationException(message);
        if (once && Captured.Add("req:" + message)) Record("PASS " + message);
    }

    private static string StateName(AnimatorStateInfo info)
    {
        foreach (string name in new[] { "Locomotion", "DrawString", "Aim", "Fire" })
            if (info.IsName(name)) return name;
        return "?";
    }

    // --------------------------------------------------------------------------------------------- capture
    private static void Capture(string name)
    {
        if (!Captured.Add(name)) return;
        if (archer == null || archer.Transform == null) return;
        Vector3 focus = archer.Transform.position + Vector3.up * 1.2f;
        Vector3 toEnemy = enemy != null && enemy.Transform != null ? (enemy.Transform.position - archer.Transform.position) : archer.Transform.forward;
        toEnemy.y = 0f; toEnemy = toEnemy.sqrMagnitude > 0.01f ? toEnemy.normalized : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, toEnemy);
        shotCamera.transform.position = focus - toEnemy * 3.0f + side * 2.2f + Vector3.up * 0.4f;
        shotCamera.transform.rotation = Quaternion.LookRotation(focus + toEnemy * 1.0f - shotCamera.transform.position);
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
            float fraction = magenta / (960f * 720f);
            Record($"Captura {name}: magenta {fraction:P3}");
            if (fraction >= 0.01f) throw new InvalidOperationException("Pixels magenta (shader com erro) acima de 1% em " + name);
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
