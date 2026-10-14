using System;
using System.Collections.Generic;

/// <summary>
/// Deterministic construction policy shared by the strategic planner and
/// future runtime adapters. It only reads a detached planning state and the
/// content catalog; placement and resource spending stay with the executor.
/// </summary>
public static class ConstructionPlanningRules
{
    /// <summary>
    /// Selects the first Barracks military order after the construction and
    /// production gates agree. The replay command remains the authoritative
    /// simulation handoff, while this intent lets AI/UI expose the same choice.
    /// </summary>
    public static bool TryCreateInitialBarracksIntent(
        BuildingContentCatalog catalog, StrategicPlanningState state,
        out StrategicActionIntent intent, out BarracksContractResult result)
    {
        intent = null;
        result = BarracksMilitaryContract.Evaluate(catalog, state);
        if (result != BarracksContractResult.Accepted) return false;
        BuildingContentProduction production;
        if (!BarracksMilitaryContract.TryResolveInitialProduction(catalog, out production))
        {
            result = BarracksContractResult.MissingProduction;
            return false;
        }
        intent = new StrategicActionIntent
        {
            action = StrategicActionKind.ProduceUnit,
            priority = StrategicPriority.High,
            goalId = "auto-produce-barracks-" + production.productCode.Trim().ToLowerInvariant(),
            targetCode = production.productCode.Trim(),
            targetEntityId = "barracks",
            quantity = 1
        };
        return true;
    }

    /// <summary>Projects the first Feudal ranged order after the Archery Range gates pass.</summary>
    public static bool TryCreateInitialArcheryRangeIntent(
        BuildingContentCatalog catalog, StrategicPlanningState state,
        out StrategicActionIntent intent, out ArcheryRangeContractResult result)
    {
        intent = null;
        result = ArcheryRangeMilitaryContract.Evaluate(catalog, state);
        if (result != ArcheryRangeContractResult.Accepted) return false;
        BuildingContentProduction production;
        if (!ArcheryRangeMilitaryContract.TryResolveInitialProduction(catalog, out production))
        {
            result = ArcheryRangeContractResult.MissingProduction;
            return false;
        }
        intent = new StrategicActionIntent
        {
            action = StrategicActionKind.ProduceUnit,
            priority = StrategicPriority.High,
            goalId = "auto-produce-archery-range-" + production.productCode.Trim().ToLowerInvariant(),
            targetCode = production.productCode.Trim(),
            targetEntityId = "archery_range",
            quantity = 1
        };
        return true;
    }

    /// <summary>Reserves a candidate through the shared concurrent ledger.</summary>
    public static ConstructionReservationResult TryReserveCandidate(
        ConstructionReservationLedger reservations,
        ConstructionReservationRequest request)
    {
        if (reservations == null) return ConstructionReservationResult.MissingStockpile;
        return reservations.TryReserve(request);
    }
    /// <summary>
    /// Selects the next playable, affordable building in the foundation order:
    /// Town Center, a resource dropoff, then the first missing production
    /// building. A pending or damaged building counts as existing so the AI
    /// does not enqueue duplicate foundations while the runtime repairs it.
    /// </summary>
    public static bool TryCreateIntent(
        StrategicPlanningState state,
        BuildingContentCatalog catalog,
        out StrategicActionIntent intent,
        out string rationale)
    {
        intent = null;
        rationale = "No construction candidate is available.";
        if (state == null)
        {
            rationale = "Planning state is required.";
            return false;
        }
        if (catalog == null || catalog.entries == null || catalog.entries.Count == 0)
        {
            rationale = "Building content catalog is empty.";
            return false;
        }

        List<BuildingContentSpec> candidates = OrderedCandidates(catalog.entries);
        bool hasTownCenter = HasOwnedRole(state, candidates, BuildingContentRole.TownCenter);
        if (!hasTownCenter)
        {
            BuildingContentSpec townCenter = FindCandidate(candidates, BuildingContentRole.TownCenter, state, false);
            if (townCenter != null)
                return Create(townCenter, StrategicPriority.Critical, out intent, out rationale,
                    "No operational Town Center exists; establish the foundation first.");
            rationale = "Town Center is missing, unavailable, not playable, or unaffordable.";
            return false;
        }

        if (!HasOwnedRole(state, candidates, BuildingContentRole.ResourceDropoff))
        {
            BuildingContentSpec dropoff = FindCandidate(candidates, BuildingContentRole.ResourceDropoff, state, true);
            if (dropoff != null)
                return Create(dropoff, StrategicPriority.High, out intent, out rationale,
                    "Town Center exists; add the first resource dropoff to shorten worker routes.");
            rationale = "Town Center exists, but no playable and affordable resource dropoff is available.";
            return false;
        }

        BuildingContentSpec production = FindCandidate(candidates, BuildingContentRole.Production, state, true);
        if (production != null)
            return Create(production, StrategicPriority.High, out intent, out rationale,
                "Core economy is established; add the first missing production building for military growth.");

        rationale = "Town Center and resource dropoff exist; no missing playable production building is affordable in this era.";
        return false;
    }

