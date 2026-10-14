using System.Collections.Generic;
using NUnit.Framework;

public sealed class ConstructionReservationRulesTests
{
    [Test]
    public void BatchAggregatesReservationsByPriorityAndCancelsWithFullRollback()
    {
        EconomyLedger stockpile = new EconomyLedger();
        stockpile.Set(EconomyResourceType.Wood, 100);
        PopulationLedger population = new PopulationLedger(5, 0);
        ConstructionReservationLedger ledger = new ConstructionReservationLedger(stockpile, population);
        Assert.That(ledger.TryReserveBatch(new List<ConstructionReservationRequest> {
            Request("low", 1, 2, 30), Request("high", 10, 1, 40)
        }), Is.EqualTo(ConstructionReservationResult.Accepted));
        Assert.AreEqual(30, stockpile.Get(EconomyResourceType.Wood));
        Assert.AreEqual(3, population.current);
        Assert.That(ledger.TryCancel("high"), Is.EqualTo(ConstructionReservationResult.Accepted));
        Assert.AreEqual(70, stockpile.Get(EconomyResourceType.Wood));
        Assert.AreEqual(2, population.current);
        Assert.That(ledger.TryCancel("low"), Is.EqualTo(ConstructionReservationResult.Accepted));
        Assert.AreEqual(100, stockpile.Get(EconomyResourceType.Wood));
        Assert.AreEqual(0, population.current);
    }

    [Test]
    public void FailedBatchRollsBackEarlierReservationsAndPreventsOverspend()
    {
        EconomyLedger stockpile = new EconomyLedger();
        stockpile.Set(EconomyResourceType.Wood, 50);
        PopulationLedger population = new PopulationLedger(2, 0);
        ConstructionReservationLedger ledger = new ConstructionReservationLedger(stockpile, population);
        Assert.That(ledger.TryReserveBatch(new List<ConstructionReservationRequest> {
            Request("first", 1, 1, 30), Request("second", 2, 2, 30)
        }), Is.EqualTo(ConstructionReservationResult.PopulationCap));
        Assert.AreEqual(50, stockpile.Get(EconomyResourceType.Wood));
        Assert.AreEqual(0, population.current);
        Assert.AreEqual(0, ledger.Count);
    }

    [Test]
    public void DuplicateAndInvalidRequestsDoNotMutateLedgers()
    {
        EconomyLedger stockpile = new EconomyLedger();
        stockpile.Set(EconomyResourceType.Wood, 50);
        ConstructionReservationLedger ledger = new ConstructionReservationLedger(stockpile, new PopulationLedger(5, 0));
        Assert.That(ledger.TryReserve(Request("same", 1, 0, 10)), Is.EqualTo(ConstructionReservationResult.Accepted));
        Assert.That(ledger.TryReserve(Request("same", 2, 0, 10)), Is.EqualTo(ConstructionReservationResult.DuplicateId));
        Assert.AreEqual(40, stockpile.Get(EconomyResourceType.Wood));
        Assert.That(ledger.TryReserve(Request("bad", 1, 0, -1)), Is.EqualTo(ConstructionReservationResult.InvalidCost));
        Assert.AreEqual(40, stockpile.Get(EconomyResourceType.Wood));
    }

    private static ConstructionReservationRequest Request(string id, int priority, int population, int wood)
    {
        return new ConstructionReservationRequest {
            reservationId = id, priority = priority, population = population,
            costs = new List<BuildingMaintenanceCost> {
                new BuildingMaintenanceCost(EconomyResourceType.Wood, wood)
            }
        };
    }
}
