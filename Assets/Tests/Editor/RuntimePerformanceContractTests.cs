using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RuntimePerformanceContractTests
{
    private const string FovCheckPath =
        "Assets/Scripts/Units/BehaviorTree/Nodes/Check/CheckEnemyInFOVRange.cs";

    [Test]
    public void FieldOfViewCheckUsesReusableNonAllocQueryContract()
    {
        MonoScript source = AssetDatabase.LoadAssetAtPath<MonoScript>(FovCheckPath);
        Assert.That(source, Is.Not.Null, "The FOV check source file is missing.");
        Assert.That(source.text, Does.Contain("OverlapSphereNonAlloc"));
        Assert.That(source.text, Does.Contain("_overlapBuffer"));
        Assert.That(source.text, Does.Not.Contain("using System.Linq;"));
        Assert.That(source.text, Does.Not.Contain("OrderBy("));
    }
}
