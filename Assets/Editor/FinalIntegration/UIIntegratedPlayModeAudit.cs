using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Audita a UI da partida real sem salvar ou modificar cenas/prefabs.
/// A composição Core + Map1 + GameScene é carregada apenas em memória.
/// </summary>
[InitializeOnLoad]
public static class UIIntegratedPlayModeAudit
{
    private const string RunningKey = "EraImperial.UIIntegratedAudit.Running";
    private const string FinishedKey = "EraImperial.UIIntegratedAudit.Finished";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";
    private const string MapSceneName = "Map1";
    private const string ReportPath = "Docs/QA/2026-10-18-ui-integrated-playmode.md";
    private static bool _finished;
    private static int _phase;
    private static AsyncOperation _mapLoad;
    private static AsyncOperation _gameLoad;
    private static readonly List<string> Checks = new List<string>();
    private static readonly List<string> Blocks = new List<string>();
    private static readonly List<string> Failures = new List<string>();

    static UIIntegratedPlayModeAudit()
    {
        EditorApplication.update += Tick;
    }

    [MenuItem("Era Imperial/QA/UI integrada na partida")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("UIIntegratedPlayModeAudit.Run exige o Editor fora do PlayMode.");

        _finished = false;
        _phase = 0;
        _mapLoad = null;
        _gameLoad = null;
        Checks.Clear();
        Blocks.Clear();
        Failures.Clear();
        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(FinishedKey, false);

        // A cena é aberta somente em memória. O Booter é desativado para que
        // o harness controle a composição sem abrir o menu e sem salvar a cena.
        EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
        CoreBooter booter = UnityEngine.Object.FindFirstObjectByType<CoreBooter>();
        if (booter != null) booter.enabled = false;
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (_finished || !SessionState.GetBool(RunningKey, false)
            || !EditorApplication.isPlaying || EditorApplication.isCompiling)
            return;

        try
        {
            if (_phase == 0)
            {
                PrepareMatchComposition();
                _phase = 1;
                return;
            }

            if (_phase == 1 && _mapLoad != null && _mapLoad.isDone)
            {
                _gameLoad = SceneManager.LoadSceneAsync("GameScene", LoadSceneMode.Additive);
                _phase = 2;
                return;
            }

            if (_phase == 2 && _gameLoad != null && _gameLoad.isDone
                && SceneManager.GetSceneByName("GameScene").isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(MapSceneName));
                _phase = 3;
                return;
            }

            // Aguarda Start/Awake da composição real e as listas dinâmicas de recursos.
            if (_phase == 3 && Time.frameCount > 30)
            {
                _phase = 4;
                RunRuntimeAudit();
                Finish();
            }
        }
        catch (Exception exception)
        {
            Failures.Add(exception.ToString());
            Finish();
        }
    }

    private static void PrepareMatchComposition()
    {
        Require(CoreDataHandler.instance != null, "CoreDataHandler não existe em Core.unity");
        MapData map = Resources.Load<MapData>("ScriptableObjects/Maps/Map1");
        Require(map != null, "Map1.asset não foi resolvido em Resources");
        CoreDataHandler.instance.SetMapData(map);
        CoreDataHandler.instance.SetGameUID(map);
        _mapLoad = SceneManager.LoadSceneAsync(MapSceneName, LoadSceneMode.Additive);
        Require(_mapLoad != null, "Map1 não pôde iniciar carregamento aditivo");
        Checks.Add("composição Core + Map1 + GameScene: carregamento iniciado");
    }

    private static void RunRuntimeAudit()
    {
        Scene game = SceneManager.GetSceneByName("GameScene");
        Require(game.IsValid() && game.isLoaded, "GameScene não está carregada");

        Canvas canvas = game.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
            .FirstOrDefault(item => item.gameObject.name == "Canvas");
        Require(canvas != null && canvas.isActiveAndEnabled, "Canvas real da partida ausente/inativo");
        Require(canvas.GetComponent<GraphicRaycaster>() != null, "Canvas real sem GraphicRaycaster");
        Checks.Add("Canvas real ativo com GraphicRaycaster");

        EventSystem eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        Require(eventSystem != null && eventSystem.isActiveAndEnabled, "EventSystem real ausente/inativo");
        Checks.Add("EventSystem real ativo");

        UIManager ui = UnityEngine.Object.FindFirstObjectByType<UIManager>();
        Require(ui != null && ui.isActiveAndEnabled, "UIManager real da partida ausente/inativo");
        Require(ui.resourcesUIParent != null, "UIManager sem resourcesUIParent");
        Require(ui.gameResourceDisplayPrefab != null, "UIManager sem prefab de ResourceDisplay");
        Require(ui.infoPanel != null && ui.constructionMenu != null && ui.selectedUnitMenu != null,
            "UIManager sem painéis principais");
        Require(ui.placedBuildingProductionRectTransform != null, "UIManager sem painel de produção no edifício");
        Require(ui.techTreePanel != null && ui.gameSettingsPanel != null, "UIManager sem painéis de tecnologia/configuração");
        Checks.Add("UIManager real com painéis e prefabs ligados");

        GameManager manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        Require(manager != null && GameManager.instance == manager, "GameManager real não inicializou");
        Checks.Add("GameManager real inicializado");

        int resourceWidgetsBefore = ui.resourcesUIParent.childCount;
        Require(resourceWidgetsBefore > 0, "UI de recursos não materializou widgets em Start");
        Checks.Add("widgets de recursos materializados: " + resourceWidgetsBefore);

        // Exercita o caminho real de evento consumido por UIManager. Os objetos
        // criados são temporários de PlayMode e desaparecem ao sair do harness.
        EventManager.TriggerEvent("UpdatedResources");
        Dictionary<InGameResource, int> production = new Dictionary<InGameResource, int>
        {
            { InGameResource.Wood, 3 },
            { InGameResource.Gold, 2 }
        };
        EventManager.TriggerEvent("UpdatedPlacedBuildingPosition",
            new object[] { production, Vector3.zero });
        Require(ui.placedBuildingProductionRectTransform.childCount == production.Count,
            "evento real de produção não populou os widgets de custo");
        Checks.Add("evento UpdatedPlacedBuildingPosition populou a UI real");

        EventManager.TriggerEvent("PlaceBuildingOn");
        EventManager.TriggerEvent("PlaceBuildingOff");
        Checks.Add("eventos reais PlaceBuildingOn/Off consumidos sem exceção");

        CaptureEvidence();
    }

    private static void CaptureEvidence()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        string path = Path.Combine(root, "TestResults", "UIIntegratedPlayMode", "game-ui.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        ScreenCapture.CaptureScreenshot(path);
        Checks.Add("captura solicitada: " + path.Replace(root + Path.DirectorySeparatorChar, ""));
    }

    private static void Finish()
    {
        _finished = true;
        SessionState.SetBool(RunningKey, false);
        bool blocked = Blocks.Count > 0;
        string status = Failures.Count > 0 ? "FAIL" : (blocked ? "BLOCKED" : "PASS");
        WriteReport(status);
        if (Failures.Count > 0)
            Debug.LogError("UI_INTEGRATED_PLAYMODE_FAIL: " + string.Join(" | ", Failures.ToArray()));
        else if (blocked)
            Debug.LogWarning("UI_INTEGRATED_PLAYMODE_BLOCKED: " + string.Join(" | ", Blocks.ToArray()));
        else
            Debug.Log("UI_INTEGRATED_PLAYMODE_PASS: Canvas/UIManager/eventos reais da partida");
        SessionState.SetBool(FinishedKey, status == "PASS");
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(status == "PASS" ? 0 : 2);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void WriteReport(string status)
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        string path = Path.Combine(root, ReportPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        List<string> lines = new List<string>
        {
            "# Auditoria de UI integrada à partida",
            "",
            "Data: 2026-10-18",
            "",
            "Status: **" + status + "**",
            "",
            "O harness carregou `Core.unity`, `Map1.unity` e `GameScene.unity` somente em memória. Nenhuma cena, prefab ou asset de produção foi salvo ou alterado.",
            "",
            "## Evidências",
            "",
            "- " + string.Join("\n- ", Checks.ToArray()),
            ""
        };
        if (Blocks.Count > 0)
        {
            lines.Add("## Bloqueios");
            lines.Add("");
            lines.Add("- " + string.Join("\n- ", Blocks.ToArray()));
            lines.Add("");
        }
        if (Failures.Count > 0)
        {
            lines.Add("## Falhas");
            lines.Add("");
            lines.Add("```text");
            lines.Add(string.Join("\n", Failures.ToArray()));
            lines.Add("```");
        }
        File.WriteAllLines(path, lines);
        AssetDatabase.Refresh();
    }
}
