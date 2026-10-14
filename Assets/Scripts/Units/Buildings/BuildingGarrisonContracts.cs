using System;
using System.Collections.Generic;

/// <summary>Stable result codes for entering and leaving a building garrison.</summary>
public enum BuildingGarrisonResult
{
    Accepted,
    InvalidContract,
    BuildingNotOperational,
    InvalidUnit,
    WrongOwner,
    UnitNotAllowed,
    CapacityFull,
    AlreadyGarrisoned,
    NotGarrisoned
}

/// <summary>
/// Pure validation rules for garrison operations. It has no scene, UI or
/// economy side effects, making save/replay and editor tests deterministic.
/// </summary>
public static class BuildingGarrisonRules
{
    public static BuildingGarrisonResult ValidateEntry(
        BuildingContentSpec spec,
        int buildingOwner,
        bool buildingOperational,
        int unitOwner,
        string unitCode,
        int occupiedSlots)
    {
        if (spec == null || !spec.supportsGarrison || spec.garrisonCapacity <= 0)
            return BuildingGarrisonResult.InvalidContract;
        if (!buildingOperational)
            return BuildingGarrisonResult.BuildingNotOperational;
        if (string.IsNullOrWhiteSpace(unitCode))
            return BuildingGarrisonResult.InvalidUnit;
        if (buildingOwner != unitOwner)
            return BuildingGarrisonResult.WrongOwner;
        if (occupiedSlots < 0 || occupiedSlots >= spec.garrisonCapacity)
            return BuildingGarrisonResult.CapacityFull;
        if (!AllowsUnit(spec.allowedUnitCodes, unitCode))
            return BuildingGarrisonResult.UnitNotAllowed;
        return BuildingGarrisonResult.Accepted;
    }

    public static BuildingGarrisonResult ValidateExit(
        BuildingContentSpec spec,
        int buildingOwner,
        int unitOwner,
        string unitCode,
        bool isGarrisoned)
    {
        if (spec == null || !spec.supportsGarrison || spec.garrisonCapacity <= 0)
            return BuildingGarrisonResult.InvalidContract;
        if (string.IsNullOrWhiteSpace(unitCode))
            return BuildingGarrisonResult.InvalidUnit;
        if (buildingOwner != unitOwner)
            return BuildingGarrisonResult.WrongOwner;
        if (!isGarrisoned)
            return BuildingGarrisonResult.NotGarrisoned;
        return BuildingGarrisonResult.Accepted;
    }

    public static bool AllowsUnit(IList<string> allowedUnitCodes, string unitCode)
    {
        if (string.IsNullOrWhiteSpace(unitCode)) return false;
        if (allowedUnitCodes == null || allowedUnitCodes.Count == 0) return true;
        for (int i = 0; i < allowedUnitCodes.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(allowedUnitCodes[i])
                && string.Equals(allowedUnitCodes[i].Trim(), unitCode.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
