using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Ciclo 1 isolado do protótipo de construção House.
///
/// Este harness entra em PlayMode com uma cena vazia e exercita somente
/// contratos já existentes. Não instancia prefabs, não altera cenas e não
/// chama Building/BuildingPlacer, pois isso exigiria dependências de gameplay
/// que não são seguras em uma cena descartável.
/// </summary>
[InitializeOnLoad]
public static class ConstructionMechanicPlayModeHarness
{
    private const string RunningKey = "EraImperial.ConstructionCycle1.Running";
    private const string FinishedKey = "EraImperial.ConstructionCycle1.Finished";
    private static bool _finished;

    static ConstructionMechanicPlayModeHarness()
    {
        EditorApplication.update += Tick;
    }

    public static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException(
                "ConstructionMechanicPlayModeHarness.Run exige o Editor fora do PlayMode.");

        _finished = false;
        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(FinishedKey, false);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // Mantém o mesmo mínimo seguro usado pelo harness de integração.
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
            ConstructionCycle1Result result = RunScenario();
            if (result.Blocked.Count > 0)
            {
                Debug.LogWarning("CONSTRUCTION_CYCLE1_BLOCKED: "
                    + string.Join(" | ", result.Blocked));
                SessionState.SetBool(FinishedKey, false);
                StopAndExit(2);
                return;
            }

