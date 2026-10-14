using System;
using System.Collections.Generic;

/// <summary>Economy state boundary; existing Globals and UI remain compatible.</summary>
[Serializable]
public sealed class EconomyPlayerState
{
    public EconomyLedger stockpile = new EconomyLedger();
    public PopulationLedger population = new PopulationLedger(10, 0);
    public List<EconomyCollector> collectors = new List<EconomyCollector>();

    public void TickCollectors(float deltaTime)
    {
        if (collectors == null) return;
        for (int i = 0; i < collectors.Count; i++) if (collectors[i] != null) collectors[i].Deposit(stockpile);
        for (int i = 0; i < collectors.Count; i++) if (collectors[i] != null) collectors[i].Tick(deltaTime);
    }
}
