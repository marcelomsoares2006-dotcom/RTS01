using UnityEngine;
using UnityEngine.SceneManagement;

public static class TerrainRuntimeRepairBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // Handles additive maps and sessions with domain reload disabled.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        => TerrainRuntimeRepair.RepairAllTerrains();
}
