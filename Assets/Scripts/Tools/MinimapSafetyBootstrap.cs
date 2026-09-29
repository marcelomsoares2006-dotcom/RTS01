using UnityEngine;
public static class MinimapSafetyBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init() => MinimapManager.IS_ENABLED = true;
}
