using System;

/// <summary>Deterministic collector contract for worker controllers.</summary>
[Serializable]
public sealed class EconomyCollector
{
    public string collectorId;
    public int ownerId;
    public EconomyResourceType resource = EconomyResourceType.Food;
    public float ratePerSecond = 1f;
    public float carryCapacity = 10f;
    [NonSerialized] private float _carried;

    public EconomyCollector() { }
    public EconomyCollector(string collectorId, int ownerId, EconomyResourceType resource, float ratePerSecond, float carryCapacity)
    {
        this.collectorId = collectorId; this.ownerId = ownerId; this.resource = resource;
        this.ratePerSecond = Math.Max(0f, ratePerSecond); this.carryCapacity = Math.Max(0f, carryCapacity);
    }
    public int Tick(float deltaTime)
    {
        if (deltaTime <= 0f || ratePerSecond <= 0f || carryCapacity <= _carried) return 0;
        float next = Math.Min(carryCapacity, _carried + ratePerSecond * deltaTime);
        int gathered = (int)Math.Floor(next) - (int)Math.Floor(_carried);
        _carried = next; return gathered;
    }
    public float CarriedAmount { get { return _carried; } }
    public void SetCarriedAmount(float carried) { _carried = Math.Max(0f, Math.Min(carryCapacity, carried)); }
    public int Deposit(EconomyLedger stockpile)
    {
        if (stockpile == null || _carried <= 0f) return 0;
        int amount = (int)Math.Floor(_carried);
        if (amount <= 0) return 0;
        stockpile.Add(resource, amount); _carried -= amount; return amount;
    }
}
