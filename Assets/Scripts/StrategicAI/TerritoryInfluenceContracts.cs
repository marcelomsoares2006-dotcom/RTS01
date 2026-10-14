using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Result of resolving ownership at one world position.</summary>
public enum StrategicTerritoryControl
{
    Neutral,
    Controlled,
    Contested
}

/// <summary>
/// Immutable result for a territory query. Scores are summed per owner from
/// operational buildings and use linear falloff inside each building's radius.
/// </summary>
[Serializable]
public sealed class StrategicTerritoryInfluence
{
    public StrategicTerritoryControl control;
    public int ownerId = -1;
    public float winningScore;
    public float opposingScore;
    public float totalScore;
    public int contributingBuildings;

    public bool IsControlledBy(int queriedOwnerId)
    {
        return control == StrategicTerritoryControl.Controlled && ownerId == queriedOwnerId;
    }
}

/// <summary>A deterministic sample in a rectangular territory field.</summary>
[Serializable]
public sealed class StrategicTerritoryCell
{
    public Vector3 position;
    public StrategicTerritoryInfluence influence;
}

/// <summary>
/// Pure territory resolver for strategic planning. It consumes the detached
/// StrategicPlanningState and never mutates it or the live scene.
/// </summary>
public static class StrategicTerritoryInfluenceResolver
{
    // Scores within this margin are considered a dispute. Keeping the value
    // explicit avoids a hidden tie-break based on list ordering.
    public const float DisputeEpsilon = 0.0001f;
    private const float ScorePrecision = 1000f;

    /// <summary>
    /// Resolves territory using only runtime-ready catalog entries. Planned or
    /// intermediate content may be shown to strategy tools, but cannot grant
    /// map control before a real BuildingData asset exists.
    /// </summary>
    public static StrategicTerritoryInfluence EvaluateAt(
        BuildingContentCatalog catalog,
        StrategicPlanningState state,
        Vector3 position)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        if (state == null) throw new ArgumentNullException(nameof(state));

        StrategicPlanningState filtered = new StrategicPlanningState
        {
            ownerId = state.ownerId,
            era = state.era,
            population = state.population,
            unlockedTechnologyCodes = state.unlockedTechnologyCodes,
            buildings = new List<StrategicBuildingObservation>()
        };
        if (state.buildings != null)
        {
            for (int i = 0; i < state.buildings.Count; i++)
            {
                StrategicBuildingObservation building = state.buildings[i];
                BuildingContentSpec entry = building == null ? null : catalog.Find(building.code);
                if (entry != null && entry.IsRuntimeReady)
                    filtered.buildings.Add(building);
            }
        }
        return EvaluateAt(filtered, position);
    }

    public static StrategicTerritoryInfluence EvaluateAt(StrategicPlanningState state, Vector3 position)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        // Quantize each contribution before aggregation so the result does not
        // change when the same observations arrive in a different list order.
        Dictionary<int, int> scores = new Dictionary<int, int>();
        int contributors = 0;
        if (state.buildings != null)
        {
            for (int i = 0; i < state.buildings.Count; i++)
            {
                StrategicBuildingObservation building = state.buildings[i];
                if (!Contributes(building)) continue;

                float radius = building.influenceRadius;
                float distance = Vector3.Distance(position, building.position);
                if (distance > radius) continue;

                float score = Mathf.Max(0f, radius - distance);
                int quantizedScore = Mathf.RoundToInt(score * ScorePrecision);
                if (quantizedScore <= 0) continue;
                int existing;
                scores.TryGetValue(building.ownerId, out existing);
                scores[building.ownerId] = existing + quantizedScore;
                contributors++;
            }
        }

        StrategicTerritoryInfluence result = new StrategicTerritoryInfluence
        {
            control = StrategicTerritoryControl.Neutral,
            ownerId = -1,
            totalScore = 0f,
            contributingBuildings = contributors
        };
        if (scores.Count == 0) return result;

        int bestScore = -1;
        int secondScore = 0;
        int bestOwner = -1;
        int totalScoreUnits = 0;
        foreach (KeyValuePair<int, int> pair in scores)
        {
            totalScoreUnits += pair.Value;
            if (pair.Value > bestScore
                || (pair.Value == bestScore && pair.Key < bestOwner))
            {
                secondScore = bestScore < 0 ? 0 : bestScore;
                bestScore = pair.Value;
                bestOwner = pair.Key;
            }
            else if (pair.Value > secondScore)
            {
                secondScore = pair.Value;
            }
        }

        result.totalScore = totalScoreUnits / ScorePrecision;
        result.winningScore = Mathf.Max(0f, bestScore / ScorePrecision);
        result.opposingScore = Mathf.Max(0f, secondScore / ScorePrecision);
        int disputeThresholdUnits = Mathf.Max(1, Mathf.RoundToInt(DisputeEpsilon * ScorePrecision));
        result.control = bestScore - secondScore <= disputeThresholdUnits
            ? StrategicTerritoryControl.Contested
            : StrategicTerritoryControl.Controlled;
        result.ownerId = result.control == StrategicTerritoryControl.Controlled ? bestOwner : -1;
        return result;
    }

    /// <summary>
    /// Samples a width by height field in row-major order. The same state and
    /// arguments always produce the same cell ordering and values.
    /// </summary>
    public static List<StrategicTerritoryCell> SampleField(
        StrategicPlanningState state,
        Vector3 origin,
        int width,
        int height,
        float cellSize)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (width < 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));

        List<StrategicTerritoryCell> cells = new List<StrategicTerritoryCell>(width * height);
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 position = origin + new Vector3(x * cellSize, 0f, z * cellSize);
                cells.Add(new StrategicTerritoryCell
                {
                    position = position,
                    influence = EvaluateAt(state, position)
                });
            }
        }
        return cells;
    }

    private static bool Contributes(StrategicBuildingObservation building)
    {
        return StrategicBuildingEligibilityRules.CanContributeToTerritory(building)
            && building.influenceRadius > DisputeEpsilon;
    }
}
