using System;

public enum BuildingPlacementPreviewSeverity
{
    Success,
    Notice,
    Error
}

/// <summary>Stable UI-facing result derived from a pure placement contract.</summary>
[Serializable]
public struct BuildingPlacementPreviewFeedback
{
    public bool canPlace;
    public BuildingPlacementPreviewSeverity severity;
    public string code;
    public string resultKey;
    public string messageKey;
    public string message;
    public string iconKey;

    public BuildingPlacementPreviewFeedback(
        bool canPlace,
        BuildingPlacementPreviewSeverity severity,
        string code,
        string resultKey,
        string messageKey,
        string message,
        string iconKey)
    {
        this.canPlace = canPlace;
        this.severity = severity;
        this.code = code ?? string.Empty;
        this.resultKey = resultKey ?? string.Empty;
        this.messageKey = messageKey ?? string.Empty;
        this.message = message ?? string.Empty;
        this.iconKey = iconKey ?? string.Empty;
    }
}

/// <summary>
/// Deterministic presentation mapping for construction preview. It has no
/// localization, scene or material dependency; UI may localize messageKey and
/// choose a sprite from iconKey without changing authorization semantics.
/// </summary>
public static class BuildingPlacementPreviewRules
{
    public static BuildingPlacementPreviewFeedback EvaluateFoundation(
        BuildingContentCatalog catalog, FoundationPlacementRequest request)
    {
        FoundationPlacementResult result = FoundationPlacementRules.Evaluate(catalog, request);
        return FromFoundationResult(result, request == null ? string.Empty : request.code);
    }

    public static BuildingPlacementPreviewFeedback FromFoundationResult(
        FoundationPlacementResult result, string code)
    {
        switch (result)
        {
            case FoundationPlacementResult.Allowed:
                return Success(code, "allowed", "placement.allowed", "Placement is valid.", "placement/allowed");
            case FoundationPlacementResult.AuditOnly:
                return Notice(code, "audit-only", "placement.audit_only", "Design audit only; runtime placement is disabled.", "placement/audit");
            case FoundationPlacementResult.ContentNotRuntimeReady:
                return Notice(code, "content-not-runtime-ready", "placement.content_not_ready", "Content is not runtime-ready.", "placement/content-not-ready");
            case FoundationPlacementResult.TerrainUnavailable:
            case FoundationPlacementResult.TerrainTooSteep:
                return Error(code, "invalid-terrain", "placement.invalid_terrain", "Terrain cannot support this building.", "placement/terrain");
            case FoundationPlacementResult.BlockedByBuilding:
                return Error(code, "invalid-spacing", "placement.invalid_spacing", "Another building is too close.", "placement/spacing");
            case FoundationPlacementResult.InsufficientResources:
                return Error(code, "insufficient-resources", "placement.insufficient_resources", "Required resources are unavailable.", "placement/resources");
            case FoundationPlacementResult.EraLocked:
            case FoundationPlacementResult.PopulationRequirement:
            case FoundationPlacementResult.TechnologyRequirement:
            case FoundationPlacementResult.BuildingRequirement:
                return Error(code, "prerequisite", "placement.prerequisite", "A construction prerequisite is not met.", "placement/prerequisite");
            case FoundationPlacementResult.InvalidFootprint:
                return Error(code, "invalid-footprint", "placement.invalid_footprint", "The building footprint is invalid.", "placement/footprint");
            case FoundationPlacementResult.InvalidConstructionCost:
            case FoundationPlacementResult.MissingDropoffCoverage:
            case FoundationPlacementResult.MissingPopulationCapacity:
                return Error(code, "invalid-content-contract", "placement.invalid_content", "The building content contract is incomplete.", "placement/content");
            default:
                return Error(code, "unavailable", "placement.unavailable", "This building cannot be placed.", "placement/error");
        }
    }

    public static BuildingPlacementPreviewFeedback FromWallGateResult(
        WallGatePlacementResult result, string code)
    {
        switch (result)
        {
            case WallGatePlacementResult.Allowed:
                return Success(code, "allowed", "placement.allowed", "Placement is valid.", "placement/allowed");
            case WallGatePlacementResult.PlannedOnly:
                return Notice(code, "content-not-runtime-ready", "placement.content_not_ready", "Content is not runtime-ready.", "placement/content-not-ready");
            case WallGatePlacementResult.InvalidTerrain:
                return Error(code, "invalid-terrain", "placement.invalid_terrain", "Terrain cannot support this piece.", "placement/terrain");
            case WallGatePlacementResult.InvalidSpacing:
                return Error(code, "invalid-spacing", "placement.invalid_spacing", "The modular spacing is invalid.", "placement/spacing");
            case WallGatePlacementResult.InvalidOwner:
                return Error(code, "invalid-owner", "placement.invalid_owner", "This piece belongs to another player.", "placement/owner");
            case WallGatePlacementResult.InvalidFootprint:
            case WallGatePlacementResult.Occupied:
                return Error(code, "invalid-footprint", "placement.invalid_footprint", "The placement footprint is blocked.", "placement/footprint");
            default:
                return Error(code, "unavailable", "placement.unavailable", "This piece cannot be placed.", "placement/error");
        }
    }

    private static BuildingPlacementPreviewFeedback Success(string code, string resultKey,
        string messageKey, string message, string iconKey)
    {
        return new BuildingPlacementPreviewFeedback(true, BuildingPlacementPreviewSeverity.Success,
            code, resultKey, messageKey, message, iconKey);
    }

    private static BuildingPlacementPreviewFeedback Notice(string code, string resultKey,
        string messageKey, string message, string iconKey)
    {
        return new BuildingPlacementPreviewFeedback(false, BuildingPlacementPreviewSeverity.Notice,
            code, resultKey, messageKey, message, iconKey);
    }

    private static BuildingPlacementPreviewFeedback Error(string code, string resultKey,
        string messageKey, string message, string iconKey)
    {
        return new BuildingPlacementPreviewFeedback(false, BuildingPlacementPreviewSeverity.Error,
            code, resultKey, messageKey, message, iconKey);
    }
}
