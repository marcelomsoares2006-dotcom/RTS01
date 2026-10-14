using NUnit.Framework;
using UnityEngine;

public sealed class BuildingDefenseContractsTests
{
    [Test]
    public void GarrisonContributesConfiguredDamagePerUnit()
    {
        Assert.That(BuildingDefenseRules.ResolveAttackDamage(12, 3, 2), Is.EqualTo(18));
        Assert.That(BuildingDefenseRules.ResolveAttackDamage(12, -1, 2), Is.EqualTo(12));
    }

    [Test]
    public void TargetingRejectsFriendlyDeadAndOutOfRangeUnits()
    {
        Assert.That(BuildingDefenseRules.CanTarget(1, 2, true,
            Vector3.zero, new Vector3(5f, 0f, 0f), 5f), Is.True);
        Assert.That(BuildingDefenseRules.CanTarget(1, 1, true,
            Vector3.zero, Vector3.right, 5f), Is.False);
        Assert.That(BuildingDefenseRules.CanTarget(1, 2, false,
            Vector3.zero, Vector3.right, 5f), Is.False);
        Assert.That(BuildingDefenseRules.CanTarget(1, 2, true,
            Vector3.zero, new Vector3(6f, 0f, 0f), 5f), Is.False);
    }
}
