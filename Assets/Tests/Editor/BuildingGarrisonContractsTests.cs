using System.Collections.Generic;
using NUnit.Framework;

public sealed class BuildingGarrisonContractsTests
{
    private static BuildingContentSpec Contract(int capacity = 2, params string[] allowed)
    {
        return new BuildingContentSpec
        {
            code = "town_center",
            supportsGarrison = true,
            garrisonCapacity = capacity,
            allowedUnitCodes = new List<string>(allowed ?? new string[0])
        };
    }

    [Test]
    public void EntryAcceptsFriendlyAllowedUnitUntilCapacity()
    {
        BuildingContentSpec spec = Contract(2, "villager", "spearman");
        Assert.That(BuildingGarrisonRules.ValidateEntry(spec, 1, true, 1, "Villager", 0), Is.EqualTo(BuildingGarrisonResult.Accepted));
        Assert.That(BuildingGarrisonRules.ValidateEntry(spec, 1, true, 1, "spearman", 1), Is.EqualTo(BuildingGarrisonResult.Accepted));
        Assert.That(BuildingGarrisonRules.ValidateEntry(spec, 1, true, 1, "villager", 2), Is.EqualTo(BuildingGarrisonResult.CapacityFull));
    }

    [Test]
    public void EntryRejectsUnsafeOrForeignRequests()
    {
        BuildingContentSpec spec = Contract(1, "villager");
        Assert.That(BuildingGarrisonRules.ValidateEntry(null, 1, true, 1, "villager", 0), Is.EqualTo(BuildingGarrisonResult.InvalidContract));
        Assert.That(BuildingGarrisonRules.ValidateEntry(spec, 1, false, 1, "villager", 0), Is.EqualTo(BuildingGarrisonResult.BuildingNotOperational));
        Assert.That(BuildingGarrisonRules.ValidateEntry(spec, 1, true, 2, "villager", 0), Is.EqualTo(BuildingGarrisonResult.WrongOwner));
        Assert.That(BuildingGarrisonRules.ValidateEntry(spec, 1, true, 1, "archer", 0), Is.EqualTo(BuildingGarrisonResult.UnitNotAllowed));
        Assert.That(BuildingGarrisonRules.ValidateEntry(spec, 1, true, 1, "", 0), Is.EqualTo(BuildingGarrisonResult.InvalidUnit));
    }

    [Test]
    public void EmptyAllowListMeansAnyUnitButEmptyCodeStillFails()
    {
        BuildingContentSpec spec = Contract(3);
        Assert.That(BuildingGarrisonRules.AllowsUnit(spec.allowedUnitCodes, "archer"), Is.True);
        Assert.That(BuildingGarrisonRules.AllowsUnit(spec.allowedUnitCodes, "  "), Is.False);
    }

    [Test]
    public void ExitRequiresMatchingOwnerAndMembership()
    {
        BuildingContentSpec spec = Contract(2, "villager");
        Assert.That(BuildingGarrisonRules.ValidateExit(spec, 1, 1, "villager", true), Is.EqualTo(BuildingGarrisonResult.Accepted));
        Assert.That(BuildingGarrisonRules.ValidateExit(spec, 1, 2, "villager", true), Is.EqualTo(BuildingGarrisonResult.WrongOwner));
        Assert.That(BuildingGarrisonRules.ValidateExit(spec, 1, 1, "villager", false), Is.EqualTo(BuildingGarrisonResult.NotGarrisoned));
    }
}
