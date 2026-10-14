using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;

public sealed class BuildingProductionReplayRulesTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown() { foreach (Object asset in _created) if (asset != null) Object.DestroyImmediate(asset); }

    [Test]
    public void RejectedCommandLeavesReplayFingerprintUnchanged()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        catalog.entries.Add(new BuildingContentSpec { code = "barracks", status = BuildingContentStatus.Planned });
        StrategicPlanningState state = new StrategicPlanningState { ownerId = 0, era = BuildingEra.Dark, population = 0,
            unlockedTechnologyCodes = new List<string>() };
        EconomyLedger stockpile = new EconomyLedger();
        stockpile.Set(EconomyResourceType.Wood, 100);
        PopulationLedger population = new PopulationLedger(10, 0);
        BuildingProductionQueue queue = new BuildingProductionQueue();
        string before = BuildingProductionReplayRules.ComputeFingerprint(catalog, state, queue, stockpile, population, "barracks");
        BuildingProductionQueueResult queueResult;
        Assert.That(BuildingProductionCommandRules.TryEnqueue(catalog, state, queue, "barracks", "spearman",
            stockpile, population, out queueResult), Is.EqualTo(BuildingProductionCommandResult.ContentUnavailable));
        string after = BuildingProductionReplayRules.ComputeFingerprint(catalog, state, queue, stockpile, population, "barracks");
        Assert.That(after, Is.EqualTo(before));
    }

    [Test]
    public void FingerprintChangesWhenProgressionTechnologyChanges()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        catalog.entries.Add(new BuildingContentSpec { code = "barracks", status = BuildingContentStatus.Planned });
        StrategicPlanningState state = new StrategicPlanningState { ownerId = 0, era = BuildingEra.Dark,
            unlockedTechnologyCodes = new List<string>() };
        string before = BuildingProductionReplayRules.ComputeFingerprint(catalog, state, new BuildingProductionQueue(), new EconomyLedger(), new PopulationLedger(), "barracks");
        state.unlockedTechnologyCodes.Add("fletching");
        string after = BuildingProductionReplayRules.ComputeFingerprint(catalog, state, new BuildingProductionQueue(), new EconomyLedger(), new PopulationLedger(), "barracks");
        Assert.That(after, Is.Not.EqualTo(before));
    }

    [Test]
    public void FingerprintIsIndependentOfCultureAndTechnologyInputOrder()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        catalog.entries.Add(new BuildingContentSpec { code = "barracks", status = BuildingContentStatus.Planned });
        StrategicPlanningState state = new StrategicPlanningState { ownerId = 0, era = BuildingEra.Dark,
            unlockedTechnologyCodes = new List<string> { "Zeta", "alpha" } };
        BuildingProductionQueue queue = new BuildingProductionQueue();
        string originalCulture = CultureInfo.CurrentCulture.Name;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
            string first = BuildingProductionReplayRules.ComputeFingerprint(catalog, state, queue, new EconomyLedger(), new PopulationLedger(), "barracks");
            state.unlockedTechnologyCodes.Reverse();
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            string second = BuildingProductionReplayRules.ComputeFingerprint(catalog, state, queue, new EconomyLedger(), new PopulationLedger(), "barracks");
            Assert.That(second, Is.EqualTo(first));
        }
        finally
        {
            CultureInfo.CurrentCulture = new CultureInfo(string.IsNullOrEmpty(originalCulture) ? "en-US" : originalCulture);
        }
    }

    [Test]
    public void FingerprintIncludesCanonicalProductionReservationCost()
    {
        BuildingContentCatalog catalog = ScriptableObject.CreateInstance<BuildingContentCatalog>();
        _created.Add(catalog);
        catalog.entries.Add(new BuildingContentSpec { code = "barracks", status = BuildingContentStatus.Planned });
        BuildingProductionQueue first = new BuildingProductionQueue();
        BuildingProductionQueue second = new BuildingProductionQueue();
        first.orders.Add(new BuildingProductionOrder {
            productCode = "spearman", duration = 15f, elapsed = 2f, populationCost = 1,
            cost = new List<ResourceValue> {
                new ResourceValue(InGameResource.Wood, 30),
                new ResourceValue(InGameResource.Food, 80)
            }
        });
        second.orders.Add(new BuildingProductionOrder {
            productCode = "spearman", duration = 15f, elapsed = 2f, populationCost = 1,
            cost = new List<ResourceValue> {
                new ResourceValue(InGameResource.Food, 80),
                new ResourceValue(InGameResource.Wood, 31)
            }
        });
        StrategicPlanningState state = new StrategicPlanningState { ownerId = 0, era = BuildingEra.Dark };
        string firstFingerprint = BuildingProductionReplayRules.ComputeFingerprint(
            catalog, state, first, new EconomyLedger(), new PopulationLedger(), "barracks");
        string secondFingerprint = BuildingProductionReplayRules.ComputeFingerprint(
            catalog, state, second, new EconomyLedger(), new PopulationLedger(), "barracks");
        Assert.That(secondFingerprint, Is.Not.EqualTo(firstFingerprint));
    }
}
