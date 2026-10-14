using System;
using System.Collections.Generic;

/// <summary>Pure priority policy for age-up prerequisites after Town Center.</summary>
public static class AgeUpTechnologyPlanningRules
{
    public static bool TryCreateIntent(
        StrategicPlanningState state,
        EraTransitionRequirement ageUp,
        IList<TechnologyDefinition> technologies,
        out StrategicActionIntent intent,
        out string rationale)
    {
        intent = null;
        rationale = "No age-up action is available.";
        if (state == null || ageUp == null)
        {
            rationale = "Planning state and age-up requirement are required.";
            return false;
        }
        if (!HasOperational(state, "town_center"))
        {
            intent = Build(StrategicActionKind.ConstructBuilding, "town_center", StrategicPriority.Critical,
                "Town Center must be operational before age-up planning can begin.");
            rationale = "Town Center is missing or not operational; establish it first.";
            return true;
        }

        ProgressionRequirementResult result = EraProgressionRules.Evaluate(state, ageUp);
        if (result == ProgressionRequirementResult.AlreadyAtOrAboveEra)
        {
            rationale = "Target era is already reached; no age-up action is needed.";
            return false;
        }
        if (result == ProgressionRequirementResult.InvalidTargetEra
            || (int)ageUp.targetEra <= (int)state.era)
        {
            rationale = "Age-up target must be the next sequential era.";
            return false;
        }

        string missingBuilding = FirstMissingBuilding(state, ageUp.requiredBuildingCodes);
        if (missingBuilding != null)
        {
            intent = Build(StrategicActionKind.ConstructBuilding, missingBuilding, StrategicPriority.Critical,
                "Age-up is blocked by a missing operational prerequisite building.");
            rationale = "Construct prerequisite building " + missingBuilding + " before age-up.";
            return true;
        }

        string missingTechnology = FirstMissingTechnology(state, ageUp.requiredTechnologyCodes);
        if (missingTechnology != null)
        {
            TechnologyDefinition definition = FindTechnology(technologies, missingTechnology);
            if (definition == null)
            {
                rationale = "Age-up is blocked by missing technology definition " + missingTechnology + ".";
                return false;
            }
            string prerequisite = FirstMissingTechnology(state, definition.prerequisiteTechnologyCodes);
            if (prerequisite != null)
            {
                if (FindTechnology(technologies, prerequisite) == null)
                {
                    rationale = "Technology " + missingTechnology + " has an undefined prerequisite " + prerequisite + ".";
                    return false;
                }
                missingTechnology = prerequisite;
                definition = FindTechnology(technologies, prerequisite);
            }
            ProgressionRequirementResult technologyResult = TechnologyProgressionRules.Evaluate(state, definition);
            if (technologyResult != ProgressionRequirementResult.Allowed)
            {
                rationale = "Technology " + missingTechnology + " is blocked by " + technologyResult + ".";
                return false;
            }
            intent = Build(StrategicActionKind.ResearchTechnology, missingTechnology, StrategicPriority.High,
                "Research the first missing age-up technology prerequisite.");
            rationale = "Research " + missingTechnology + " before advancing to " + ageUp.targetEra + ".";
            return true;
        }

        if (result != ProgressionRequirementResult.Allowed)
        {
            rationale = "Age-up is blocked by " + result + ".";
            return false;
        }
        intent = Build(StrategicActionKind.AdvanceEra, "age:" + ageUp.targetEra.ToString().ToLowerInvariant(), StrategicPriority.Critical,
            "All age-up prerequisites are satisfied.");
        rationale = "Advance from " + state.era + " to " + ageUp.targetEra + ".";
        return true;
    }

    private static StrategicActionIntent Build(StrategicActionKind action, string target,
        StrategicPriority priority, string reason)
    {
        return new StrategicActionIntent
        {
            action = action,
            priority = priority,
            goalId = "auto-age-up-" + target,
            targetCode = target,
            quantity = 1
        };
    }

    private static string FirstMissingBuilding(StrategicPlanningState state, IEnumerable<string> codes)
    {
        if (codes == null) return null;
        foreach (string code in codes)
            if (!string.IsNullOrWhiteSpace(code) && !HasOperational(state, code)) return code.Trim();
        return null;
    }

    private static string FirstMissingTechnology(StrategicPlanningState state, IEnumerable<string> codes)
    {
        if (codes == null) return null;
        foreach (string code in codes)
            if (!string.IsNullOrWhiteSpace(code) && !HasTechnology(state, code)) return code.Trim();
        return null;
    }

    private static TechnologyDefinition FindTechnology(IList<TechnologyDefinition> definitions, string code)
    {
        if (definitions == null) return null;
        for (int i = 0; i < definitions.Count; i++)
            if (definitions[i] != null && string.Equals(definitions[i].code, code, StringComparison.OrdinalIgnoreCase)) return definitions[i];
        return null;
    }

    private static bool HasOperational(StrategicPlanningState state, string code)
    {
        if (state.buildings == null) return false;
        for (int i = 0; i < state.buildings.Count; i++)
        {
            StrategicBuildingObservation building = state.buildings[i];
            if (building != null && building.ownerId == state.ownerId
                && building.lifecycle == BuildingLifecycleState.Operational
                && string.Equals(building.code, code, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool HasTechnology(StrategicPlanningState state, string code)
    {
        return state.unlockedTechnologyCodes != null
            && state.unlockedTechnologyCodes.Exists(value => string.Equals(value, code, StringComparison.OrdinalIgnoreCase));
    }
}
