using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Deterministic, UI-independent resource stockpile.</summary>
[Serializable]
public sealed class EconomyLedger : ISerializationCallbackReceiver
{
    [Serializable]
    public sealed class Entry
    {
        public EconomyResourceType resource;
        public int amount;
        public Entry() { }
        public Entry(EconomyResourceType resource, int amount) { this.resource = resource; this.amount = amount < 0 ? 0 : amount; }
    }

    public List<Entry> entries = new List<Entry>();
    [NonSerialized] private Dictionary<EconomyResourceType, Entry> _lookup = new Dictionary<EconomyResourceType, Entry>();

    public EconomyLedger() { RebuildLookup(); }
    public int Get(EconomyResourceType resource) { return FindOrCreate(resource).amount; }
    public void Set(EconomyResourceType resource, int amount) { FindOrCreate(resource).amount = amount < 0 ? 0 : amount; }
    public void Add(EconomyResourceType resource, int amount)
    {
        Entry entry = FindOrCreate(resource);
        long result = (long)entry.amount + amount;
        entry.amount = result <= 0 ? 0 : result > int.MaxValue ? int.MaxValue : (int)result;
    }
    public bool CanSpend(EconomyResourceType resource, int amount) { return amount >= 0 && Get(resource) >= amount; }
    public bool TrySpend(EconomyResourceType resource, int amount)
    {
        if (!CanSpend(resource, amount)) return false;
        FindOrCreate(resource).amount -= amount;
        return true;
    }
    public void RebuildLookup()
    {
        _lookup.Clear();
        if (entries == null) entries = new List<Entry>();
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entry entry = entries[i];
            if (entry == null) { entries.RemoveAt(i); continue; }
            entry.amount = entry.amount < 0 ? 0 : entry.amount;
            if (_lookup.ContainsKey(entry.resource)) { _lookup[entry.resource].amount += entry.amount; entries.RemoveAt(i); }
            else _lookup.Add(entry.resource, entry);
        }
    }
    public void OnBeforeSerialize() { }
    public void OnAfterDeserialize() { RebuildLookup(); }
    private Entry FindOrCreate(EconomyResourceType resource)
    {
        if (_lookup == null) RebuildLookup();
        Entry entry;
        if (_lookup.TryGetValue(resource, out entry)) return entry;
        entry = new Entry(resource, 0);
        if (entries == null) entries = new List<Entry>();
        entries.Add(entry); _lookup[resource] = entry; return entry;
    }
}
