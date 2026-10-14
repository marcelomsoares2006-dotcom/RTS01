using System;

/// <summary>
/// Deterministic damage resolution shared by units and defensive buildings.
/// Armor is flat mitigation and never makes positive damage zero, preserving
/// the existing combat loop while giving future unit assets a safe defense stat.
/// </summary>
public static class CombatDamageProfile
{
    public static int ResolveDamage(int rawDamage, int armor)
    {
        if (rawDamage <= 0) return 0;
        return Math.Max(1, rawDamage - Math.Max(0, armor));
    }
}
