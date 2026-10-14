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
        long safeBase = Mathf.Max(0, baseDamage);
        long safeCount = Mathf.Max(0, garrisonCount);
        long safeBonus = Mathf.Max(0, damagePerGarrison);
        long total = safeBase + safeCount * safeBonus;
        return total >= int.MaxValue ? int.MaxValue : (int)total;
    }

    public static bool CanTarget(int owner, int targetOwner, bool targetAlive,
        Vector3 origin, Vector3 target, float range)
    {
        if (!targetAlive || owner == targetOwner || !IsFinite(range) || range <= 0f
            || !IsFinite(origin) || !IsFinite(target))
            return false;
        double dx = (double)target.x - origin.x;
        double dy = (double)target.y - origin.y;
        double dz = (double)target.z - origin.z;
        double distanceSquared = dx * dx + dy * dy + dz * dz;
        double rangeSquared = (double)range * range;
        return distanceSquared <= rangeSquared;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
