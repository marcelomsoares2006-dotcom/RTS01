using UnityEngine;

/// <summary>
/// Pure construction rules shared by runtime presentation and tests. Keeping
/// the lifecycle calculation free of scene references makes save/load and AI
/// adapters able to report the same state as the live building.
/// </summary>
public static class BuildingConstructionRules
{
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
