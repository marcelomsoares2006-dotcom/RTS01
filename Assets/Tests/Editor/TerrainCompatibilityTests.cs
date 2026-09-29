using EraImperial.Compatibility;
using NUnit.Framework;
using UnityEngine;

public class TerrainCompatibilityTests
{
    [Test]
    public void LayerRepair_PreservesAuthoredSlotsAndRepairsOnlyMissingSlots()
    {
        var authored = new TerrainLayer { name = "NewLayer - hand painted" };
        var grass = new TerrainLayer();
        var dirt = new TerrainLayer();
        try
        {
            var defaults = new[] { grass, dirt };
            var shortPalette = TerrainCompatibility.FillMissingLayers(new[] { authored }, defaults);
            Assert.That(shortPalette, Has.Length.EqualTo(1));
            Assert.That(shortPalette[0], Is.SameAs(authored));
            var original = new[] { authored, null };
            var repaired = TerrainCompatibility.FillMissingLayers(original, defaults);
            Assert.That(repaired[0], Is.SameAs(authored));
            Assert.That(repaired[1], Is.SameAs(dirt));
            Assert.That(original[1], Is.Null, "Repair must not mutate the caller's palette.");
        }
        finally
        {
            Object.DestroyImmediate(authored);
            Object.DestroyImmediate(grass);
            Object.DestroyImmediate(dirt);
        }
    }

    [Test]
    public void TreeRepair_PreservesValidPrototypesAndEveryInstanceIndex()
    {
        var existing = new GameObject("Authored tree");
        var fallback = new GameObject("Replacement tree");
        try
        {
            var valid = new TreePrototype { prefab = existing, bendFactor = 0.25f };
            var missing = new TreePrototype { bendFactor = 0.5f };
            var original = new[] { missing, valid, null };
            var repaired = TerrainCompatibility.FillMissingTreePrefabs(original, fallback);
            Assert.That(repaired, Has.Length.EqualTo(original.Length));
            Assert.That(repaired[0].prefab, Is.SameAs(fallback));
            Assert.That(repaired[0].bendFactor, Is.EqualTo(0.5f));
            Assert.That(repaired[1], Is.SameAs(valid));
            Assert.That(repaired[2].prefab, Is.SameAs(fallback));
            Assert.That(missing.prefab, Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(existing);
            Object.DestroyImmediate(fallback);
        }
    }

    [Test]
    public void MaterialRepair_ReplacesMissingOrErrorShaderButPreservesSupportedMaterial()
    {
        Shader shader = Shader.Find("Nature/Terrain/Standard");
        Assert.That(shader, Is.Not.Null);
        Assert.That(shader.isSupported, Is.True, "Requires the actual graphics device, not -nographics.");
        var fallback = new Material(shader);
        var authored = new Material(shader);
        var broken = new Material(Shader.Find("Hidden/InternalErrorShader"));
        try
        {
            Assert.That(TerrainCompatibility.SelectMaterial(null, fallback), Is.SameAs(fallback));
            Assert.That(TerrainCompatibility.SelectMaterial(broken, fallback), Is.SameAs(fallback));
            Assert.That(TerrainCompatibility.SelectMaterial(authored, fallback), Is.SameAs(authored));
            Assert.That(TerrainCompatibility.SelectMaterial(authored, null), Is.SameAs(authored));
        }
        finally
        {
            Object.DestroyImmediate(fallback);
            Object.DestroyImmediate(authored);
            Object.DestroyImmediate(broken);
        }
    }
}
