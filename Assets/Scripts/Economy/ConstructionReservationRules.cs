using System;
using System.Collections.Generic;

public enum ConstructionReservationResult
{
    Accepted,
    MissingId,
    DuplicateId,
    InvalidCost,
    MissingStockpile,
    MissingPopulationLedger,
    InsufficientResources,
    PopulationCap,
    NotFound
}

[Serializable]
public sealed class ConstructionReservationRequest
{
    public string reservationId;
    public int priority;
    public int population;
    public List<BuildingMaintenanceCost> costs = new List<BuildingMaintenanceCost>();
}

[Serializable]
public sealed class ConstructionReservationRecord
{
    public string reservationId;
    public int priority;
    public int population;
    public List<BuildingMaintenanceCost> costs = new List<BuildingMaintenanceCost>();
}

/// <summary>
/// Atomic reservation ledger shared by construction and production adapters.
/// Requests are ordered by priority then stable ID; a failed batch rolls back
/// every debit made by that batch.
/// </summary>
public sealed class ConstructionReservationLedger
{
    private readonly EconomyLedger _stockpile;
    private readonly PopulationLedger _population;
    private readonly Dictionary<string, ConstructionReservationRecord> _records =
        new Dictionary<string, ConstructionReservationRecord>(StringComparer.OrdinalIgnoreCase);

    public ConstructionReservationLedger(EconomyLedger stockpile, PopulationLedger population)
    {
        _stockpile = stockpile;
        _population = population;
    }

    public int Count { get { return _records.Count; } }
    public bool Contains(string id) { return !string.IsNullOrWhiteSpace(id) && _records.ContainsKey(id.Trim()); }

    public ConstructionReservationResult TryReserve(ConstructionReservationRequest request)
    {
        ConstructionReservationResult result = ValidateRequest(request);
        if (result != ConstructionReservationResult.Accepted) return result;
        if (Contains(request.reservationId)) return ConstructionReservationResult.DuplicateId;
        return TryReserveInternal(request);
    }

    public ConstructionReservationResult TryReserveBatch(
        IList<ConstructionReservationRequest> requests)
    {
        if (requests == null) return ConstructionReservationResult.MissingId;
        List<ConstructionReservationRequest> ordered = new List<ConstructionReservationRequest>(requests);
        ordered.Sort(CompareRequests);
        List<string> added = new List<string>();
        for (int i = 0; i < ordered.Count; i++)
        {
            ConstructionReservationResult validation = ValidateRequest(ordered[i]);
            if (validation != ConstructionReservationResult.Accepted || Contains(ordered[i].reservationId))
            {
                Rollback(added);
                return validation == ConstructionReservationResult.Accepted
                    ? ConstructionReservationResult.DuplicateId : validation;
            }
            ConstructionReservationResult result = TryReserveInternal(ordered[i]);
            if (result != ConstructionReservationResult.Accepted)
            {
                Rollback(added);
                return result;
            }
            added.Add(ordered[i].reservationId.Trim());
        }
        return ConstructionReservationResult.Accepted;
    }

    public ConstructionReservationResult TryCancel(string reservationId)
    {
        if (string.IsNullOrWhiteSpace(reservationId)) return ConstructionReservationResult.MissingId;
        string id = reservationId.Trim();
        ConstructionReservationRecord record;
        if (!_records.TryGetValue(id, out record)) return ConstructionReservationResult.NotFound;
        Refund(record);
        _records.Remove(id);
        return ConstructionReservationResult.Accepted;
    }

    public ConstructionReservationResult Commit(string reservationId)
    {
        if (string.IsNullOrWhiteSpace(reservationId)) return ConstructionReservationResult.MissingId;
        return _records.Remove(reservationId.Trim())
            ? ConstructionReservationResult.Accepted : ConstructionReservationResult.NotFound;
    }

    public bool TryGet(string reservationId, out ConstructionReservationRecord record)
    {
        record = null;
        if (string.IsNullOrWhiteSpace(reservationId)) return false;
        return _records.TryGetValue(reservationId.Trim(), out record);
    }

    private ConstructionReservationResult TryReserveInternal(ConstructionReservationRequest request)
    {
        List<BuildingMaintenanceCost> costs = Normalize(request.costs);
        if (costs == null) return ConstructionReservationResult.InvalidCost;
        for (int i = 0; i < costs.Count; i++)
            if (!_stockpile.CanSpend(costs[i].resource, costs[i].amount))
                return ConstructionReservationResult.InsufficientResources;
        if (!_population.CanAdd(request.population)) return ConstructionReservationResult.PopulationCap;
        for (int i = 0; i < costs.Count; i++) _stockpile.TrySpend(costs[i].resource, costs[i].amount);
        _population.TryAdd(request.population);
        _records.Add(request.reservationId.Trim(), new ConstructionReservationRecord {
            reservationId = request.reservationId.Trim(), priority = request.priority,
            population = request.population, costs = costs
        });
        return ConstructionReservationResult.Accepted;
    }

    private ConstructionReservationResult ValidateRequest(ConstructionReservationRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.reservationId)) return ConstructionReservationResult.MissingId;
        if (_stockpile == null) return ConstructionReservationResult.MissingStockpile;
        if (_population == null) return ConstructionReservationResult.MissingPopulationLedger;
        if (request.population < 0 || Normalize(request.costs) == null) return ConstructionReservationResult.InvalidCost;
        return ConstructionReservationResult.Accepted;
    }

    private void Rollback(List<string> ids)
    {
        for (int i = ids.Count - 1; i >= 0; i--) TryCancel(ids[i]);
    }

    private void Refund(ConstructionReservationRecord record)
    {
        for (int i = 0; i < record.costs.Count; i++) _stockpile.Add(record.costs[i].resource, record.costs[i].amount);
        _population.TryRemove(record.population);
    }

    private static int CompareRequests(ConstructionReservationRequest left, ConstructionReservationRequest right)
    {
        int priority = right.priority.CompareTo(left.priority);
        return priority != 0 ? priority : string.Compare(left.reservationId, right.reservationId, StringComparison.OrdinalIgnoreCase);
    }

    private static List<BuildingMaintenanceCost> Normalize(List<BuildingMaintenanceCost> source)
    {
        List<BuildingMaintenanceCost> result = new List<BuildingMaintenanceCost>();
        if (source == null) return result;
        for (int i = 0; i < source.Count; i++)
        {
            BuildingMaintenanceCost item = source[i];
            if (item == null || item.amount < 0) return null;
            if (item.amount == 0) continue;
            BuildingMaintenanceCost existing = result.Find(x => x.resource == item.resource);
            if (existing == null) result.Add(new BuildingMaintenanceCost(item.resource, item.amount));
            else
            {
                long total = (long)existing.amount + item.amount;
                if (total > int.MaxValue) return null;
                existing.amount = (int)total;
            }
        }
        return result;
    }
}
