using UnityEngine;

/// <summary>
/// Pure construction rules shared by runtime presentation and tests. Keeping
/// the lifecycle calculation free of scene references makes save/load and AI
/// adapters able to report the same state as the live building.
/// </summary>
public static class BuildingConstructionRules
{
    public static float SnapCoordinate(float value, float gridSize)
    {
        if (gridSize <= 0f) return value;
        return Mathf.Round(value / gridSize) * gridSize;
    }

    public static Vector3 SnapPosition(Vector3 position, float gridSize)
    {
        if (gridSize <= 0f) return position;
        return new Vector3(
            SnapCoordinate(position.x, gridSize),
            position.y,
            SnapCoordinate(position.z, gridSize));
    }

    public static float SnapRotation(float degrees, float stepDegrees)
    {
        if (stepDegrees <= 1f) return degrees;
        return Mathf.Round(degrees / stepDegrees) * stepDegrees;
    }

    public static bool IsTerrainSlopeAllowed(Vector3 normal, float maximumSlopeDegrees)
    {
        if (maximumSlopeDegrees < 0f || normal.sqrMagnitude <= Mathf.Epsilon) return false;
        float slope = Vector3.Angle(normal.normalized, Vector3.up);
        return slope <= maximumSlopeDegrees + 0.001f;
    }

    public static BuildingLifecycleState GetLifecycleState(
        bool isFixed,
        bool isOperational,
        int currentHealth,
        int maxHealth,
        int constructionHealth)
    {
        if (!isFixed)
            return BuildingLifecycleState.Blueprint;
        if (!isOperational)
            return BuildingLifecycleState.UnderConstruction;
        if (currentHealth <= 0)
            return BuildingLifecycleState.Destroyed;
        if (maxHealth > 0 && currentHealth < maxHealth)
            return BuildingLifecycleState.Damaged;
        return BuildingLifecycleState.Operational;
    }

    public static bool CanBeginConstruction(BuildingAvailabilityResult availability,
        bool placementValid, bool canBuy)
    {
        return availability == BuildingAvailabilityResult.Available
            && placementValid
            && canBuy;
    }

    public static bool CanAssignConstructor(
        bool isFixed,
        bool isOperational,
        int currentConstructors,
        int maxConstructors)
    {
        return isFixed
            && !isOperational
            && currentConstructors >= 0
            && maxConstructors > 0
            && currentConstructors < maxConstructors;
    }

    public static bool ShouldReleaseConstructors(
        BuildingLifecycleState previous,
        BuildingLifecycleState current)
    {
        return current == BuildingLifecycleState.Operational
            || current == BuildingLifecycleState.Destroyed
            || current == BuildingLifecycleState.Ruined;
    }
}
