using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Content status for the building roster. Planned entries are design contracts
/// and must not be offered to runtime construction until their assets exist.
/// </summary>
public enum BuildingContentStatus
{
    Planned,
    Prototype,
    Playable,
    Validated
}

/// <summary>
/// Asset lifecycle exposed to UI and strategic systems. Intermediate content
/// may have documented paths and complete economy contracts, but is not a
/// runtime construction candidate until authored references exist.
/// </summary>
public enum BuildingContentAssetStage
{
    Planned,
    AssetIntermediate,
    RuntimeReady
}

public enum BuildingContentRole
{
    TownCenter,
    Housing,
    ResourceDropoff,
    Production,
    Defense,
    Technology,
    Wonder
}

[Serializable]
public sealed class BuildingContentCost
{
    public EconomyResourceType resource;

    [Min(0)]
    public int amount;
}

[Serializable]
public sealed class BuildingContentProduction
{
    [Tooltip("Stable unit or building code produced by this entry.")]
    public string productCode;

    public List<BuildingContentCost> cost = new List<BuildingContentCost>();

    [Min(0.1f)]
    public float productionTime = 1f;

    [Min(1)]
    public int queueLimit = 5;

    [Min(0)]
    public int populationCost;
}

[Serializable]
public sealed class BuildingContentSpec
{
    [Tooltip("Stable code used by design, save and validation tooling.")]
    public string code;

    public string displayName;
    public BuildingContentRole role;
    public BuildingContentStatus status = BuildingContentStatus.Planned;
    public BuildingEra minimumEra = BuildingEra.Dark;

    [Tooltip("Existing BuildingData. Required when status is Prototype or above.")]
    public BuildingData buildingData;

    [Tooltip("Expected prefab path. This is documentation until the asset exists.")]
    public string prefabPath;

    [Tooltip("Expected model or model folder path. This is documentation until the asset exists.")]
    public string modelPath;

    [Tooltip("Existing build or production skills required by the completed entry.")]
    public List<string> requiredSkillCodes = new List<string>();

    [Header("Gameplay contract")]
    [Tooltip("Economy resources paid when construction starts. This is a design contract until a runtime adapter consumes it.")]
    public List<BuildingContentCost> constructionCost = new List<BuildingContentCost>();

    [Tooltip("Era, technology, population and building prerequisites for this entry.")]
    public List<BuildingRequirement> requirements = new List<BuildingRequirement>();

    [Tooltip("Units or buildings produced after the entry is operational.")]
    public List<BuildingContentProduction> production = new List<BuildingContentProduction>();

    [Tooltip("Resource types accepted by a dropoff building. Empty means this entry is not a dropoff contract.")]
    public List<EconomyResourceType> dropoffResources = new List<EconomyResourceType>();

    [Min(0)]
    public int garrisonCapacity;

    [Tooltip("Additional attack damage contributed by each garrisoned unit in a defensive building.")]
    [Min(0)]
    public int garrisonAttackDamageBonus;

    [Tooltip("Stable unit codes allowed to enter this building. Empty means any unit code.")]
    public List<string> allowedUnitCodes = new List<string>();

    [Min(0)]
    public int populationCapacity;

    public bool supportsDropoff;
    public bool supportsProduction;
    public bool supportsGarrison;

    [TextArea(2, 5)]
    public string acceptanceCriteria;

    public bool IsRuntimeReady
    {
        get { return status >= BuildingContentStatus.Playable && buildingData != null; }
    }

    public BuildingContentAssetStage AssetStage
    {
        get
        {
            if (IsRuntimeReady) return BuildingContentAssetStage.RuntimeReady;
            if (!string.IsNullOrWhiteSpace(prefabPath) || !string.IsNullOrWhiteSpace(modelPath))
                return BuildingContentAssetStage.AssetIntermediate;
            return BuildingContentAssetStage.Planned;
        }
    }
}

/// <summary>
/// Source of truth for the building content roadmap. It deliberately stores
/// planned asset paths as strings, so missing content is visible without
/// creating broken Unity object references or fake meshes.
/// </summary>
[CreateAssetMenu(
    fileName = "Building Content Catalog",
    menuName = "Scriptable Objects/Building Content Catalog",
    order = 3)]
public sealed class BuildingContentCatalog : ScriptableObject
{
    public List<BuildingContentSpec> entries = new List<BuildingContentSpec>();

    public BuildingContentSpec Find(string code)
    {
        if (entries == null || string.IsNullOrEmpty(code)) return null;
        for (int i = 0; i < entries.Count; i++)
        {
            BuildingContentSpec entry = entries[i];
            if (entry != null && string.Equals(entry.code, code, StringComparison.OrdinalIgnoreCase))
                return entry;
        }
        return null;
    }

