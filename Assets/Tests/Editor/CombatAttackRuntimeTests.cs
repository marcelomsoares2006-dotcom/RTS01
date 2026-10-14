using NUnit.Framework;
using UnityEngine;

public class CombatAttackRuntimeTests
{
    [Test]
    public void ProjectileIsRejectedOutsideAttackRange()
    {
        var cooldown = new CombatCooldownState();

        bool created = CombatAttackResolver.TryCreateProjectile(
            Vector3.zero,
            new Vector3(11f, 0f, 0f),
            10f,
            12,
            1.5f,
            0f,
            cooldown,
            out CombatProjectilePayload projectile);

        Assert.That(created, Is.False);
        Assert.That(cooldown.HasAttacked, Is.False);
        Assert.That(projectile.RawDamage, Is.EqualTo(0));
    }

    [Test]
    public void CooldownIsDeterministicAndAllowsAttackAtReadyTime()
    {
        var cooldown = new CombatCooldownState();

        Assert.That(cooldown.TryReserve(10f, 1.5f), Is.True);
        Assert.That(cooldown.TryReserve(11.49f, 1.5f), Is.False);
        Assert.That(cooldown.TryReserve(11.5f, 1.5f), Is.True);
        Assert.That(cooldown.NextReadyAt, Is.EqualTo(13f).Within(0.0001f));
    }

    [Test]
    public void ImpactKeepsDamageSeparateFromArmorResolution()
    {
        CombatProjectilePayload projectile = new CombatProjectilePayload(
            Vector3.zero,
            Vector3.right,
            12,
            2f);

        CombatImpact impact = CombatImpact.FromProjectile(projectile);

        Assert.That(impact.RawDamage, Is.EqualTo(12));
        Assert.That(CombatDamageProfile.ResolveDamage(impact.RawDamage, 5), Is.EqualTo(7));
    }
}
