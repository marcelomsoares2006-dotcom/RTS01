using System;
using UnityEngine;

/// <summary>Deterministic readiness results for the Mill food route.</summary>
public enum MillContractResult
{
    Accepted,
    MissingCatalog,
    MissingState,
    MissingMill,
    ContentNotRuntimeReady,
    InvalidConstructionContract,
    MissingTownCenter,
    MillNotOperational,
    UnsupportedResource
}

/// <summary>
/// Runtime-free economic adapter for the Mill. The Mill is a Dark Age
/// building costing 50 Wood and accepts Food only. Food stays in the new
/// EconomyLedger domain; no legacy InGameResource conversion is attempted.
/// </summary>
public sealed class MillEconomyContract
{
    public const string BuildingCode = "mill";
    public const int ConstructionWoodCost = 50;
    public const EconomyResourceType Resource = EconomyResourceType.Food;

    private readonly int _ownerId;
    private readonly ResourceDropoffContract _dropoff;

    public int OwnerId { get { return _ownerId; } }
    public ResourceDropoffContract Dropoff { get { return _dropoff; } }

    private MillEconomyContract(int ownerId, ResourceDropoffContract dropoff)
    {
        _ownerId = ownerId;
        _dropoff = dropoff;
    }

    public static MillContractResult Evaluate(
        BuildingContentCatalog catalog, StrategicPlanningState state)
    {
        if (catalog == null) return MillContractResult.MissingCatalog;
        if (state == null) return MillContractResult.MissingState;

        BuildingContentSpec entry = catalog.Find(BuildingCode);
        if (entry == null) return MillContractResult.MissingMill;
        if (!entry.IsRuntimeReady) return MillContractResult.ContentNotRuntimeReady;
        if (entry.minimumEra != BuildingEra.Dark || !HasExactWoodCost(entry)
            || !HasTownCenterRequirement(entry))
            return MillContractResult.InvalidConstructionContract;
        if (!entry.supportsDropoff || entry.dropoffResources == null
            || entry.dropoffResources.Count != 1
            || !entry.dropoffResources.Contains(Resource))
            return MillContractResult.UnsupportedResource;
        if (!BuildingEconomyRules.HasOperationalOwnedBuilding(state, "town_center"))
            return MillContractResult.MissingTownCenter;
        if (!BuildingEconomyRules.HasOperationalOwnedBuilding(state, BuildingCode))
            return MillContractResult.MillNotOperational;

        return BuildingEconomyRules.EvaluateResourceRouting(
            catalog, state, BuildingCode, Resource) == BuildingResourceRoutingResult.Accepted
            ? MillContractResult.Accepted
            : MillContractResult.MillNotOperational;
    }

    public static bool TryCreate(
        BuildingContentCatalog catalog, StrategicPlanningState state,
        int ownerId, Vector3 position, float accessRadius,
        out MillEconomyContract contract, out MillContractResult result)
    {
        contract = null;
        result = Evaluate(catalog, state);
        if (result != MillContractResult.Accepted) return false;

        BuildingContentSpec entry = catalog.Find(BuildingCode);
        contract = new MillEconomyContract(ownerId,
            new ResourceDropoffContract(entry, ownerId, position, accessRadius));
        return true;
    }

    /// <summary>
    /// Deposits the integer carried amount through the shared dropoff gate.
    /// Fractional carry remains on the collector for a later trip.
    /// </summary>
    public bool TryDeposit(EconomyCollector collector, Vector3 workerPosition,
        EconomyLedger ledger, out ResourceDropoffResult result)
    {
        if (collector == null)
        {
            result = ResourceDropoffResult.InvalidAmount;
            return false;
        }

        int amount = (int)Math.Floor(collector.CarriedAmount);
        if (amount <= 0)
        {
            result = ResourceDropoffResult.InvalidAmount;
            return false;
        }

        if (!_dropoff.TryDeposit(collector.resource, collector.ownerId,
            workerPosition, amount, ledger, out result)) return false;
        collector.SetCarriedAmount(collector.CarriedAmount - amount);
        return true;
    }

    private static bool HasExactWoodCost(BuildingContentSpec entry)
    {
        if (entry == null || entry.constructionCost == null
            || entry.constructionCost.Count != 1) return false;
        BuildingContentCost cost = entry.constructionCost[0];
        return cost != null && cost.resource == EconomyResourceType.Wood
            && cost.amount == ConstructionWoodCost;
    }

    private static bool HasTownCenterRequirement(BuildingContentSpec entry)
    {
        if (entry.requirements == null) return false;
        for (int i = 0; i < entry.requirements.Count; i++)
        {
            BuildingRequirement requirement = entry.requirements[i];
            if (requirement != null && string.Equals(
                requirement.buildingCode, "town_center", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
