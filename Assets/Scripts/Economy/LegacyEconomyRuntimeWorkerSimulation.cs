using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Read-only state supplied by a worker controller to the economy loop.</summary>
public struct LegacyEconomyWorkerSnapshot
{
    public string workerId;
    public int ownerId;
    public EconomyResourceType resource;
    public float ratePerSecond;
    public float carryCapacity;
    public int populationCost;
    public bool alive;

    public LegacyEconomyWorkerSnapshot(string workerId, int ownerId,
        EconomyResourceType resource, float ratePerSecond, float carryCapacity,
        int populationCost = 1, bool alive = true)
    {
        this.workerId = workerId;
        this.ownerId = ownerId;
        this.resource = resource;
        this.ratePerSecond = ratePerSecond;
        this.carryCapacity = carryCapacity;
        this.populationCost = populationCost;
        this.alive = alive;
    }
}

/// <summary>
/// Deterministic registration/reconciliation loop for legacy worker controllers.
/// The loop owns only IDs and economy bindings; scene objects remain owned by the caller.
/// </summary>
public sealed class LegacyEconomyRuntimeWorkerSimulation
{
    private sealed class Binding
    {
        public string workerId;
        public int ownerId;
        public EconomyResourceType resource;
        public float ratePerSecond;
        public float carryCapacity;
        public int populationCost;

        public bool Matches(LegacyEconomyWorkerSnapshot snapshot)
        {
            return ownerId == snapshot.ownerId && resource == snapshot.resource &&
                Mathf.Approximately(ratePerSecond, snapshot.ratePerSecond) &&
                Mathf.Approximately(carryCapacity, snapshot.carryCapacity) &&
                populationCost == snapshot.populationCost;
        }
    }

    private readonly LegacyEconomyRuntimeAdapter _adapter;
    private readonly Dictionary<string, Binding> _bindings =
        new Dictionary<string, Binding>(StringComparer.Ordinal);
    private readonly List<string> _orderedIds = new List<string>();
    private readonly List<LegacyEconomyWorkerSnapshot> _orderedSnapshots =
        new List<LegacyEconomyWorkerSnapshot>();

    public LegacyEconomyRuntimeWorkerSimulation(LegacyEconomyRuntimeAdapter adapter = null)
    {
        _adapter = adapter ?? new LegacyEconomyRuntimeAdapter();
    }

    public LegacyEconomyRuntimeAdapter Adapter { get { return _adapter; } }
    public int RegisteredWorkerCount { get { return _bindings.Count; } }

    /// <summary>
    /// Reconciles one complete worker snapshot. Missing or dead workers are unregistered.
    /// Duplicate IDs are ignored after a stable ID/owner sort, so iteration order cannot
    /// change population or resource results.
    /// </summary>
    public void Reconcile(IEnumerable<LegacyEconomyWorkerSnapshot> snapshots)
    {
        _orderedSnapshots.Clear();
        if (snapshots != null)
        {
            foreach (LegacyEconomyWorkerSnapshot snapshot in snapshots)
            {
                if (IsValid(snapshot) && snapshot.alive)
                    _orderedSnapshots.Add(snapshot);
            }
        }
        _orderedSnapshots.Sort(CompareSnapshots);

        Dictionary<string, LegacyEconomyWorkerSnapshot> desired =
            new Dictionary<string, LegacyEconomyWorkerSnapshot>(StringComparer.Ordinal);
        for (int i = 0; i < _orderedSnapshots.Count; i++)
        {
            LegacyEconomyWorkerSnapshot snapshot = _orderedSnapshots[i];
            if (!desired.ContainsKey(snapshot.workerId))
                desired.Add(snapshot.workerId, snapshot);
        }

        _orderedIds.Clear();
        foreach (string workerId in _bindings.Keys) _orderedIds.Add(workerId);
        _orderedIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < _orderedIds.Count; i++)
        {
            string workerId = _orderedIds[i];
            Binding binding = _bindings[workerId];
            LegacyEconomyWorkerSnapshot snapshot;
            if (!desired.TryGetValue(workerId, out snapshot))
            {
                Unregister(binding);
                _bindings.Remove(workerId);
            }
            else if (!binding.Matches(snapshot))
            {
                Unregister(binding);
                _bindings.Remove(workerId);
                Register(snapshot);
            }
        }

        foreach (KeyValuePair<string, LegacyEconomyWorkerSnapshot> entry in desired)
            if (!_bindings.ContainsKey(entry.Key)) Register(entry.Value);
    }

    public void Tick(float deltaTime)
    {
        _adapter.Tick(deltaTime);
    }

    public bool RemoveWorker(string workerId)
    {
        Binding binding;
        if (string.IsNullOrEmpty(workerId) || !_bindings.TryGetValue(workerId, out binding))
            return false;
        bool removed = Unregister(binding);
        if (removed) _bindings.Remove(workerId);
        return removed;
    }

    private bool Register(LegacyEconomyWorkerSnapshot snapshot)
    {
        if (_bindings.ContainsKey(snapshot.workerId)) return false;
        if (!_adapter.TryRegisterWorker(snapshot.workerId, snapshot.ownerId, snapshot.resource,
            snapshot.ratePerSecond, snapshot.carryCapacity, snapshot.populationCost)) return false;
        _bindings.Add(snapshot.workerId, new Binding
        {
            workerId = snapshot.workerId,
            ownerId = snapshot.ownerId,
            resource = snapshot.resource,
            ratePerSecond = snapshot.ratePerSecond,
            carryCapacity = snapshot.carryCapacity,
            populationCost = snapshot.populationCost
        });
        return true;
    }

    private bool Unregister(Binding binding)
    {
        return _adapter.TryUnregisterWorker(binding.workerId, binding.ownerId, binding.populationCost);
    }

    private static bool IsValid(LegacyEconomyWorkerSnapshot snapshot)
    {
        return !string.IsNullOrEmpty(snapshot.workerId) && snapshot.ownerId >= 0 &&
            snapshot.ratePerSecond >= 0f && snapshot.carryCapacity >= 0f &&
            snapshot.populationCost >= 0;
    }

    private static int CompareSnapshots(LegacyEconomyWorkerSnapshot left,
        LegacyEconomyWorkerSnapshot right)
    {
        int id = string.CompareOrdinal(left.workerId, right.workerId);
        return id != 0 ? id : left.ownerId.CompareTo(right.ownerId);
    }
}

/// <summary>
/// Optional Unity host. A worker system calls Reconcile with its current snapshot;
/// Update advances collection and naturally drops workers marked dead or omitted.
/// </summary>
public sealed class LegacyEconomyRuntimeController : MonoBehaviour
{
    private LegacyEconomyRuntimeWorkerSimulation _simulation;

    public LegacyEconomyRuntimeWorkerSimulation Simulation
    {
        get
        {
            if (_simulation == null) _simulation = new LegacyEconomyRuntimeWorkerSimulation();
            return _simulation;
        }
    }

    public void Reconcile(IEnumerable<LegacyEconomyWorkerSnapshot> snapshots)
    {
        Simulation.Reconcile(snapshots);
    }

    public void TickSimulation(float deltaTime)
    {
        Simulation.Tick(deltaTime);
    }

    private void Update()
    {
        Simulation.Tick(Time.deltaTime);
    }
}
