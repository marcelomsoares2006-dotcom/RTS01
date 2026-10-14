using System;
using System.Collections.Generic;

public enum StableContractResult
{
    Accepted,
    MissingCatalog,
    MissingState,
    MissingStable,
    ContentNotRuntimeReady,
    InvalidConstructionContract,
    FeudalEraRequired,
    MissingRiding,
    MissingTownCenter,
    StableNotOperational,
    MissingProduction,
    UnsupportedEconomyResource
}

/// <summary>
/// Shared Feudal Stable contract. The catalog owns the costs and production
/// values; this boundary repeats the gates for AI and deterministic replay.
/// </summary>
public static class StableMilitaryContract
{
    public const string BuildingCode = "stable";
    public const string PreferredUnitCode = "horseman";
    public const string RequiredTechnologyCode = "riding";
    public const int ConstructionWoodCost = 150;

    public static StableContractResult Evaluate(BuildingContentCatalog catalog, StrategicPlanningState state)
    {
        if (catalog == null) return StableContractResult.MissingCatalog;
        if (state == null) return StableContractResult.MissingState;
        BuildingContentSpec entry = catalog.Find(BuildingCode);
        if (entry == null) return StableContractResult.MissingStable;
        if (!entry.IsRuntimeReady) return StableContractResult.ContentNotRuntimeReady;
        if (entry.minimumEra != BuildingEra.Feudal || !HasExactWoodCost(entry)
            || !HasRequirement(entry, RequiredTechnologyCode, null)
            || !HasRequirement(entry, null, "town_center"))
            return StableContractResult.InvalidConstructionContract;
        if ((int)state.era < (int)BuildingEra.Feudal)
            return StableContractResult.FeudalEraRequired;
        if (!ContainsCode(state.unlockedTechnologyCodes, RequiredTechnologyCode))
            return StableContractResult.MissingRiding;
        if (!BuildingEconomyRules.HasOperationalOwnedBuilding(state, "town_center"))
            return StableContractResult.MissingTownCenter;
        if (!BuildingEconomyRules.HasOperationalOwnedBuilding(state, BuildingCode))
            return StableContractResult.StableNotOperational;
        BuildingContentProduction production;
        return TryResolveProduction(entry, out production)
            ? StableContractResult.Accepted : StableContractResult.MissingProduction;
    }

    /// <summary>Prefers Horseman, then the smallest valid product code.</summary>
    public static bool TryResolveInitialProduction(BuildingContentCatalog catalog,
        out BuildingContentProduction production)
    {
        production = null;
        return catalog != null && TryResolveProduction(catalog.Find(BuildingCode), out production);
    }

    /// <summary>Creates the first mounted order without wall-clock or random state.</summary>
    public static bool TryCreateInitialProductionCommand(BuildingContentCatalog catalog,
        StrategicPlanningState state, long simulationTick, long issuedAtMilliseconds,
        string commandId, out ConstructionReplayCommand command, out StableContractResult result)
    {
        command = null;
        result = Evaluate(catalog, state);
        if (result != StableContractResult.Accepted) return false;
        if (simulationTick < 0 || issuedAtMilliseconds < 0)
        {
            result = StableContractResult.InvalidConstructionContract;
            return false;
        }
        BuildingContentProduction production;
        if (!TryResolveInitialProduction(catalog, out production))
        {
            result = StableContractResult.MissingProduction;
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
        return "stable-initial-" + ownerId + "-" + simulationTick + "-"
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

    private static bool HasRequirement(BuildingContentSpec entry, string technologyCode, string buildingCode)
    {
        if (entry == null || entry.requirements == null) return false;
        for (int i = 0; i < entry.requirements.Count; i++)
        {
            BuildingRequirement requirement = entry.requirements[i];
            if (requirement != null
                && (technologyCode == null || string.Equals(requirement.technologyCode, technologyCode, StringComparison.OrdinalIgnoreCase))
                && (buildingCode == null || string.Equals(requirement.buildingCode, buildingCode, StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }

    private static bool ContainsCode(List<string> codes, string value)
    {
        if (codes == null) return false;
        for (int i = 0; i < codes.Count; i++)
            if (string.Equals(codes[i], value, StringComparison.OrdinalIgnoreCase)) return true;
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
