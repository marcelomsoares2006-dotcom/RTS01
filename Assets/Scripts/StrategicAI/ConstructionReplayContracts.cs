using System;
using System.Collections.Generic;
using System.Text;

public enum ConstructionReplayCommandKind
{
    ConstructBuilding,
    ProduceUnit,
    CancelConstruction,
    ReserveResources,
    AdvanceEra
}

[Serializable]
public sealed class ConstructionReplayCommand
{
    public long simulationTick;
    public long issuedAtMilliseconds;
    public int priority;
    public string commandId;
    public int ownerId;
    public ConstructionReplayCommandKind kind;
    public string targetCode;
    public string reservationId;
    public int quantity = 1;
    public int populationCost;
    public List<BuildingMaintenanceCost> costs = new List<BuildingMaintenanceCost>();
}

public enum ConstructionReplayResult
{
    Accepted,
    InvalidCommand,
    DuplicateCommandId,
    InvalidOwner,
    InvalidTimestamp,
    InvalidTarget,
    DivergentReplay
}

[Serializable]
public sealed class ConstructionReplaySnapshot
{
    public BuildingEra era = BuildingEra.Dark;
    public List<string> activeBuildings = new List<string>();
    public List<string> producedUnits = new List<string>();
    public List<string> reservations = new List<string>();
    public string fingerprint;
}

[Serializable]
public sealed class ConstructionReplayState
{
    public List<string> appliedCommandIds = new List<string>();
    public string fingerprint;
}

/// <summary>
/// Canonical command log for construction-related simulation. It uses no
/// Unity object, wall clock or random value, so identical input replays to the
/// same ordered command stream and fingerprint.
/// </summary>
public sealed class ConstructionReplayLog
{
    private readonly List<ConstructionReplayCommand> _commands = new List<ConstructionReplayCommand>();
    private readonly HashSet<string> _ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ConstructionReplayCommand> Commands { get { return _commands.AsReadOnly(); } }

    public ConstructionReplayResult TryAppend(ConstructionReplayCommand command)
    {
        ConstructionReplayResult validation = Validate(command);
        if (validation != ConstructionReplayResult.Accepted) return validation;
        if (!_ids.Add(command.commandId.Trim())) return ConstructionReplayResult.DuplicateCommandId;
        _commands.Add(Clone(command));
        _commands.Sort(Compare);
        return ConstructionReplayResult.Accepted;
    }

    public ConstructionReplayResult TryReplay(ConstructionReplayState state)
    {
        if (state == null) return ConstructionReplayResult.InvalidCommand;
        string expected = ComputeFingerprint(_commands);
        if (!string.IsNullOrEmpty(state.fingerprint) && state.fingerprint != expected)
            return ConstructionReplayResult.DivergentReplay;
        state.appliedCommandIds = new List<string>();
        for (int i = 0; i < _commands.Count; i++) state.appliedCommandIds.Add(_commands[i].commandId);
        state.fingerprint = expected;
        return ConstructionReplayResult.Accepted;
    }

    public static string ComputeFingerprint(IList<ConstructionReplayCommand> commands)
    {
        StringBuilder canonical = new StringBuilder();
        if (commands != null)
        {
            List<ConstructionReplayCommand> ordered = new List<ConstructionReplayCommand>(commands);
            ordered.Sort(Compare);
            for (int i = 0; i < ordered.Count; i++)
            {
                ConstructionReplayCommand c = ordered[i];
                canonical.Append(c.simulationTick).Append('|').Append(c.issuedAtMilliseconds).Append('|')
                    .Append(c.priority).Append('|').Append(c.commandId == null ? string.Empty : c.commandId.Trim()).Append('|')
                    .Append(c.ownerId).Append('|').Append((int)c.kind).Append('|')
                    .Append(c.targetCode == null ? string.Empty : c.targetCode.Trim()).Append('|')
                    .Append(c.reservationId == null ? string.Empty : c.reservationId.Trim()).Append('|')
                    .Append(c.quantity).Append('|').Append(c.populationCost).Append('|');
                if (c.costs != null)
                    for (int j = 0; j < c.costs.Count; j++)
                        if (c.costs[j] != null)
                            canonical.Append((int)c.costs[j].resource).Append('=').Append(c.costs[j].amount).Append(',');
                canonical.Append(';');
            }
        }
        unchecked
        {
            ulong hash = 1469598103934665603UL;
            string value = canonical.ToString();
            for (int i = 0; i < value.Length; i++) { hash ^= value[i]; hash *= 1099511628211UL; }
            return hash.ToString("X16");
        }
    }

