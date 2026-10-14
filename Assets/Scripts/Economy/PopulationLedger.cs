using System;

/// <summary>Population accounting shared by unit production and housing.</summary>
[Serializable]
public sealed class PopulationLedger
{
    public int current;
    public int capacity;
    public PopulationLedger(int initialCapacity = 0, int initialPopulation = 0)
    {
        capacity = Math.Max(0, initialCapacity);
        current = Math.Max(0, Math.Min(initialPopulation, capacity));
    }
    public int AvailableCapacity { get { return Math.Max(0, capacity - current); } }
    public bool CanAdd(int amount) { return amount >= 0 && current <= capacity - amount; }
    public bool TryAdd(int amount) { if (!CanAdd(amount)) return false; current += amount; return true; }
    public bool TryRemove(int amount) { if (amount < 0 || amount > current) return false; current -= amount; return true; }
    public void AddCapacity(int amount) { if (amount > 0) capacity = capacity > int.MaxValue - amount ? int.MaxValue : capacity + amount; }
}
