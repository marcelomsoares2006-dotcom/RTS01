using System;
using UnityEngine;

/// <summary>Architectural role for a modular wall or gate piece.</summary>
public enum WallGatePieceKind
{
    Segment,
    Corner,
    End,
    Gate
}

/// <summary>Stable lifecycle state for a gate opening.</summary>
public enum WallGateOpeningState
{
    Closed,
    Opening,
    Open,
    Closing
}

public enum WallGateLifecycleState
{
    Blueprint,
    UnderConstruction,
    Operational,
    Damaged,
    Destroyed,
    Ruined
}

/// <summary>Explicit result codes used by placement and connection UI.</summary>
public enum WallGatePlacementResult
{
    Allowed,
    PlannedOnly,
    MissingCode,
    MissingFamily,
    MissingAsset,
    InvalidFootprint,
    InvalidTerrain,
    InvalidOwner,
    InvalidSpacing,
    InvalidLifecycle,
    Occupied,
    RequiresConnection
}

public enum WallGateConnectionResult
{
    Connected,
    PlannedOnly,
    MissingCode,
    IncompatibleFamily,
    InvalidDistance,
    OccupiedEndpoint,
    UnsupportedRole,
    InvalidOwner,
    InvalidLifecycle
}

public enum WallGateOpeningResult
{
    Allowed,
    PlannedOnly,
    NotAGate,
    AlreadyInRequestedState,
    Blocked,
    InvalidOwner,
    InvalidLifecycle
}

/// <summary>
/// Declarative specification for one modular wall/gate family. This is a
/// contract only: no asset is created and no runtime component discovers or
/// registers these entries until a real prefab/model has passed the content
/// gate.
/// </summary>
[Serializable]
public sealed class WallGateContract
{
    [Tooltip("Stable code used by content, save data and future placement UI.")]
    public string code;

    [Tooltip("Pieces with the same family code may connect to each other.")]
    public string familyCode;

    public WallGatePieceKind pieceKind = WallGatePieceKind.Segment;

    [Min(0.01f)]
    public float connectionRadius = 0.35f;

    [Min(0.01f)]
    public float segmentLength = 2f;

    [Range(0f, 90f)]
    public float maximumTerrainSlope = 12f;

    [Min(0f)]
    public float gateClearance = 1f;

    [Tooltip("False keeps a design entry outside runtime discovery.")]
    public bool runtimeEnabled;

    [Tooltip("True only after prefab, model, colliders and construction states exist.")]
    public bool assetsReady;
    public int ownerId = -1;
    public WallGateLifecycleState lifecycle = WallGateLifecycleState.Operational;
    [Min(0f)] public float minimumSpacing;

    [Tooltip("Expected prefab path for audit/documentation; not loaded by this contract.")]
    public string expectedPrefabPath;

    [Tooltip("Expected model path for audit/documentation; not loaded by this contract.")]
    public string expectedModelPath;

    public bool IsRuntimeReady
    {
        get { return runtimeEnabled && assetsReady; }
    }

    public bool IsValidIdentity
    {
        get { return !string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(familyCode); }
    }

    public bool CanConnectTo(WallGateContract other)
    {
        if (other == null || !IsValidIdentity || !other.IsValidIdentity)
            return false;
        if (!string.Equals(familyCode, other.familyCode, StringComparison.Ordinal))
            return false;
        return pieceKind != WallGatePieceKind.End && other.pieceKind != WallGatePieceKind.End;
    }
}

/// <summary>Physics-free observation captured by a future placement adapter.</summary>
public struct WallGatePlacementObservation
{
    public WallGatePlacementObservation(int ownerId = -1)
    {
        footprintClear = false;
        terrainSupported = false;
        endpointAvailable = false;
        requiresConnection = false;
        occupied = false;
        terrainSlope = 0f;
        this.ownerId = ownerId;
        spacingAvailable = false;
        nearestPieceDistance = 0f;
    }

    public bool footprintClear;
    public bool terrainSupported;
    public bool endpointAvailable;
    public bool requiresConnection;
    public bool occupied;
    public float terrainSlope;
    public int ownerId;
    public bool spacingAvailable;
    public float nearestPieceDistance;
}

