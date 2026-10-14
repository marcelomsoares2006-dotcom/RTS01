using System;
using System.Collections.Generic;

/// <summary>
/// The single decision point used immediately before a building is placed.
/// It deliberately has no scene or UI dependency: catalog availability,
/// placement validity and affordability are evaluated before the legacy
/// Building.Place method consumes resources.
/// </summary>
public enum BuildingConstructionGateResult
{
    Allowed,
    MissingCode,
    NotFound,
    EraLocked,
    PopulationRequirement,
    TechnologyRequirement,
    BuildingRequirement,
    InvalidPlacement,
    InvalidOwner,
    InsufficientResources,
    InvalidCost
}

public static class BuildingConstructionGate
{
    public static BuildingConstructionGateResult Evaluate(
        BuildingCatalogResolver resolver,
        string buildingCode,
        BuildingEra currentEra,
        int population,
        bool placementValid,
        IDictionary<InGameResource, int> availableResources,
        IEnumerable<string> unlockedTechnologyCodes = null,
        IEnumerable<string> ownedBuildingCodes = null)
    {
        if (resolver == null) return BuildingConstructionGateResult.NotFound;
        BuildingAvailabilityResult availability = resolver.Validate(
            buildingCode, currentEra, population,
            unlockedTechnologyCodes, ownedBuildingCodes);
        BuildingConstructionGateResult availabilityResult = MapAvailability(availability);
        if (availabilityResult != BuildingConstructionGateResult.Allowed)
            return availabilityResult;

        BuildingData data;
        if (!resolver.TryGetBuildingData(buildingCode, out data) || data == null)
            return BuildingConstructionGateResult.NotFound;
        return EvaluateCostAndPlacement(data, placementValid, availableResources);
    }

    /// <summary>
    /// Compatibility gate for the current match runtime. The match does not
    /// yet expose era/population state to the placer, so catalog validation is
    /// intentionally left to the command entry point and this method checks
    /// only the final placement and legacy stockpile immediately before Place.
    /// </summary>
    public static BuildingConstructionGateResult EvaluateLegacy(
        BuildingData data, int owner, bool placementValid)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.code))
            return BuildingConstructionGateResult.MissingCode;
        if (!placementValid)
            return BuildingConstructionGateResult.InvalidPlacement;
        if (Globals.GAME_RESOURCES == null || owner < 0 || owner >= Globals.GAME_RESOURCES.Length)
            return BuildingConstructionGateResult.InvalidOwner;
        Dictionary<InGameResource, int> available = new Dictionary<InGameResource, int>();
        Dictionary<InGameResource, GameResource> stockpile = Globals.GAME_RESOURCES[owner];
        if (stockpile == null) return BuildingConstructionGateResult.InvalidOwner;
        foreach (KeyValuePair<InGameResource, GameResource> pair in stockpile)
            available[pair.Key] = pair.Value == null ? 0 : pair.Value.Amount;
        return EvaluateCostAndPlacement(data, true, available);
    }

    /// <summary>Atomically spends a new economy ledger after a successful gate.</summary>
    public static bool TryConsume(EconomyLedger ledger, IEnumerable<ResourceValue> cost)
    {
        if (ledger == null || !CanAfford(ledger, cost)) return false;
        if (cost == null) return true;
        foreach (ResourceValue value in cost)
            ledger.TrySpend(ToEconomyResource(value.code), value.amount);
        return true;
    }

    public static bool CanAfford(EconomyLedger ledger, IEnumerable<ResourceValue> cost)
    {
        if (ledger == null) return false;
        Dictionary<InGameResource, int> available = new Dictionary<InGameResource, int>();
        foreach (InGameResource resource in Enum.GetValues(typeof(InGameResource)))
            available[resource] = ledger.Get(ToEconomyResource(resource));
        return EvaluateCost(cost, available) == BuildingConstructionGateResult.Allowed;
    }

    private static BuildingConstructionGateResult EvaluateCostAndPlacement(
        BuildingData data, bool placementValid,
        IDictionary<InGameResource, int> availableResources)
    {
        if (!placementValid) return BuildingConstructionGateResult.InvalidPlacement;
        return EvaluateCost(data.cost, availableResources);
    }

    private static BuildingConstructionGateResult EvaluateCost(
        IEnumerable<ResourceValue> cost,
        IDictionary<InGameResource, int> availableResources)
    {
        if (cost == null) return BuildingConstructionGateResult.Allowed;
        if (availableResources == null) return BuildingConstructionGateResult.InsufficientResources;
        Dictionary<InGameResource, long> required = new Dictionary<InGameResource, long>();
        foreach (ResourceValue value in cost)
        {
            if (value == null || value.amount < 0)
                return BuildingConstructionGateResult.InvalidCost;
            long total;
            required.TryGetValue(value.code, out total);
            required[value.code] = total + value.amount;
        }
        foreach (KeyValuePair<InGameResource, long> requirement in required)
        {
            int amount;
            if (!availableResources.TryGetValue(requirement.Key, out amount)
                || amount < 0 || requirement.Value > amount)
                return BuildingConstructionGateResult.InsufficientResources;
        }
        return BuildingConstructionGateResult.Allowed;
    }

    private static BuildingConstructionGateResult MapAvailability(BuildingAvailabilityResult result)
    {
        switch (result)
        {
            case BuildingAvailabilityResult.Available: return BuildingConstructionGateResult.Allowed;
            case BuildingAvailabilityResult.MissingCode: return BuildingConstructionGateResult.MissingCode;
            case BuildingAvailabilityResult.EraLocked: return BuildingConstructionGateResult.EraLocked;
            case BuildingAvailabilityResult.PopulationRequirement: return BuildingConstructionGateResult.PopulationRequirement;
            case BuildingAvailabilityResult.TechnologyRequirement: return BuildingConstructionGateResult.TechnologyRequirement;
            case BuildingAvailabilityResult.BuildingRequirement: return BuildingConstructionGateResult.BuildingRequirement;
            default: return BuildingConstructionGateResult.NotFound;
        }
    }

    private static EconomyResourceType ToEconomyResource(InGameResource resource)
    {
        return (EconomyResourceType)Enum.Parse(typeof(EconomyResourceType), resource.ToString(), true);
    }

}