    public IEnumerable<BuildingContentSpec> RuntimeReadyEntries()
    {
        if (entries == null) yield break;
        for (int i = 0; i < entries.Count; i++)
        {
            BuildingContentSpec entry = entries[i];
            if (entry != null && entry.IsRuntimeReady)
                yield return entry;
        }
    }

    /// <summary>
    /// Validates the gameplay contracts without requiring a prefab or a scene.
    /// Planned entries may fail the asset gate while still being required to
    /// describe their economy and production behavior completely.
    /// </summary>
    public IEnumerable<string> ValidateGameplayContracts()
    {
        if (entries == null) yield break;
        for (int i = 0; i < entries.Count; i++)
        {
            BuildingContentSpec entry = entries[i];
            if (entry == null) continue;

            bool requiresConstructionCost = entry.role != BuildingContentRole.Wonder;
            if (requiresConstructionCost && (entry.constructionCost == null || entry.constructionCost.Count == 0))
                yield return entry.code + " needs a construction cost contract.";
            else if (entry.constructionCost != null)
            {
                for (int j = 0; j < entry.constructionCost.Count; j++)
                {
                    BuildingContentCost cost = entry.constructionCost[j];
                    if (cost == null || cost.amount <= 0)
                        yield return entry.code + " has an invalid construction cost.";
                }
            }

            if (entry.production != null)
            {
                HashSet<string> productionCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int j = 0; j < entry.production.Count; j++)
                {
                    BuildingContentProduction production = entry.production[j];
                    if (production == null || string.IsNullOrWhiteSpace(production.productCode)
                        || production.productionTime <= 0f || production.queueLimit <= 0)
                        yield return entry.code + " has an invalid production contract.";
                    else if (!productionCodes.Add(production.productCode.Trim()))
                        yield return entry.code + " repeats a production product code.";
                    if (production != null && production.cost != null)
                    {
                        for (int k = 0; k < production.cost.Count; k++)
                        {
                            BuildingContentCost cost = production.cost[k];
                            if (cost == null || cost.amount <= 0)
                                yield return entry.code + " has an invalid production cost.";
                        }
                    }
                }
            }

            if (entry.supportsProduction && (entry.production == null || entry.production.Count == 0))
                yield return entry.code + " enables production but has no production entries.";
            if (!entry.supportsProduction && entry.production != null && entry.production.Count > 0)
                yield return entry.code + " has production entries without enabling production.";

            if (entry.supportsDropoff && (entry.dropoffResources == null || entry.dropoffResources.Count == 0))
                yield return entry.code + " enables dropoff but accepts no resources.";
            if (entry.dropoffResources != null)
            {
                HashSet<EconomyResourceType> dropoffResources = new HashSet<EconomyResourceType>();
                for (int j = 0; j < entry.dropoffResources.Count; j++)
                    if (!dropoffResources.Add(entry.dropoffResources[j]))
                        yield return entry.code + " repeats an accepted dropoff resource.";
            }

            if (entry.requirements != null)
            {
                for (int j = 0; j < entry.requirements.Count; j++)
                {
                    BuildingRequirement requirement = entry.requirements[j];
                    if (requirement == null || requirement.IsEmpty)
                        yield return entry.code + " has an empty building requirement.";
                }
            }

            if (entry.role == BuildingContentRole.TownCenter)
            {
                if (!entry.supportsDropoff || entry.dropoffResources == null || entry.dropoffResources.Count == 0)
                    yield return entry.code + " needs resource dropoff coverage.";
                if (!entry.supportsProduction || !ContainsProduct(entry.production, "villager"))
                    yield return entry.code + " needs villager production.";
                if (!entry.supportsGarrison || entry.garrisonCapacity <= 0)
                    yield return entry.code + " needs garrison capacity.";
                if (entry.populationCapacity <= 0)
                    yield return entry.code + " needs population capacity.";
            }

            if (entry.role == BuildingContentRole.ResourceDropoff
                && (!entry.supportsDropoff || entry.dropoffResources == null || entry.dropoffResources.Count == 0))
                yield return entry.code + " needs at least one accepted resource type.";

            if (entry.supportsGarrison && entry.garrisonCapacity <= 0)
                yield return entry.code + " supports garrison but has no capacity.";
            if (!entry.supportsGarrison && entry.garrisonCapacity > 0)
                yield return entry.code + " declares garrison capacity without enabling garrison.";
            if (entry.allowedUnitCodes != null)
            {
                for (int j = 0; j < entry.allowedUnitCodes.Count; j++)
                {
                    if (string.IsNullOrWhiteSpace(entry.allowedUnitCodes[j]))
                        yield return entry.code + " has an empty allowed garrison unit code.";
                }
            }
        }
    }

    private static bool ContainsProduct(List<BuildingContentProduction> production, string productCode)
    {
        if (production == null) return false;
        for (int i = 0; i < production.Count; i++)
        {
            BuildingContentProduction entry = production[i];
            if (entry != null && string.Equals(entry.productCode, productCode, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
