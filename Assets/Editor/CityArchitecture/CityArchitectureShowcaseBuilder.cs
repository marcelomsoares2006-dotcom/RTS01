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

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Showcase Ground";
        ground.transform.position = new Vector3(6f, -0.02f, 6f);
        ground.transform.localScale = new Vector3(4.5f, 1f, 4.5f);
        Object.DestroyImmediate(ground.GetComponent<Collider>());

        var cameraGo = new GameObject("Showcase Camera");
        var camera = cameraGo.AddComponent<Camera>();
        camera.transform.position = new Vector3(18f, 18f, -22f);
        camera.transform.LookAt(new Vector3(6f, 2f, 6f));
        camera.fieldOfView = 42f;
        camera.farClipPlane = 100f;

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
