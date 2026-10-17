using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Ciclo 2 isolado do protótipo economia/produção.
///
/// Exercita somente contratos existentes: catálogo, ledger, coletor
/// determinístico, fila de produção, cancelamento/reembolso e snapshot/save.
/// Não instancia gameplay, não salva cenas/prefabs e não cria assets.
/// </summary>
[InitializeOnLoad]
public static class ProductionEconomyPlayModeHarness
{
    private const string RunningKey = "EraImperial.ProductionEconomyCycle2.Running";
    private const string FinishedKey = "EraImperial.ProductionEconomyCycle2.Finished";
    private static bool _finished;

    static ProductionEconomyPlayModeHarness()
    {
        EditorApplication.update += Tick;
    }

    public static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException(
                "ProductionEconomyPlayModeHarness.Run exige o Editor fora do PlayMode.");

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
            ProductionEconomyCycle2Result result = RunScenario();
            if (result.Blocked.Count > 0)
            {
                Debug.LogWarning("PRODUCTION_ECONOMY_CYCLE2_BLOCKED: "
                    + string.Join(" | ", result.Blocked));
                SessionState.SetBool(FinishedKey, false);
                StopAndExit(2);
                return;
            }

            Debug.Log("PRODUCTION_ECONOMY_CYCLE2_PASS: "
                + "catalog -> ledger -> collector-contract -> "
                + "town-center-queue -> barracks-queue -> cancel-refund -> save-load");
            SessionState.SetBool(FinishedKey, true);
            StopAndExit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError("PRODUCTION_ECONOMY_CYCLE2_FAIL: " + exception);
            SessionState.SetBool(FinishedKey, false);
            StopAndExit(1);
        }
    }

    private static ProductionEconomyCycle2Result RunScenario()
    {
        ProductionEconomyCycle2Result result = new ProductionEconomyCycle2Result();
        BuildingContentCatalog catalog = Resources.Load<BuildingContentCatalog>(
            "ScriptableObjects/Units/Building Content Catalog");
        if (catalog == null)
        {
            result.Blocked.Add("Building Content Catalog ausente em Resources");
            return result;
        }

        RunCatalogContract(catalog, result);
        RunInitialLedgerContract();
        RunCollectorContract(catalog, result);

        StrategicPlanningState state = FinalIntegrationFixtures.PlanningState(BuildingEra.Dark);
        EconomyLedger stockpile = FinalIntegrationFixtures.RichEconomy();
        PopulationLedger population = FinalIntegrationFixtures.Population();

        RunTownCenterQueue(catalog, state, stockpile, population, result);
        RunBarracksQueue(catalog, state, stockpile, population, result);
        RunSaveLoad(catalog, state, stockpile, population, result);
        return result;
    }

    private static void RunCatalogContract(
        BuildingContentCatalog catalog, ProductionEconomyCycle2Result result)
    {
        Require(catalog.Find("town_center") != null,
            "catálogo não possui town_center");
        Require(catalog.Find("barracks") != null,
            "catálogo não possui barracks");

        List<string> errors = catalog.ValidateGameplayContracts()
            .Where(error => !string.IsNullOrWhiteSpace(error)).ToList();
        Require(errors.Count == 0,
            "contratos do catálogo inválidos: " + string.Join("; ", errors));

        BuildingContentSpec townCenter = catalog.Find("town_center");
        BuildingContentSpec barracks = catalog.Find("barracks");
        if (!townCenter.IsRuntimeReady || !barracks.IsRuntimeReady)
            result.Blocked.Add("Town Center ou Barracks não está RuntimeReady");
        if (!HasProduction(townCenter, "villager"))
            result.Blocked.Add("Town Center não possui produção villager");
        if (!HasAnyProduction(barracks))
            result.Blocked.Add("Barracks não possui produção catalogada");
    }

    private static void RunInitialLedgerContract()
    {
        EconomyLedger ledger = new EconomyLedger();
        foreach (EconomyResourceType resource in Enum.GetValues(typeof(EconomyResourceType)))
            Require(ledger.Get(resource) == 0,
                "ledger novo não iniciou recurso em zero: " + resource);

        ledger.Set(EconomyResourceType.Food, 120);
        ledger.Add(EconomyResourceType.Food, 30);
        Require(ledger.Get(EconomyResourceType.Food) == 150,
            "ledger não acumulou Food corretamente");
        Require(ledger.TrySpend(EconomyResourceType.Food, 50),
            "ledger rejeitou gasto disponível");
        Require(ledger.Get(EconomyResourceType.Food) == 100,
            "ledger debitou valor incorreto");
        Require(!ledger.TrySpend(EconomyResourceType.Food, 101),
            "ledger aceitou gasto acima do estoque");
        Require(ledger.Get(EconomyResourceType.Food) == 100,
            "gasto rejeitado alterou o ledger");
    }

    private static void RunCollectorContract(
        BuildingContentCatalog catalog, ProductionEconomyCycle2Result result)
    {
        BuildingContentSpec townCenter = catalog.Find("town_center");
        if (townCenter == null || !townCenter.supportsDropoff)
        {
            result.Blocked.Add("não existe contrato de dropoff operacional para coleta");
            return;
        }

        EconomyPlayerState economy = new EconomyPlayerState();
        economy.stockpile.Set(EconomyResourceType.Wood, 10);
        economy.collectors.Add(new EconomyCollector(
            "worker-cycle2", 0, EconomyResourceType.Wood, 2f, 10f));
        economy.TickCollectors(5f);
        Require(economy.stockpile.Get(EconomyResourceType.Wood) == 10,
            "TickCollectors depositou antes de haver carga, contrato inesperado");
        economy.TickCollectors(0f);
        Require(economy.collectors[0].CarriedAmount == 0f
            && economy.stockpile.Get(EconomyResourceType.Wood) == 20,
            "coletor existente não entregou a carga no ledger");

        // Não há aqui movimento/chegada de worker nem binding de Building real.
        // O contrato de coleta é validado, mas a produção viva de coleta fica
        // explicitamente bloqueada até existir essa integração runtime.
        result.Blocked.Add("coleta trabalhador->movimento->dropoff não possui API runtime integrada");
    }

    private static void RunTownCenterQueue(
        BuildingContentCatalog catalog, StrategicPlanningState state,
        EconomyLedger stockpile, PopulationLedger population,
        ProductionEconomyCycle2Result result)
    {
        BuildingProductionEntry entry;
        BuildingProductionCatalogResult validation;
        if (!BuildingProductionCatalogRules.TryCreateLegacyQueueEntry(
            catalog, "town_center", "villager", state,
            out entry, out validation))
        {
            result.Blocked.Add("fila Town Center/villager indisponível: " + validation);
            return;
        }

        BuildingProductionQueue queue = new BuildingProductionQueue(entry.queueLimit);
        int foodBefore = stockpile.Get(EconomyResourceType.Food);
        Require(BuildingProductionCommandRules.TryEnqueue(
                catalog, state, queue, "town_center", "villager",
                stockpile, population, out _)
            == BuildingProductionCommandResult.Accepted,
            "Town Center rejeitou villager com recursos e população disponíveis");
        Require(queue.Count == 1 && stockpile.Get(EconomyResourceType.Food) < foodBefore,
            "Town Center não reservou a produção");
        Require(queue.Advance(entry.productionTime).Contains("villager"),
            "Town Center não completou villager");
    }

    private static void RunBarracksQueue(
        BuildingContentCatalog catalog, StrategicPlanningState state,
        EconomyLedger stockpile, PopulationLedger population,
        ProductionEconomyCycle2Result result)
    {
        FinalIntegrationFixtures.AddOperationalBuilding(state, "barracks");
        BuildingProductionEntry entry;
        BuildingProductionCatalogResult validation;
        if (!BuildingProductionCatalogRules.TryCreateLegacyQueueEntry(
            catalog, "barracks", "spearman", state,
            out entry, out validation))
        {
            result.Blocked.Add("fila Barracks/spearman indisponível: " + validation);
            return;
        }

        BuildingProductionQueue queue = new BuildingProductionQueue(entry.queueLimit);
        int woodBefore = stockpile.Get(EconomyResourceType.Wood);
        int foodBefore = stockpile.Get(EconomyResourceType.Food);
        Require(BuildingProductionCommandRules.TryEnqueue(
                catalog, state, queue, "barracks", "spearman",
                stockpile, population, out _)
            == BuildingProductionCommandResult.Accepted,
            "Barracks rejeitou spearman com recursos e população disponíveis");
        Require(queue.Count == 1
            && stockpile.Get(EconomyResourceType.Wood) < woodBefore
            && stockpile.Get(EconomyResourceType.Food) < foodBefore,
            "Barracks não reservou o custo do spearman");

        int foodAfterReserve = stockpile.Get(EconomyResourceType.Food);
        int woodAfterReserve = stockpile.Get(EconomyResourceType.Wood);
        BuildingProductionOrder cancelled;
        Require(BuildingProductionCommandRules.TryCancel(
                catalog, state, queue, "barracks", 0,
                stockpile, population, out cancelled)
            == BuildingProductionCommandResult.Accepted,
            "cancelamento de spearman falhou");
        Require(cancelled != null && queue.Count == 0,
            "cancelamento não removeu a ordem");
        Require(stockpile.Get(EconomyResourceType.Food) > foodAfterReserve
            && stockpile.Get(EconomyResourceType.Wood) > woodAfterReserve,
            "cancelamento não reembolsou os recursos");
    }

    private static void RunSaveLoad(
        BuildingContentCatalog catalog, StrategicPlanningState state,
        EconomyLedger stockpile, PopulationLedger population,
        ProductionEconomyCycle2Result result)
    {
        BuildingProductionEntry entry;
        BuildingProductionCatalogResult validation;
        if (!BuildingProductionCatalogRules.TryCreateLegacyQueueEntry(
            catalog, "town_center", "villager", state,
            out entry, out validation))
        {
            result.Blocked.Add("save/load sem produto Town Center: " + validation);
            return;
        }

        BuildingProductionQueue source = new BuildingProductionQueue(entry.queueLimit);
        Require(source.TryEnqueue(entry, stockpile, population)
            == BuildingProductionQueueResult.Accepted,
            "não foi possível preparar ordem para save/load");
        source.Advance(entry.productionTime * 0.25f);

        BuildingProductionQueueSnapshot snapshot;
        Require(BuildingProductionSaveRules.Capture(
                catalog, state, source, "town_center", out snapshot)
            == BuildingProductionSnapshotResult.Captured,
            "snapshot de produção não foi capturado");

        string path = Path.Combine(Application.persistentDataPath,
            "ProductionEconomyCycle2", "queue-" + Guid.NewGuid().ToString("N") + ".data");
        try
        {
            SaveFileStore.Save(path, new ProductionEconomySavePayload { snapshot = snapshot });
            ProductionEconomySavePayload loaded = SaveFileStore.Load<ProductionEconomySavePayload>(path);
            Require(loaded != null && loaded.snapshot != null
                && loaded.snapshot.buildingCode == snapshot.buildingCode
                && loaded.snapshot.orders.Count == snapshot.orders.Count,
                "SaveFileStore perdeu o snapshot de produção");

            BuildingProductionQueue restored = new BuildingProductionQueue(entry.queueLimit);
            Require(BuildingProductionSaveRules.Restore(
                    catalog, state, restored, loaded.snapshot)
                == BuildingProductionSnapshotResult.Restored,
                "snapshot de produção não foi restaurado");
            Require(restored.Count == 1 && restored.Peek.productCode == "villager",
                "fila restaurada não preservou villager");
        }
        catch (Exception exception)
        {
            result.Blocked.Add("SaveFileStore indisponível: " + exception.GetType().Name);
            Debug.LogWarning("PRODUCTION_ECONOMY_CYCLE2_SAVELOAD_BLOCKED: " + exception.Message);
        }
        finally
        {
            DeleteSaveArtifacts(path);
        }
    }

    private static bool HasProduction(BuildingContentSpec entry, string product)
    {
        if (entry == null || entry.production == null) return false;
        return entry.production.Any(item => item != null
            && string.Equals(item.productCode, product, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasAnyProduction(BuildingContentSpec entry)
    {
        return entry != null && entry.production != null
            && entry.production.Any(item => item != null
                && !string.IsNullOrWhiteSpace(item.productCode));
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
    private sealed class ProductionEconomySavePayload
    {
        public BuildingProductionQueueSnapshot snapshot;
    }

    private sealed class ProductionEconomyCycle2Result
    {
        public readonly List<string> Blocked = new List<string>();
    }
}
