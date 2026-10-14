using UnityEngine;

/// <summary>
/// Mutable, deterministic cooldown state for one attacker. The state is
/// intentionally independent of Unity's frame loop so simulations and tests
/// can provide their own clock.
/// </summary>
public sealed class CombatCooldownState
{
    private bool _hasAttacked;
    private float _nextReadyAt;

    public bool TryReserve(float now, float attackRate)
    {
        // Time values enter this state from Unity's clock today, but replay
        // and network callers can supply arbitrary floats.  NaN would bypass
        // the cooldown comparison forever and infinity would poison the next
        // ready timestamp, so reject non-finite inputs at the boundary.
        if (float.IsNaN(now) || float.IsInfinity(now)
            || float.IsNaN(attackRate) || float.IsInfinity(attackRate)
            || attackRate < 0f)
            return false;

        float interval = attackRate;
        if (now > float.MaxValue - interval)
            return false;
        if (_hasAttacked && now < _nextReadyAt)
            return false;

        _hasAttacked = true;
        _nextReadyAt = now + interval;
        return true;
    }

    public float NextReadyAt => _nextReadyAt;
    public bool HasAttacked => _hasAttacked;
}

/// <summary>
/// Data handed to a projectile visual or a network transport. Creating this
/// payload does not apply damage; impact resolution remains a separate step.
/// </summary>
public readonly struct CombatProjectilePayload
{
    public readonly Vector3 Origin;
    public readonly Vector3 Destination;
    public readonly int RawDamage;
    public readonly float CreatedAt;

    public CombatProjectilePayload(Vector3 origin, Vector3 destination, int rawDamage, float createdAt)
    {
        Origin = origin;
        Destination = destination;
        RawDamage = rawDamage;
        CreatedAt = createdAt;
    }
}

/// <summary>
/// A projectile arrival event. It carries no target reference, which keeps
/// projectile presentation and damage application decoupled.
/// </summary>
public readonly struct CombatImpact
{
    public readonly Vector3 Position;
    public readonly int RawDamage;

    public CombatImpact(Vector3 position, int rawDamage)
    {
        Position = position;
        RawDamage = rawDamage;
    }

    public static CombatImpact FromProjectile(CombatProjectilePayload projectile)
    {
        return new CombatImpact(projectile.Destination, projectile.RawDamage);
    }
}

public static class CombatAttackResolver
{
    public static bool TryCreateProjectile(
        Vector3 origin,
        Vector3 destination,
        float range,
        int rawDamage,
        float attackRate,
        float now,
        CombatCooldownState cooldown,
        out CombatProjectilePayload projectile)
    {
        projectile = default(CombatProjectilePayload);
        if (cooldown == null || !IsFinite(range) || range <= 0f || rawDamage <= 0
            || !IsFinite(now) || !IsFinite(origin) || !IsFinite(destination))
            return false;

        // Compare in double precision so a very large, but finite, authored
        // range cannot overflow float and accidentally turn into an
        // unlimited attack radius.
        double dx = (double)destination.x - origin.x;
        double dy = (double)destination.y - origin.y;
        double dz = (double)destination.z - origin.z;
        double distanceSquared = dx * dx + dy * dy + dz * dz;
        double rangeSquared = (double)range * range;
        if (distanceSquared > rangeSquared)
            return false;

        if (!cooldown.TryReserve(now, attackRate))
            return false;

        projectile = new CombatProjectilePayload(origin, destination, rawDamage, now);
        return true;
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
