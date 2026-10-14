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
            if (cost != null && cost.resource.TryGetLegacyResource(out legacy))
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
                if (cost == null || !cost.resource.TryGetLegacyResource(out ignored))
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
}