    private static ConstructionReplayResult Validate(ConstructionReplayCommand command)
    {
        if (command == null || string.IsNullOrWhiteSpace(command.commandId)) return ConstructionReplayResult.InvalidCommand;
        if (command.ownerId < 0) return ConstructionReplayResult.InvalidOwner;
        if (command.simulationTick < 0 || command.issuedAtMilliseconds < 0) return ConstructionReplayResult.InvalidTimestamp;
        if (command.quantity <= 0) return ConstructionReplayResult.InvalidCommand;
        if (command.kind != ConstructionReplayCommandKind.AdvanceEra && string.IsNullOrWhiteSpace(command.targetCode)
            && string.IsNullOrWhiteSpace(command.reservationId)) return ConstructionReplayResult.InvalidTarget;
        return ConstructionReplayResult.Accepted;
    }

    private static int Compare(ConstructionReplayCommand left, ConstructionReplayCommand right)
    {
        int tick = left.simulationTick.CompareTo(right.simulationTick);
        if (tick != 0) return tick;
        int time = left.issuedAtMilliseconds.CompareTo(right.issuedAtMilliseconds);
        if (time != 0) return time;
        int priority = right.priority.CompareTo(left.priority);
        if (priority != 0) return priority;
        return string.Compare(left.commandId, right.commandId, StringComparison.OrdinalIgnoreCase);
    }

    private static ConstructionReplayCommand Clone(ConstructionReplayCommand c)
    {
        return new ConstructionReplayCommand {
            simulationTick = c.simulationTick, issuedAtMilliseconds = c.issuedAtMilliseconds,
            priority = c.priority, commandId = c.commandId.Trim(), ownerId = c.ownerId,
            kind = c.kind, targetCode = c.targetCode == null ? string.Empty : c.targetCode.Trim(),
            reservationId = c.reservationId == null ? string.Empty : c.reservationId.Trim(), quantity = c.quantity,
            populationCost = c.populationCost, costs = CopyCosts(c.costs)
        };
    }

    private static List<BuildingMaintenanceCost> CopyCosts(List<BuildingMaintenanceCost> source)
    {
        List<BuildingMaintenanceCost> result = new List<BuildingMaintenanceCost>();
        if (source == null) return result;
        for (int i = 0; i < source.Count; i++)
            if (source[i] != null)
                result.Add(new BuildingMaintenanceCost(source[i].resource, source[i].amount));
        return result;
    }
}

/// <summary>Applies a canonical log to detached RTS progression state.</summary>
public static class ConstructionReplayExecutor
{
    public static ConstructionReplayResult Replay(
        ConstructionReplayLog log,
        ConstructionReplaySnapshot expected,
        out ConstructionReplaySnapshot snapshot)
    {
        snapshot = null;
        if (log == null) return ConstructionReplayResult.InvalidCommand;
        ConstructionReplaySnapshot candidate = new ConstructionReplaySnapshot();
        IReadOnlyList<ConstructionReplayCommand> commands = log.Commands;
        for (int i = 0; i < commands.Count; i++)
        {
            ConstructionReplayCommand command = commands[i];
            ConstructionReplayResult result = Apply(candidate, command);
            if (result != ConstructionReplayResult.Accepted) return result;
        }
        candidate.activeBuildings.Sort(StringComparer.OrdinalIgnoreCase);
        candidate.producedUnits.Sort(StringComparer.OrdinalIgnoreCase);
        candidate.reservations.Sort(StringComparer.OrdinalIgnoreCase);
        candidate.fingerprint = Fingerprint(candidate);
        if (expected != null && expected.fingerprint != null
            && !string.Equals(expected.fingerprint, candidate.fingerprint, StringComparison.Ordinal))
            return ConstructionReplayResult.DivergentReplay;
        snapshot = candidate;
        return ConstructionReplayResult.Accepted;
    }

