using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Este assembly de testes (EraImperial.EditorTests) nao enxerga o Assembly-CSharp, entao os tipos do
// Archer sao acessados por nome (ScriptableObject.CreateInstance(string)) e reflexao.
public class ArcherInfrastructureTests
{
    private static object Get(object target, string member)
    {
        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
        if (property != null) return property.GetValue(target);
        FieldInfo field = type.GetField(member, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, $"Membro ausente: {type.Name}.{member}");
        return field.GetValue(target);
    }

    private static void Set(object target, string field, object value)
    {
        FieldInfo info = target.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(info, Is.Not.Null, $"Campo ausente: {target.GetType().Name}.{field}");
        info.SetValue(target, value);
    }

    [Test]
    public void NewArcherContract_IsBlockedUntilSourceMeshAndLicenseExist()
    {
        ScriptableObject contract = ScriptableObject.CreateInstance("ArcherUnitContract");
        try
        {
            Assert.That(contract, Is.Not.Null, "ArcherUnitContract nao encontrado");
            Assert.That(Get(contract, "CharacterCode"), Is.EqualTo("archer"));
            Assert.That(Get(contract, "HasSourceMesh"), Is.False);
            Assert.That(Get(contract, "IsReadyForVisualIntegration"), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(contract);
        }
    }

    [Test]
    public void RangedAttackProfile_UsesRangeAndCooldownGates()
    {
        ScriptableObject profile = ScriptableObject.CreateInstance("RangedAttackProfile");
        try
        {
            Assert.That(profile, Is.Not.Null, "RangedAttackProfile nao encontrado");
            Set(profile, "maximumRange", 10f);
            Set(profile, "attackCooldown", 2f);
            MethodInfo canAttack = profile.GetType().GetMethod("CanAttack");
            Assert.That(canAttack, Is.Not.Null);

            Assert.That(canAttack.Invoke(profile, new object[] { 10f, 2f }), Is.EqualTo(true));
            Assert.That(canAttack.Invoke(profile, new object[] { 10.01f, 2f }), Is.EqualTo(false));
            Assert.That(canAttack.Invoke(profile, new object[] { 5f, 1.99f }), Is.EqualTo(false));
            Assert.That(canAttack.Invoke(profile, new object[] { -1f, 2f }), Is.EqualTo(false));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }
}
