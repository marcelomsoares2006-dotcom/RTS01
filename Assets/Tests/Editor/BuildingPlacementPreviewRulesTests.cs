using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BuildingPlacementPreviewRulesTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object asset in _created)
            if (asset != null) Object.DestroyImmediate(asset);
        _created.Clear();
    }

    [Test]
    public void PreviewNeverAuthorizesIntermediateFoundationContent()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        catalog.entries.Add(new BuildingContentSpec
        {
            code = "town_center",
            role = BuildingContentRole.TownCenter,
            minimumEra = BuildingEra.Dark,
            populationCapacity = 10,
            supportsDropoff = true,
            dropoffResources = new List<EconomyResourceType> { EconomyResourceType.Wood },
            constructionCost = new List<BuildingContentCost>
            {
                new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 50 }
            }
        });
        StrategicPlanningState state = new StrategicPlanningState { ownerId = 0, era = BuildingEra.Dark };
        state.SetAmount(InGameResource.Wood, 100);
        FoundationPlacementRequest request = new FoundationPlacementRequest
        {
            code = "town_center", state = state, terrainHit = true,
            terrainNormal = Vector3.up, footprintRadius = 2f
        };

        BuildingPlacementPreviewFeedback feedback = BuildingPlacementPreviewRules.EvaluateFoundation(catalog, request);
        Assert.IsFalse(feedback.canPlace);
        Assert.AreEqual("content-not-runtime-ready", feedback.resultKey);
        Assert.AreEqual("placement/content-not-ready", feedback.iconKey);
    }

    [Test]
    public void PreviewProvidesStableFeedbackForGeometryAndResources()
    {
        AssertFeedback(FoundationPlacementResult.TerrainTooSteep, "invalid-terrain", "placement/terrain");
        AssertFeedback(FoundationPlacementResult.BlockedByBuilding, "invalid-spacing", "placement/spacing");
        AssertFeedback(FoundationPlacementResult.InsufficientResources, "insufficient-resources", "placement/resources");
        AssertFeedback(FoundationPlacementResult.Allowed, "allowed", "placement/allowed", true);
    }

    [Test]
    public void WallGatePreviewAlsoTreatsPlannedAndBlockedStatesAsNonPlaceable()
    {
        BuildingPlacementPreviewFeedback planned = BuildingPlacementPreviewRules.FromWallGateResult(
            WallGatePlacementResult.PlannedOnly, "wall_future");
        Assert.IsFalse(planned.canPlace);
        Assert.AreEqual("content-not-runtime-ready", planned.resultKey);

        BuildingPlacementPreviewFeedback blocked = BuildingPlacementPreviewRules.FromWallGateResult(
            WallGatePlacementResult.InvalidSpacing, "wall_segment");
        Assert.IsFalse(blocked.canPlace);
        Assert.AreEqual("placement/spacing", blocked.iconKey);
    }

    private static void AssertFeedback(FoundationPlacementResult result,
        string resultKey, string iconKey, bool canPlace = false)
    {
        BuildingPlacementPreviewFeedback feedback = BuildingPlacementPreviewRules.FromFoundationResult(result, "building");
        Assert.AreEqual(canPlace, feedback.canPlace);
        Assert.AreEqual(resultKey, feedback.resultKey);
        Assert.AreEqual(iconKey, feedback.iconKey);
        Assert.IsFalse(string.IsNullOrWhiteSpace(feedback.messageKey));
        Assert.IsFalse(string.IsNullOrWhiteSpace(feedback.message));
    }
}
