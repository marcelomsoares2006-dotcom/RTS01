using System.Collections.Generic;
using UnityEngine;

public class TechnologyNodeActioners
{
    private static readonly Dictionary<string, float> Multipliers = new Dictionary<string, float>
    {
        { "attack_booster", 1f },
        { "cost_reducer_buy", 1f }
    };

    public static void ResetMultipliers()
    {
        Multipliers["attack_booster"] = 1f;
        Multipliers["cost_reducer_buy"] = 1f;
    }

    private static void SetAttackMultiplier(float multiplier)
    {
        // Second upgrade is 4x base damage, not another 4x on top of 2x.
        float ratio = multiplier / Multipliers["attack_booster"];
        Multipliers["attack_booster"] = multiplier;
        if (GameManager.instance == null || Unit.UNITS_BY_OWNER == null) return;
        int owner = GameManager.instance.gamePlayersParameters.myPlayerId;
        if (!Unit.UNITS_BY_OWNER.TryGetValue(owner, out List<Unit> units)) return;
        foreach (Unit unit in units)
            if (unit.Transform != null)
                unit.SetAttackDamage(Mathf.RoundToInt(unit.AttackDamage * ratio));
    }

    public static void Apply(string code)
    {
        switch (code)
        {
            case "attack_booster": SetAttackMultiplier(2f); break;
            case "attack_booster_2": SetAttackMultiplier(4f); break;
            case "cost_reducer_buy": Multipliers["cost_reducer_buy"] = 0.9f; break;
            case "cost_reducer_buy_2": Multipliers["cost_reducer_buy"] = 0.8f; break;
            case "root": break;
            default: Debug.LogWarning($"No actioner defined for tech tree node '{code}'."); break;
        }
    }

    public static float GetMultiplier(string code)
        => Multipliers.TryGetValue(code, out float multiplier) ? multiplier : 1f;
}