    public static ConstructionReplayResult Replay(
        ConstructionReplayLog log,
        EconomyLedger stockpile,
        PopulationLedger population,
        ConstructionReplaySnapshot expected,
        out ConstructionReplaySnapshot snapshot)
    {
        snapshot = null;
        if (stockpile == null) return ConstructionReplayResult.InvalidCommand;
        if (population == null) return ConstructionReplayResult.InvalidCommand;
        if (log == null) return ConstructionReplayResult.InvalidCommand;
        ConstructionReplaySnapshot candidate = new ConstructionReplaySnapshot();
        ConstructionReservationLedger reservations = new ConstructionReservationLedger(stockpile, population);
        Dictionary<EconomyResourceType, int> initialResources = CaptureResources(stockpile);
        int initialPopulation = population.current;
        IReadOnlyList<ConstructionReplayCommand> commands = log.Commands;
        for (int i = 0; i < commands.Count; i++)
        {
            ConstructionReplayResult result = Apply(candidate, commands[i], reservations);
            if (result != ConstructionReplayResult.Accepted)
            {
                RestoreResources(stockpile, initialResources);
                population.current = initialPopulation;
                return result;
            }
        }
        candidate.activeBuildings.Sort(StringComparer.OrdinalIgnoreCase);
        candidate.producedUnits.Sort(StringComparer.OrdinalIgnoreCase);
        candidate.reservations.Sort(StringComparer.OrdinalIgnoreCase);
        candidate.fingerprint = Fingerprint(candidate);
        if (expected != null && expected.fingerprint != null && expected.fingerprint != candidate.fingerprint)
        {
            RestoreResources(stockpile, initialResources);
            population.current = initialPopulation;
            return ConstructionReplayResult.DivergentReplay;
        }
        snapshot = candidate;
        return ConstructionReplayResult.Accepted;
    }

    private static Dictionary<EconomyResourceType, int> CaptureResources(EconomyLedger stockpile)
    {
        Dictionary<EconomyResourceType, int> result = new Dictionary<EconomyResourceType, int>();
        foreach (EconomyResourceType resource in Enum.GetValues(typeof(EconomyResourceType)))
            result[resource] = stockpile.Get(resource);
        return result;
    }

    private static void RestoreResources(EconomyLedger stockpile, Dictionary<EconomyResourceType, int> values)
    {
        foreach (KeyValuePair<EconomyResourceType, int> value in values)
            stockpile.Set(value.Key, value.Value);
    }

    private static ConstructionReplayResult Apply(
        ConstructionReplaySnapshot state, ConstructionReplayCommand command)
    {
        string target = command.targetCode == null ? string.Empty : command.targetCode.Trim();
        string reservation = command.reservationId == null ? string.Empty : command.reservationId.Trim();
        switch (command.kind)
        {
            case ConstructionReplayCommandKind.ConstructBuilding:
                if (string.IsNullOrEmpty(target)) return ConstructionReplayResult.InvalidTarget;
                state.activeBuildings.Add(target);
                return ConstructionReplayResult.Accepted;
            case ConstructionReplayCommandKind.ProduceUnit:
                if (string.IsNullOrEmpty(target)) return ConstructionReplayResult.InvalidTarget;
                for (int i = 0; i < command.quantity; i++) state.producedUnits.Add(target);
                return ConstructionReplayResult.Accepted;
            case ConstructionReplayCommandKind.ReserveResources:
                if (string.IsNullOrEmpty(reservation) || state.reservations.Contains(reservation))
                    return ConstructionReplayResult.InvalidTarget;
                state.reservations.Add(reservation);
                return ConstructionReplayResult.Accepted;
            case ConstructionReplayCommandKind.CancelConstruction:
                if (string.IsNullOrEmpty(target)) return ConstructionReplayResult.InvalidTarget;
                state.activeBuildings.Remove(target);
                if (!string.IsNullOrEmpty(reservation)) state.reservations.Remove(reservation);
                return ConstructionReplayResult.Accepted;
            case ConstructionReplayCommandKind.AdvanceEra:
                return AdvanceEra(state, target);
            default:
                return ConstructionReplayResult.InvalidCommand;
        }
    }

    private static ConstructionReplayResult Apply(
        ConstructionReplaySnapshot state, ConstructionReplayCommand command,
        ConstructionReservationLedger reservations)
    {
        if (command == null) return ConstructionReplayResult.InvalidCommand;
        if (command.kind == ConstructionReplayCommandKind.ProduceUnit)
        {
            // Military contract commands carry the same cost/population payload
            // that BuildingProductionQueue reserves at enqueue time.  The
            // detached economic replay must consume that payload too; otherwise
            // AI/replay can produce units for free while the live queue rejects
            // the same order.  Commit the synthetic reservation immediately so
            // it models a production enqueue without polluting construction
            // reservation snapshots.
            if (string.IsNullOrWhiteSpace(command.targetCode))
                return ConstructionReplayResult.InvalidTarget;
            List<BuildingMaintenanceCost> scaledCosts;
            if (!TryScaleCosts(command.costs, command.quantity, out scaledCosts)
                || command.populationCost < 0
                || command.populationCost > int.MaxValue / command.quantity)
                return ConstructionReplayResult.InvalidCommand;
            string productionReservationId = "production:" + command.commandId.Trim();
            ConstructionReservationResult productionResult = reservations.TryReserve(
                new ConstructionReservationRequest {
                    reservationId = productionReservationId,
                    priority = command.priority,
                    population = command.populationCost * command.quantity,
                    costs = scaledCosts
                });
            if (productionResult != ConstructionReservationResult.Accepted)
                return ConstructionReplayResult.DivergentReplay;
            if (reservations.Commit(productionReservationId) != ConstructionReservationResult.Accepted)
                return ConstructionReplayResult.DivergentReplay;
        }
        else if (command.kind == ConstructionReplayCommandKind.ReserveResources)
        {
            ConstructionReservationResult result = reservations.TryReserve(new ConstructionReservationRequest {
                reservationId = command.reservationId,
                priority = command.priority,
                population = command.populationCost,
                costs = command.costs
            });
            if (result != ConstructionReservationResult.Accepted) return ConstructionReplayResult.DivergentReplay;
        }
        else if (command.kind == ConstructionReplayCommandKind.CancelConstruction
            && !string.IsNullOrWhiteSpace(command.reservationId))
        {
            ConstructionReservationResult result = reservations.TryCancel(command.reservationId);
            if (result != ConstructionReservationResult.Accepted && result != ConstructionReservationResult.NotFound)
                return ConstructionReplayResult.DivergentReplay;
        }
        return Apply(state, command);
    }

