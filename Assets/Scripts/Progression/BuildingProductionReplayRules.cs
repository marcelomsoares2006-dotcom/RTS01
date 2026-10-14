using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>Deterministic fingerprint for production/progression replay checkpoints.</summary>
public static class BuildingProductionReplayRules
{
    public static string ComputeFingerprint(
        BuildingContentCatalog catalog,
        StrategicPlanningState state,
        BuildingProductionQueue queue,
        EconomyLedger stockpile,
        PopulationLedger population,
        string buildingCode)
    {
        StringBuilder value = new StringBuilder();
        BuildingContentSpec entry = catalog == null ? null : catalog.Find(buildingCode);
        value.Append(state == null ? -1 : state.ownerId).Append('|')
            .Append(state == null ? -1 : (int)state.era).Append('|')
            .Append(entry == null ? "missing" : entry.code.Trim()).Append('|')
            .Append(entry != null && entry.IsRuntimeReady ? "runtime" : "unavailable").Append('|');
        List<string> technologies = new List<string>();
        if (state != null && state.unlockedTechnologyCodes != null)
            for (int i = 0; i < state.unlockedTechnologyCodes.Count; i++)
                if (!string.IsNullOrWhiteSpace(state.unlockedTechnologyCodes[i]))
                    technologies.Add(state.unlockedTechnologyCodes[i].Trim().ToLowerInvariant());
        technologies.Sort(StringComparer.Ordinal);
        for (int i = 0; i < technologies.Count; i++) value.Append(technologies[i]).Append(',');
        value.Append('|').Append(population == null ? -1 : population.current).Append('/')
            .Append(population == null ? -1 : population.capacity).Append('|');
        foreach (EconomyResourceType resource in Enum.GetValues(typeof(EconomyResourceType)))
            value.Append(stockpile == null ? -1 : stockpile.Get(resource)).Append(',');
        if (queue != null && queue.orders != null)
            for (int i = 0; i < queue.orders.Count; i++)
            {
                BuildingProductionOrder order = queue.orders[i];
                if (order == null) { value.Append("null;"); continue; }
                value.Append(order.productCode).Append(':')
                    .Append(order.duration.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                    .Append(order.elapsed.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                    .Append(order.populationCost).Append(';');
            }
        return Hash(value.ToString());
    }

    private static string Hash(string value)
    {
        unchecked
        {
            ulong hash = 1469598103934665603UL;
            for (int i = 0; i < value.Length; i++) { hash ^= value[i]; hash *= 1099511628211UL; }
            return hash.ToString("X16");
        }
    }
}
