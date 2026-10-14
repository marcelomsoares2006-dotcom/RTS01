using System;

/// <summary>
/// Runtime combat defaults for legacy unit assets that predate the combat
/// profile fields. Keeping these values in code lets old save data and
/// ScriptableObjects continue to load while defensive buildings gain a real
/// attack without requiring an asset migration.
/// </summary>
public static class CombatAttackProfile
{
    public const float DefaultTowerRange = 12f;
    public const int DefaultTowerDamage = 12;
    public const float DefaultTowerAttackRate = 1.5f;

    public static void Resolve(
        string unitCode,
        float assetRange,
        int assetDamage,
        float assetAttackRate,
        out float range,
        out int damage,
        out float attackRate)
    {
        range = assetRange;
        damage = assetDamage;
        attackRate = assetAttackRate;

        // Tower.asset in older projects intentionally has zero attack values.
        // Only fill missing values, so designers can override the defaults by
        // setting any of the fields on a future asset version.
        if (string.Equals(unitCode, "tower", StringComparison.OrdinalIgnoreCase))
        {
            if (range <= 0f) range = DefaultTowerRange;
            if (damage <= 0) damage = DefaultTowerDamage;
            if (attackRate <= 0f) attackRate = DefaultTowerAttackRate;
        }
    }
}
