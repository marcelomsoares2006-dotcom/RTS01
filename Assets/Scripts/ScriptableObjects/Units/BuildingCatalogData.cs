using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Broad gameplay role used to group buildings in the construction UI and AI.
/// The enum is intentionally independent from the legacy BuildingData asset so
/// existing scenes and save data keep working while the catalog is introduced.
/// </summary>
public enum BuildingCategory
{
    Infrastructure,
    Housing,
    Resource,
    Military,
    Technology,
    Economy,
    Defense,
    Wonder
}

/// <summary>
/// The minimum age in which a building can be constructed.
/// </summary>
public enum BuildingEra
{
    Dark = 1,
    Feudal = 2,
    Castle = 3,
    Imperial = 4
}

/// <summary>
/// Runtime construction lifecycle. This is data-facing for now; the existing
/// Building class can adopt it incrementally without changing its current API.
/// </summary>
public enum BuildingLifecycleState
{
    Blueprint,
    UnderConstruction,
    Operational,
    Damaged,
    Destroyed,
    Ruined
}

[Serializable]
public class BuildingRequirement
{
    [Tooltip("Optional technology code that must be unlocked.")]
    public string technologyCode;

    [Tooltip("Optional building code that must already exist for the owner.")]
    public string buildingCode;

    [Tooltip("Minimum population supported by the owner before this building is available.")]
    [Min(0)]
    public int minimumPopulation;

    public bool IsEmpty
    {
        get
        {
            return string.IsNullOrEmpty(technologyCode)
                && string.IsNullOrEmpty(buildingCode)
                && minimumPopulation <= 0;
        }
    }
}

[Serializable]
public class BuildingProductionEntry
{
    [Tooltip("Unit or building code produced by this building.")]
    public string productCode;

    [Min(0f)]
    public float productionTime = 1f;

    [Min(1)]
    public int queueLimit = 5;

    [Tooltip("Optional skill used by the legacy production system.")]
    public SkillData productionSkill;
}

[Serializable]
public class BuildingGarrisonData
{
    [Min(0)]
    public int capacity;

    [Tooltip("Codes allowed to occupy this building. Empty means any unit.")]
    public List<string> allowedUnitCodes = new List<string>();

    [Min(0f)]
    public float ejectTime;

    public bool Allows(string unitCode)
    {
        if (capacity <= 0) return false;
        if (allowedUnitCodes == null || allowedUnitCodes.Count == 0) return true;
        return allowedUnitCodes.Contains(unitCode);
    }
}

/// <summary>
/// Optional, forward-compatible catalog record for one building. It references
/// the existing BuildingData asset instead of replacing it, so the current
/// placement, save and production code remains compatible.
/// </summary>
[CreateAssetMenu(
    fileName = "Building Catalog Entry",
    menuName = "Scriptable Objects/Building Catalog Entry",
    order = 2)]
public class BuildingCatalogData : ScriptableObject
{
    [Header("Legacy asset link")]
    [Tooltip("Existing BuildingData used by the current runtime.")]
    public BuildingData buildingData;

    [Header("Classification")]
    public BuildingCategory category = BuildingCategory.Infrastructure;
    public BuildingEra minimumEra = BuildingEra.Dark;

    [Header("Requirements")]
    public List<BuildingRequirement> requirements = new List<BuildingRequirement>();

    [Header("Production")]
    public List<BuildingProductionEntry> production = new List<BuildingProductionEntry>();

    [Header("Garrison")]
    public BuildingGarrisonData garrison = new BuildingGarrisonData();

    [Header("Lifecycle")]
    public BuildingLifecycleState initialState = BuildingLifecycleState.Blueprint;
    [Min(0f)]
    public float repairRate = 1f;
    public bool leavesRuins = true;

    public string Code
    {
        get
        {
            if (buildingData != null && !string.IsNullOrEmpty(buildingData.code))
                return buildingData.code;
            return name;
        }
    }

    public bool IsAvailable(BuildingEra currentEra, int population)
    {
        if ((int)currentEra < (int)minimumEra) return false;
        if (requirements == null) return true;
        for (int i = 0; i < requirements.Count; i++)
        {
            BuildingRequirement requirement = requirements[i];
            if (requirement != null && population < requirement.minimumPopulation)
                return false;
        }
        return true;
    }

    public bool CanGarrison(string unitCode, int occupiedSlots)
    {
        if (garrison == null || occupiedSlots >= garrison.capacity)
            return false;
        return garrison.Allows(unitCode);
    }
}
