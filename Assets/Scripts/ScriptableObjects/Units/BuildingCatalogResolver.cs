using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Resolves the optional building catalog without changing the legacy
/// BuildingData based runtime.  Catalog assets and legacy assets intentionally
/// share the Resources folder, which lets the catalog be introduced gradually.
/// </summary>
public sealed class BuildingCatalogResolver
{
    public const string DefaultResourcesPath = "ScriptableObjects/Units/Buildings";

    private readonly Dictionary<string, BuildingCatalogData> _catalogByCode;
    private readonly Dictionary<string, BuildingData> _legacyByCode;

    public IEnumerable<BuildingCatalogData> CatalogEntries
    {
        get { return _catalogByCode.Values; }
    }

    public IEnumerable<BuildingData> LegacyEntries
    {
        get { return _legacyByCode.Values; }
    }

    public BuildingCatalogResolver(IEnumerable<BuildingCatalogData> catalogEntries,
        IEnumerable<BuildingData> legacyEntries = null)
    {
        _catalogByCode = new Dictionary<string, BuildingCatalogData>(StringComparer.OrdinalIgnoreCase);
        _legacyByCode = new Dictionary<string, BuildingData>(StringComparer.OrdinalIgnoreCase);

        AddCatalogEntries(catalogEntries);
        AddLegacyEntries(legacyEntries);
    }

    /// <summary>Loads catalog records and legacy BuildingData assets from Resources.</summary>
    public static BuildingCatalogResolver LoadFromResources(string resourcesPath = DefaultResourcesPath)
    {
        BuildingCatalogData[] catalogs = Resources.LoadAll<BuildingCatalogData>(resourcesPath);
        BuildingData[] legacy = Resources.LoadAll<BuildingData>(resourcesPath);
        return new BuildingCatalogResolver(catalogs, legacy);
    }

    public bool TryGetCatalog(string code, out BuildingCatalogData catalog)
    {
        catalog = null;
        return !string.IsNullOrWhiteSpace(code) && _catalogByCode.TryGetValue(code.Trim(), out catalog);
    }

    /// <summary>
    /// Resolves the linked legacy data for both catalog-backed and legacy-only buildings.
    /// </summary>
    public bool TryGetBuildingData(string code, out BuildingData buildingData)
    {
        buildingData = null;
        if (string.IsNullOrWhiteSpace(code)) return false;

        string normalizedCode = code.Trim();
        BuildingCatalogData catalog;
        if (_catalogByCode.TryGetValue(normalizedCode, out catalog) && catalog != null
            && catalog.buildingData != null)
        {
            buildingData = catalog.buildingData;
            return true;
        }

        return _legacyByCode.TryGetValue(normalizedCode, out buildingData) && buildingData != null;
    }

    public BuildingAvailabilityResult Validate(string code, BuildingEra currentEra, int population,
        IEnumerable<string> unlockedTechnologyCodes = null,
        IEnumerable<string> ownedBuildingCodes = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BuildingAvailabilityResult.MissingCode;

        BuildingCatalogData catalog;
        if (!TryGetCatalog(code, out catalog))
            return TryGetBuildingData(code, out _) ? BuildingAvailabilityResult.Available : BuildingAvailabilityResult.NotFound;

        if ((int)currentEra < (int)catalog.minimumEra)
            return BuildingAvailabilityResult.EraLocked;

        HashSet<string> technologies = ToCodeSet(unlockedTechnologyCodes);
        HashSet<string> buildings = ToCodeSet(ownedBuildingCodes);
        if (catalog.requirements != null)
        {
            foreach (BuildingRequirement requirement in catalog.requirements)
            {
                if (requirement == null) continue;
                if (population < requirement.minimumPopulation)
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

    private void AddCatalogEntries(IEnumerable<BuildingCatalogData> entries)
    {
        if (entries == null) return;
        foreach (BuildingCatalogData entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Code)) continue;
            string code = entry.Code.Trim();
            if (!_catalogByCode.ContainsKey(code)) _catalogByCode.Add(code, entry);
            else Debug.LogWarning("Duplicate BuildingCatalogData code ignored: " + code);
        }
    }

    private void AddLegacyEntries(IEnumerable<BuildingData> entries)
    {
        if (entries == null) return;
        foreach (BuildingData entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.code)) continue;
            string code = entry.code.Trim();
            if (!_legacyByCode.ContainsKey(code)) _legacyByCode.Add(code, entry);
            else Debug.LogWarning("Duplicate BuildingData code ignored: " + code);
        }
    }

    private static HashSet<string> ToCodeSet(IEnumerable<string> codes)
    {
        if (codes == null) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return new HashSet<string>(codes.Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim()), StringComparer.OrdinalIgnoreCase);
    }
}

public enum BuildingAvailabilityResult
{
    Available,
    /// <summary>
    /// The design catalog knows this building, but its authored runtime
    /// content is not ready to be offered by construction commands.
    /// </summary>
    ContentUnavailable,
    MissingCode,
    NotFound,
    EraLocked,
    PopulationRequirement,
    TechnologyRequirement,
    BuildingRequirement
}
