using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Fiscalização adicional e isolada das construções existentes.
/// Não substitui BuildingEraDecorationTests e não depende de muralhas ainda não catalogadas.
/// </summary>
public sealed class BuildingConstructionFiscalTests
{
    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;

    [Test]
    public void BuildingCatalog_ContainsOnlyAssetsWithConstructionMeshes()
    {
        foreach (string code in new[] { "House", "Tower" })
        {
            ScriptableObject data = Resources.Load<ScriptableObject>("ScriptableObjects/Units/Buildings/" + code);
            Assert.That(data, Is.Not.Null, "Asset de construção ausente: " + code);

            FieldInfo meshesField = data.GetType().GetField("constructionMeshes");
            Assert.That(meshesField, Is.Not.Null, "Campo constructionMeshes ausente em " + code);
            Mesh[] meshes = meshesField.GetValue(data) as Mesh[];
            Assert.That(meshes, Is.Not.Null.And.Not.Empty, "Construção sem malha de construção: " + code);
            Assert.That(Array.Exists(meshes, mesh => mesh != null), Is.True,
                "Nenhuma malha válida no catálogo: " + code);
        }
    }

    [Test]
    public void Decoration_IsIdempotentAndKeepsConstructionGroupsIsolated()
    {
        Type state = Type.GetType("AdvancedEraState, Assembly-CSharp");
        Type decorationType = Type.GetType("BuildingEraDecoration, Assembly-CSharp");
        Type eraType = Type.GetType("EraImperial.Units.Advanced.ImperialEra, EraImperial.Units.Advanced");
        Assert.That(state, Is.Not.Null);
        Assert.That(decorationType, Is.Not.Null);
        Assert.That(eraType, Is.Not.Null);

        ScriptableObject data = Resources.Load<ScriptableObject>("ScriptableObjects/Units/Buildings/House");
        Mesh[] meshes = (Mesh[])data.GetType().GetField("constructionMeshes").GetValue(data);
        GameObject root = new GameObject("Fiscal house");
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        Component decoration = root.AddComponent(decorationType);
        MethodInfo initialize = decorationType.GetMethod("Initialize");
        MethodInfo finish = decorationType.GetMethod("SetFinished");
        MethodInfo reset = state.GetMethod("Reset", PublicStatic);

        try
        {
            reset.Invoke(null, null);
            initialize.Invoke(decoration, new object[] { "house", visual.transform, meshes[meshes.Length - 1] });
            int firstCount = visual.transform.childCount;
            initialize.Invoke(decoration, new object[] { "house", visual.transform, meshes[meshes.Length - 1] });
            Assert.That(visual.transform.childCount, Is.EqualTo(firstCount),
                "Inicialização repetida não pode duplicar grupos de era.");

            Assert.That(firstCount, Is.EqualTo(4));
            Assert.That(CountActiveChildren(visual.transform), Is.Zero,
                "Detalhes devem permanecer ocultos durante a construção.");
            finish.Invoke(decoration, new object[] { true });
            Assert.That(CountActiveChildren(visual.transform), Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            reset.Invoke(null, null);
        }
    }

    [Test]
    public void TowerDecoration_ProvidesDefensiveReadinessByEra()
    {
        Type state = Type.GetType("AdvancedEraState, Assembly-CSharp");
        Type decorationType = Type.GetType("BuildingEraDecoration, Assembly-CSharp");
        Type eraType = Type.GetType("EraImperial.Units.Advanced.ImperialEra, EraImperial.Units.Advanced");
        Assert.That(state, Is.Not.Null);
        Assert.That(decorationType, Is.Not.Null);
        Assert.That(eraType, Is.Not.Null);

        ScriptableObject data = Resources.Load<ScriptableObject>("ScriptableObjects/Units/Buildings/Tower");
        Mesh[] meshes = (Mesh[])data.GetType().GetField("constructionMeshes").GetValue(data);
        GameObject root = new GameObject("Fiscal tower");
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        Component decoration = root.AddComponent(decorationType);
        MethodInfo initialize = decorationType.GetMethod("Initialize");
        MethodInfo finish = decorationType.GetMethod("SetFinished");
        MethodInfo set = state.GetMethod("Set", PublicStatic);
        MethodInfo reset = state.GetMethod("Reset", PublicStatic);

        try
        {
            reset.Invoke(null, null);
            initialize.Invoke(decoration, new object[] { "tower", visual.transform, meshes[meshes.Length - 1] });
            finish.Invoke(decoration, new object[] { true });

            foreach (int era in new[] { 0, 1, 2, 3 })
            {
                set.Invoke(null, new[] { Enum.ToObject(eraType, era) });
                Transform group = visual.transform.GetChild(era);
                Assert.That(group.gameObject.activeSelf, Is.True);
                Assert.That(group.Find("WallFront"), Is.Not.Null);
                Assert.That(group.Find("ButtressL"), Is.Not.Null);
                Assert.That(group.Find("Crenel_0"), Is.Not.Null);
                if (era >= 2)
                    Assert.That(group.Find("ArrowSlitL"), Is.Not.Null);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            reset.Invoke(null, null);
        }
    }

    private static int CountActiveChildren(Transform parent)
    {
        int count = 0;
        foreach (Transform child in parent)
            if (child.gameObject.activeSelf) count++;
        return count;
    }
}
