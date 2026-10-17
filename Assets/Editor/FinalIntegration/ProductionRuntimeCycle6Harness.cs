using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Ciclo 6: protótipo isolado da produção runtime.
///
/// O primeiro bloco exercita o fluxo real disponível no domínio: catálogo ->
/// reserva -> avanço da fila -> callback de produto concluído. O segundo bloco
/// tenta localizar as dependências necessárias para materializar um produto em
/// uma partida. Como este harness não cria nem altera prefabs/cenas de produção,
/// ele deve terminar como BLOCKED quando não há Building/GameManager/economia
/// vivos para o BuildingProductionRuntimeDriver.
/// </summary>
[InitializeOnLoad]
public static class ProductionRuntimeCycle6Harness
{
    private const string RunningKey = "EraImperial.ProductionRuntimeCycle6.Running";
    private const string FinishedKey = "EraImperial.ProductionRuntimeCycle6.Finished";
    private static bool _finished;

    static ProductionRuntimeCycle6Harness()
    {
        EditorApplication.update += Tick;
    }

    /// <summary>Executa em uma cena vazia e não salva a cena.</summary>
    public static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException(
                "ProductionRuntimeCycle6Harness.Run exige o Editor fora do PlayMode.");

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
            ProductionRuntimeCycle6Result result = RunScenario();
            if (result.Blocked.Count > 0)
            {
                Debug.LogWarning("PRODUCTION_RUNTIME_CYCLE6_BLOCKED: "
                    + string.Join(" | ", result.Blocked.ToArray()));
                SessionState.SetBool(FinishedKey, false);
                StopAndExit(2);
                return;
            }

            Debug.Log("PRODUCTION_RUNTIME_CYCLE6_PASS: catálogo -> reserva -> "
                + "avanço -> callback -> materialização runtime");
            SessionState.SetBool(FinishedKey, true);
            StopAndExit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError("PRODUCTION_RUNTIME_CYCLE6_FAIL: " + exception);
            SessionState.SetBool(FinishedKey, false);
            StopAndExit(1);
        }
    }

    private static ProductionRuntimeCycle6Result RunScenario()
    {
        ProductionRuntimeCycle6Result result = new ProductionRuntimeCycle6Result();
        BuildingContentCatalog catalog = Resources.Load<BuildingContentCatalog>(
            "ScriptableObjects/Units/Building Content Catalog");
        Require(catalog != null, "Building Content Catalog ausente em Resources");

        StrategicPlanningState state = FinalIntegrationFixtures.PlanningState(BuildingEra.Dark);
        EconomyLedger stockpile = FinalIntegrationFixtures.RichEconomy();
        PopulationLedger population = FinalIntegrationFixtures.Population();

        BuildingProductionEntry villager = RequireEntry(
            catalog, "town_center", "villager", state);
        BuildingProductionEntry spearman = RequireEntry(
            catalog, "barracks", "spearman", state);

        RunAdapterFlow("town_center", 0, villager, stockpile, population, result, "villager");

        // O contrato de barracks exige a construção operacional no estado de
        // planejamento. Isso valida a regra sem criar um Building/prefab real.
        FinalIntegrationFixtures.AddOperationalBuilding(state, "barracks");
        RunAdapterFlow("barracks", 0, spearman, stockpile, population, result, "spearman");

        ProbeLiveRuntimeDependencies(catalog, result);
        return result;
    }

    private static void RunAdapterFlow(
        string buildingId,
        int owner,
        BuildingProductionEntry entry,
        EconomyLedger stockpile,
        PopulationLedger population,
        ProductionRuntimeCycle6Result result,
        string expectedProduct)
    {
        List<string> completed = new List<string>();
        BuildingProductionRuntimeAdapter adapter = new BuildingProductionRuntimeAdapter(
            buildingId,
            owner,
            stockpile,
            population,
            5,
            () => true,
            product => completed.Add(product));

        int queuedBefore = adapter.Queue.Count;
        int populationBefore = population.current;
        Require(adapter.TryEnqueue(owner, entry, out BuildingProductionQueueResult enqueueResult)
            == BuildingProductionRuntimeResult.Accepted,
            buildingId + "/" + expectedProduct + " não aceitou produção: " + enqueueResult);
        Require(adapter.Queue.Count == queuedBefore + 1,
            buildingId + " não adicionou uma ordem à fila runtime");
        Require(population.current == populationBefore + entry.populationCost,
            buildingId + " não reservou população no enfileiramento");

        List<string> advanced;
        Require(adapter.Advance(owner, entry.productionTime, out advanced)
            == BuildingProductionRuntimeResult.Accepted,
            buildingId + " falhou ao avançar a fila runtime");
        Require(advanced.Count == 1 && advanced[0] == expectedProduct,
            buildingId + " não completou o produto esperado");
        Require(completed.Count == 1 && completed[0] == expectedProduct,
            buildingId + " não entregou o produto pelo callback runtime");
        Require(adapter.Queue.Count == 0,
            buildingId + " manteve uma ordem após conclusão");

        // A população continua ocupada pelo produto concluído; o cancelamento
        // não é usado depois da conclusão e não deve liberar esse espaço.
        Require(population.current == populationBefore + entry.populationCost,
            buildingId + " liberou população após produção concluída");
    }

    private static void ProbeLiveRuntimeDependencies(
        BuildingContentCatalog catalog,
        ProductionRuntimeCycle6Result result)
    {
        if (GameManager.instance == null)
            result.Blocked.Add("GameManager.instance não está disponível na cena isolada");
        if (DataHandler.EconomyRuntime == null)
            result.Blocked.Add("DataHandler.EconomyRuntime não está inicializado");

        CharacterData worker = FindCharacter("worker", "villager");
        CharacterData spearman = FindCharacter("spearman");
        if (worker == null || worker.prefab == null)
            result.Blocked.Add("CharacterData/prefab do villager (worker) não resolvido");
        if (spearman == null || spearman.prefab == null)
            result.Blocked.Add("CharacterData/prefab do spearman não resolvido");

        // O driver vivo somente pode ser ligado a um Building operacional.
        // Não há um Building criado aqui: criar um exigiria prefab, cena e
        // inicialização de GameManager, fora do escopo seguro deste ciclo.
        result.Blocked.Add("BuildingProductionRuntimeDriver não foi ligado a Building operacional sem alterar produção");
        result.Blocked.Add("materialização real do Character/Unit em partida permanece sem PlayMode de cena de produção");
    }

    private static BuildingProductionEntry RequireEntry(
        BuildingContentCatalog catalog,
        string buildingCode,
        string productCode,
        StrategicPlanningState state)
    {
        BuildingProductionEntry entry;
        BuildingProductionCatalogResult result;
        Require(BuildingProductionCatalogRules.TryCreateLegacyQueueEntry(
            catalog, buildingCode, productCode, state, out entry, out result),
            buildingCode + "/" + productCode + " indisponível: " + result);
        return entry;
    }

    private static CharacterData FindCharacter(params string[] codes)
    {
        if (Globals.CHARACTER_DATA == null) return null;
        foreach (string code in codes)
        {
            CharacterData data;
            if (Globals.CHARACTER_DATA.TryGetValue(code, out data) && data != null)
                return data;
        }
        return null;
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

    private sealed class ProductionRuntimeCycle6Result
    {
        public readonly List<string> Blocked = new List<string>();
    }
}
