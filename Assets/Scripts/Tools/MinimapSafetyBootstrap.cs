using UnityEngine;
using UnityEngine.SceneManagement;
public static class MinimapSafetyBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
{
        DisableMinimap();
        SceneManager.sceneLoaded += OnSceneLoaded;
}
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
{
        DisableMinimap();
}
    private static void DisableMinimap()
{
        foreach (MinimapManager minimap in Object.FindObjectsOfType<MinimapManager>())
{
            if (minimap != null)
                minimap.enabled = false;
}
}
}
