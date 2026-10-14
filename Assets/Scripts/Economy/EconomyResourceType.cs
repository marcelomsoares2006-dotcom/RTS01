using System;

/// <summary>Resource contract for the new economy layer. Legacy InGameResource remains unchanged.</summary>
public enum EconomyResourceType { Food, Wood, Gold, Stone }

public static class EconomyResourceTypeExtensions
{
    public static bool TryGetLegacyResource(this EconomyResourceType resource, out InGameResource legacyResource)
    {
        switch (resource)
        {
            case EconomyResourceType.Wood: legacyResource = InGameResource.Wood; return true;
            case EconomyResourceType.Gold: legacyResource = InGameResource.Gold; return true;
            case EconomyResourceType.Stone: legacyResource = InGameResource.Stone; return true;
            default: legacyResource = default(InGameResource); return false;
        }
    }
}
