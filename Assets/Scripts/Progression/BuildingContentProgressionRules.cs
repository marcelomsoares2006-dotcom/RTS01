using System;
using System.Collections.Generic;

/// <summary>
/// Immutable-at-use snapshot of a building's declarative construction
/// contract. It intentionally contains no prefab or scene reference.
/// </summary>
public sealed class BuildingConstructionContentContract
{
    public string code;
    public BuildingContentRole role;
    public BuildingEra minimumEra;
    public List<BuildingContentCost> constructionCost = new List<BuildingContentCost>();
    public List<BuildingRequirement> requirements = new List<BuildingRequirement>();
    public List<BuildingContentProduction> production = new List<BuildingContentProduction>();
    public List<EconomyResourceType> dropoffResources = new List<EconomyResourceType>();
    public bool supportsDropoff;
    public bool supportsProduction;
}

/// <summary>Prefab-free economic projection for UI and strategic AI.</summary>
public sealed class BuildingEconomyContentProjection
{
    public string code;
    public string displayName;
    public BuildingContentRole role;
    public BuildingEra minimumEra;
    /// <summary>Origin/readiness state consumed by UI and strategic AI.</summary>
    public BuildingContentAssetStage assetOrigin;
    [Obsolete("Use assetOrigin; retained as a compatibility alias for older UI adapters.")]
    public BuildingContentAssetStage assetStage;
    public bool runtimeReady;
    public BuildingAvailabilityResult availability;
    public int populationCapacity;
    public List<BuildingContentCost> constructionCost = new List<BuildingContentCost>();
    public List<BuildingContentProduction> production = new List<BuildingContentProduction>();
    public List<EconomyResourceType> dropoffResources = new List<EconomyResourceType>();
}

/// <summary>
/// Bridges the authoritative building content roadmap to the shared
/// era/technology planning state.  This keeps planned entries visible to
/// design tools while preventing them from leaking into runtime construction
/// before their authored BuildingData exists.
/// </summary>
public static class BuildingContentProgressionRules
{
    /// <summary>
    /// Projects the catalog into data that UI/AI can consume even while the
    /// asset is intermediate. The projection never carries a prefab or
    /// BuildingData reference, and intermediate content remains unavailable
    /// to placement through <see cref="availability"/>.
    /// </summary>
    public static bool TryProjectEconomy(
        BuildingContentCatalog catalog,
        string buildingCode,
        StrategicPlanningState state,
        out BuildingEconomyContentProjection projection)
    {
        projection = null;
        if (catalog == null || string.IsNullOrWhiteSpace(buildingCode)) return false;
        BuildingContentSpec entry = catalog.Find(buildingCode.Trim());
        if (entry == null) return false;

        projection = new BuildingEconomyContentProjection
        {
            code = entry.code.Trim(),
            displayName = entry.displayName,
            role = entry.role,
            minimumEra = entry.minimumEra,
            assetOrigin = entry.AssetStage,
            assetStage = entry.AssetStage,
            runtimeReady = entry.IsRuntimeReady,
            availability = state == null
                ? BuildingAvailabilityResult.ContentUnavailable
                : Validate(catalog, entry.code, state),
            populationCapacity = entry.populationCapacity
        };
        CopyCosts(entry.constructionCost, projection.constructionCost);
        CopyProduction(entry.production, projection.production);
        if (entry.dropoffResources != null)
            projection.dropoffResources.AddRange(entry.dropoffResources);
        return true;
    }

