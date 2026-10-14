using NUnit.Framework;

public class CombatDamageProfileTests
{
    [Test]
    public void ZeroOrNegativeAttackDoesNoDamage()
    {
        Assert.That(CombatDamageProfile.ResolveDamage(0, 5), Is.EqualTo(0));
        Assert.That(CombatDamageProfile.ResolveDamage(-10, 5), Is.EqualTo(0));
    }

    [Test]
    public void ArmorReducesDamageButCannotMakeAHitZero()
    {
        Assert.That(CombatDamageProfile.ResolveDamage(12, 3), Is.EqualTo(9));
        Assert.That(CombatDamageProfile.ResolveDamage(4, 8), Is.EqualTo(1));
    }

    [Test]
    public void NegativeArmorIsTreatedAsZero()
    {
        Assert.That(CombatDamageProfile.ResolveDamage(12, -4), Is.EqualTo(12));
    }
}
