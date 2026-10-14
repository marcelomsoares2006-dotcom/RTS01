using NUnit.Framework;
using UnityEngine;

public sealed class ResourceDropoffContractTests
{
    [Test]
    public void AcceptedResourceDepositsIntoLedgerWhenOwnerAndRangeMatch()
    {
        BuildingContentSpec spec = CreateSpec(EconomyResourceType.Wood, EconomyResourceType.Gold);
        ResourceDropoffContract dropoff = new ResourceDropoffContract(spec, 3, Vector3.zero, 4f);
        EconomyLedger ledger = new EconomyLedger();

        ResourceDropoffResult result;
        Assert.IsTrue(dropoff.TryDeposit(EconomyResourceType.Wood, 3,
            new Vector3(3f, 20f, 0f), 7, ledger, out result));
        Assert.AreEqual(ResourceDropoffResult.Accepted, result);
        Assert.AreEqual(7, ledger.Get(EconomyResourceType.Wood));
    }

    [Test]
    public void FoodIsRejectedWhenCatalogDoesNotAcceptFood()
    {
        ResourceDropoffContract dropoff = new ResourceDropoffContract(
            CreateSpec(EconomyResourceType.Wood), 1, Vector3.zero, 5f);
        EconomyLedger ledger = new EconomyLedger();
        ResourceDropoffResult result;

        Assert.IsFalse(dropoff.TryDeposit(EconomyResourceType.Food, 1,
            Vector3.zero, 4, ledger, out result));
        Assert.AreEqual(ResourceDropoffResult.UnsupportedResource, result);
        Assert.AreEqual(0, ledger.Get(EconomyResourceType.Food));
    }

    [Test]
    public void LegacyResourceUsesCatalogAndLegacyFoodPathIsRejected()
    {
        ResourceDropoffContract dropoff = new ResourceDropoffContract(
            CreateSpec(EconomyResourceType.Wood), 1, Vector3.zero, 5f);
        EconomyLedger ledger = new EconomyLedger();
        ResourceDropoffResult result;

        Assert.IsTrue(dropoff.TryDeposit(InGameResource.Wood, 1,
            Vector3.zero, 3, ledger, out result));
        Assert.AreEqual(3, ledger.Get(EconomyResourceType.Wood));

        // Food is intentionally absent from InGameResource. The enum cast
        // below represents an incompatible legacy value and must not route it.
        Assert.IsFalse(dropoff.TryDeposit((InGameResource)99, 1,
            Vector3.zero, 2, ledger, out result));
        Assert.AreEqual(ResourceDropoffResult.LegacyResourceUnsupported, result);
    }

    [Test]
    public void OwnerRangeOperationalAndAmountGatesRejectWithoutMutatingLedger()
    {
        ResourceDropoffContract dropoff = new ResourceDropoffContract(
            CreateSpec(EconomyResourceType.Gold), 8, Vector3.zero, 2f, false);
        EconomyLedger ledger = new EconomyLedger();
        ResourceDropoffResult result;

        Assert.IsFalse(dropoff.TryDeposit(EconomyResourceType.Gold, 7,
            Vector3.zero, 1, ledger, out result));
        Assert.AreEqual(ResourceDropoffResult.DropoffNotOperational, result);
        Assert.IsFalse(dropoff.TryDeposit(EconomyResourceType.Gold, 8,
            new Vector3(3f, 0f, 0f), 1, ledger, out result));
        Assert.AreEqual(ResourceDropoffResult.DropoffNotOperational, result);
        Assert.AreEqual(0, ledger.Get(EconomyResourceType.Gold));
    }

    private static BuildingContentSpec CreateSpec(params EconomyResourceType[] accepted)
    {
        BuildingContentSpec spec = new BuildingContentSpec
        {
            code = "test_dropoff",
            role = BuildingContentRole.ResourceDropoff,
            supportsDropoff = true
        };
        for (int i = 0; i < accepted.Length; i++) spec.dropoffResources.Add(accepted[i]);
        return spec;
    }
}
