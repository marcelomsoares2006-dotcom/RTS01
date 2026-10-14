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
    /// <summary>Stable text/semantic hints consumed by keyboard and screen-reader adapters.</summary>
    public string accessibilityLabel;
    public string statusText;
    public string shortcut;
    public string iconKey;
    public BuildingUiContrastClass contrastClass;
}

public enum BuildingUiContrastClass { Disabled, Normal, Emphasis }

/// <summary>Deterministic accessibility projection with no sprite or UI dependency.</summary>
public static class BuildingConstructionUiAccessibilityRules
{
    public static string StatusText(BuildingAvailabilityResult availability, BuildingContentAssetStage assetOrigin)
    {
        if (assetOrigin != BuildingContentAssetStage.RuntimeReady) return "Conteúdo planejado";
        switch (availability)
        {
            case BuildingAvailabilityResult.Available: return "Disponível";
            case BuildingAvailabilityResult.EraLocked: return "Era bloqueada";
            case BuildingAvailabilityResult.TechnologyRequirement: return "Tecnologia necessária";
            case BuildingAvailabilityResult.BuildingRequirement: return "Construção necessária";
            case BuildingAvailabilityResult.PopulationRequirement: return "População necessária";
            case BuildingAvailabilityResult.ContentUnavailable: return "Conteúdo indisponível";
            default: return "Indisponível";
        }
    }

    public static string Shortcut(int ordinal)
    {
        return ordinal < 0 ? string.Empty : "B" + (ordinal + 1).ToString();
    }

    public static BuildingUiContrastClass Contrast(bool interactable, bool highlighted)
    {
        if (!interactable) return BuildingUiContrastClass.Disabled;
        return highlighted ? BuildingUiContrastClass.Emphasis : BuildingUiContrastClass.Normal;
    }
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
            BuildingAvailabilityResult availability = BuildingContentProgressionRules.Validate(catalog, entry.code, state);
            bool interactable = runtimeReady && state != null && availability == BuildingAvailabilityResult.Available;
            int ordinal = result.Count;
            result.Add(new BuildingConstructionUiEntryState
            {
                code = entry.code.Trim(),
                displayName = entry.displayName,
                role = entry.role,
                minimumEra = entry.minimumEra,
                assetOrigin = entry.AssetStage,
                availability = availability,
                visible = true,
                interactable = interactable,
                accessibilityLabel = (string.IsNullOrWhiteSpace(entry.displayName) ? entry.code.Trim() : entry.displayName.Trim())
                    + ". " + BuildingConstructionUiAccessibilityRules.StatusText(availability, entry.AssetStage),
                statusText = BuildingConstructionUiAccessibilityRules.StatusText(availability, entry.AssetStage),
                shortcut = BuildingConstructionUiAccessibilityRules.Shortcut(ordinal),
                iconKey = "building/" + entry.code.Trim().ToLowerInvariant(),
                contrastClass = BuildingConstructionUiAccessibilityRules.Contrast(interactable, false)
            });
        }
        return result;
    }
}
