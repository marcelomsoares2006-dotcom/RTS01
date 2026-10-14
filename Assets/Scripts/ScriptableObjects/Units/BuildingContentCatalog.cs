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
}
