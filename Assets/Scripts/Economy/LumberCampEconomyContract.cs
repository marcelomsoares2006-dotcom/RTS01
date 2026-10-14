using System;
using UnityEngine;

/// <summary>Deterministic readiness results for the Lumber Camp route.</summary>
public enum LumberCampContractResult
{
    Accepted,
    MissingCatalog,
    MissingState,
    MissingLumberCamp,
    ContentNotRuntimeReady,
    InvalidConstructionContract,
    MissingTownCenter,
    LumberCampNotOperational,
    UnsupportedResource
}

/// <summary>
/// Runtime-free economic adapter for the Lumber Camp. It binds the catalog
/// contract to a ResourceDropoffContract only after an owned operational Town
/// Center and Lumber Camp have been observed. Worker movement stays outside
/// this class; delivery is deterministic once a collector reaches the camp.
/// </summary>
public sealed class LumberCampEconomyContract
{
    public const string BuildingCode = "lumber_camp";
    public const int ConstructionWoodCost = 50;
    public const EconomyResourceType Resource = EconomyResourceType.Wood;

    private readonly int _ownerId;
    private readonly ResourceDropoffContract _dropoff;

    public int OwnerId { get { return _ownerId; } }
    public ResourceDropoffContract Dropoff { get { return _dropoff; } }

    private LumberCampEconomyContract(int ownerId, ResourceDropoffContract dropoff)
    {
        _ownerId = ownerId;
        _dropoff = dropoff;
    }

    public static LumberCampContractResult Evaluate(
        BuildingContentCatalog catalog, StrategicPlanningState state)
    {
        if (catalog == null) return LumberCampContractResult.MissingCatalog;
        if (state == null) return LumberCampContractResult.MissingState;
        BuildingContentSpec entry = catalog.Find(BuildingCode);
        if (entry == null) return LumberCampContractResult.MissingLumberCamp;
        if (!entry.IsRuntimeReady) return LumberCampContractResult.ContentNotRuntimeReady;
        if (!HasExactWoodCost(entry)) return LumberCampContractResult.InvalidConstructionContract;
        if (!entry.supportsDropoff || entry.dropoffResources == null
            || !entry.dropoffResources.Contains(Resource))
            return LumberCampContractResult.UnsupportedResource;
        if (!BuildingEconomyRules.HasOperationalOwnedBuilding(state, "town_center"))
            return LumberCampContractResult.MissingTownCenter;
        if (!BuildingEconomyRules.HasOperationalOwnedBuilding(state, BuildingCode))
            return LumberCampContractResult.LumberCampNotOperational;
        return BuildingEconomyRules.EvaluateResourceRouting(
            catalog, state, BuildingCode, Resource) == BuildingResourceRoutingResult.Accepted
            ? LumberCampContractResult.Accepted
            : LumberCampContractResult.LumberCampNotOperational;
    }

    public static bool TryCreate(
        BuildingContentCatalog catalog, StrategicPlanningState state,
        int ownerId, Vector3 position, float accessRadius,
        out LumberCampEconomyContract contract,
        out LumberCampContractResult result)
    {
        contract = null;
        result = Evaluate(catalog, state);
        if (result != LumberCampContractResult.Accepted) return false;

        BuildingContentSpec entry = catalog.Find(BuildingCode);
        contract = new LumberCampEconomyContract(ownerId,
            new ResourceDropoffContract(entry, ownerId, position, accessRadius));
        return true;
    }

    /// <summary>
    /// Deposits the integer portion carried by a collector and clears exactly
    /// the delivered amount. Fractional carry remains deterministic and is
    /// delivered on a later trip.
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
        return cost != null && cost.resource == Resource
            && cost.amount == ConstructionWoodCost;
    }
}
