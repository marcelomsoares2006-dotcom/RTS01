using NUnit.Framework;

public sealed class BuildingConstructionLifecycleTests
{
    [Test]
    public void ConstructorAssignmentRequiresFixedIncompleteBuildingAndCapacity()
    {
        Assert.That(BuildingConstructionRules.CanAssignConstructor(true, false, 0, 3), Is.True);
        Assert.That(BuildingConstructionRules.CanAssignConstructor(false, false, 0, 3), Is.False);
        Assert.That(BuildingConstructionRules.CanAssignConstructor(true, true, 0, 3), Is.False);
        Assert.That(BuildingConstructionRules.CanAssignConstructor(true, false, 3, 3), Is.False);
        Assert.That(BuildingConstructionRules.CanAssignConstructor(true, false, -1, 3), Is.False);
    }

    [Test]
    public void OperationalAndTerminalStatesReleaseAttachedConstructors()
    {
        Assert.That(BuildingConstructionRules.ShouldReleaseConstructors(
            BuildingLifecycleState.UnderConstruction,
            BuildingLifecycleState.Operational), Is.True);
        Assert.That(BuildingConstructionRules.ShouldReleaseConstructors(
            BuildingLifecycleState.UnderConstruction,
            BuildingLifecycleState.Destroyed), Is.True);
        Assert.That(BuildingConstructionRules.ShouldReleaseConstructors(
            BuildingLifecycleState.Blueprint,
            BuildingLifecycleState.UnderConstruction), Is.False);
    }
}
