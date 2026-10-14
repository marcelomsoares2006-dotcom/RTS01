using System.Collections.Generic;
using NUnit.Framework;

public sealed class BuildingConstructionGateTests
{
    [Test]
    public void GateRejectsEraBeforeCheckingPlacementOrCost()
    {
        BuildingData data = CreateBuilding("keep", new ResourceValue(InGameResource.Stone, 100));
        BuildingCatalogData catalog = CreateCatalog(data, BuildingEra.Imperial);
        BuildingConstructionGateResult result = BuildingConstructionGate.Evaluate(
            new BuildingCatalogResolver(new[] { catalog }, new[] { data }),
            "keep", BuildingEra.Castle, 0, true,
            new Dictionary<InGameResource, int> { { InGameResource.Stone, 100 } });
        Assert.That(result, Is.EqualTo(BuildingConstructionGateResult.EraLocked));
    }

    [Test]
    public void GateRequiresPlacementAndAllCostsBeforeAllowingConstruction()
    {
        BuildingData data = CreateBuilding("house", new ResourceValue(InGameResource.Wood, 50));
        BuildingCatalogData catalog = CreateCatalog(data, BuildingEra.Dark);
        BuildingCatalogResolver resolver = new BuildingCatalogResolver(new[] { catalog }, new[] { data });
        Dictionary<InGameResource, int> stockpile = new Dictionary<InGameResource, int>
        { { InGameResource.Wood, 50 } };
        Assert.That(BuildingConstructionGate.Evaluate(resolver, "house", BuildingEra.Dark, 0, false, stockpile),
            Is.EqualTo(BuildingConstructionGateResult.InvalidPlacement));
        Assert.That(BuildingConstructionGate.Evaluate(resolver, "house", BuildingEra.Dark, 0, true,
            new Dictionary<InGameResource, int> { { InGameResource.Wood, 49 } }),
            Is.EqualTo(BuildingConstructionGateResult.InsufficientResources));
        Assert.That(BuildingConstructionGate.Evaluate(resolver, "house", BuildingEra.Dark, 0, true, stockpile),
            Is.EqualTo(BuildingConstructionGateResult.Allowed));
    }

    [Test]
    public void LedgerConsumptionIsAtomicWhenCostCannotBePaid()
    {
        EconomyLedger ledger = new EconomyLedger();
        ledger.Set(EconomyResourceType.Wood, 10);
        List<ResourceValue> cost = new List<ResourceValue>
        { new ResourceValue(InGameResource.Wood, 8), new ResourceValue(InGameResource.Stone, 1) };
        Assert.IsFalse(BuildingConstructionGate.TryConsume(ledger, cost));
        Assert.AreEqual(10, ledger.Get(EconomyResourceType.Wood));
        Assert.AreEqual(0, ledger.Get(EconomyResourceType.Stone));
    }

    [Test]
    public void LegacyGateRejectsUndefinedResourceWithoutThrowingOrSpending()
    {
        EconomyLedger ledger = new EconomyLedger();
        ledger.Set(EconomyResourceType.Wood, 100);
        List<ResourceValue> cost = new List<ResourceValue>
        { new ResourceValue((InGameResource)999, 1) };

        Assert.IsFalse(BuildingConstructionGate.TryConsume(ledger, cost));
        Assert.AreEqual(100, ledger.Get(EconomyResourceType.Wood));
    }

    [Test]
    public void ContentGateRejectsUndefinedResourceWithoutOverflowingCostTotals()
    {
        BuildingContentCatalog catalog = UnityEngine.ScriptableObject.CreateInstance<BuildingContentCatalog>();
        BuildingData data = UnityEngine.ScriptableObject.CreateInstance<BuildingData>();
        data.code = "house";
        catalog.entries.Add(new BuildingContentSpec
        {
            code = "house",
            status = BuildingContentStatus.Playable,
            buildingData = data,
            constructionCost = new List<BuildingContentCost>
            {
                new BuildingContentCost { resource = (EconomyResourceType)999, amount = 1 }
            }
        });
        StrategicPlanningState state = new StrategicPlanningState { ownerId = 0, era = BuildingEra.Dark };

        Assert.That(BuildingConstructionGate.Evaluate(
            catalog, "house", state, true,
            new Dictionary<InGameResource, int> { { InGameResource.Wood, 100 } }),
            Is.EqualTo(BuildingConstructionGateResult.InvalidCost));

        UnityEngine.Object.DestroyImmediate(data);
        UnityEngine.Object.DestroyImmediate(catalog);
    }

    [Test]
    public void ContentCatalogGateBlocksPlannedEntriesAndChecksDeclaredCost()
    {
        BuildingContentCatalog catalog = UnityEngine.ScriptableObject.CreateInstance<BuildingContentCatalog>();
        BuildingData data = UnityEngine.ScriptableObject.CreateInstance<BuildingData>();
        data.code = "house";
        BuildingContentSpec entry = new BuildingContentSpec
        {
            code = "house",
            status = BuildingContentStatus.Playable,
            buildingData = data,
            constructionCost = new List<BuildingContentCost>
            {
                new BuildingContentCost { resource = EconomyResourceType.Wood, amount = 50 }
            }
        };
        catalog.entries.Add(entry);
        StrategicPlanningState state = new StrategicPlanningState { ownerId = 0, era = BuildingEra.Dark };
        Dictionary<InGameResource, int> resources = new Dictionary<InGameResource, int>
        { { InGameResource.Wood, 50 } };

        Assert.That(BuildingConstructionGate.Evaluate(catalog, "house", state, true, resources),
            Is.EqualTo(BuildingConstructionGateResult.Allowed));
        resources[InGameResource.Wood] = 49;
        Assert.That(BuildingConstructionGate.Evaluate(catalog, "house", state, true, resources),
            Is.EqualTo(BuildingConstructionGateResult.InsufficientResources));

        entry.status = BuildingContentStatus.Planned;
        Assert.That(BuildingConstructionGate.Evaluate(catalog, "house", state, true, resources),
            Is.EqualTo(BuildingConstructionGateResult.ContentUnavailable));
        UnityEngine.Object.DestroyImmediate(data);
        UnityEngine.Object.DestroyImmediate(catalog);
    }

    private static BuildingData CreateBuilding(string code, params ResourceValue[] cost)
    {
        BuildingData data = UnityEngine.ScriptableObject.CreateInstance<BuildingData>();
        data.code = code;
        data.cost = new List<ResourceValue>(cost);
        return data;
    }

    private static BuildingCatalogData CreateCatalog(BuildingData data, BuildingEra era)
    {
        BuildingCatalogData catalog = UnityEngine.ScriptableObject.CreateInstance<BuildingCatalogData>();
        catalog.buildingData = data;
        catalog.minimumEra = era;
        return catalog;
    }
}
