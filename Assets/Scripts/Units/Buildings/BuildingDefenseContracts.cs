using UnityEngine;

/// <summary>
/// Pure rules shared by defensive buildings, simulations and the runtime
/// attack adapter. The rule deliberately accepts primitive values so a tower
/// can be validated without instantiating a prefab or creating new assets.
/// </summary>
public static class BuildingDefenseRules
{
    public static int ResolveAttackDamage(int baseDamage, int garrisonCount,
        int damagePerGarrison)
    {
        int safeBase = Mathf.Max(0, baseDamage);
        int safeCount = Mathf.Max(0, garrisonCount);
        int safeBonus = Mathf.Max(0, damagePerGarrison);
        return safeBase + safeCount * safeBonus;
    }

    public static bool CanTarget(int owner, int targetOwner, bool targetAlive,
        Vector3 origin, Vector3 target, float range)
    {
        if (!targetAlive || owner == targetOwner || range <= 0f)
            return false;
        return (target - origin).sqrMagnitude <= range * range;
    }
}
