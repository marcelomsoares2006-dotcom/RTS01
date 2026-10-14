using NUnit.Framework;

public class CombatAttackProfileTests
{
    [Test]
    public void LegacyTowerGetsDefensiveCombatDefaults()
    {
        CombatAttackProfile.Resolve("tower", 0f, 0, 0f,
            out float range, out int damage, out float attackRate);

        Assert.That(range, Is.EqualTo(CombatAttackProfile.DefaultTowerRange));
        Assert.That(damage, Is.EqualTo(CombatAttackProfile.DefaultTowerDamage));
        Assert.That(attackRate, Is.EqualTo(CombatAttackProfile.DefaultTowerAttackRate));
    }

    [Test]
    public void ExplicitTowerValuesRemainDesignerControlled()
    {
        CombatAttackProfile.Resolve("tower", 20f, 30, 0.75f,
            out float range, out int damage, out float attackRate);

        Assert.That(range, Is.EqualTo(20f));
        Assert.That(damage, Is.EqualTo(30));
        Assert.That(attackRate, Is.EqualTo(0.75f));
    }

    [Test]
    public void NonTowerUnitsAreUnchanged()
    {
        CombatAttackProfile.Resolve("house", 0f, 0, 0f,
            out float range, out int damage, out float attackRate);

        Assert.That(range, Is.EqualTo(0f));
        Assert.That(damage, Is.EqualTo(0));
        Assert.That(attackRate, Is.EqualTo(0f));
    }
}
