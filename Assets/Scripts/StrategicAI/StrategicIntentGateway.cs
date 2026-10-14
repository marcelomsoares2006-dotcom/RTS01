using System;
using UnityEngine;

public enum StrategicIntentRejectionReason
{
    None,
    InvalidOwner,
    MissingIntent,
    InvalidQuantity,
    TargetOwnedByAnotherPlayer,
    AmbiguousTarget,
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

        // Most targetCode values are definitions (a building to construct, a
        // unit to produce, a resource, or a technology). DefendArea is the
        // action whose selector must resolve to one live world entity.
        if (intent.action == StrategicActionKind.DefendArea)
        {
            StrategicTargetResolution target = ResolveTarget(snapshot, intent);
            if (target.ambiguous)
                return StrategicIntentResult.Rejected(
                    StrategicIntentRejectionReason.AmbiguousTarget,
                    "The requested target is ambiguous; use targetEntityId or owner+position.");
            if (target.found && target.ownerId != ownerId)
                return StrategicIntentResult.Rejected(
                    StrategicIntentRejectionReason.TargetOwnedByAnotherPlayer,
                    "The requested target belongs to another player.");
        }

        if (_executor == null)
            return StrategicIntentResult.Rejected(StrategicIntentRejectionReason.NoExecutor, "No gameplay executor is registered for this intent.");

        string failureReason;
        if (!_executor.TryExecute(ownerId, intent, out failureReason))
            return StrategicIntentResult.Rejected(
                StrategicIntentRejectionReason.ExecutorRejected,
                string.IsNullOrWhiteSpace(failureReason) ? "The gameplay executor rejected the intent." : failureReason);

        return StrategicIntentResult.Accepted();
    }

    private struct StrategicTargetResolution
    {
        public bool found;
        public bool ambiguous;
        public int ownerId;
    }

    private static StrategicTargetResolution ResolveTarget(StrategicPlanningState snapshot, StrategicActionIntent intent)
    {
        string entityId = Normalize(intent.targetEntityId);
        string targetCode = Normalize(intent.targetCode);
        bool hasPosition = intent.hasTargetPosition || intent.targetPosition != Vector3.zero;
        int count = 0;
        int owner = 0;

        if (snapshot.units != null)
            for (int i = 0; i < snapshot.units.Count; i++)
            {
                StrategicUnitObservation unit = snapshot.units[i];
                if (unit == null || !Matches(unit.entityId, unit.code, unit.position, entityId, targetCode, intent.targetPosition, hasPosition)) continue;
                count++; owner = unit.ownerId;
            }
        if (snapshot.buildings != null)
            for (int i = 0; i < snapshot.buildings.Count; i++)
            {
                StrategicBuildingObservation building = snapshot.buildings[i];
                if (building == null || !Matches(building.entityId, building.code, building.position, entityId, targetCode, intent.targetPosition, hasPosition)) continue;
                count++; owner = building.ownerId;
            }

        // An unknown selector remains valid for old abstract intents. Once a
        // live code resolves to more than one entity, accepting it would make
        // the command nondeterministic, so require a stable id or position.
        return new StrategicTargetResolution { found = count == 1, ambiguous = count > 1, ownerId = owner };
    }

    private static bool Matches(string entityId, string code, Vector3 position, string requestedEntityId,
        string requestedCode, Vector3 requestedPosition, bool hasPosition)
    {
        if (requestedEntityId.Length > 0)
            return string.Equals(entityId, requestedEntityId, StringComparison.OrdinalIgnoreCase);
        if (requestedCode.Length == 0 || !string.Equals(code, requestedCode, StringComparison.OrdinalIgnoreCase))
            return false;
        return !hasPosition || (position - requestedPosition).sqrMagnitude <= 0.25f;
    }

    private static string Normalize(string value)
    {
        return value == null ? string.Empty : value.Trim();
    }
}
