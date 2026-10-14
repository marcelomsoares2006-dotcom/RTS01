using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Why a resource delivery was accepted or rejected.</summary>
public enum ResourceDropoffResult
{
    Accepted,
    MissingCatalogContract,
    DropoffNotOperational,
    WrongOwner,
    OutOfRange,
    UnsupportedResource,
    InvalidAmount,
    MissingLedger,
    LegacyResourceUnsupported
}

/// <summary>
/// Deterministic delivery boundary for a resource dropoff or Town Center.
/// It intentionally contains no prefab, scene lookup or worker movement code.
/// The worker controller can ask this boundary to deliver only after reaching
/// the building; the same rules then apply to simulation, save replay and AI.
/// </summary>
[Serializable]
public sealed class ResourceDropoffContract
{
    private readonly int _ownerId;
    private readonly Vector3 _position;
    private readonly float _accessRadius;
    private readonly bool _operational;
    private readonly HashSet<EconomyResourceType> _acceptedResources;

    public int OwnerId { get { return _ownerId; } }
    public Vector3 Position { get { return _position; } }
    public float AccessRadius { get { return _accessRadius; } }
    public bool IsOperational { get { return _operational; } }
    public IEnumerable<EconomyResourceType> AcceptedResources { get { return _acceptedResources; } }

    /// <summary>
    /// Builds a contract from the content catalog. A non-dropoff catalog entry
    /// is retained as an invalid contract so callers receive a deterministic
    /// rejection instead of silently routing resources elsewhere.
    /// </summary>
    public ResourceDropoffContract(BuildingContentSpec catalog,
        int ownerId, Vector3 position, float accessRadius, bool operational = true)
    {
        _ownerId = ownerId;
        _position = position;
        _accessRadius = Mathf.Max(0f, accessRadius);
        _operational = operational;
        _acceptedResources = new HashSet<EconomyResourceType>();

        if (catalog == null || !catalog.supportsDropoff || catalog.dropoffResources == null)
            return;

        for (int i = 0; i < catalog.dropoffResources.Count; i++)
            _acceptedResources.Add(catalog.dropoffResources[i]);
    }

    public ResourceDropoffResult Validate(EconomyResourceType resource,
        int workerOwnerId, Vector3 workerPosition, int amount, EconomyLedger ledger)
    {
        if (_acceptedResources.Count == 0) return ResourceDropoffResult.MissingCatalogContract;
        if (!_operational) return ResourceDropoffResult.DropoffNotOperational;
        if (workerOwnerId != _ownerId) return ResourceDropoffResult.WrongOwner;
        if (!IsInRange(workerPosition)) return ResourceDropoffResult.OutOfRange;
        if (!_acceptedResources.Contains(resource)) return ResourceDropoffResult.UnsupportedResource;
        if (amount <= 0) return ResourceDropoffResult.InvalidAmount;
        if (ledger == null) return ResourceDropoffResult.MissingLedger;
        return ResourceDropoffResult.Accepted;
    }

    public bool TryDeposit(EconomyResourceType resource,
        int workerOwnerId, Vector3 workerPosition, int amount, EconomyLedger ledger,
        out ResourceDropoffResult result)
    {
        result = Validate(resource, workerOwnerId, workerPosition, amount, ledger);
        if (result != ResourceDropoffResult.Accepted) return false;
        ledger.Add(resource, amount);
        return true;
    }

    /// <summary>
    /// Routes the legacy three-resource enum through the same catalog gate.
    /// Food has no legacy representation and is rejected explicitly instead
    /// of being coerced into Wood or another resource.
    /// </summary>
    public bool TryDeposit(InGameResource resource,
        int workerOwnerId, Vector3 workerPosition, int amount, EconomyLedger ledger,
        out ResourceDropoffResult result)
    {
        EconomyResourceType mapped;
        if (!TryMapLegacy(resource, out mapped))
        {
            result = ResourceDropoffResult.LegacyResourceUnsupported;
            return false;
        }
        return TryDeposit(mapped, workerOwnerId, workerPosition, amount, ledger, out result);
    }

    public bool IsInRange(Vector3 workerPosition)
    {
        Vector3 delta = workerPosition - _position;
        delta.y = 0f;
        return delta.sqrMagnitude <= _accessRadius * _accessRadius;
    }

    private static bool TryMapLegacy(InGameResource resource, out EconomyResourceType mapped)
    {
        switch (resource)
        {
            case InGameResource.Gold: mapped = EconomyResourceType.Gold; return true;
            case InGameResource.Wood: mapped = EconomyResourceType.Wood; return true;
            case InGameResource.Stone: mapped = EconomyResourceType.Stone; return true;
            default: mapped = default(EconomyResourceType); return false;
        }
    }
}
