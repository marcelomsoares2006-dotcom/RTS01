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

    [Test]
    public void PlacementSnapsToConfiguredGridWithoutChangingHeight()
    {
        Vector3 snapped = BuildingConstructionRules.SnapPosition(
            new UnityEngine.Vector3(3.24f, 7.5f, -1.76f), 2f);
        Assert.That(snapped.x, Is.EqualTo(4f).Within(0.001f));
        Assert.That(snapped.y, Is.EqualTo(7.5f).Within(0.001f));
        Assert.That(snapped.z, Is.EqualTo(-2f).Within(0.001f));
        Assert.That(BuildingConstructionRules.SnapPosition(UnityEngine.Vector3.one, 0f),
            Is.EqualTo(UnityEngine.Vector3.one));
    }

    [Test]
    public void PlacementRotationUsesConfiguredIncrement()
    {
        Assert.That(BuildingConstructionRules.SnapRotation(37f, 15f), Is.EqualTo(30f).Within(0.001f));
        Assert.That(BuildingConstructionRules.SnapRotation(37f, 1f), Is.EqualTo(37f).Within(0.001f));
    }

    [Test]
    public void PlacementRejectsTerrainBeyondBuildingSlopeLimit()
    {
        UnityEngine.Vector3 steep = new UnityEngine.Vector3(0.8f, 0.6f, 0f);
        Assert.That(BuildingConstructionRules.IsTerrainSlopeAllowed(
            UnityEngine.Vector3.up, 5f), Is.True);
        Assert.That(BuildingConstructionRules.IsTerrainSlopeAllowed(steep, 30f), Is.False);
        Assert.That(BuildingConstructionRules.IsTerrainSlopeAllowed(steep, 60f), Is.True);
    }

    [Test]
    public void PlacementRejectsBuildingsInsideConfiguredDistance()
    {
        List<UnityEngine.Vector3> existing = new List<UnityEngine.Vector3>
        { UnityEngine.Vector3.zero };
        Assert.That(BuildingConstructionRules.IsBuildingDistanceAllowed(
            new UnityEngine.Vector3(1f, 0f, 0f), existing, 2f), Is.False);
        Assert.That(BuildingConstructionRules.IsBuildingDistanceAllowed(
            new UnityEngine.Vector3(3f, 0f, 0f), existing, 2f), Is.True);
        Assert.That(BuildingConstructionRules.IsBuildingDistanceAllowed(
            UnityEngine.Vector3.one, existing, 0f), Is.True);
    }
}
