using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BuildingObjectiveProjectionRulesTests
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
    public void PlannedContentIsVisibleButCannotBecomeConstructionOrObjectiveProgress()
    {
        BuildingContentCatalog catalog = CreateCatalog();
        catalog.entries.Add(new BuildingContentSpec { code = "barracks", displayName = "Barracks", minimumEra = BuildingEra.Feudal });
        BuildingObjectiveProjection projection;

        Assert.That(BuildingObjectiveProjectionRules.TryProject(catalog, State(BuildingEra.Feudal), "barracks", out projection), Is.True);
        Assert.That(projection.runtimeReady, Is.False);
        Assert.That(projection.constructionEligible, Is.False);
        Assert.That(projection.objectiveEligible, Is.False);
        Assert.That(projection.gate, Is.EqualTo(BuildingObjectiveGate.ContentUnavailable));
    }

    [Test]
    public void OperationalLifecycleIsRequiredBeforeConstructionTargetCountsAsObjective()
    {
        BuildingContentCatalog catalog = CreateCatalog();
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        data.code = "house";
        _created.Add(data);
        catalog.entries.Add(new BuildingContentSpec
        {
            code = "house", displayName = "House", minimumEra = BuildingEra.Dark,
            status = BuildingContentStatus.Playable, buildingData = data
        });
        StrategicPlanningState state = State(BuildingEra.Dark);
        state.buildings.Add(new StrategicBuildingObservation
        {
            ownerId = state.ownerId, code = "house", lifecycle = BuildingLifecycleState.UnderConstruction,
            assetOrigin = BuildingContentAssetStage.RuntimeReady, contentRuntimeReady = true
        });

        BuildingObjectiveProjection projection;
        Assert.That(BuildingObjectiveProjectionRules.TryProject(catalog, state, "house", out projection), Is.True);
        Assert.That(projection.constructionEligible, Is.True);
        Assert.That(projection.objectiveEligible, Is.False);
        Assert.That(projection.gate, Is.EqualTo(BuildingObjectiveGate.LifecycleUnavailable));

        state.buildings[0].lifecycle = BuildingLifecycleState.Operational;
        Assert.That(BuildingObjectiveProjectionRules.TryProject(catalog, state, "house", out projection), Is.True);
        Assert.That(projection.objectiveEligible, Is.True);
        Assert.That(projection.gate, Is.EqualTo(BuildingObjectiveGate.Available));
    }

    private BuildingContentCatalog CreateCatalog()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        return catalog;
    }

    private static StrategicPlanningState State(BuildingEra era)
    {
        return new StrategicPlanningState
        {
            ownerId = 0, era = era, population = 10,
            unlockedTechnologyCodes = new List<string>(),
            buildings = new List<StrategicBuildingObservation>()
        };
    }
}
