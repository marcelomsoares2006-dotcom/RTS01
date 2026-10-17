using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Siege;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runner PlayMode sem cena, prefab ou FBX. É deliberadamente separado do
/// smoke de gameplay existente para testar somente a cadeia final em estado
/// descartável e encerrar com código de processo verificável.
/// </summary>
[InitializeOnLoad]
public static class FinalIntegrationPlayModeHarness
{
    private const string RunningKey = "EraImperial.FinalIntegration.Running";
    private const string FinishedKey = "EraImperial.FinalIntegration.Finished";
    private static bool _finished;

    static FinalIntegrationPlayModeHarness()
    {
        EditorApplication.update += Tick;
    }

    public static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("FinalIntegrationPlayModeHarness.Run exige o Editor fora do PlayMode.");

        _finished = false;
        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(FinishedKey, false);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // O DebugConsole é criado automaticamente após o carregamento da cena
        // e registra listeners no EventManager. O harness usa uma cena vazia,
        // portanto cria explicitamente a dependência mínima do cenário.
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
            RunScenario();
            Debug.Log("FINAL_INTEGRATION_PLAYMODE_PASS: production -> advanced unit -> siege -> save/load -> imperial");
            SessionState.SetBool(FinishedKey, true);
            StopAndExit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError("FINAL_INTEGRATION_PLAYMODE_FAIL: " + exception);
            SessionState.SetBool(FinishedKey, false);
            StopAndExit(1);
        }
    }

    private static void RunScenario()
    {
        BuildingContentCatalog catalog = FinalIntegrationFixtures.LoadMilitaryCatalog();
        Require(catalog != null, "catálogo militar ausente em Resources");

        StrategicPlanningState planning = FinalIntegrationFixtures.PlanningState(BuildingEra.Dark);
        ConstructionReplayRuntime.Restore(new List<ConstructionReplayCommand>(), null);
        Require(ConstructionReplayRuntime.TryAdvanceEra(
            0, BuildingEra.Feudal, 1, 1, 100, "play-classical") == ConstructionReplayResult.Accepted,
            "Ancient -> Classical rejeitado");
        Require(ConstructionReplayRuntime.TryAdvanceEra(
            0, BuildingEra.Castle, 2, 2, 100, "play-medieval") == ConstructionReplayResult.Accepted,
            "Classical -> Medieval rejeitado");
        Require(ConstructionReplayRuntime.TryAdvanceEra(
            0, BuildingEra.Imperial, 3, 3, 100, "play-imperial") == ConstructionReplayResult.Accepted,
            "Medieval -> Imperial rejeitado");
        Require(AdvancedEraState.Current == ImperialEra.Imperial,
            "AdvancedEraState não acompanhou o replay até Imperial");

        planning.era = BuildingEra.Imperial;
        FinalIntegrationFixtures.AddOperationalBuilding(planning, "siege_workshop");
        BuildingProductionEntry production;
        BuildingProductionCatalogResult catalogResult;
        Require(BuildingProductionCatalogRules.TryCreateLegacyQueueEntry(
            catalog, "siege_workshop", "catapult", planning,
            out production, out catalogResult), "catapulta não resolvida: " + catalogResult);

        EconomyLedger economy = FinalIntegrationFixtures.RichEconomy();
        PopulationLedger population = FinalIntegrationFixtures.Population();
        BuildingProductionQueue queue = new BuildingProductionQueue(3);
        Require(queue.TryEnqueue(production, economy, population)
            == BuildingProductionQueueResult.Accepted, "produção da catapulta rejeitada");
        queue.Advance(production.productionTime);
        Require(queue.Count == 0, "fila militar não concluiu a catapulta");

        CatapultContract catapult = CatapultDefaults.Create(CatapultDefaults.CreateProjectile());
        try
        {
            CatapultEraTier tier = catapult.tiers[(int)ImperialEra.Imperial];
            Require(SiegeAttackProfile.CanFire(tier, 12f, tier.attackCooldown),
                "catapulta não disparou dentro do envelope de cerco");
            List<SiegeHit> hits = SiegeAttackProfile.ResolveImpact(
                tier, Vector3.zero, 1, 0,
                new[]
                {
                    new SiegeTarget(1, 1, Vector3.zero),
                    new SiegeTarget(2, 1, Vector3.right * 2f)
                });
            Require(hits.Any(hit => hit.Id == 1 && hit.IsDirect),
                "impacto direto da catapulta não foi resolvido");
            Require(hits.Any(hit => hit.Id == 2 && !hit.IsDirect),
                "dano radial da catapulta não foi resolvido");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(catapult.Projectile);
            UnityEngine.Object.DestroyImmediate(catapult);
        }

        string path = Path.Combine(Application.persistentDataPath,
            "FinalIntegration", "playmode-" + Guid.NewGuid().ToString("N") + ".data");
        try
        {
            SaveFileStore.Save(path, new PlayModeSavePayload
            {
                era = AdvancedEraState.Current,
                produced = "catapult",
                replayCommands = ConstructionReplayRuntime.ExportCommands()
            });
            PlayModeSavePayload loaded = SaveFileStore.Load<PlayModeSavePayload>(path);
            Require(loaded != null && loaded.era == ImperialEra.Imperial
                && loaded.produced == "catapult"
                && loaded.replayCommands.Count == 3,
                "save/load PlayMode perdeu a era, a produção ou o replay");
        }
        finally
        {
            DeleteSaveArtifacts(path);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void StopAndExit(int code)
    {
        SessionState.SetBool(RunningKey, false);
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(code);
    }

    private static void DeleteSaveArtifacts(string path)
    {
        foreach (string candidate in new[] { path, path + ".tmp", path + ".bak" })
            if (File.Exists(candidate)) File.Delete(candidate);
        string folder = Path.GetDirectoryName(path);
        if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
            Directory.Delete(folder);
    }

    [Serializable]
    private sealed class PlayModeSavePayload
    {
        public ImperialEra era;
        public string produced;
        public List<ConstructionReplayCommand> replayCommands;
    }
}
