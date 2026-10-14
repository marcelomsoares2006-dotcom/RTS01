using System;
using System.Collections.Generic;

public enum ConstructionLockstepResult
{
    Accepted,
    InvalidCommand,
    InvalidOwner,
    MissingCommandId,
    DuplicateCommandId,
    StaleTick,
    FingerprintMismatch
}

[Serializable]
public sealed class ConstructionLockstepConfirmation
{
    public long confirmedThroughTick = -1;
    public int ownerId;
    public string commandId;
    public bool accepted;
    public string fingerprint;
}

/// <summary>
/// Deterministic lockstep ingress for construction/production commands. It
/// rejects late or duplicate inputs before they reach the replay executor.
/// </summary>
public sealed class ConstructionLockstepCoordinator
{
    private readonly ConstructionReplayLog _log = new ConstructionReplayLog();
    private readonly HashSet<string> _commandIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private long _lastTick = -1;
    private readonly int _expectedOwnerId;

    public ConstructionLockstepCoordinator() : this(-1) { }
    public ConstructionLockstepCoordinator(int expectedOwnerId)
    {
        if (expectedOwnerId < -1) throw new ArgumentOutOfRangeException(nameof(expectedOwnerId));
        _expectedOwnerId = expectedOwnerId;
    }

    public long LastAcceptedTick { get { return _lastTick; } }
    public string Fingerprint { get { return ConstructionReplayLog.ComputeFingerprint(_log.Commands); } }
    public IReadOnlyList<ConstructionReplayCommand> Commands { get { return _log.Commands; } }

    public ConstructionLockstepResult Submit(
        ConstructionReplayCommand command,
        string expectedFingerprint,
        out ConstructionLockstepConfirmation confirmation)
    {
        return SubmitCore(command, expectedFingerprint, _expectedOwnerId, out confirmation);
    }

    public ConstructionLockstepResult Submit(
        int expectedOwnerId,
        ConstructionReplayCommand command,
        string expectedFingerprint,
        out ConstructionLockstepConfirmation confirmation)
    {
        return SubmitCore(command, expectedFingerprint, expectedOwnerId, out confirmation);
    }

    private ConstructionLockstepResult SubmitCore(
        ConstructionReplayCommand command,
        string expectedFingerprint,
        int expectedOwnerId,
        out ConstructionLockstepConfirmation confirmation)
    {
        confirmation = new ConstructionLockstepConfirmation {
            ownerId = command == null ? -1 : command.ownerId,
            commandId = command == null ? string.Empty : command.commandId,
            fingerprint = Fingerprint
        };
        if (command == null) return ConstructionLockstepResult.InvalidCommand;
        if (_expectedOwnerId >= 0 && expectedOwnerId != _expectedOwnerId)
            return ConstructionLockstepResult.InvalidOwner;
        if (command.ownerId < 0 || (expectedOwnerId >= 0 && command.ownerId != expectedOwnerId)) return ConstructionLockstepResult.InvalidOwner;
        if (string.IsNullOrWhiteSpace(command.commandId)) return ConstructionLockstepResult.MissingCommandId;
        if (!_commandIds.Contains(command.commandId.Trim()) && command.simulationTick < _lastTick)
            return ConstructionLockstepResult.StaleTick;
        if (_commandIds.Contains(command.commandId.Trim())) return ConstructionLockstepResult.DuplicateCommandId;
        if (expectedFingerprint != null && !string.Equals(expectedFingerprint, Fingerprint, StringComparison.Ordinal))
            return ConstructionLockstepResult.FingerprintMismatch;
        ConstructionReplayResult append = _log.TryAppend(command);
        if (append == ConstructionReplayResult.DuplicateCommandId)
            return ConstructionLockstepResult.DuplicateCommandId;
        if (append != ConstructionReplayResult.Accepted)
            return ConstructionLockstepResult.InvalidCommand;
        _commandIds.Add(command.commandId.Trim());
        if (command.simulationTick > _lastTick) _lastTick = command.simulationTick;
        confirmation.confirmedThroughTick = _lastTick;
        confirmation.accepted = true;
        confirmation.fingerprint = Fingerprint;
        return ConstructionLockstepResult.Accepted;
    }
}
