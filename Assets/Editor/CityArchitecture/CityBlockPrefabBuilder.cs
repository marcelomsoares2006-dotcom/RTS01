using UnityEditor;
using UnityEngine;

namespace EraImperial.Editor.CityArchitecture
{
    /// <summary>
    /// Builds the reusable city-block prefab from the generated FBX.
    /// Re-running replaces the same prefab asset and does not modify scenes.
    /// </summary>
    public static class CityBlockPrefabBuilder
    {
        private const string SourceFbxPath =
            "Assets/CityArchitecture/Generated/EraImperialCityBlock.fbx";
        private const string PrefabDirectory = "Assets/CityArchitecture/Prefabs";
        private const string PrefabPath =
            "Assets/CityArchitecture/Prefabs/EraImperialCityBlock.prefab";

        [MenuItem("Tools/City Architecture/Build Era Imperial City Block Prefab")]
        public static void BuildPrefab()
        {
            BuildPrefabInternal();
        }

        public static GameObject BuildPrefabInternal()
        {
            EnsurePrefabDirectory();

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbxPath);
            if (source == null)
            {
                Debug.LogError($"City block FBX was not found or is not imported: {SourceFbxPath}");
                return null;
            }

            var instance = Object.Instantiate(source);
            instance.name = "EraImperialCityBlock";

            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"Could not create city block prefab: {PrefabPath}");
                    return null;
                }

                AssetDatabase.SaveAssets();
                Debug.Log($"Era Imperial city block prefab ready: {PrefabPath}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void EnsurePrefabDirectory()
        {
            if (AssetDatabase.IsValidFolder(PrefabDirectory))
                return;

            if (!AssetDatabase.IsValidFolder("Assets/CityArchitecture"))
                AssetDatabase.CreateFolder("Assets", "CityArchitecture");

            AssetDatabase.CreateFolder("Assets/CityArchitecture", "Prefabs");
        }
    }
}
