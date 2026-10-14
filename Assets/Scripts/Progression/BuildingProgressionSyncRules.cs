using System;
using System.Collections.Generic;

public enum BuildingProgressionSyncResult
{
    Synchronized,
    MissingState,
    MissingSnapshot,
    StateMismatch,
    InvalidSnapshot
}

/// <summary>Read-only preflight for synchronizing progression and production.</summary>
public static class BuildingProgressionSyncRules
{
    public static BuildingProgressionSyncResult Validate(
        BuildingContentCatalog catalog,
        StrategicPlanningState state,
        BuildingProductionQueueSnapshot snapshot)
    {
        if (catalog == null || state == null) return BuildingProgressionSyncResult.MissingState;
        if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.buildingCode)) return BuildingProgressionSyncResult.MissingSnapshot;
        if (snapshot.ownerId != state.ownerId) return BuildingProgressionSyncResult.StateMismatch;
        BuildingContentSpec entry = catalog.Find(snapshot.buildingCode);
        if (entry == null) return BuildingProgressionSyncResult.InvalidSnapshot;
        if (!entry.IsRuntimeReady || snapshot.assetOrigin != BuildingContentAssetStage.RuntimeReady
            || snapshot.era != state.era || snapshot.assetOrigin != entry.AssetStage
            || !TechnologyCodesMatch(snapshot.unlockedTechnologyCodes, state.unlockedTechnologyCodes))
            return BuildingProgressionSyncResult.StateMismatch;
        return BuildingProgressionSyncResult.Synchronized;
    }

    private static bool TechnologyCodesMatch(List<string> saved, List<string> current)
    {
        List<string> left = Normalize(saved);
        List<string> right = Normalize(current);
        if (left.Count != right.Count) return false;
        for (int i = 0; i < left.Count; i++)
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal)) return false;
        return true;
    }

    private static List<string> Normalize(List<string> source)
    {
        List<string> result = new List<string>();
        if (source != null)
            for (int i = 0; i < source.Count; i++)
                if (!string.IsNullOrWhiteSpace(source[i])) result.Add(source[i].Trim().ToLowerInvariant());
        result.Sort(StringComparer.Ordinal);
        return result;
    }
}