/// <summary>Pure rules for modular defensive construction.</summary>
public static class WallGateContractRules
{
    public static WallGatePlacementResult EvaluatePlacement(
        WallGateContract contract,
        WallGatePlacementObservation observation)
    {
        if (contract == null || string.IsNullOrWhiteSpace(contract.code))
            return WallGatePlacementResult.MissingCode;
        if (string.IsNullOrWhiteSpace(contract.familyCode))
            return WallGatePlacementResult.MissingFamily;
        if (!contract.IsRuntimeReady)
            return WallGatePlacementResult.PlannedOnly;
        if (contract.lifecycle == WallGateLifecycleState.Destroyed
            || contract.lifecycle == WallGateLifecycleState.Ruined)
            return WallGatePlacementResult.InvalidLifecycle;
        if (contract.ownerId >= 0 && observation.ownerId != contract.ownerId)
            return WallGatePlacementResult.InvalidOwner;
        if (!observation.footprintClear)
            return WallGatePlacementResult.InvalidFootprint;
        if (observation.occupied)
            return WallGatePlacementResult.Occupied;
        if (!observation.terrainSupported || float.IsNaN(observation.terrainSlope)
            || float.IsInfinity(observation.terrainSlope) || observation.terrainSlope < 0f
            || observation.terrainSlope > contract.maximumTerrainSlope)
            return WallGatePlacementResult.InvalidTerrain;
        if (observation.spacingAvailable && (float.IsNaN(observation.nearestPieceDistance)
            || float.IsInfinity(observation.nearestPieceDistance)
            || observation.nearestPieceDistance < contract.minimumSpacing))
            return WallGatePlacementResult.InvalidSpacing;
        if (contract.minimumSpacing > 0f && !observation.spacingAvailable)
            return WallGatePlacementResult.InvalidSpacing;
        if (observation.requiresConnection && !observation.endpointAvailable)
            return WallGatePlacementResult.RequiresConnection;
        return WallGatePlacementResult.Allowed;
    }

    public static WallGateConnectionResult EvaluateConnection(
        WallGateContract first,
        WallGateContract second,
        float endpointDistance,
        bool firstEndpointAvailable,
        bool secondEndpointAvailable)
    {
        if (first == null || second == null || string.IsNullOrWhiteSpace(first.code)
            || string.IsNullOrWhiteSpace(second.code))
            return WallGateConnectionResult.MissingCode;
        if (!first.IsRuntimeReady || !second.IsRuntimeReady)
            return WallGateConnectionResult.PlannedOnly;
        if (first.lifecycle == WallGateLifecycleState.Destroyed
            || first.lifecycle == WallGateLifecycleState.Ruined
            || second.lifecycle == WallGateLifecycleState.Destroyed
            || second.lifecycle == WallGateLifecycleState.Ruined)
            return WallGateConnectionResult.InvalidLifecycle;
        if (first.ownerId >= 0 && second.ownerId >= 0 && first.ownerId != second.ownerId)
            return WallGateConnectionResult.InvalidOwner;
        if (!first.CanConnectTo(second))
            return string.Equals(first.familyCode, second.familyCode, StringComparison.Ordinal)
                ? WallGateConnectionResult.UnsupportedRole
                : WallGateConnectionResult.IncompatibleFamily;
        if (!firstEndpointAvailable || !secondEndpointAvailable)
            return WallGateConnectionResult.OccupiedEndpoint;
        float allowedDistance = Mathf.Min(first.connectionRadius, second.connectionRadius);
        if (float.IsNaN(endpointDistance) || float.IsInfinity(endpointDistance)
            || endpointDistance < 0f || endpointDistance > allowedDistance)
            return WallGateConnectionResult.InvalidDistance;
        return WallGateConnectionResult.Connected;
    }

    public static WallGateOpeningResult EvaluateOpening(
        WallGateContract contract,
        WallGateOpeningState currentState,
        WallGateOpeningState requestedState,
        bool sameOwner,
        bool clearanceAvailable)
    {
        if (contract == null || contract.pieceKind != WallGatePieceKind.Gate)
            return WallGateOpeningResult.NotAGate;
        if (!contract.IsRuntimeReady)
            return WallGateOpeningResult.PlannedOnly;
        if (contract.lifecycle == WallGateLifecycleState.Destroyed
            || contract.lifecycle == WallGateLifecycleState.Ruined)
            return WallGateOpeningResult.InvalidLifecycle;
        if (!sameOwner)
            return WallGateOpeningResult.InvalidOwner;
        if (currentState == requestedState)
            return WallGateOpeningResult.AlreadyInRequestedState;
        if (requestedState == WallGateOpeningState.Open && !clearanceAvailable)
            return WallGateOpeningResult.Blocked;
        if (requestedState != WallGateOpeningState.Open && requestedState != WallGateOpeningState.Closed)
            return WallGateOpeningResult.Blocked;
        return WallGateOpeningResult.Allowed;
    }
}
