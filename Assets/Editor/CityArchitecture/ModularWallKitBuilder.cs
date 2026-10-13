using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates a standalone visual kit for city walls. The generated prefabs live
/// outside the existing building catalog and do not modify scenes or source prefabs.
/// </summary>
public static class ModularWallKitBuilder
{
    private const string Root = "Assets/CityArchitecture";
    private const string PrefabFolder = Root + "/Prefabs";
    private const string MaterialFolder = Root + "/Materials";

    private enum Era { Ancient, Classical, Medieval, Imperial }

    [MenuItem("Tools/City Architecture/Build Modular Wall Kit")]
    public static void BuildKit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Pare o Play Mode antes de gerar o kit de muralha.");

        EnsureFolders();
        var materials = CreateMaterials();
        BuildPrefab("WallStraight", root => BuildStraight(root, materials), materials);
        BuildPrefab("WallCorner", root => BuildCorner(root, materials), materials);
        BuildPrefab("Gatehouse", root => BuildGatehouse(root, materials), materials);
        BuildPrefab("WallTowerConnector", root => BuildTowerConnector(root, materials), materials);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Kit modular de muralha criado em " + PrefabFolder);
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "CityArchitecture");
        EnsureFolder(Root, "Prefabs");
        EnsureFolder(Root, "Materials");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }

    private static Dictionary<Era, Material> CreateMaterials()
    {
        var result = new Dictionary<Era, Material>();
        result[Era.Ancient] = Material("Ancient", new Color(.34f, .20f, .10f));
        result[Era.Classical] = Material("Classical", new Color(.55f, .52f, .44f));
        result[Era.Medieval] = Material("Medieval", new Color(.28f, .30f, .32f));
        result[Era.Imperial] = Material("Imperial", new Color(.17f, .20f, .23f));
        return result;
    }

    private static Material Material(string suffix, Color color)
    {
        string path = MaterialFolder + "/Wall_" + suffix + ".mat";
        var asset = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (asset == null)
        {
            asset = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(asset, path);
        }
        asset.color = color;
        return asset;
    }

    private static void BuildPrefab(string name, Action<Transform> build, Dictionary<Era, Material> materials)
    {
        string path = PrefabFolder + "/" + name + ".prefab";
        var root = new GameObject(name);
        root.AddComponent<WallKitPieceMetadata>().pieceType = name;
        try
        {
            build(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void BuildStraight(Transform root, Dictionary<Era, Material> mats)
    {
        for (int i = 0; i < 4; i++)
        {
            Era era = (Era)i;
            var eraRoot = Child(root, era.ToString(), new Vector3(0f, 0f, i * 0.01f));
            float height = 2.6f + i * .35f;
            Cube(eraRoot, "WallBody", new Vector3(0, height * .5f, 0), new Vector3(6f, height, .55f), mats[era]);
            AddMerlons(eraRoot, height, 6f, .55f, mats[era]);
            AddPillars(eraRoot, height, 6f, mats[era]);
        }
    }

    private static void BuildCorner(Transform root, Dictionary<Era, Material> mats)
    {
        for (int i = 0; i < 4; i++)
        {
            Era era = (Era)i;
            var eraRoot = Child(root, era.ToString(), new Vector3(0, 0, i * .01f));
            float height = 2.6f + i * .35f;
            Cube(eraRoot, "WallX", new Vector3(2.5f, height * .5f, 0), new Vector3(5f, height, .55f), mats[era]);
            Cube(eraRoot, "WallZ", new Vector3(0, height * .5f, 2.5f), new Vector3(.55f, height, 5f), mats[era]);
            Cube(eraRoot, "CornerPillar", new Vector3(0, height * .55f, 0), new Vector3(.8f, height * 1.1f, .8f), mats[era]);
            AddMerlons(eraRoot, height, 5f, .55f, mats[era]);
        }
    }

    private static void BuildGatehouse(Transform root, Dictionary<Era, Material> mats)
    {
        for (int i = 0; i < 4; i++)
        {
            Era era = (Era)i;
            var eraRoot = Child(root, era.ToString(), new Vector3(0, 0, i * .01f));
            float height = 4.2f + i * .45f;
            Cube(eraRoot, "LeftTower", new Vector3(-2.3f, height * .5f, 0), new Vector3(1.5f, height, 2.2f), mats[era]);
            Cube(eraRoot, "RightTower", new Vector3(2.3f, height * .5f, 0), new Vector3(1.5f, height, 2.2f), mats[era]);
            Cube(eraRoot, "GateBeam", new Vector3(0, height - .65f, 0), new Vector3(3.2f, 1.3f, 2.2f), mats[era]);
            Cube(eraRoot, "GateThreshold", new Vector3(0, .12f, -.02f), new Vector3(1.8f, .24f, 2.3f), mats[era]);
            AddMerlons(eraRoot, height, 5.2f, 2.2f, mats[era]);
        }
    }

    private static void BuildTowerConnector(Transform root, Dictionary<Era, Material> mats)
    {
        for (int i = 0; i < 4; i++)
        {
            Era era = (Era)i;
            var eraRoot = Child(root, era.ToString(), new Vector3(0, 0, i * .01f));
            float height = 4.8f + i * .5f;
            Cylinder(eraRoot, "Tower", new Vector3(0, height * .5f, 0), .95f, height, mats[era]);
            AddMerlons(eraRoot, height, 2.0f, 2.0f, mats[era]);
            Cube(eraRoot, "ConnectorX", new Vector3(2.2f, 1.3f, 0), new Vector3(3.4f, 2.6f, .55f), mats[era]);
            Cube(eraRoot, "ConnectorZ", new Vector3(0, 1.3f, 2.2f), new Vector3(.55f, 2.6f, 3.4f), mats[era]);
        }
    }

    private static Transform Child(Transform parent, string name, Vector3 localPosition)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = localPosition; return go.transform;
    }

    private static void AddPillars(Transform parent, float height, float width, Material mat)
    {
        Cube(parent, "PillarL", new Vector3(-width * .5f, height * .5f, 0), new Vector3(.7f, height * 1.1f, .8f), mat);
        Cube(parent, "PillarR", new Vector3(width * .5f, height * .5f, 0), new Vector3(.7f, height * 1.1f, .8f), mat);
    }

    private static void AddMerlons(Transform parent, float height, float width, float depth, Material mat)
    {
        int count = Mathf.Clamp(Mathf.RoundToInt(width), 3, 8);
        for (int i = 0; i < count; i++)
        {
            float x = count == 1 ? 0 : Mathf.Lerp(-width * .5f + .25f, width * .5f - .25f, i / (float)(count - 1));
            Cube(parent, "Merlon_" + i, new Vector3(x, height + .28f, 0), new Vector3(.42f, .55f, depth), mat);
        }
    }

    private static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }

    private static GameObject Cylinder(Transform parent, string name, Vector3 position, float radius, float height, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = new Vector3(radius, height * .5f, radius); go.GetComponent<Renderer>().sharedMaterial = mat; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }
}

public sealed class WallKitPieceMetadata : MonoBehaviour
{
    public string pieceType;
    public string integrationNote = "Visual-only modular kit; add gameplay collider separately if required.";
}
