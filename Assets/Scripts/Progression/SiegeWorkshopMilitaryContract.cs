using System;
using System.Collections.Generic;

public enum SiegeWorkshopContractResult
{
    Accepted,
    MissingCatalog,
    MissingState,
    MissingSiegeWorkshop,
    ContentNotRuntimeReady,
    InvalidConstructionContract,
    EraRequirement,
    MissingTownCenter,
    SiegeWorkshopNotOperational,
    MissingProduction,
    UnsupportedEconomyResource
}

/// <summary>Shared siege workshop contract for Feudal/Castle catalog entries.</summary>
public static class SiegeWorkshopMilitaryContract
{
    public const string BuildingCode = "siege_workshop";
    public const string PreferredUnitCode = "battering_ram";
    public const int ConstructionWoodCost = 300;

    public static SiegeWorkshopContractResult Evaluate(BuildingContentCatalog catalog, StrategicPlanningState state)
    {
        if (catalog == null) return SiegeWorkshopContractResult.MissingCatalog;
        if (state == null) return SiegeWorkshopContractResult.MissingState;
        BuildingContentSpec entry = catalog.Find(BuildingCode);
        if (entry == null) return SiegeWorkshopContractResult.MissingSiegeWorkshop;
        if (!entry.IsRuntimeReady) return SiegeWorkshopContractResult.ContentNotRuntimeReady;
        if (!IsSupportedEra(entry.minimumEra) || !HasExactWoodCost(entry)
            || !HasRequirement(entry, "town_center"))
            return SiegeWorkshopContractResult.InvalidConstructionContract;
        if ((int)state.era < (int)entry.minimumEra)
            return SiegeWorkshopContractResult.EraRequirement;
        if (!BuildingEconomyRules.HasOperationalOwnedBuilding(state, "town_center"))
            return SiegeWorkshopContractResult.MissingTownCenter;
        if (!BuildingEconomyRules.HasOperationalOwnedBuilding(state, BuildingCode))
            return SiegeWorkshopContractResult.SiegeWorkshopNotOperational;
        BuildingContentProduction production;
        return TryResolveProduction(entry, out production)
            ? SiegeWorkshopContractResult.Accepted : SiegeWorkshopContractResult.MissingProduction;
    }

    public static bool TryResolveInitialProduction(BuildingContentCatalog catalog, out BuildingContentProduction production)
    {
        production = null;
        return catalog != null && TryResolveProduction(catalog.Find(BuildingCode), out production);
    }

    public static bool TryCreateInitialProductionCommand(BuildingContentCatalog catalog,
        StrategicPlanningState state, long simulationTick, long issuedAtMilliseconds,
        string commandId, out ConstructionReplayCommand command, out SiegeWorkshopContractResult result)
    {
        command = null;
        result = Evaluate(catalog, state);
        if (result != SiegeWorkshopContractResult.Accepted) return false;
        if (simulationTick < 0 || issuedAtMilliseconds < 0)
        {
            result = SiegeWorkshopContractResult.InvalidConstructionContract;
            return false;
        }
        BuildingContentProduction production;
        if (!TryResolveInitialProduction(catalog, out production))
        {
            result = SiegeWorkshopContractResult.MissingProduction;
            return false;
        }
        string product = production.productCode.Trim();
        string stableId = string.IsNullOrWhiteSpace(commandId)
            ? BuildInitialCommandId(state.ownerId, simulationTick, product) : commandId.Trim();
        command = new ConstructionReplayCommand {
            simulationTick = simulationTick, issuedAtMilliseconds = issuedAtMilliseconds,
            priority = (int)StrategicPriority.High, commandId = stableId, ownerId = state.ownerId,
            kind = ConstructionReplayCommandKind.ProduceUnit, targetCode = product, quantity = 1,
            populationCost = production.populationCost, costs = ToReplayCosts(production.cost)
        };
        return true;
    }

    public static string BuildInitialCommandId(int ownerId, long simulationTick, string productCode)
    {
        return "siege-workshop-initial-" + ownerId + "-" + simulationTick + "-"
            + (productCode == null ? string.Empty : productCode.Trim().ToLowerInvariant());
    }

    private static bool IsSupportedEra(BuildingEra era)
    {
        return era == BuildingEra.Feudal || era == BuildingEra.Castle;
    }

    private static bool TryResolveProduction(BuildingContentSpec entry, out BuildingContentProduction production)
    {
        production = null;
        if (entry == null || !entry.supportsProduction || entry.production == null) return false;
        BuildingContentProduction fallback = null;
        for (int i = 0; i < entry.production.Count; i++)
        {
            BuildingContentProduction candidate = entry.production[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.productCode)
                || candidate.productionTime <= 0f || candidate.queueLimit <= 0
                || !HasValidCosts(candidate.cost)) continue;
            if (string.Equals(candidate.productCode.Trim(), PreferredUnitCode, StringComparison.OrdinalIgnoreCase))
            {
                production = candidate;
                return true;
            }
            if (fallback == null || string.Compare(candidate.productCode.Trim(), fallback.productCode.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                fallback = candidate;
        }
        production = fallback;
        return production != null;
    }

    private static bool HasExactWoodCost(BuildingContentSpec entry)
    {
        if (entry == null || entry.constructionCost == null || entry.constructionCost.Count != 1) return false;
        BuildingContentCost cost = entry.constructionCost[0];
        return cost != null && cost.resource == EconomyResourceType.Wood && cost.amount == ConstructionWoodCost;
    }

    private static bool HasRequirement(BuildingContentSpec entry, string buildingCode)
    {
        if (entry == null || entry.requirements == null) return false;
        for (int i = 0; i < entry.requirements.Count; i++)
        {
            BuildingRequirement requirement = entry.requirements[i];
            if (requirement != null && string.Equals(requirement.buildingCode, buildingCode, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool HasValidCosts(List<BuildingContentCost> costs)
    {
        if (costs == null || costs.Count == 0) return false;
        for (int i = 0; i < costs.Count; i++)
            if (costs[i] == null || costs[i].amount <= 0) return false;
        return true;
    }

    private static List<BuildingMaintenanceCost> ToReplayCosts(List<BuildingContentCost> costs)
    {
        List<BuildingMaintenanceCost> result = new List<BuildingMaintenanceCost>();
        if (costs == null) return result;
        for (int i = 0; i < costs.Count; i++)
        {
            BuildingContentCost cost = costs[i];
            if (cost != null) result.Add(new BuildingMaintenanceCost(cost.resource, cost.amount));
        }
        result.Sort((left, right) => left.resource.CompareTo(right.resource));
        return result;
    }
}
