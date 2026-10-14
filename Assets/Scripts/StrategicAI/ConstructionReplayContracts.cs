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
                    .Append(c.quantity).Append(';');
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
            reservationId = c.reservationId == null ? string.Empty : c.reservationId.Trim(), quantity = c.quantity
        };
    }
}
