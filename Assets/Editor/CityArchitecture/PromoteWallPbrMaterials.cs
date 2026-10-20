using UnityEditor;
using UnityEngine;

public static class PromoteWallPbrMaterials
{
    public static void Promote()
    {
        string[] eras = { "Ancient", "Classical", "Medieval", "Imperial" };
        foreach (string era in eras)
        {
            string productionPath = "Assets/CityArchitecture/Materials/Wall_" + era + ".mat";
            string candidatePath = "Assets/CityArchitecture/Materials/Candidates/Wall_" + era + "_Candidate.mat";
            Material production = AssetDatabase.LoadAssetAtPath<Material>(productionPath);
            Material candidate = AssetDatabase.LoadAssetAtPath<Material>(candidatePath);
            if (production == null || candidate == null)
                throw new System.IO.FileNotFoundException("Wall material missing", productionPath + " / " + candidatePath);
            string name = production.name;
            EditorUtility.CopySerialized(candidate, production);
            production.name = name;
            EditorUtility.SetDirty(production);
            Debug.Log("WALL_PBR_PROMOTED " + era + " candidate=" + candidatePath + " production=" + productionPath);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("WALL_PBR_PROMOTION_PASS");
    }
}
