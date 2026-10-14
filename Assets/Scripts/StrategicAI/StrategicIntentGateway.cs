using System;
using UnityEngine;

public enum StrategicIntentRejectionReason
{
    None,
    InvalidOwner,
    MissingIntent,
    InvalidQuantity,
    TargetOwnedByAnotherPlayer,
    ExecutorRejected,
    NoExecutor
}

public sealed class StrategicIntentResult
{
    public bool accepted;
    public StrategicIntentRejectionReason rejectionReason;
    public string message;

    public static StrategicIntentResult Accepted(string message = "Intent accepted.")
    {
        return new StrategicIntentResult { accepted = true, rejectionReason = StrategicIntentRejectionReason.None, message = message };
    }

    public static StrategicIntentResult Rejected(StrategicIntentRejectionReason reason, string message)
    {
        return new StrategicIntentResult { accepted = false, rejectionReason = reason, message = message };
    }
}

/// <summary>Execution seam for gameplay systems; this adapter does not implement a planner.</summary>
public interface IStrategicIntentExecutor
{
    bool TryExecute(int ownerId, StrategicActionIntent intent, out string failureReason);
}

/// <summary>
/// Validates ownership at the last possible boundary before a strategic command
/// reaches gameplay. Unsupported actions are rejected explicitly by the executor.
/// </summary>
public sealed class StrategicIntentGateway
{
    private readonly IStrategicRuntimeSnapshotSource _snapshotSource;
    private readonly IStrategicIntentExecutor _executor;

    public StrategicIntentGateway(IStrategicRuntimeSnapshotSource snapshotSource, IStrategicIntentExecutor executor = null)
    {
        _snapshotSource = snapshotSource ?? throw new ArgumentNullException(nameof(snapshotSource));
        _executor = executor;
    }

    public StrategicIntentResult TryExecute(int ownerId, StrategicActionIntent intent)
    {
        if (ownerId < 0)
            return StrategicIntentResult.Rejected(StrategicIntentRejectionReason.InvalidOwner, "Owner id must be non-negative.");
        if (intent == null)
            return StrategicIntentResult.Rejected(StrategicIntentRejectionReason.MissingIntent, "Intent is required.");
        if (intent.quantity < 1)
            return StrategicIntentResult.Rejected(StrategicIntentRejectionReason.InvalidQuantity, "Intent quantity must be positive.");

        StrategicPlanningState snapshot = _snapshotSource.Capture(ownerId);
        if (snapshot == null)
            return StrategicIntentResult.Rejected(StrategicIntentRejectionReason.ExecutorRejected, "Runtime snapshot was unavailable.");

        string targetCode = intent.targetCode == null ? string.Empty : intent.targetCode.Trim();
        // Most targetCode values are definitions (a building to construct, a
        // unit to produce, a resource, or a technology). DefendArea is the
        // current contract action whose code identifies a live world entity.
        if (intent.action == StrategicActionKind.DefendArea
            && targetCode.Length > 0
            && HasForeignTarget(snapshot, targetCode, ownerId))
            return StrategicIntentResult.Rejected(
                StrategicIntentRejectionReason.TargetOwnedByAnotherPlayer,
                "The requested target belongs to another player.");

        if (_executor == null)
            return StrategicIntentResult.Rejected(StrategicIntentRejectionReason.NoExecutor, "No gameplay executor is registered for this intent.");

        string failureReason;
        if (!_executor.TryExecute(ownerId, intent, out failureReason))
            return StrategicIntentResult.Rejected(
                StrategicIntentRejectionReason.ExecutorRejected,
                string.IsNullOrWhiteSpace(failureReason) ? "The gameplay executor rejected the intent." : failureReason);

        return StrategicIntentResult.Accepted();
    }

    private static bool HasForeignTarget(StrategicPlanningState snapshot, string targetCode, int ownerId)
    {
        if (snapshot.units != null)
            foreach (StrategicUnitObservation unit in snapshot.units)
                if (unit != null && string.Equals(unit.code, targetCode, StringComparison.OrdinalIgnoreCase))
                {
                    if (unit.ownerId != ownerId) return true;
                }
        if (snapshot.buildings != null)
            foreach (StrategicBuildingObservation building in snapshot.buildings)
                if (building != null && string.Equals(building.code, targetCode, StringComparison.OrdinalIgnoreCase))
                {
                    if (building.ownerId != ownerId) return true;
                }
        return false;
    }
}
