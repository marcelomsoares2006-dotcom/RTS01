using System;

public enum BuildingProductionCommandResult
{
    Accepted,
    MissingCatalog,
    MissingState,
    ContentUnavailable,
    EraLocked,
    TechnologyRequirement,
    BuildingRequirement,
    ProductUnavailable,
    UnsupportedEconomyResource,
    QueueRejected
}

/// <summary>
/// Command boundary that applies progression before touching the production
/// queue. Cancellation uses the same boundary, making refunds deterministic
/// and preventing a stale or non-runtime building from changing economy state.
/// </summary>
public static class BuildingProductionCommandRules
{
    public static BuildingProductionCommandResult TryEnqueue(
        BuildingContentCatalog catalog,
        StrategicPlanningState state,
        BuildingProductionQueue queue,
        string buildingCode,
        string productCode,
        EconomyLedger stockpile,
        PopulationLedger population,
        out BuildingProductionQueueResult queueResult)
    {
        queueResult = BuildingProductionQueueResult.MissingEntry;
        if (catalog == null) return BuildingProductionCommandResult.MissingCatalog;
        if (state == null) return BuildingProductionCommandResult.MissingState;
        BuildingProductionEntry entry;
        BuildingProductionCatalogResult validation;
        if (!BuildingProductionCatalogRules.TryCreateLegacyQueueEntry(
            catalog, buildingCode, productCode, state, out entry, out validation))
            return Map(validation);
        if (queue == null) return BuildingProductionCommandResult.QueueRejected;
        queueResult = queue.TryEnqueue(entry, stockpile, population);
        return queueResult == BuildingProductionQueueResult.Accepted
            ? BuildingProductionCommandResult.Accepted
            : BuildingProductionCommandResult.QueueRejected;
    }

    public static BuildingProductionCommandResult TryCancel(
        BuildingContentCatalog catalog,
        StrategicPlanningState state,
        BuildingProductionQueue queue,
        string buildingCode,
        int index,
        EconomyLedger stockpile,
        PopulationLedger population,
        out BuildingProductionOrder cancelled)
    {
        cancelled = null;
        BuildingAvailabilityResult availability = BuildingContentProgressionRules.Validate(
            catalog, buildingCode, state);
        BuildingProductionCommandResult gate = Map(availability);
        if (gate != BuildingProductionCommandResult.Accepted)
            return gate;
        if (queue == null) return BuildingProductionCommandResult.QueueRejected;
        return queue.TryCancel(index, stockpile, population, out cancelled)
            ? BuildingProductionCommandResult.Accepted
            : BuildingProductionCommandResult.QueueRejected;
    }

    private static BuildingProductionCommandResult Map(BuildingProductionCatalogResult result)
    {
        switch (result)
        {
            case BuildingProductionCatalogResult.Available: return BuildingProductionCommandResult.Accepted;
            case BuildingProductionCatalogResult.ContentUnavailable: return BuildingProductionCommandResult.ContentUnavailable;
            case BuildingProductionCatalogResult.EraLocked: return BuildingProductionCommandResult.EraLocked;
            case BuildingProductionCatalogResult.TechnologyRequirement: return BuildingProductionCommandResult.TechnologyRequirement;
            case BuildingProductionCatalogResult.BuildingRequirement: return BuildingProductionCommandResult.BuildingRequirement;
            case BuildingProductionCatalogResult.UnsupportedEconomyResource: return BuildingProductionCommandResult.UnsupportedEconomyResource;
            default: return BuildingProductionCommandResult.ProductUnavailable;
        }
    }

    private static BuildingProductionCommandResult Map(BuildingAvailabilityResult result)
    {
        switch (result)
        {
            case BuildingAvailabilityResult.Available: return BuildingProductionCommandResult.Accepted;
            case BuildingAvailabilityResult.ContentUnavailable: return BuildingProductionCommandResult.ContentUnavailable;
            case BuildingAvailabilityResult.EraLocked: return BuildingProductionCommandResult.EraLocked;
            case BuildingAvailabilityResult.TechnologyRequirement: return BuildingProductionCommandResult.TechnologyRequirement;
            case BuildingAvailabilityResult.BuildingRequirement: return BuildingProductionCommandResult.BuildingRequirement;
            default: return BuildingProductionCommandResult.MissingCatalog;
        }
    }
}
