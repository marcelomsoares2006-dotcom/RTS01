using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EraImperial.EditorTests cannot reference Assembly-CSharp directly.
public class BuildingEraDecorationTests
{
    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;

    [TestCase("House")]
    [TestCase("Tower")]
    public void DetailsRespectConstructionAndRepeatedEraChanges(string name)
    {
        Type state = Type.GetType("AdvancedEraState, Assembly-CSharp");
        Type eraType = Type.GetType("EraImperial.Units.Advanced.ImperialEra, EraImperial.Units.Advanced");
        Type decorationType = Type.GetType("BuildingEraDecoration, Assembly-CSharp");
        Assert.That(state, Is.Not.Null);
        Assert.That(eraType, Is.Not.Null);
        Assert.That(decorationType, Is.Not.Null);

        ScriptableObject data = Resources.Load<ScriptableObject>("ScriptableObjects/Units/Buildings/" + name);
        Assert.That(data, Is.Not.Null);
        Mesh[] meshes = (Mesh[])data.GetType().GetField("constructionMeshes").GetValue(data);
        Assert.That(meshes, Is.Not.Empty);

        GameObject root = new GameObject("Test building");
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform);
        Component decoration = root.AddComponent(decorationType);
        MethodInfo set = state.GetMethod("Set", PublicStatic);
        MethodInfo reset = state.GetMethod("Reset", PublicStatic);
        MethodInfo initialize = decorationType.GetMethod("Initialize");
        MethodInfo finish = decorationType.GetMethod("SetFinished");
        try
        {
            reset.Invoke(null, null);
            initialize.Invoke(decoration, new object[] { name.ToLowerInvariant(), visual.transform, meshes[meshes.Length - 1] });
            Assert.That(visual.transform.childCount, Is.EqualTo(4));
            Assert.That(ActiveGroups(visual.transform), Is.Zero);
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                Assert.That(collider.enabled, Is.False, "Decorations must not affect placement.");

            finish.Invoke(decoration, new object[] { true });
            foreach (int era in new[] { 3, 1, 2, 0, 3, 0 })
            {
                set.Invoke(null, new[] { Enum.ToObject(eraType, era) });
                Assert.That(ActiveGroups(visual.transform), Is.EqualTo(1));
                Assert.That(visual.transform.GetChild(era).gameObject.activeSelf, Is.True);
                Assert.That(visual.transform.childCount, Is.EqualTo(4));
                if (name == "Tower")
                {
                    Assert.That(visual.transform.GetChild(era).Find("WallFront"), Is.Not.Null);
                    Assert.That(visual.transform.GetChild(era).Find("ButtressL"), Is.Not.Null);
                    Assert.That(visual.transform.GetChild(era).Find("Crenel_0"), Is.Not.Null);
                }
            }
            finish.Invoke(decoration, new object[] { false });
            Assert.That(ActiveGroups(visual.transform), Is.Zero);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            reset.Invoke(null, null);
        }
    }

    private static int ActiveGroups(Transform visual)
    {
        int count = 0;
        foreach (Transform child in visual)
            if (child.gameObject.activeSelf) count++;
        return count;
    }
}
