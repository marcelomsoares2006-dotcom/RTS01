using NUnit.Framework;

public sealed class PlacementNavigationRulesTests
{
    private static PlacementNavigationObservation Ready(bool corridor = false)
    {
        return new PlacementNavigationObservation {
            contentRuntimeReady = true, footprintClear = true,
            footprintWidth = 4f, footprintDepth = 4f,
            navMeshReady = true, obstacleReady = true,
            corridorRequired = corridor, corridorClear = true,
            corridorWidth = 2f, minimumCorridorWidth = 1.5f, unitRadius = 0.5f
        };
    }

    [Test]
    public void ReadyFootprintAndCorridorAreAllowed()
    {
        Assert.That(PlacementNavigationRules.Evaluate(Ready(true)), Is.EqualTo(PlacementNavigationResult.Allowed));
    }

    [TestCase(false, PlacementNavigationResult.ContentNotRuntimeReady)]
    [TestCase(true, PlacementNavigationResult.NavMeshUnavailable)]
    public void ContentAndNavMeshAreHardGates(bool runtimeReady, PlacementNavigationResult expected)
    {
        PlacementNavigationObservation observation = Ready();
        observation.contentRuntimeReady = runtimeReady;
        observation.navMeshReady = runtimeReady;
        Assert.That(PlacementNavigationRules.Evaluate(observation), Is.EqualTo(expected));
    }

    [Test]
    public void WallGateCorridorMustFitUnitRadius()
    {
        PlacementNavigationObservation observation = Ready(true);
        observation.corridorWidth = 0.8f;
        Assert.That(PlacementNavigationRules.Evaluate(observation), Is.EqualTo(PlacementNavigationResult.BlockedCorridor));
        observation.corridorWidth = 2f;
        observation.corridorClear = false;
        Assert.That(PlacementNavigationRules.Evaluate(observation), Is.EqualTo(PlacementNavigationResult.BlockedCorridor));
    }

    [Test]
    public void PreviewFeedbackNeverAuthorizesNavigationFailure()
    {
        BuildingPlacementPreviewFeedback feedback = BuildingPlacementPreviewRules.FromNavigationResult(
            PlacementNavigationResult.ObstacleNotReady, "wall_gate");
        Assert.IsFalse(feedback.canPlace);
        Assert.AreEqual("placement/navigation", feedback.iconKey);
        Assert.IsFalse(string.IsNullOrWhiteSpace(feedback.messageKey));
    }
}
