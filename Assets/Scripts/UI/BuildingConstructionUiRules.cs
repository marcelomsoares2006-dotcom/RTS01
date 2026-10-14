using System;
using System.Collections.Generic;

/// <summary>UI-facing state for one catalog construction option.</summary>
public sealed class BuildingConstructionUiEntryState
{
    public string code;
    public string displayName;
    public BuildingContentRole role;
    public BuildingEra minimumEra;
    public BuildingContentAssetStage assetOrigin;
    public BuildingAvailabilityResult availability;
    public bool visible;
    public bool interactable;
}

/// <summary>
/// Builds deterministic construction-menu state from the same progression
/// rules used by commands and AI. It contains no GameObject or UI framework
/// dependency, so a real panel can consume it when authored later.
/// </summary>
public static class BuildingConstructionUiRules
{
    public static List<BuildingConstructionUiEntryState> BuildEntries(
        BuildingContentCatalog catalog,
        StrategicPlanningState state,
        bool includeIntermediate)
    {
        List<BuildingConstructionUiEntryState> result = new List<BuildingConstructionUiEntryState>();
        if (catalog == null || catalog.entries == null) return result;
        for (int i = 0; i < catalog.entries.Count; i++)
        {
            BuildingContentSpec entry = catalog.entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.code)) continue;
            bool runtimeReady = entry.IsRuntimeReady;
            if (!includeIntermediate && !runtimeReady) continue;
            result.Add(new BuildingConstructionUiEntryState
            {
                code = entry.code.Trim(),
                displayName = entry.displayName,
                role = entry.role,
                minimumEra = entry.minimumEra,
                assetOrigin = entry.AssetStage,
                availability = BuildingContentProgressionRules.Validate(catalog, entry.code, state),
                visible = true,
                interactable = runtimeReady && state != null
                    && BuildingContentProgressionRules.Validate(catalog, entry.code, state) == BuildingAvailabilityResult.Available
            });
        }
        return result;
    }
}
