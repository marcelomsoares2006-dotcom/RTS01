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

    [Test]
    public void NonFiniteCombatInputsAreRejectedWithoutPoisoningCooldown()
    {
        var cooldown = new CombatCooldownState();
        CombatProjectilePayload projectile;

        Assert.That(CombatAttackResolver.TryCreateProjectile(
            Vector3.zero, Vector3.right, 10f, 12, float.NaN, 0f,
            cooldown, out projectile), Is.False);
        Assert.That(cooldown.HasAttacked, Is.False);
        Assert.That(CombatAttackResolver.TryCreateProjectile(
            Vector3.zero, Vector3.right, float.PositiveInfinity, 12, 1f, 0f,
            cooldown, out projectile), Is.False);
        Assert.That(CombatAttackResolver.TryCreateProjectile(
            new Vector3(float.NaN, 0f, 0f), Vector3.right, 10f, 12, 1f, 0f,
            cooldown, out projectile), Is.False);
        Assert.That(cooldown.HasAttacked, Is.False);
    }

    [Test]
    public void NegativeAttackRateIsRejectedWithoutMutatingCooldown()
    {
        var cooldown = new CombatCooldownState();
        Assert.That(cooldown.TryReserve(0f, -1f), Is.False);
        Assert.That(cooldown.HasAttacked, Is.False);
    }
}
