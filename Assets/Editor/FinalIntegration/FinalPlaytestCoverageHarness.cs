using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EraImperial.SaveMigrationV2;
using EraImperial.Units.Advanced;
using EraImperial.Units.Siege;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Matriz descartável da cobertura de playtest do stack authored.
///
/// Este harness comprova somente os caminhos que pode exercitar sem alterar
/// gameplay: UI real, contratos de eras/combate/cerco, SaveMigrationV2 e
/// NavMesh da composição Core + GameScene + Map1. Produção viva e cerco com
/// alvo estrutural continuam delegados ao harness de produção viva, para que
/// a ausência de uma dependência não seja convertida em falso PASS.
/// </summary>
[InitializeOnLoad]
public static class FinalPlaytestCoverageHarness
{
    private const string RunningKey = "EraImperial.FinalPlaytestCoverage.Running";
    private const string FinishedKey = "EraImperial.FinalPlaytestCoverage.Finished";
    private const string ReportPath = "Docs/QA/2026-10-19-playtest-coverage-audit.md";
    private static bool _finished;
    private static int _frames;
    private static readonly List<string> Passes = new List<string>();
    private static readonly List<string> Blocks = new List<string>();

    static FinalPlaytestCoverageHarness()
    {
        EditorApplication.update += Tick;
    }

    public static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException(
                "FinalPlaytestCoverageHarness.Run exige o Editor fora do PlayMode.");

        _finished = false;
        _frames = 0;
        Passes.Clear();
        Blocks.Clear();
        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(FinishedKey, false);