    private static bool TryScaleCosts(
        List<BuildingMaintenanceCost> costs,
        int quantity,
        out List<BuildingMaintenanceCost> scaled)
    {
        scaled = new List<BuildingMaintenanceCost>();
        if (quantity <= 0) return false;
        if (costs == null) return true;
        for (int i = 0; i < costs.Count; i++)
        {
            BuildingMaintenanceCost cost = costs[i];
            if (cost == null || cost.amount < 0) return false;
            long amount = (long)cost.amount * quantity;
            if (amount > int.MaxValue) return false;
            scaled.Add(new BuildingMaintenanceCost(cost.resource, (int)amount));
        }
        return true;
    }

    private static ConstructionReplayResult AdvanceEra(ConstructionReplaySnapshot state, string target)
    {
        if (string.IsNullOrEmpty(target) || !target.StartsWith("age:", StringComparison.OrdinalIgnoreCase))
            return ConstructionReplayResult.InvalidTarget;
        BuildingEra next;
        if (!Enum.TryParse(target.Substring(4), true, out next)
            || (int)next != (int)state.era + 1)
            return ConstructionReplayResult.InvalidCommand;
        state.era = next;
        return ConstructionReplayResult.Accepted;
    }

    private static string Fingerprint(ConstructionReplaySnapshot state)
    {
        string canonical = ((int)state.era) + ":" + string.Join(",", state.activeBuildings.ToArray())
            + ":" + string.Join(",", state.producedUnits.ToArray())
            + ":" + string.Join(",", state.reservations.ToArray());
        unchecked
        {
            ulong hash = 1469598103934665603UL;
            for (int i = 0; i < canonical.Length; i++) { hash ^= canonical[i]; hash *= 1099511628211UL; }
            return hash.ToString("X16");
        }
    }
}

/// <summary>Process-local bridge used by GameData without exposing the log internals.</summary>
public static class ConstructionReplayRuntime
{
    private static ConstructionReplayLog _log = new ConstructionReplayLog();
    private static ConstructionReplaySnapshot _state;
    public static ConstructionReplayResult LastRestoreResult { get; private set; }
    public static ConstructionReplaySnapshot State { get { return _state; } }

    public static List<ConstructionReplayCommand> ExportCommands()
    {
        List<ConstructionReplayCommand> result = new List<ConstructionReplayCommand>();
        for (int i = 0; i < _log.Commands.Count; i++) result.Add(_log.Commands[i]);
        return result;
    }

    public static ConstructionReplayResult Restore(IList<ConstructionReplayCommand> commands, ConstructionReplaySnapshot expected)
    {
        _log = new ConstructionReplayLog();
        if (commands != null)
            for (int i = 0; i < commands.Count; i++)
            {
                ConstructionReplayResult append = _log.TryAppend(commands[i]);
                if (append != ConstructionReplayResult.Accepted)
                {
                    LastRestoreResult = append == ConstructionReplayResult.DuplicateCommandId
                        ? ConstructionReplayResult.DuplicateCommandId
                        : ConstructionReplayResult.InvalidCommand;
                    _state = null;
                    return LastRestoreResult;
                }
            }
        ConstructionReplaySnapshot restored = null;
        LastRestoreResult = ConstructionReplayExecutor.Replay(_log, expected, out restored);
        if (LastRestoreResult != ConstructionReplayResult.Accepted)
        {
            _state = null;
            return LastRestoreResult;
        }
        _state = restored;
        return LastRestoreResult;
    }
}
