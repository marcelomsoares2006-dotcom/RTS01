using System.Collections.Generic;

/// <summary>
/// Stable technology presets for the planned military roster. The presets are
/// pure data and are intentionally separate from prefab/runtime readiness.
/// </summary>
public static class MilitaryProgressionRules
{
    public static bool TryGetTechnologyForBuilding(string buildingCode, out TechnologyDefinition definition)
    {
        definition = null;
        if (string.Equals(buildingCode, "archery_range", System.StringComparison.OrdinalIgnoreCase))
        {
            definition = Create("fletching");
            return true;
        }
        if (string.Equals(buildingCode, "stable", System.StringComparison.OrdinalIgnoreCase))
        {
            definition = Create("riding");
            return true;
        }
        return false;
    }

    public static ProgressionRequirementResult EvaluateMilitaryUnlock(
        StrategicPlanningState state, string buildingCode)
    {
        TechnologyDefinition definition;
        if (!TryGetTechnologyForBuilding(buildingCode, out definition))
            return ProgressionRequirementResult.InvalidRequirement;
        return TechnologyProgressionRules.Evaluate(state, definition);
    }

    public static bool TryUnlockMilitaryTechnology(
        StrategicPlanningState state, string buildingCode)
    {
        TechnologyDefinition definition;
        return TryGetTechnologyForBuilding(buildingCode, out definition)
            && TechnologyProgressionRules.TryUnlock(state, definition);
    }

    private static TechnologyDefinition Create(string code)
    {
        TechnologyDefinition definition = new TechnologyDefinition
        {
            code = code,
            minimumEra = BuildingEra.Feudal,
            requiredBuildingCodes = new List<string> { "town_center" },
            prerequisiteTechnologyCodes = new List<string>(),
            cost = new List<ProgressionResourceRequirement>()
        };
        return definition;
    }
}
