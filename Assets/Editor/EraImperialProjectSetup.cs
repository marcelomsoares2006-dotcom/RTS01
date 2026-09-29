using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class EraImperialProjectSetup
{
    static EraImperialProjectSetup()
    {
        EditorApplication.delayCall += () =>
        {
            // Core owns the lifecycle and additive loading of the menu and maps.
            // Play remains safe even when the designer is currently viewing Map1/Map2.
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Core.unity");
        };
    }

    [MenuItem("Tools/Era Imperial/Open Core Scene")]
    private static void OpenCore()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene("Assets/Scenes/Core.unity");
    }
}