        Scene core = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Additive);
        Scene map = EditorSceneManager.OpenScene("Assets/Scenes/Maps/Map1.unity", OpenSceneMode.Additive);
        SceneManager.SetActiveScene(map);

        CoreDataHandler coreData = UnityEngine.Object.FindAnyObjectByType<CoreDataHandler>();
        MapData mapData = Resources.Load<MapData>("ScriptableObjects/Maps/Map1");
        if (coreData != null && mapData != null)
        {
            coreData.SetMapData(mapData);
            coreData.SetGameUID("Map1__FinalPlaytestCoverage__" + Guid.NewGuid().ToString("N"));
        }

        CoreBooter booter = UnityEngine.Object.FindAnyObjectByType<CoreBooter>();
        if (booter != null) booter.enabled = false;
        Require(core.IsValid(), "Core.unity não pôde ser aberto");
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (_finished || !SessionState.GetBool(RunningKey, false)
            || !EditorApplication.isPlaying || EditorApplication.isCompiling)
            return;

        try
        {
            if (++_frames < 45) return;
            RunAudit();
            Finish();
        }
        catch (Exception exception)
        {
            _finished = true;
            SessionState.SetBool(RunningKey, false);
            SessionState.SetBool(FinishedKey, false);
            Debug.LogError("FINAL_PLAYTEST_COVERAGE_FAIL: " + exception);
            EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }

    private static void RunAudit()
    {
        Scene game = SceneManager.GetSceneByName("GameScene");
        Require(game.IsValid() && game.isLoaded, "GameScene não está carregada");
        AuditUi(game);
        AuditEraAndSaveLoad();
        AuditNavMesh();
        AuditCombatAndSiegeContracts();

        Blocks.Add("combate/cerco com unidades produzidas e alvo BuildingManager real: executar "
            + "VillagerSpearmanLiveProductionPlayModeHarness.Run; este harness não cria gameplay");
        Blocks.Add("playtest humano, controles físicos e revisão visual de pixels: fora do escopo batch");
    }

    private static void AuditUi(Scene game)
    {
        Canvas canvas = game.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
            .FirstOrDefault(item => item.gameObject.name == "Canvas");
        Require(canvas != null && canvas.isActiveAndEnabled, "Canvas real ausente/inativo");
        Require(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() != null,
            "Canvas real sem GraphicRaycaster");

        EventSystem eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
        Require(eventSystem != null && eventSystem.isActiveAndEnabled,
            "EventSystem real ausente/inativo");
        UIManager ui = UnityEngine.Object.FindAnyObjectByType<UIManager>();
        Require(ui != null && ui.isActiveAndEnabled, "UIManager real ausente/inativo");
        Require(ui.resourcesUIParent != null && ui.gameResourceDisplayPrefab != null,
            "UIManager sem recursos ligados");
        Require(ui.techTreePanel != null && ui.gameSettingsPanel != null,
            "UIManager sem painéis de tecnologia/configuração");
        Passes.Add("UI: Canvas + GraphicRaycaster + EventSystem + UIManager/painéis reais");
    }

    private static void AuditEraAndSaveLoad()
    {
        AdvancedEraState.Reset();
        foreach (ImperialEra era in new[]
        {
            ImperialEra.Classical, ImperialEra.Medieval, ImperialEra.Imperial
        })
        {
            AdvancedEraState.Set(era);
            Require(AdvancedEraState.Current == era, "estado de era não acompanhou " + era);
        }
        Passes.Add("eras: Ancient -> Classical -> Medieval -> Imperial em memória");

        string path = Path.Combine(Application.persistentDataPath, "FinalIntegration",
            "coverage-" + Guid.NewGuid().ToString("N") + ".data");
        try
        {
            GameData source = new GameData
            {
                camPosition = Vector3.one,
                players = new GamePlayerData[0],
                unlockedTechnologyNodeCodes = new[] { "coverage_imperial" },
                constructionReplayCommands = new List<ConstructionReplayCommand>(),
                constructionReplayState = null
            };
            SaveMigrationV2Store.Save(path, source);
            SaveMigrationV2ReadResult loaded = SaveMigrationV2Store.Load(path);
            Require(loaded != null && loaded.Succeeded && loaded.Source == SaveMigrationV2Source.V2,
                "SaveMigrationV2 não retornou V2 válido");
            Require(loaded.Data.unlockedTechnologyNodeCodes.Contains("coverage_imperial"),
                "SaveMigrationV2 perdeu o desbloqueio de teste");
            Passes.Add("save/load: round-trip V2 preservou era/tecnologia de auditoria");
        }
        finally
        {
            DeleteSaveArtifacts(path);
            AdvancedEraState.Reset();
        }
    }

    private static void AuditNavMesh()
    {
        NavMeshSurface[] surfaces = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(
            FindObjectsInactive.Include);
        int vertices = NavMesh.CalculateTriangulation().vertices.Length;
        Require(surfaces.Length > 0 && vertices > 0,
            "NavMesh real ausente ou sem triangulação");

        GameObject spawnpoints = GameObject.Find("Spawnpoints");
        Require(spawnpoints != null && spawnpoints.transform.childCount > 0,
            "Spawnpoints reais ausentes");
        NavMeshHit hit;
        Require(NavMesh.SamplePosition(spawnpoints.transform.GetChild(0).position,
            out hit, 30f, NavMesh.AllAreas), "Spawnpoint não amostrado no NavMesh");
        Passes.Add("NavMesh: superfície, triangulação e Spawnpoint amostrado");
    }

    private static void AuditCombatAndSiegeContracts()
    {
        BuildingContentCatalog catalog = Resources.Load<BuildingContentCatalog>(
            "ScriptableObjects/Units/Building Content Catalog");
        Require(catalog != null, "Building Content Catalog ausente");
        Require(BuildingContentProgressionRules.ValidateEraContracts(catalog).Count == 0,
            "catálogo militar possui violações de era");

        ProjectileContract projectile = CatapultDefaults.CreateProjectile();
        CatapultContract catapult = CatapultDefaults.Create(projectile);
        try
        {
            CatapultEraTier tier = catapult.tiers[(int)ImperialEra.Imperial];
            Require(SiegeAttackProfile.CanFire(tier, tier.attackRange, tier.attackCooldown),
                "contrato de cerco rejeitou alcance válido");
            List<SiegeHit> hits = SiegeAttackProfile.ResolveImpact(tier, Vector3.zero, 1, 0,
                new[]
                {
                    new SiegeTarget(1, 1, Vector3.zero),
                    new SiegeTarget(2, 1, Vector3.right * 2f),
                    new SiegeTarget(3, 0, Vector3.right)
                });
            Require(hits.Any(hit => hit.Id == 1 && hit.IsDirect),
                "contrato de cerco não confirmou impacto direto");
            Require(!hits.Any(hit => hit.Id == 3), "contrato de cerco atingiu aliado");
            Passes.Add("combate/cerco: catálogo, alcance, impacto direto e friendly-fire lógico");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(catapult);
            UnityEngine.Object.DestroyImmediate(projectile);
        }
    }

    private static void Finish()
    {
        _finished = true;
        SessionState.SetBool(RunningKey, false);
        bool blocked = Blocks.Count > 0;
        string status = blocked ? "BLOCKED" : "PASS";
        WriteReport(status);
        if (blocked)
            Debug.LogWarning("FINAL_PLAYTEST_COVERAGE_BLOCKED: " + string.Join(" | ", Blocks.ToArray()));
        else
            Debug.Log("FINAL_PLAYTEST_COVERAGE_PASS: " + string.Join(" | ", Passes.ToArray()));
        SessionState.SetBool(FinishedKey, !blocked);
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(blocked ? 2 : 0);
    }

    private static void WriteReport(string status)
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        string path = Path.Combine(root, ReportPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        List<string> lines = new List<string>
        {
            "# Auditoria de cobertura do playtest",
            "",
            "Data: " + DateTime.Now.ToString("s"),
            "",
            "Status: **" + status + "**",
            "",
            "Escopo: harness descartável; nenhuma cena, prefab, ScriptableObject ou asset de produção foi salvo.",
            "",
            "## Passou",
            "",
            "- " + string.Join("\n- ", Passes.ToArray())
        };
        if (Blocks.Count > 0)
        {
            lines.Add("");
            lines.Add("## Continua bloqueado");
            lines.Add("");
            lines.Add("- " + string.Join("\n- ", Blocks.ToArray()));
        }
        File.WriteAllLines(path, lines);
        AssetDatabase.Refresh();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void DeleteSaveArtifacts(string path)
    {
        foreach (string candidate in new[] { path, path + ".tmp", path + ".bak" })
            if (File.Exists(candidate)) File.Delete(candidate);
        string folder = Path.GetDirectoryName(path);
        if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
            Directory.Delete(folder);
    }
}
