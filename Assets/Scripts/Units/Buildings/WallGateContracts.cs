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
    UnsupportedRole
}

public enum WallGateOpeningResult
{
    Allowed,
    PlannedOnly,
    NotAGate,
    AlreadyInRequestedState,
    Blocked,
    InvalidOwner
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
    public bool footprintClear;
    public bool terrainSupported;
    public bool endpointAvailable;
    public bool requiresConnection;
    public bool occupied;
    public float terrainSlope;
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
        if (!observation.footprintClear)
            return WallGatePlacementResult.InvalidFootprint;
        if (observation.occupied)
            return WallGatePlacementResult.Occupied;
        if (!observation.terrainSupported || observation.terrainSlope > contract.maximumTerrainSlope)
            return WallGatePlacementResult.InvalidTerrain;
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
        if (!first.CanConnectTo(second))
            return string.Equals(first.familyCode, second.familyCode, StringComparison.Ordinal)
                ? WallGateConnectionResult.UnsupportedRole
                : WallGateConnectionResult.IncompatibleFamily;
        if (!firstEndpointAvailable || !secondEndpointAvailable)
            return WallGateConnectionResult.OccupiedEndpoint;
        float allowedDistance = Mathf.Min(first.connectionRadius, second.connectionRadius);
        if (endpointDistance < 0f || endpointDistance > allowedDistance)
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
