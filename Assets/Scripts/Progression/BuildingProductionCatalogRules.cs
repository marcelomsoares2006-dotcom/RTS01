using System;
using System.Collections.Generic;

public enum BuildingProductionCatalogResult
{
    Available,
    MissingCatalog,
    MissingState,
    ContentUnavailable,
    EraLocked,
    TechnologyRequirement,
    BuildingRequirement,
    ProductUnavailable,
    UnsupportedEconomyResource
}

/// <summary>
/// Validates the catalog production contract before it reaches the legacy
/// queue. The queue currently accepts only legacy resources, so Food is
/// surfaced as an explicit compatibility result instead of being coerced.
/// </summary>
public static class BuildingProductionCatalogRules
{
    public static bool TryCreateLegacyQueueEntry(
        BuildingContentCatalog catalog,
        string buildingCode,
        string productCode,
        StrategicPlanningState state,
        out BuildingProductionEntry entry,
        out BuildingProductionCatalogResult result,
        bool requireRuntimeReady = true)
    {
        entry = null;
        result = Evaluate(catalog, buildingCode, productCode, state, requireRuntimeReady);
        if (result != BuildingProductionCatalogResult.Available) return false;

        BuildingContentSpec building = catalog.Find(buildingCode);
        BuildingContentProduction production = FindProduction(building, productCode);
        entry = new BuildingProductionEntry
        {
            productCode = production.productCode.Trim(),
            productionTime = production.productionTime,
            queueLimit = production.queueLimit,
            populationCost = production.populationCost,
            cost = new List<ResourceValue>()
        };
        for (int i = 0; i < production.cost.Count; i++)
        {
            BuildingContentCost cost = production.cost[i];
            InGameResource legacy;
            if (TryGetProductionResource(cost, out legacy))
                entry.cost.Add(new ResourceValue(legacy, cost.amount));
        }
        return true;
    }

    public static BuildingProductionCatalogResult Evaluate(
        BuildingContentCatalog catalog,
        string buildingCode,
        string productCode,
        StrategicPlanningState state,
        bool requireRuntimeReady = true)
    {
        if (catalog == null) return BuildingProductionCatalogResult.MissingCatalog;
        if (state == null) return BuildingProductionCatalogResult.MissingState;
        BuildingContentSpec preflightBuilding = catalog.Find(buildingCode);
        BuildingContentProduction preflightProduction = FindProduction(preflightBuilding, productCode);
        if (!requireRuntimeReady && preflightProduction != null
            && !HasProductionCompatibleCosts(preflightProduction))
            return BuildingProductionCatalogResult.UnsupportedEconomyResource;

        BuildingAvailabilityResult availability = BuildingContentProgressionRules.Validate(
            catalog, buildingCode, state, requireRuntimeReady);
        switch (availability)
        {
            case BuildingAvailabilityResult.ContentUnavailable:
                return BuildingProductionCatalogResult.ContentUnavailable;
            case BuildingAvailabilityResult.EraLocked:
                return BuildingProductionCatalogResult.EraLocked;
            case BuildingAvailabilityResult.TechnologyRequirement:
                return BuildingProductionCatalogResult.TechnologyRequirement;
            case BuildingAvailabilityResult.BuildingRequirement:
                return BuildingProductionCatalogResult.BuildingRequirement;
            case BuildingAvailabilityResult.Available:
                break;
            default:
                return BuildingProductionCatalogResult.MissingCatalog;
        }

        BuildingContentProduction production = FindProduction(catalog.Find(buildingCode), productCode);
        if (production == null) return BuildingProductionCatalogResult.ProductUnavailable;
        if (production.cost != null)
            for (int i = 0; i < production.cost.Count; i++)
            {
                BuildingContentCost cost = production.cost[i];
                InGameResource ignored;
                if (cost == null || !TryGetProductionResource(cost, out ignored))
                    return BuildingProductionCatalogResult.UnsupportedEconomyResource;
            }
        return BuildingProductionCatalogResult.Available;
    }

    private static BuildingContentProduction FindProduction(BuildingContentSpec building, string productCode)
    {
        if (building == null || building.production == null || string.IsNullOrWhiteSpace(productCode)) return null;
        for (int i = 0; i < building.production.Count; i++)
        {
            BuildingContentProduction production = building.production[i];
            if (production != null && string.Equals(production.productCode, productCode.Trim(), StringComparison.OrdinalIgnoreCase))
                return production;
        }
        return null;
    }

    private static bool HasProductionCompatibleCosts(BuildingContentProduction production)
    {
        if (production == null || production.cost == null) return true;
        for (int i = 0; i < production.cost.Count; i++)
        {
            InGameResource ignored;
            if (!TryGetProductionResource(production.cost[i], out ignored)) return false;
        }
        return true;
    }

    private static bool TryGetProductionResource(BuildingContentCost cost, out InGameResource resource)
    {
        resource = default(InGameResource);
        if (cost == null) return false;
        switch (cost.resource)
        {
            case EconomyResourceType.Food: resource = InGameResource.Food; return true;
            case EconomyResourceType.Wood: resource = InGameResource.Wood; return true;
            case EconomyResourceType.Gold: resource = InGameResource.Gold; return true;
            case EconomyResourceType.Stone: resource = InGameResource.Stone; return true;
            default: return false;
        }
    }
}
