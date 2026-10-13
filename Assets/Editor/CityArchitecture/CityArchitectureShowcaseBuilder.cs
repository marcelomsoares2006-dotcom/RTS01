using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CityArchitectureShowcaseBuilder
{
    private const string ScenePath = "Assets/Scenes/CityArchitectureShowcase.unity";

    [MenuItem("Tools/City Architecture/Build Showcase Scene")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var lightGo = new GameObject("Showcase Sun");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

        string[] pieces = { "WallStraight", "WallCorner", "Gatehouse", "WallTowerConnector" };
        for (int i = 0; i < pieces.Length; i++)
        {
            string path = "Assets/CityArchitecture/Prefabs/" + pieces[i] + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new System.IO.FileNotFoundException("Prefab ausente", path);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = pieces[i] + "_Showcase_" + i;
            instance.transform.position = new Vector3((i % 2) * 12f, 0f, (i / 2) * 12f);
            SelectEra(instance.transform, i);
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Cena de showcase criada em " + ScenePath);
    }

    private static void SelectEra(Transform root, int era)
    {
        for (int i = 0; i < root.childCount; i++)
            root.GetChild(i).gameObject.SetActive(i == era);
    }
}
