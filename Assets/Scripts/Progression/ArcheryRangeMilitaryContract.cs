using System;
using System.Collections.Generic;

public enum ArcheryRangeContractResult
{
    Accepted,
    MissingCatalog,
    MissingState,
    MissingArcheryRange,
    ContentNotRuntimeReady,
    InvalidConstructionContract,
    FeudalEraRequired,
    MissingFletching,
    MissingTownCenter,
    ArcheryRangeNotOperational,
    MissingProduction,
    UnsupportedEconomyResource
}

/// <summary>
/// Shared Feudal Age Archery Range contract. Costs, technology and production
/// are read from the content catalog; this adapter exposes the same invariant
/// to strategic AI and the deterministic replay stream.
/// </summary>
public static class ArcheryRangeMilitaryContract
{
    public const string BuildingCode = "archery_range";
    public const string PreferredUnitCode = "archer";
    public const string RequiredTechnologyCode = "fletching";
    public const int ConstructionWoodCost = 150;

    public static ArcheryRangeContractResult Evaluate(BuildingContentCatalog catalog, StrategicPlanningState state)
    {
        if (catalog == null) return ArcheryRangeContractResult.MissingCatalog;
        if (state == null) return ArcheryRangeContractResult.MissingState;
        BuildingContentSpec entry = catalog.Find(BuildingCode);
        if (entry == null) return ArcheryRangeContractResult.MissingArcheryRange;
        if (!entry.IsRuntimeReady) return ArcheryRangeContractResult.ContentNotRuntimeReady;
        if (entry.minimumEra != BuildingEra.Feudal || !HasExactWoodCost(entry)
            || !HasRequirement(entry, RequiredTechnologyCode, null)
            || !HasRequirement(entry, null, "town_center"))
            return ArcheryRangeContractResult.InvalidConstructionContract;
        if ((int)state.era < (int)BuildingEra.Feudal)
            return ArcheryRangeContractResult.FeudalEraRequired;
        if (!ContainsCode(state.unlockedTechnologyCodes, RequiredTechnologyCode))
            return ArcheryRangeContractResult.MissingFletching;
        if (!BuildingEconomyRules.HasOperationalOwnedBuilding(state, "town_center"))
            return ArcheryRangeContractResult.MissingTownCenter;
        if (!BuildingEconomyRules.HasOperationalOwnedBuilding(state, BuildingCode))
            return ArcheryRangeContractResult.ArcheryRangeNotOperational;
        BuildingContentProduction production;
        return TryResolveProduction(entry, out production)
            ? ArcheryRangeContractResult.Accepted : ArcheryRangeContractResult.MissingProduction;
    }

    public static bool TryResolveInitialProduction(BuildingContentCatalog catalog,
        out BuildingContentProduction production)
    {
        production = null;
        return catalog != null && TryResolveProduction(catalog.Find(BuildingCode), out production);
    }

    public static bool TryCreateInitialProductionCommand(BuildingContentCatalog catalog,
        StrategicPlanningState state, long simulationTick, long issuedAtMilliseconds,
        string commandId, out ConstructionReplayCommand command, out ArcheryRangeContractResult result)
    {
        command = null;
        result = Evaluate(catalog, state);
        if (result != ArcheryRangeContractResult.Accepted) return false;
        if (simulationTick < 0 || issuedAtMilliseconds < 0)
        {
            result = ArcheryRangeContractResult.InvalidConstructionContract;
            return false;
        }
        BuildingContentProduction production;
        if (!TryResolveInitialProduction(catalog, out production))
        {
            result = ArcheryRangeContractResult.MissingProduction;
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
        return "archery-range-initial-" + ownerId + "-" + simulationTick + "-"
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
