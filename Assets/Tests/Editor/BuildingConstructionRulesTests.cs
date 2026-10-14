using NUnit.Framework;

public sealed class BuildingConstructionRulesTests
{
    [Test]
    public void LifecycleDistinguishesBlueprintAndUnderConstruction()
    {
        Assert.That(BuildingConstructionRules.GetLifecycleState(
            false, false, 0, 100, 0), Is.EqualTo(BuildingLifecycleState.Blueprint));
        Assert.That(BuildingConstructionRules.GetLifecycleState(
            true, false, 25, 100, 25), Is.EqualTo(BuildingLifecycleState.UnderConstruction));
    }

    [Test]
    public void LifecycleReportsOperationalDamagedAndDestroyed()
    {
        Assert.That(BuildingConstructionRules.GetLifecycleState(
            true, true, 100, 100, 100), Is.EqualTo(BuildingLifecycleState.Operational));
        Assert.That(BuildingConstructionRules.GetLifecycleState(
            true, true, 40, 100, 100), Is.EqualTo(BuildingLifecycleState.Damaged));
        Assert.That(BuildingConstructionRules.GetLifecycleState(
            true, true, 0, 100, 100), Is.EqualTo(BuildingLifecycleState.Destroyed));
    }

    [Test]
    public void ConstructionRequiresCatalogAvailabilityPlacementAndResources()
    {
        Assert.That(BuildingConstructionRules.CanBeginConstruction(
            BuildingAvailabilityResult.Available, true, true), Is.True);
        Assert.That(BuildingConstructionRules.CanBeginConstruction(
            BuildingAvailabilityResult.EraLocked, true, true), Is.False);
        Assert.That(BuildingConstructionRules.CanBeginConstruction(
            BuildingAvailabilityResult.Available, false, true), Is.False);
        Assert.That(BuildingConstructionRules.CanBeginConstruction(
            BuildingAvailabilityResult.Available, true, false), Is.False);
    }
}
