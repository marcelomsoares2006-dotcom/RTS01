using System;

public enum PlacementNavigationResult
{
    Allowed,
    ContentNotRuntimeReady,
    InvalidFootprint,
    NavMeshUnavailable,
    ObstacleNotReady,
    BlockedCorridor,
    InsufficientClearance
}

/// <summary>Physics-free observation supplied by the placement adapter.</summary>
[Serializable]
public sealed class PlacementNavigationObservation
{
    public bool contentRuntimeReady;
    public bool footprintClear;
    public float footprintWidth;
    public float footprintDepth;
    public bool navMeshReady;
    public bool obstacleReady;
    public bool corridorRequired;
    public bool corridorClear;
    public float corridorWidth;
    public float minimumCorridorWidth;
    public float unitRadius;
}

/// <summary>Deterministic navigation and footprint gate for placement preview.</summary>
public static class PlacementNavigationRules
{
    public static PlacementNavigationResult Evaluate(PlacementNavigationObservation observation)
    {
        if (observation == null || !observation.contentRuntimeReady)
            return PlacementNavigationResult.ContentNotRuntimeReady;
        if (!observation.footprintClear || observation.footprintWidth <= 0f
            || observation.footprintDepth <= 0f || float.IsNaN(observation.footprintWidth)
            || float.IsInfinity(observation.footprintWidth) || float.IsNaN(observation.footprintDepth)
            || float.IsInfinity(observation.footprintDepth))
            return PlacementNavigationResult.InvalidFootprint;
        if (!observation.navMeshReady)
            return PlacementNavigationResult.NavMeshUnavailable;
        if (!observation.obstacleReady)
            return PlacementNavigationResult.ObstacleNotReady;
        if (observation.unitRadius < 0f || float.IsNaN(observation.unitRadius)
            || float.IsInfinity(observation.unitRadius))
            return PlacementNavigationResult.InsufficientClearance;
        if (observation.corridorRequired
            && (!observation.corridorClear || float.IsNaN(observation.corridorWidth)
                || float.IsInfinity(observation.corridorWidth)
                || observation.corridorWidth < observation.minimumCorridorWidth
                || observation.corridorWidth < observation.unitRadius * 2f))
            return PlacementNavigationResult.BlockedCorridor;
        return PlacementNavigationResult.Allowed;
    }
}
