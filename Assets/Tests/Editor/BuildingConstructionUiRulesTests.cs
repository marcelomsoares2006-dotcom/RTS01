using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BuildingConstructionUiRulesTests
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
    public void RuntimeReadyEntriesAreInteractiveOnlyWhenEraAndPopulationAllow()
    {
        BuildingContentCatalog catalog = CreateCatalog();
        BuildingContentSpec house = AddEntry(catalog, "house", BuildingEra.Dark, BuildingContentStatus.Playable, 5);
        house.requirements.Add(new BuildingRequirement { minimumPopulation = 2 });
        AddEntry(catalog, "archery_range", BuildingEra.Feudal, BuildingContentStatus.Playable, 0);
        StrategicPlanningState state = State(BuildingEra.Dark, 2);

        List<BuildingConstructionUiEntryState> entries = BuildingConstructionUiRules.BuildEntries(catalog, state, false);
        Assert.That(entries.Count, Is.EqualTo(2));
        Assert.That(Find(entries, "house").interactable, Is.True);
        Assert.That(Find(entries, "archery_range").interactable, Is.False);
        Assert.That(Find(entries, "archery_range").availability, Is.EqualTo(BuildingAvailabilityResult.EraLocked));

        state.population = 5;
        Assert.That(Find(BuildingConstructionUiRules.BuildEntries(catalog, state, false), "house").interactable, Is.False);
        Assert.That(Find(BuildingConstructionUiRules.BuildEntries(catalog, state, false), "house").availability,
            Is.EqualTo(BuildingAvailabilityResult.PopulationRequirement));
    }

    [Test]
    public void IntermediateEntriesCanBeShownButNeverBecomeInteractive()
    {
        BuildingContentCatalog catalog = CreateCatalog();
        AddEntry(catalog, "barracks", BuildingEra.Dark, BuildingContentStatus.Planned, 0);
        StrategicPlanningState state = State(BuildingEra.Dark, 0);

        Assert.That(BuildingConstructionUiRules.BuildEntries(catalog, state, false), Is.Empty);
        List<BuildingConstructionUiEntryState> entries = BuildingConstructionUiRules.BuildEntries(catalog, state, true);
        Assert.That(entries.Count, Is.EqualTo(1));
        Assert.That(entries[0].assetOrigin, Is.EqualTo(BuildingContentAssetStage.Planned));
        Assert.That(entries[0].availability, Is.EqualTo(BuildingAvailabilityResult.ContentUnavailable));
        Assert.That(entries[0].interactable, Is.False);
    }

    [Test]
    public void TechnologyAndBuildingPrerequisitesAreReflectedInUiState()
    {
        BuildingContentCatalog catalog = CreateCatalog();
        BuildingContentSpec university = AddEntry(catalog, "university", BuildingEra.Castle,
            BuildingContentStatus.Playable, 0);
        university.requirements.Add(new BuildingRequirement { technologyCode = "writing" });
        university.requirements.Add(new BuildingRequirement { buildingCode = "town_center" });
        StrategicPlanningState state = State(BuildingEra.Castle, 0);

        BuildingConstructionUiEntryState result = Find(BuildingConstructionUiRules.BuildEntries(catalog, state, false), "university");
        Assert.That(result.availability, Is.EqualTo(BuildingAvailabilityResult.TechnologyRequirement));
        Assert.That(result.interactable, Is.False);
        state.unlockedTechnologyCodes.Add("writing");
        result = Find(BuildingConstructionUiRules.BuildEntries(catalog, state, false), "university");
        Assert.That(result.availability, Is.EqualTo(BuildingAvailabilityResult.BuildingRequirement));
    }

    private BuildingContentCatalog CreateCatalog()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        return catalog;
    }

    private BuildingContentSpec AddEntry(BuildingContentCatalog catalog, string code, BuildingEra era,
        BuildingContentStatus status, int populationCapacity)
    {
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        _created.Add(data);
        BuildingContentSpec entry = new BuildingContentSpec
        {
            code = code, minimumEra = era, status = status,
            buildingData = status >= BuildingContentStatus.Playable ? data : null,
            populationCapacity = populationCapacity
        };
        catalog.entries.Add(entry);
        return entry;
    }

    private static StrategicPlanningState State(BuildingEra era, int population)
    {
        return new StrategicPlanningState
        {
            ownerId = 0, era = era, population = population,
            buildings = new List<StrategicBuildingObservation>(),
            unlockedTechnologyCodes = new List<string>()
        };
    }

    private static BuildingConstructionUiEntryState Find(List<BuildingConstructionUiEntryState> entries, string code)
    {
        return entries.Find(entry => string.Equals(entry.code, code, System.StringComparison.OrdinalIgnoreCase));
    }
}
