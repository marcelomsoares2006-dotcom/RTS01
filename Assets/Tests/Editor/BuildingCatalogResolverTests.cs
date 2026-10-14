using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BuildingCatalogResolverTests
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
    public void LegacyBuildingRemainsResolvableWithoutCatalogEntry()
    {
        BuildingData legacy = CreateBuilding("house");
        BuildingCatalogResolver resolver = new BuildingCatalogResolver(null, new[] { legacy });

        BuildingData resolved;
        Assert.That(resolver.TryGetBuildingData("HOUSE", out resolved), Is.True);
        Assert.That(resolved, Is.SameAs(legacy));
        Assert.That(resolver.Validate("house", BuildingEra.Dark, 0), Is.EqualTo(BuildingAvailabilityResult.Available));
    }

    [Test]
    public void CatalogEntryValidatesEraAndAllRequirements()
    {
        BuildingData legacy = CreateBuilding("university");
        BuildingCatalogData catalog = CreateCatalog(legacy);
        catalog.minimumEra = BuildingEra.Castle;
        catalog.requirements.Add(new BuildingRequirement { technologyCode = "writing" });
        catalog.requirements.Add(new BuildingRequirement { buildingCode = "market" });
        catalog.requirements.Add(new BuildingRequirement { minimumPopulation = 10 });
        BuildingCatalogResolver resolver = new BuildingCatalogResolver(new[] { catalog }, new[] { legacy });

        Assert.That(resolver.Validate("university", BuildingEra.Feudal, 20), Is.EqualTo(BuildingAvailabilityResult.EraLocked));
        Assert.That(resolver.Validate("university", BuildingEra.Castle, 20, new[] { "writing" }), Is.EqualTo(BuildingAvailabilityResult.BuildingRequirement));
        Assert.That(resolver.Validate("university", BuildingEra.Castle, 20, new[] { "writing" }, new[] { "market" }), Is.EqualTo(BuildingAvailabilityResult.Available));
    }

    [Test]
    public void CatalogTakesPrecedenceWhenLegacyAndCatalogShareCode()
    {
        BuildingData legacy = CreateBuilding("tower");
        BuildingCatalogData catalog = CreateCatalog(legacy);
        catalog.minimumEra = BuildingEra.Imperial;
        BuildingCatalogResolver resolver = new BuildingCatalogResolver(new[] { catalog }, new[] { legacy });

        BuildingCatalogData resolvedCatalog;
        Assert.That(resolver.TryGetCatalog("tower", out resolvedCatalog), Is.True);
        Assert.That(resolvedCatalog, Is.SameAs(catalog));
        Assert.That(resolver.Validate("tower", BuildingEra.Castle, 0), Is.EqualTo(BuildingAvailabilityResult.EraLocked));
    }

    private BuildingData CreateBuilding(string code)
    {
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        data.code = code;
        data.name = code;
        _created.Add(data);
        return data;
    }

    private BuildingCatalogData CreateCatalog(BuildingData building)
    {
        BuildingCatalogData catalog = ScriptableObject.CreateInstance<BuildingCatalogData>();
        catalog.buildingData = building;
        _created.Add(catalog);
        return catalog;
    }
}