    /// <summary>
    /// Resolves an entry into a detached construction contract after applying
    /// the same era, technology, owner and runtime-readiness gates used by
    /// placement. This is the safe handoff for UI and AI commands: assets are
    /// read as data, while prefab instantiation remains a separate runtime
    /// concern.
    /// </summary>
    public static bool TryResolveConstructionContract(
        BuildingContentCatalog catalog,
        string buildingCode,
        StrategicPlanningState state,
        out BuildingConstructionContentContract contract,
        out BuildingAvailabilityResult result,
        bool requireRuntimeReady = true)
    {
        contract = null;
        result = Validate(catalog, buildingCode, state, requireRuntimeReady);
        if (result != BuildingAvailabilityResult.Available)
            return false;

        BuildingContentSpec entry = catalog.Find(buildingCode.Trim());
        if (entry == null)
        {
            result = BuildingAvailabilityResult.NotFound;
            return false;
        }

        contract = new BuildingConstructionContentContract
        {
            code = entry.code.Trim(),
            role = entry.role,
            minimumEra = entry.minimumEra,
            supportsDropoff = entry.supportsDropoff,
            supportsProduction = entry.supportsProduction
        };
        CopyCosts(entry.constructionCost, contract.constructionCost);
        CopyRequirements(entry.requirements, contract.requirements);
        CopyProduction(entry.production, contract.production);
        if (entry.dropoffResources != null)
            contract.dropoffResources.AddRange(entry.dropoffResources);
        return true;
    }

    /// <summary>
    /// Audits the roster's age graph without loading scenes or prefabs. A
    /// dropoff or production building must be anchored to an operational Town
    /// Center, while forward defenses remain free to be placed independently.
    /// </summary>
    public static IEnumerable<string> ValidateEraContracts(BuildingContentCatalog catalog)
    {
        if (catalog == null)
        {
            yield return "Building content catalog is missing.";
            yield break;
        }

        BuildingContentSpec townCenter = catalog.Find("town_center");
        if (townCenter == null)
            yield return "town_center is required as the era progression anchor.";
        else if (townCenter.minimumEra != BuildingEra.Dark)
            yield return "town_center must be available in the Dark Age.";

        if (catalog.entries == null) yield break;
        for (int i = 0; i < catalog.entries.Count; i++)
        {
            BuildingContentSpec entry = catalog.entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.code)) continue;
            if ((int)entry.minimumEra < (int)BuildingEra.Dark
                || (int)entry.minimumEra > (int)BuildingEra.Imperial)
                yield return entry.code + " has an invalid minimum era.";

            bool anchoredRole = entry.role == BuildingContentRole.ResourceDropoff
                || entry.role == BuildingContentRole.Production;
            if (anchoredRole && !string.Equals(entry.code, "town_center", StringComparison.OrdinalIgnoreCase)
                && !HasBuildingRequirement(entry, "town_center"))
                yield return entry.code + " must require an operational town_center.";

