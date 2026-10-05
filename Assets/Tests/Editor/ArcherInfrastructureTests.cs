using NUnit.Framework;
using UnityEngine;

public class ArcherInfrastructureTests
{
    [Test]
    public void NewArcherContract_IsBlockedUntilSourceMeshAndLicenseExist()
    {
        ArcherUnitContract contract = ScriptableObject.CreateInstance<ArcherUnitContract>();
        try
        {
            Assert.That(contract.CharacterCode, Is.EqualTo("archer"));
            Assert.That(contract.HasSourceMesh, Is.False);
            Assert.That(contract.IsReadyForVisualIntegration, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(contract);
        }
    }

    [Test]
    public void RangedAttackProfile_UsesRangeAndCooldownGates()
    {
        RangedAttackProfile profile = ScriptableObject.CreateInstance<RangedAttackProfile>();
        try
        {
            profile.maximumRange = 10f;
            profile.attackCooldown = 2f;

            Assert.That(profile.CanAttack(10f, 2f), Is.True);
            Assert.That(profile.CanAttack(10.01f, 2f), Is.False);
            Assert.That(profile.CanAttack(5f, 1.99f), Is.False);
            Assert.That(profile.CanAttack(-1f, 2f), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }
}
