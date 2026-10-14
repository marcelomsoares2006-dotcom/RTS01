using System;
using UnityEngine;

/// <summary>Deterministic readiness results for the Mining Camp route.</summary>
public enum MiningCampContractResult
{
    Accepted,
    MissingCatalog,
    MissingState,
    MissingMiningCamp,
    ContentNotRuntimeReady,
    InvalidConstructionContract,
    MissingTownCenter,
    MiningCampNotOperational,
    UnsupportedResource
}

/// <summary>
/// Runtime-free economic adapter for the Mining Camp. The catalog remains the
/// source of truth: the camp costs 50 Wood and accepts Gold and Stone only.
/// Both AI planning and worker delivery pass through the same operational
/// Town Center and BuildingEconomyRules gates.
/// </summary>
public sealed class MiningCampEconomyContract
{
    public const string BuildingCode = "mining_camp";
    public const int ConstructionWoodCost = 50;

    private readonly int _ownerId;
    private readonly ResourceDropoffContract _dropoff;

    public int OwnerId { get { return _ownerId; } }
    public ResourceDropoffContract Dropoff { get { return _dropoff; } }

    private MiningCampEconomyContract(int ownerId, ResourceDropoffContract dropoff)
    {
        _ownerId = ownerId;
        _dropoff = dropoff;
    }

    public static MiningCampContractResult Evaluate(
        BuildingContentCatalog catalog, StrategicPlanningState state)
    {
        if (catalog == null) return MiningCampContractResult.MissingCatalog;
        if (state == null) return MiningCampContractResult.MissingState;
        BuildingContentSpec entry = catalog.Find(BuildingCode);
        if (entry == null) return MiningCampContractResult.MissingMiningCamp;
        if (!entry.IsRuntimeReady) return MiningCampContractResult.ContentNotRuntimeReady;
        if (entry.minimumEra != BuildingEra.Dark || !HasExactWoodCost(entry)
            || !HasTownCenterRequirement(entry))
            return MiningCampContractResult.InvalidConstructionContract;
        if (!entry.supportsDropoff || entry.dropoffResources == null
            || entry.dropoffResources.Count != 2
            || !entry.dropoffResources.Contains(EconomyResourceType.Gold)
            || !entry.dropoffResources.Contains(EconomyResourceType.Stone))
            return MiningCampContractResult.UnsupportedResource;
        if (!BuildingEconomyRules.HasOperationalOwnedBuilding(state, "town_center"))
            return MiningCampContractResult.MissingTownCenter;
        if (!BuildingEconomyRules.HasOperationalOwnedBuilding(state, BuildingCode))
            return MiningCampContractResult.MiningCampNotOperational;

        BuildingResourceRoutingResult gold = BuildingEconomyRules.EvaluateResourceRouting(
            catalog, state, BuildingCode, EconomyResourceType.Gold);
        BuildingResourceRoutingResult stone = BuildingEconomyRules.EvaluateResourceRouting(
            catalog, state, BuildingCode, EconomyResourceType.Stone);
        return gold == BuildingResourceRoutingResult.Accepted
            && stone == BuildingResourceRoutingResult.Accepted
            ? MiningCampContractResult.Accepted
            : MiningCampContractResult.MiningCampNotOperational;
    }

    public static bool TryCreate(
        BuildingContentCatalog catalog, StrategicPlanningState state,
        int ownerId, Vector3 position, float accessRadius,
        out MiningCampEconomyContract contract,
        out MiningCampContractResult result)
    {
        contract = null;
        result = Evaluate(catalog, state);
        if (result != MiningCampContractResult.Accepted) return false;

        BuildingContentSpec entry = catalog.Find(BuildingCode);
        contract = new MiningCampEconomyContract(ownerId,
            new ResourceDropoffContract(entry, ownerId, position, accessRadius));
        return true;
    }

    /// <summary>
    /// Deposits the integer carried amount and preserves fractional carry for
    /// the next trip. ResourceDropoffContract performs all ownership, range,
    /// operational and accepted-resource checks before touching the ledger.
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