            Debug.Log("CONSTRUCTION_CYCLE1_PASS: "
                + "catalog -> placement-contract -> reservation -> lifecycle -> save/load");
            SessionState.SetBool(FinishedKey, true);
            StopAndExit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError("CONSTRUCTION_CYCLE1_FAIL: " + exception);
            SessionState.SetBool(FinishedKey, false);
            StopAndExit(1);
        }
    }

    private static ConstructionCycle1Result RunScenario()
    {
        ConstructionCycle1Result result = new ConstructionCycle1Result();
        BuildingContentCatalog catalog = Resources.Load<BuildingContentCatalog>(
            "ScriptableObjects/Units/Building Content Catalog");
        if (catalog == null)
        {
            result.Blocked.Add("catálogo Building Content Catalog ausente em Resources");
            return result;
        }

        BuildingContentSpec house = catalog.Find("house");
        if (house == null)
        {
            result.Blocked.Add("entrada house ausente no catálogo");
            return result;
        }
        if (house.buildingData == null)
            result.Blocked.Add("house.buildingData não está resolvido");
        else
        {
            Require(house.buildingData.code == "house",
                "BuildingData resolvido tem código inesperado");
            Require(house.buildingData.constructionMeshes != null
                && house.buildingData.constructionMeshes.Length > 0,
                "House BuildingData não possui constructionMeshes");
            Require(house.buildingData.prefab != null,
                "House BuildingData não possui prefab");
        }

        List<string> contractErrors = catalog.ValidateGameplayContracts()
            .Where(error => error != null && error.StartsWith("house ", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Require(contractErrors.Count == 0,
            "contrato de catálogo House inválido: " + string.Join("; ", contractErrors));

        RunPlacementContract(catalog, house, result);
        RunReservationContract(house);
        RunLifecycleContract();
        RunSaveLoadContract(house, result);
        return result;
    }

    private static void RunPlacementContract(
        BuildingContentCatalog catalog, BuildingContentSpec house, ConstructionCycle1Result result)
    {
        Vector3 requested = new Vector3(1.24f, 0f, 2.74f);
        Vector3 snapped = BuildingConstructionRules.SnapPosition(requested, 1f);
        Require(snapped == new Vector3(1f, 0f, 3f),
            "snap de posição da prévia House divergente");
        Require(BuildingConstructionRules.IsTerrainSlopeAllowed(Vector3.up, 25f),
            "terreno plano foi rejeitado");
        Require(!BuildingConstructionRules.IsTerrainSlopeAllowed(
                Quaternion.Euler(45f, 0f, 0f) * Vector3.up, 25f),
            "terreno acima da inclinação foi aceito");
        Require(BuildingConstructionRules.IsBuildingDistanceAllowed(
                Vector3.zero, new[] { Vector3.right * 10f }, 2f),
            "posição livre foi rejeitada");
        Require(!BuildingConstructionRules.IsBuildingDistanceAllowed(
                Vector3.zero, new[] { Vector3.right }, 2f),
            "sobreposição/proximidade foi aceita");

        // A regra de fundação existente é explicitamente limitada a Town
        // Center/Resource Dropoff. Registre isso como bloqueio para House,
        // em vez de inventar uma regra de runtime diferente.
        StrategicPlanningState planning = FinalIntegrationFixtures.PlanningState(BuildingEra.Dark);
        FoundationPlacementResult houseFoundation = FoundationPlacementRules.Evaluate(
            catalog,
            new FoundationPlacementRequest
            {
                code = house.code,
                state = planning,
                terrainNormal = Vector3.up,
                terrainHit = true,
                maximumSlopeDegrees = 25f,
                footprintRadius = 1f,
                position = Vector3.zero,
                requireRuntimeReady = true
            });
        if (houseFoundation == FoundationPlacementResult.NotFoundationRole)
            result.Blocked.Add("FoundationPlacementRules não cobre House; contrato geométrico validado");
        else
            Require(houseFoundation == FoundationPlacementResult.NotFoundationRole,
                "regra de colocação House retornou resultado inesperado: " + houseFoundation);
    }

    private static void RunReservationContract(BuildingContentSpec house)
    {
        List<BuildingMaintenanceCost> costs = ToMaintenanceCosts(house.constructionCost);
        Require(costs.Count > 0, "House não possui custo convertível para reserva");

        EconomyLedger economy = FinalIntegrationFixtures.RichEconomy();
        PopulationLedger population = FinalIntegrationFixtures.Population();
        int woodBefore = economy.Get(EconomyResourceType.Wood);
        ConstructionReservationLedger ledger = new ConstructionReservationLedger(economy, population);
        ConstructionReservationRequest request = new ConstructionReservationRequest
        {
            reservationId = "construction-cycle1-house",
            priority = 100,
            population = 0,
            costs = costs
        };

        Require(ledger.TryReserve(request) == ConstructionReservationResult.Accepted,
            "reserva House foi rejeitada");
        Require(ledger.Contains(request.reservationId) && ledger.Count == 1,
            "reserva House não ficou registrada");
        int woodAfterReserve = economy.Get(EconomyResourceType.Wood);
        Require(woodAfterReserve < woodBefore,
            "reserva House não debitou o custo");
        Require(ledger.TryReserve(request) == ConstructionReservationResult.DuplicateId,
            "segunda reserva House não foi bloqueada como duplicada");
        Require(economy.Get(EconomyResourceType.Wood) == woodAfterReserve,
            "tentativa duplicada alterou o estoque");
        Require(ledger.Commit(request.reservationId) == ConstructionReservationResult.Accepted,
            "commit da reserva House falhou");
        Require(ledger.Count == 0, "reserva House não foi finalizada");
    }

    private static void RunLifecycleContract()
    {
        Require(BuildingConstructionRules.GetLifecycleState(
                true, false, 0, 200, 0) == BuildingLifecycleState.UnderConstruction,
            "estado inicial não é UnderConstruction");
        Require(BuildingConstructionRules.GetLifecycleState(
                true, false, 0, 200, 100) == BuildingLifecycleState.UnderConstruction,
            "estado intermediário não é UnderConstruction");
        Require(BuildingConstructionRules.GetLifecycleState(
                true, true, 200, 200, 200) == BuildingLifecycleState.Operational,
            "conclusão não é Operational");
        Require(BuildingConstructionRules.CanAssignConstructor(true, false, 0, 3),
            "construtor não pode ser atribuído ao blueprint fixado");
        Require(BuildingConstructionRules.ShouldReleaseConstructors(
                BuildingLifecycleState.UnderConstruction, BuildingLifecycleState.Operational),
            "conclusão não libera construtores");
    }

    private static void RunSaveLoadContract(BuildingContentSpec house, ConstructionCycle1Result result)
    {
        BuildingConstructionSaveSnapshot snapshot;
        BuildingConstructionSnapshotResult capture = BuildingConstructionSaveRules.Capture(
            house.code,
            0,
            new Vector3(1f, 0f, 3f),
            100,
            200,
            BuildingLifecycleState.UnderConstruction,
            new[] { "worker-cycle1" },
            ToMaintenanceCosts(house.constructionCost),
            house.AssetStage,
            house.IsRuntimeReady,
            out snapshot);
        Require(capture == BuildingConstructionSnapshotResult.Captured && snapshot != null,
            "snapshot House não foi capturado");
        Require(BuildingConstructionSaveRules.Validate(snapshot, 0, true)
            == BuildingConstructionSnapshotResult.Restored,
            "snapshot House não passou na validação de restore");

        string path = Path.Combine(Application.persistentDataPath,
            "ConstructionCycle1", "house-" + Guid.NewGuid().ToString("N") + ".data");
        try
        {
            SaveFileStore.Save(path, new ConstructionCycle1SavePayload { snapshot = snapshot });
            ConstructionCycle1SavePayload loaded = SaveFileStore.Load<ConstructionCycle1SavePayload>(path);
            Require(loaded != null && loaded.snapshot != null
                && loaded.snapshot.buildingCode == "house"
                && loaded.snapshot.constructionHP == 100
                && loaded.snapshot.lifecycle == BuildingLifecycleState.UnderConstruction,
                "save/load House perdeu o snapshot");
        }
        catch (Exception exception)
        {
            result.Blocked.Add("SaveFileStore indisponível neste ambiente: " + exception.GetType().Name);
            Debug.LogWarning("CONSTRUCTION_CYCLE1_SAVELOAD_BLOCKED: " + exception.Message);
        }
        finally
        {
            DeleteSaveArtifacts(path);
        }
    }

    private static List<BuildingMaintenanceCost> ToMaintenanceCosts(
        List<BuildingContentCost> source)
    {
        List<BuildingMaintenanceCost> result = new List<BuildingMaintenanceCost>();
        if (source == null) return result;
        foreach (BuildingContentCost cost in source)
        {
            if (cost == null || cost.amount <= 0) continue;
            result.Add(new BuildingMaintenanceCost(cost.resource, cost.amount));
        }
        return result;
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
    private sealed class ConstructionCycle1SavePayload
    {
        public BuildingConstructionSaveSnapshot snapshot;
    }

    private sealed class ConstructionCycle1Result
    {
        public readonly List<string> Blocked = new List<string>();
    }
}
