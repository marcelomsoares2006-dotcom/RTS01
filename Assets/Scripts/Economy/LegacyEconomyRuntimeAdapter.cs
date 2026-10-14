using System;
using System.Collections.Generic;

/// <summary>
/// Runtime boundary between legacy workers/resources and the deterministic economy
/// contracts. It owns no scene objects and can therefore be driven by simulation,
/// save/load, or a Unity MonoBehaviour without coupling those layers together.
/// </summary>
public sealed class LegacyEconomyRuntimeAdapter
{
    private readonly Dictionary<int, EconomyPlayerState> _players =
        new Dictionary<int, EconomyPlayerState>();
    private readonly List<int> _ownerOrder = new List<int>();

    public EconomyPlayerState GetOrCreatePlayer(int ownerId, int initialCapacity = 10)
    {
        if (ownerId < 0) throw new ArgumentOutOfRangeException(nameof(ownerId));
        EconomyPlayerState state;
        if (_players.TryGetValue(ownerId, out state)) return state;

        state = new EconomyPlayerState();
        state.population.capacity = initialCapacity < 0 ? 0 : initialCapacity;
        _players.Add(ownerId, state);
        _ownerOrder.Add(ownerId);
        _ownerOrder.Sort();
        return state;
    }

    public bool TryGetPlayer(int ownerId, out EconomyPlayerState state)
    {
        return _players.TryGetValue(ownerId, out state);
    }

    /// <summary>Registers one existing worker and reserves its population slot.</summary>
    public bool TryRegisterWorker(string workerId, int ownerId, EconomyResourceType resource,
        float ratePerSecond, float carryCapacity, int populationCost = 1)
    {
        if (string.IsNullOrEmpty(workerId) || ownerId < 0 || populationCost < 0)
            return false;
        EconomyPlayerState state = GetOrCreatePlayer(ownerId);
        if (FindCollector(state, workerId) != null) return false;
        if (!state.population.TryAdd(populationCost)) return false;

        state.collectors.Add(new EconomyCollector(
            workerId, ownerId, resource, ratePerSecond, carryCapacity));
        state.collectors.Sort(CompareCollectors);
        return true;
    }

    /// <summary>Convenience boundary for the existing Unit runtime.</summary>
    public bool TryRegisterWorker(Unit worker, EconomyResourceType resource,
        float ratePerSecond, float carryCapacity, int populationCost = 1)
    {
        if (worker == null) return false;
        return TryRegisterWorker(worker.Uid, worker.Owner, resource,
            ratePerSecond, carryCapacity, populationCost);
    }

    public bool TryUnregisterWorker(string workerId, int ownerId, int populationCost = 1)
    {
        if (string.IsNullOrEmpty(workerId) || populationCost < 0) return false;
        EconomyPlayerState state;
        if (!_players.TryGetValue(ownerId, out state)) return false;
        EconomyCollector collector = FindCollector(state, workerId);
        if (collector == null) return false;
        if (state.population.current < populationCost) return false;
        if (!state.population.TryRemove(populationCost)) return false;
        state.collectors.Remove(collector);
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        for (int i = 0; i < _ownerOrder.Count; i++)
            _players[_ownerOrder[i]].TickCollectors(deltaTime);
    }

    public bool TickOwner(int ownerId, float deltaTime)
    {
        EconomyPlayerState state;
        if (deltaTime <= 0f || !_players.TryGetValue(ownerId, out state)) return false;
        state.TickCollectors(deltaTime);
        return true;
    }

    /// <summary>Copies legacy stockpile values into the new ledger; Food is preserved.</summary>
    public static void ImportLegacyStockpile(EconomyLedger target,
        IDictionary<InGameResource, GameResource> legacyStockpile)
    {
        if (target == null || legacyStockpile == null) return;
        GameResource value;
        if (legacyStockpile.TryGetValue(InGameResource.Gold, out value) && value != null)
            target.Set(EconomyResourceType.Gold, value.Amount);
        if (legacyStockpile.TryGetValue(InGameResource.Wood, out value) && value != null)
            target.Set(EconomyResourceType.Wood, value.Amount);
        if (legacyStockpile.TryGetValue(InGameResource.Stone, out value) && value != null)
            target.Set(EconomyResourceType.Stone, value.Amount);
    }

    /// <summary>Copies shared resources back to the legacy dictionary; Food stays in the new ledger.</summary>
    public static void ExportLegacyStockpile(EconomyLedger source,
        IDictionary<InGameResource, GameResource> legacyStockpile)
    {
        if (source == null || legacyStockpile == null) return;
        SetLegacyAmount(source, legacyStockpile, EconomyResourceType.Gold, InGameResource.Gold);
        SetLegacyAmount(source, legacyStockpile, EconomyResourceType.Wood, InGameResource.Wood);
        SetLegacyAmount(source, legacyStockpile, EconomyResourceType.Stone, InGameResource.Stone);
    }

    private static void SetLegacyAmount(EconomyLedger source,
        IDictionary<InGameResource, GameResource> target,
        EconomyResourceType resource, InGameResource legacyResource)
    {
        GameResource value;
        if (target.TryGetValue(legacyResource, out value) && value != null)
            value.Amount = source.Get(resource);
    }

    private static EconomyCollector FindCollector(EconomyPlayerState state, string workerId)
    {
        if (state == null || state.collectors == null) return null;
        for (int i = 0; i < state.collectors.Count; i++)
            if (state.collectors[i] != null && state.collectors[i].collectorId == workerId)
                return state.collectors[i];
        return null;
    }

    private static int CompareCollectors(EconomyCollector left, EconomyCollector right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left == null) return -1;
        if (right == null) return 1;
        return string.CompareOrdinal(left.collectorId, right.collectorId);
    }
}
