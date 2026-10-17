using System;
using System.Collections.Generic;
using System.IO;
using EraImperial.SaveMigrationV2;
using EraImperial.Units.Advanced;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Ciclo 3 isolado do protótipo jogável de avanço de eras.
///
/// Exercita somente regras e o replay existentes: Ancient -> Classical ->
/// Medieval -> Imperial, rejeições determinísticas, desbloqueios por era e
/// round-trip pelo SaveMigrationV2. Não inventa nem afirma uma UI jogável.
/// </summary>
[InitializeOnLoad]
public static class EraProgressionPlayModeHarness
{
    private const string RunningKey = "EraImperial.EraProgressionCycle3.Running";
    private const string FinishedKey = "EraImperial.EraProgressionCycle3.Finished";
    private static bool _finished;

    static EraProgressionPlayModeHarness()
    {
        EditorApplication.update += Tick;
    }

    public static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException(
                "EraProgressionPlayModeHarness.Run exige o Editor fora do PlayMode.");

        _finished = false;
        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(FinishedKey, false);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("EventManager").AddComponent<EventManager>();
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (_finished || !SessionState.GetBool(RunningKey, false)
            || !EditorApplication.isPlaying || EditorApplication.isCompiling)
            return;

        _finished = true;
        try
        {
            Require(Application.isPlaying, "o runner não entrou em PlayMode");
            EraProgressionCycle3Result result = RunScenario();
            if (result.Blocked.Count > 0)
            {
                Debug.LogWarning("ERA_PROGRESSION_CYCLE3_BLOCKED: "
                    + string.Join(" | ", result.Blocked.ToArray()));
                SessionState.SetBool(FinishedKey, false);
                StopAndExit(2);
                return;
            }

            Debug.Log("ERA_PROGRESSION_CYCLE3_PASS: "
                + "Ancient -> Classical -> Medieval -> Imperial -> "
                + "rejections -> era-unlocks -> V2-save-load; UI/feedback real BLOCKED");
            SessionState.SetBool(FinishedKey, true);
            StopAndExit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError("ERA_PROGRESSION_CYCLE3_FAIL: " + exception);
            SessionState.SetBool(FinishedKey, false);
            StopAndExit(1);
        }
    }

    private static EraProgressionCycle3Result RunScenario()
    {
        EraProgressionCycle3Result result = new EraProgressionCycle3Result();
        StrategicPlanningState state = CreatePlanningState();

        RunRejectedTransitions(state);
        RunEraProgression(state);
        RunSaveLoadRoundTrip(result);

        // Não há contrato de UI/feedback audiovisual neste harness editor-free.
        result.Blocked.Add("UI real de avanço, feedback visual/sonoro e playtest manual não são exercitados por este harness");
        return result;
    }

    private static void RunRejectedTransitions(StrategicPlanningState state)
    {
        ConstructionReplayRuntime.Restore(new List<ConstructionReplayCommand>(), null);
        Require(AdvancedEraState.Current == ImperialEra.Ancient,
            "reset do replay não iniciou AdvancedEraState em Ancient");

        // Salto direto deve ser rejeitado tanto pela regra de planejamento
        // quanto pelo comando canônico do replay.
        EraTransitionRequirement jump = new EraTransitionRequirement(BuildingEra.Castle);
        Require(EraProgressionRules.Evaluate(state, jump)
            == ProgressionRequirementResult.InvalidTargetEra,
            "salto Ancient -> Medieval não foi rejeitado pela regra de era");
        Require(ConstructionReplayRuntime.TryAdvanceEra(
                0, BuildingEra.Castle, 1, 1, 100, "cycle3-invalid-jump")
            == ConstructionReplayResult.InvalidCommand,
            "replay aceitou salto direto para Medieval");

        // Requisito tecnológico ausente deve bloquear a transição; a política
        // pode encaminhá-lo para pesquisa, mas não pode declarar age-up pronto.
        EraTransitionRequirement missing = new EraTransitionRequirement(BuildingEra.Feudal);
        missing.requiredTechnologyCodes.Add("cycle3_feudal_technology");
        Require(EraProgressionRules.Evaluate(state, missing)
            == ProgressionRequirementResult.MissingTechnology,
            "requisito tecnológico ausente não foi detectado");
        Require(!EraProgressionRules.TryAdvance(state, missing),
            "avanço foi aplicado sem requisito tecnológico");

        StrategicActionIntent intent;
        string rationale;
        TechnologyDefinition technology = Technology("cycle3_feudal_technology", BuildingEra.Dark);
        Require(AgeUpTechnologyPlanningRules.TryCreateIntent(
                state, missing, new[] { technology }, out intent, out rationale),
            "planejador não encaminhou requisito faltante para pesquisa");
        Require(intent != null && intent.action == StrategicActionKind.ResearchTechnology
            && intent.targetCode == technology.code,
            "planejador produziu intenção incorreta para requisito faltante");
    }

    private static void RunEraProgression(StrategicPlanningState state)
    {
        AdvanceOneEra(state, BuildingEra.Feudal, ImperialEra.Classical,
            "cycle3_feudal_technology", 1, "cycle3-age-classical");
        AdvanceOneEra(state, BuildingEra.Castle, ImperialEra.Medieval,
            "cycle3_castle_technology", 2, "cycle3-age-medieval");
        AdvanceOneEra(state, BuildingEra.Imperial, ImperialEra.Imperial,
            "cycle3_imperial_technology", 3, "cycle3-age-imperial");

        Require(state.era == BuildingEra.Imperial,
            "estado de planejamento não terminou em Imperial");
        Require(AdvancedEraState.Current == ImperialEra.Imperial,
            "AdvancedEraState não terminou em Imperial");
    }

    private static void AdvanceOneEra(
        StrategicPlanningState state,
        BuildingEra targetEra,
        ImperialEra expectedImperialEra,
        string technologyCode,
        long tick,
        string commandId)
    {
        TechnologyDefinition technology = Technology(technologyCode, state.era);
        EraTransitionRequirement requirement = new EraTransitionRequirement(targetEra);
        requirement.requiredTechnologyCodes.Add(technologyCode);

        StrategicActionIntent intent;
        string rationale;
        Require(AgeUpTechnologyPlanningRules.TryCreateIntent(
                state, requirement, new[] { technology }, out intent, out rationale), rationale);
        Require(intent != null && intent.action == StrategicActionKind.ResearchTechnology,
            "age-up não exigiu a tecnologia " + technologyCode);
        Require(TechnologyProgressionRules.Evaluate(state, technology)
            == ProgressionRequirementResult.Allowed,
            "tecnologia da era não estava desbloqueável no momento correto");
        Require(TechnologyProgressionRules.TryUnlock(state, technology),
            "não foi possível desbloquear " + technologyCode);

        Require(AgeUpTechnologyPlanningRules.TryCreateIntent(
                state, requirement, new[] { technology }, out intent, out rationale), rationale);
        Require(intent != null && intent.action == StrategicActionKind.AdvanceEra,
            "planejador não liberou avanço após a pesquisa");
        Require(ConstructionReplayRuntime.TryAdvanceEra(
                state.ownerId, targetEra, tick, tick, 100, commandId)
            == ConstructionReplayResult.Accepted,
            "replay rejeitou avanço para " + targetEra);

        state.era = targetEra;
        Require(AdvancedEraState.Current == expectedImperialEra,
            "desbloqueio visual/estado não acompanhou " + targetEra);
    }

    private static void RunSaveLoadRoundTrip(EraProgressionCycle3Result result)
    {
        string path = Path.Combine(Application.persistentDataPath, "Data_Dev",
            "Tests", "era-progression-cycle3-" + Guid.NewGuid().ToString("N") + ".data");
        try
        {
            GameData.gameUid = "era-progression-cycle3";
            ConstructionReplaySnapshot snapshot = ConstructionReplayRuntime.State;
            Require(snapshot != null && snapshot.era == BuildingEra.Imperial,
                "snapshot Imperial ausente antes do save");

            GameData source = new GameData {
                camPosition = Vector3.zero,
                players = new GamePlayerData[0],
                unlockedTechnologyNodeCodes = new[] {
                    "cycle3_feudal_technology", "cycle3_castle_technology",
                    "cycle3_imperial_technology"
                },
                constructionReplayCommands = ConstructionReplayRuntime.ExportCommands(),
                constructionReplayState = snapshot
            };
            SaveMigrationV2Store.Save(path, source);
            SaveMigrationV2ReadResult loaded = SaveMigrationV2Store.Load(path);
            Require(loaded != null && loaded.Succeeded, "SaveMigrationV2 não carregou: " + (loaded == null ? "null" : loaded.Error));
            Require(loaded.Source == SaveMigrationV2Source.V2,
                "save de era não retornou a fonte V2");
            Require(loaded.Data != null && loaded.Data.constructionReplayState != null
                && loaded.Data.constructionReplayState.era == BuildingEra.Imperial,
                "save/load perdeu a era Imperial");
            Require(loaded.Data.constructionReplayCommands != null
                && loaded.Data.constructionReplayCommands.Count == 3,
                "save/load perdeu comandos de avanço");
            Require(Array.IndexOf(loaded.Data.unlockedTechnologyNodeCodes,
                "cycle3_imperial_technology") >= 0,
                "save/load perdeu desbloqueio Imperial");

            AdvancedEraState.Reset();
            ConstructionReplayRuntime.Restore(new List<ConstructionReplayCommand>(), null);
            Require(AdvancedEraState.Current == ImperialEra.Ancient,
                "reset pós-save não retornou a Ancient");
            Require(ConstructionReplayRuntime.Restore(
                    loaded.Data.constructionReplayCommands,
                    loaded.Data.constructionReplayState)
                == ConstructionReplayResult.Accepted,
                "replay restaurado pelo save foi rejeitado");
            Require(AdvancedEraState.Current == ImperialEra.Imperial,
                "restore do save não republicou Imperial");
        }
        finally
        {
            DeleteIfExists(path);
            DeleteIfExists(path + ".bak");
            DeleteIfExists(path + ".tmp");
        }
    }

    private static StrategicPlanningState CreatePlanningState()
    {
        return new StrategicPlanningState {
            ownerId = 0,
            era = BuildingEra.Dark,
            population = 100,
            buildings = new List<StrategicBuildingObservation> {
                new StrategicBuildingObservation {
                    ownerId = 0,
                    code = "town_center",
                    lifecycle = BuildingLifecycleState.Operational
                }
            },
            unlockedTechnologyCodes = new List<string>()
        };
    }

    private static TechnologyDefinition Technology(string code, BuildingEra minimumEra)
    {
        return new TechnologyDefinition {
            code = code,
            minimumEra = minimumEra,
            prerequisiteTechnologyCodes = new List<string>(),
            requiredBuildingCodes = new List<string> { "town_center" },
            cost = new List<ProgressionResourceRequirement>()
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private static void StopAndExit(int code)
    {
        SessionState.SetBool(RunningKey, false);
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(code);
    }
}

internal sealed class EraProgressionCycle3Result
{
    public readonly List<string> Blocked = new List<string>();
}