    private static bool Create(BuildingContentSpec spec, StrategicPriority priority,
        out StrategicActionIntent intent, out string rationale, string reason)
    {
        intent = new StrategicActionIntent
        {
            action = StrategicActionKind.ConstructBuilding,
            priority = priority,
            goalId = "auto-construct-" + spec.code,
            targetCode = spec.code,
            quantity = 1
        };
        rationale = reason + " Candidate: " + spec.code + ".";
        return true;
    }

    private static List<BuildingContentSpec> OrderedCandidates(List<BuildingContentSpec> entries)
    {
        List<BuildingContentSpec> result = new List<BuildingContentSpec>();
        for (int i = 0; i < entries.Count; i++)
            if (entries[i] != null && !string.IsNullOrWhiteSpace(entries[i].code))
                result.Add(entries[i]);
        result.Sort(CompareCandidates);
        return result;
    }

    private static int CompareCandidates(BuildingContentSpec left, BuildingContentSpec right)
    {
        int role = RoleOrder(left.role).CompareTo(RoleOrder(right.role));
        if (role != 0) return role;
        int preference = ProductionPreference(left.code).CompareTo(ProductionPreference(right.code));
        if (preference != 0) return preference;
        return string.Compare(left.code, right.code, StringComparison.OrdinalIgnoreCase);
    }

    private static int RoleOrder(BuildingContentRole role)
    {
        switch (role)
        {
            case BuildingContentRole.TownCenter: return 0;
            case BuildingContentRole.ResourceDropoff: return 1;
            case BuildingContentRole.Production: return 2;
            default: return 3;
        }
    }

    private static int ProductionPreference(string code)
    {
        if (string.Equals(code, "barracks", StringComparison.OrdinalIgnoreCase)) return 0;
        if (string.Equals(code, "archery_range", StringComparison.OrdinalIgnoreCase)) return 1;
        if (string.Equals(code, "stable", StringComparison.OrdinalIgnoreCase)) return 2;
        return 3;
    }

    private static BuildingContentSpec FindCandidate(List<BuildingContentSpec> candidates,
        BuildingContentRole role, StrategicPlanningState state, bool skipOwned)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            BuildingContentSpec candidate = candidates[i];
            if (candidate.role != role || !candidate.IsRuntimeReady)
                continue;
            if (skipOwned && HasOwnedCode(state, candidate.code))
                continue;
            if (!MeetsRequirements(candidate, state) || !CanAfford(candidate, state))
                continue;
            return candidate;
        }
        return null;
    }

    private static bool HasOwnedRole(StrategicPlanningState state, List<BuildingContentSpec> candidates,
        BuildingContentRole role)
    {
        for (int i = 0; i < candidates.Count; i++)
            if (candidates[i].role == role && HasOwnedCode(state, candidates[i].code))
                return true;
        return false;
    }

    private static bool HasOwnedCode(StrategicPlanningState state, string code)
    {
        if (state.buildings == null || string.IsNullOrWhiteSpace(code)) return false;
        for (int i = 0; i < state.buildings.Count; i++)
        {
            StrategicBuildingObservation building = state.buildings[i];
            if (building == null || building.ownerId != state.ownerId
                || !string.Equals(building.code, code, StringComparison.OrdinalIgnoreCase))
                continue;
            if (building.lifecycle != BuildingLifecycleState.Destroyed
                && building.lifecycle != BuildingLifecycleState.Ruined)
                return true;
        }
        return false;
    }

    private static bool MeetsRequirements(BuildingContentSpec candidate, StrategicPlanningState state)
    {
        if ((int)state.era < (int)candidate.minimumEra) return false;
        if (candidate.requirements == null) return true;
        for (int i = 0; i < candidate.requirements.Count; i++)
        {
            BuildingRequirement requirement = candidate.requirements[i];
            if (requirement == null) continue;
            if (state.population < requirement.minimumPopulation) return false;
            if (!string.IsNullOrWhiteSpace(requirement.buildingCode)
                && !BuildingEconomyRules.HasOperationalOwnedBuilding(state, requirement.buildingCode)) return false;
            if (!string.IsNullOrWhiteSpace(requirement.technologyCode)
                && !ContainsCode(state.unlockedTechnologyCodes, requirement.technologyCode)) return false;
        }
        return true;
    }

    private static bool ContainsCode(List<string> codes, string value)
    {
        if (codes == null) return false;
        for (int i = 0; i < codes.Count; i++)
            if (string.Equals(codes[i], value, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool CanAfford(BuildingContentSpec candidate, StrategicPlanningState state)
    {
        if (candidate.constructionCost == null) return true;
        Dictionary<InGameResource, int> required = new Dictionary<InGameResource, int>();
        for (int i = 0; i < candidate.constructionCost.Count; i++)
        {
            BuildingContentCost cost = candidate.constructionCost[i];
            if (cost == null || cost.amount <= 0) return false;
            InGameResource resource;
            if (!cost.resource.TryGetLegacyResource(out resource))
                return false;
            int current;
            required.TryGetValue(resource, out current);
            if (current > int.MaxValue - cost.amount) return false;
            required[resource] = current + cost.amount;
        }
        foreach (KeyValuePair<InGameResource, int> cost in required)
            if (state.GetAvailableAmount(cost.Key) < cost.Value) return false;
        return true;
    }
}
