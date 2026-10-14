using System;
using System.Collections.Generic;

public enum BarracksContractResult
{
    Accepted,
    MissingCatalog,
    MissingState,
    MissingBarracks,
    ContentNotRuntimeReady,
    InvalidConstructionContract,
    MissingTownCenter,
    BarracksNotOperational,
    MissingProduction,
    UnsupportedEconomyResource
}

/// <summary>
/// Shared Dark Age Barracks contract. The catalog remains authoritative for
/// costs and timing; this adapter enforces the gameplay invariants and
/// projects the first military order into the deterministic replay stream.
/// </summary>
public static class BarracksMilitaryContract
{
    public const string BuildingCode = "barracks";
    public const string PreferredUnitCode = "spearman";
    public const int ConstructionWoodCost = 150;

    public static BarracksContractResult Evaluate(BuildingContentCatalog catalog, StrategicPlanningState state)
    {
        if (catalog == null) return BarracksContractResult.MissingCatalog;
        if (state == null) return BarracksContractResult.MissingState;
        BuildingContentSpec entry = catalog.Find(BuildingCode);
        if (entry == null) return BarracksContractResult.MissingBarracks;
        if (!entry.IsRuntimeReady) return BarracksContractResult.ContentNotRuntimeReady;
        if (entry.minimumEra != BuildingEra.Dark || !HasExactWoodCost(entry)
            || !HasTownCenterRequirement(entry))
            return BarracksContractResult.InvalidConstructionContract;
        if (!BuildingEconomyRules.HasOperationalOwnedBuilding(state, "town_center"))
            return BarracksContractResult.MissingTownCenter;
        if (!BuildingEconomyRules.HasOperationalOwnedBuilding(state, BuildingCode))
            return BarracksContractResult.BarracksNotOperational;
        BuildingContentProduction production;
        return TryResolveProduction(entry, out production)
            ? BarracksContractResult.Accepted : BarracksContractResult.MissingProduction;
    }

    /// <summary>Prefers Spearman, then selects the smallest valid product code.</summary>
    public static bool TryResolveInitialProduction(BuildingContentCatalog catalog,
        out BuildingContentProduction production)
    {
        production = null;
        if (catalog == null) return false;
        return TryResolveProduction(catalog.Find(BuildingCode), out production);
    }

    /// <summary>Creates the first military command without wall-clock or random state.</summary>
    public static bool TryCreateInitialProductionCommand(BuildingContentCatalog catalog,
        StrategicPlanningState state, long simulationTick, long issuedAtMilliseconds,
        string commandId, out ConstructionReplayCommand command, out BarracksContractResult result)
    {
        command = null;
        result = Evaluate(catalog, state);
        if (result != BarracksContractResult.Accepted) return false;
        if (simulationTick < 0 || issuedAtMilliseconds < 0)
        {
            result = BarracksContractResult.InvalidConstructionContract;
            return false;
        }
        BuildingContentProduction production;
        if (!TryResolveInitialProduction(catalog, out production))
        {
            result = BarracksContractResult.MissingProduction;
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
        return "barracks-initial-" + ownerId + "-" + simulationTick + "-"
            + (productCode == null ? string.Empty : productCode.Trim().ToLowerInvariant());
    }

    private static bool TryResolveProduction(BuildingContentSpec entry,
        out BuildingContentProduction production)
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
            if (string.Equals(candidate.productCode.Trim(), PreferredUnitCode,
                StringComparison.OrdinalIgnoreCase)) { production = candidate; return true; }
            if (fallback == null || string.Compare(candidate.productCode.Trim(),
                fallback.productCode.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
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

    private static bool HasTownCenterRequirement(BuildingContentSpec entry)
    {
        if (entry.requirements == null) return false;
        for (int i = 0; i < entry.requirements.Count; i++)
        {
            BuildingRequirement requirement = entry.requirements[i];
            if (requirement != null && string.Equals(requirement.buildingCode,
                "town_center", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool HasValidCosts(List<BuildingContentCost> costs)
    {
        if (costs == null || costs.Count == 0) return false;
        for (int i = 0; i < costs.Count; i++)
        {
            BuildingContentCost cost = costs[i];
            if (cost == null || cost.amount <= 0) return false;
        }
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
