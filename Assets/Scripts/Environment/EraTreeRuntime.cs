using EraImperial.Units.Advanced;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Changes only a global shader uniform on era transitions. Terrain tree instances,
/// prototype indices and the serialized TerrainData are never read back or rewritten.
/// </summary>
public static class EraTreeRuntime
{
    private static readonly int EraProperty = Shader.PropertyToID("_EITreeEra");
    private const float MaximumTreeDistance = 320f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Shader.SetGlobalFloat(EraProperty, 0f);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        AdvancedEraState.Changed -= ApplyEra;
        AdvancedEraState.Changed += ApplyEra;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyEra(AdvancedEraState.Current);
        ConfigureActiveTerrains();
    }

    private static void ApplyEra(ImperialEra era)
    {
        Shader.SetGlobalFloat(EraProperty, Mathf.Clamp((int)era, 0, 3));
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ConfigureActiveTerrains();
    }

    private static void ConfigureActiveTerrains()
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null || terrain.terrainData.treeInstanceCount == 0)
                continue;

            // Component-only Play Mode setting. Never assign or mutate terrain.terrainData.
            terrain.treeDistance = Mathf.Min(terrain.treeDistance, MaximumTreeDistance);
        }
    }
}