            if (entry.requirements == null) continue;
            for (int j = 0; j < entry.requirements.Count; j++)
            {
                BuildingRequirement requirement = entry.requirements[j];
                if (requirement != null && !string.IsNullOrWhiteSpace(requirement.buildingCode)
                    && string.Equals(requirement.buildingCode.Trim(), entry.code.Trim(), StringComparison.OrdinalIgnoreCase))
                    yield return entry.code + " cannot require itself.";
            }
        }
    }

    public static BuildingAvailabilityResult Validate(
        BuildingContentCatalog catalog,
        string buildingCode,
        StrategicPlanningState state,
        bool requireRuntimeReady = true)
    {
        if (catalog == null || state == null)
            return BuildingAvailabilityResult.NotFound;
        if (string.IsNullOrWhiteSpace(buildingCode))
            return BuildingAvailabilityResult.MissingCode;

        BuildingContentSpec entry = catalog.Find(buildingCode.Trim());
        if (entry == null)
            return BuildingAvailabilityResult.NotFound;
        if (requireRuntimeReady && !entry.IsRuntimeReady)
            return BuildingAvailabilityResult.ContentUnavailable;
        if ((int)state.era < (int)entry.minimumEra)
            return BuildingAvailabilityResult.EraLocked;

        if (state.population < 0)
            return BuildingAvailabilityResult.PopulationRequirement;
        if (entry.populationCapacity > 0 && state.population >= entry.populationCapacity)
            return BuildingAvailabilityResult.PopulationRequirement;

        HashSet<string> technologies = ToCodeSet(state.unlockedTechnologyCodes);
        HashSet<string> buildings = OperationalOwnedBuildingCodes(state);
        if (entry.requirements != null)
        {
            for (int i = 0; i < entry.requirements.Count; i++)
            {
                BuildingRequirement requirement = entry.requirements[i];
                if (requirement == null) continue;
                if (state.population < requirement.minimumPopulation)
                    return BuildingAvailabilityResult.PopulationRequirement;
                if (!string.IsNullOrWhiteSpace(requirement.technologyCode)
                    && !technologies.Contains(requirement.technologyCode.Trim()))
                    return BuildingAvailabilityResult.TechnologyRequirement;
                if (!string.IsNullOrWhiteSpace(requirement.buildingCode)
                    && !buildings.Contains(requirement.buildingCode.Trim()))
                    return BuildingAvailabilityResult.BuildingRequirement;
            }
        }
        return BuildingAvailabilityResult.Available;
    }

    /// <summary>
    /// Uses the content catalog as the source of truth for a construction
    /// command while retaining the existing resolver overload for legacy and
    /// BuildingCatalogData backed scenes.
    /// </summary>
    public static BuildingAvailabilityResult Validate(
        BuildingContentCatalog catalog,
        string buildingCode,
        StrategicPlanningState state)
    {
        return Validate(catalog, buildingCode, state, true);
    }

    private static HashSet<string> ToCodeSet(IEnumerable<string> codes)
    {
        HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (codes == null) return result;
        foreach (string code in codes)
            if (!string.IsNullOrWhiteSpace(code)) result.Add(code.Trim());
        return result;
    }

    private static bool HasBuildingRequirement(BuildingContentSpec entry, string code)
    {
        if (entry.requirements == null) return false;
        for (int i = 0; i < entry.requirements.Count; i++)
        {
            BuildingRequirement requirement = entry.requirements[i];
            if (requirement != null && string.Equals(requirement.buildingCode, code, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void CopyCosts(IEnumerable<BuildingContentCost> source, List<BuildingContentCost> target)
    {
        if (source == null) return;
        foreach (BuildingContentCost value in source)
            if (value != null) target.Add(new BuildingContentCost { resource = value.resource, amount = value.amount });
    }

    private static void CopyRequirements(IEnumerable<BuildingRequirement> source, List<BuildingRequirement> target)
    {
        if (source == null) return;
        foreach (BuildingRequirement value in source)
            if (value != null) target.Add(new BuildingRequirement
            {
                technologyCode = value.technologyCode,
                buildingCode = value.buildingCode,
                minimumPopulation = value.minimumPopulation
            });
    }

    private static void CopyProduction(IEnumerable<BuildingContentProduction> source, List<BuildingContentProduction> target)
    {
        if (source == null) return;
        foreach (BuildingContentProduction value in source)
        {
            if (value == null) continue;
            BuildingContentProduction copy = new BuildingContentProduction
            {
                productCode = value.productCode,
                productionTime = value.productionTime,
                queueLimit = value.queueLimit,
                populationCost = value.populationCost
            };
            CopyCosts(value.cost, copy.cost);
            target.Add(copy);
        }
    }

    private static HashSet<string> OperationalOwnedBuildingCodes(StrategicPlanningState state)
    {
        HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (state.buildings == null) return result;
        for (int i = 0; i < state.buildings.Count; i++)
        {
            StrategicBuildingObservation building = state.buildings[i];
            if (building != null && building.ownerId == state.ownerId
                && building.lifecycle == BuildingLifecycleState.Operational
                && !string.IsNullOrWhiteSpace(building.code))
                result.Add(building.code.Trim());
        }
        return result;
    }
}
