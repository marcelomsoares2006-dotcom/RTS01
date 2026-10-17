using System;
using System.Collections.Generic;

/// <summary>
/// Dados descartáveis compartilhados pela matriz final. Este arquivo só compõe
/// contratos existentes; não cria assets, prefabs, cenas ou componentes de gameplay.
/// </summary>
internal static class FinalIntegrationFixtures
{
    public static BuildingContentCatalog LoadMilitaryCatalog()
    {
        return UnityEngine.Resources.Load<BuildingContentCatalog>(
            "ScriptableObjects/Units/Building Content Catalog");
    }

    public static StrategicPlanningState PlanningState(BuildingEra era, params string[] technologies)
    {
        return new StrategicPlanningState
        {
            ownerId = 0,
            era = era,
            population = 5,
            buildings = new List<StrategicBuildingObservation>
            {
                new StrategicBuildingObservation
                {
                    ownerId = 0,
                    code = "town_center",
                    lifecycle = BuildingLifecycleState.Operational
                }
            },
            unlockedTechnologyCodes = technologies == null
                ? new List<string>()
                : new List<string>(technologies)
        };
    }

    public static void AddOperationalBuilding(StrategicPlanningState state, string code)
    {
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = state.ownerId,
            code = code,
            lifecycle = BuildingLifecycleState.Operational
        });
    }

    public static EconomyLedger RichEconomy()
    {
        EconomyLedger ledger = new EconomyLedger();
        foreach (EconomyResourceType resource in Enum.GetValues(typeof(EconomyResourceType)))
            ledger.Set(resource, 10000);
        return ledger;
    }

    public static PopulationLedger Population()
    {
        return new PopulationLedger(100, 0);
    }

    public static ConstructionReplayCommand Command(
        string id,
        long tick,
        ConstructionReplayCommandKind kind,
        string target,
        int quantity = 1,
        int populationCost = 0,
        params BuildingMaintenanceCost[] costs)
    {
        ConstructionReplayCommand command = new ConstructionReplayCommand
        {
            commandId = id,
            ownerId = 0,
            simulationTick = tick,
            issuedAtMilliseconds = tick,
            priority = 100,
            kind = kind,
            targetCode = target,
            quantity = quantity,
            populationCost = populationCost,
            costs = new List<BuildingMaintenanceCost>()
        };
        if (costs != null) command.costs.AddRange(costs);
        return command;
    }

    public static EconomyResourceType ToEconomyResource(InGameResource resource)
    {
        switch (resource)
        {
            case InGameResource.Food: return EconomyResourceType.Food;
            case InGameResource.Wood: return EconomyResourceType.Wood;
            case InGameResource.Gold: return EconomyResourceType.Gold;
            case InGameResource.Stone: return EconomyResourceType.Stone;
            default: throw new ArgumentOutOfRangeException(nameof(resource), resource, null);
        }
    }

    public static List<BuildingMaintenanceCost> ToReplayCosts(List<ResourceValue> costs)
    {
        List<BuildingMaintenanceCost> result = new List<BuildingMaintenanceCost>();
        if (costs == null) return result;
        for (int i = 0; i < costs.Count; i++)
        {
            ResourceValue cost = costs[i];
            if (cost != null)
                result.Add(new BuildingMaintenanceCost(
                    ToEconomyResource(cost.code), cost.amount));
        }
        return result;
    }
}
