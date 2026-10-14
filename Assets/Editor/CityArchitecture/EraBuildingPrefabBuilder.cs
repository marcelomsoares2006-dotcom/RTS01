using UnityEditor;
using UnityEngine;

namespace EraImperial.Editor.CityArchitecture
{
    public static class EraBuildingPrefabBuilder
    {
        private const string SourceFolder = "Assets/CityArchitecture/Generated/Buildings";
        private const string Folder = "Assets/CityArchitecture/Prefabs/EraBuildings";

        [MenuItem("Tools/City Architecture/Build Era Building Prefabs")]
        public static void BuildAll()
        {
            EnsureFolders();
            var names = new[] { "House_Medieval", "Barracks_Medieval", "Temple_Imperial", "Market_Classical" };
            var built = 0;
            foreach (var name in names)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{SourceFolder}/{name}.fbx");
                if (source == null) { Debug.LogError($"Missing individual building FBX: {name}"); continue; }
                var instance = Object.Instantiate(source);
                instance.name = name;
                try { if (PrefabUtility.SaveAsPrefabAsset(instance, $"{Folder}/{name}.prefab") != null) built++; }
                finally { Object.DestroyImmediate(instance); }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (built != names.Length) Debug.LogError($"Only {built}/{names.Length} era building prefabs were generated.");
            else Debug.Log($"Era building prefabs generated: {built}/{names.Length}.");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/CityArchitecture")) AssetDatabase.CreateFolder("Assets", "CityArchitecture");
            if (!AssetDatabase.IsValidFolder("Assets/CityArchitecture/Prefabs")) AssetDatabase.CreateFolder("Assets/CityArchitecture", "Prefabs");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/CityArchitecture/Prefabs", "EraBuildings");
        }
    }
}
