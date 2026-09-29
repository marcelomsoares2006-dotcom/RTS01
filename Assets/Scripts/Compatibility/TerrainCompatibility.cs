using System;
using UnityEngine;

namespace EraImperial.Compatibility
{
    public static class TerrainCompatibility
    {
        public static TerrainLayer[] FillMissingLayers(TerrainLayer[] layers, TerrainLayer[] defaults)
        {
            if (defaults == null) throw new ArgumentNullException(nameof(defaults));
            if (layers == null || layers.Length == 0) return (TerrainLayer[])defaults.Clone();
            TerrainLayer[] result = (TerrainLayer[])layers.Clone();
            for (int i = 0; i < result.Length; i++)
            {
                // Keep indices: reordering also changes the meaning of the painted alphamap.
                if (result[i] == null && i < defaults.Length) result[i] = defaults[i];
            }
            return result;
        }

        public static TreePrototype[] FillMissingTreePrefabs(TreePrototype[] prototypes, GameObject fallback)
        {
            if (prototypes == null) return Array.Empty<TreePrototype>();
            TreePrototype[] result = (TreePrototype[])prototypes.Clone();
            for (int i = 0; i < result.Length; i++)
            {
                TreePrototype source = result[i];
                if (source != null && source.prefab != null) continue;
                if (fallback == null) throw new ArgumentNullException(nameof(fallback));
                result[i] = new TreePrototype
                {
                    prefab = fallback,
                    bendFactor = source != null ? source.bendFactor : 0f
                };
            }
            // Nothing is removed/reordered, so tree instances retain their indices and positions.
            return result;
        }

        public static Material SelectMaterial(Material current, Material fallback)
        {
            if (IsSupported(current)) return current;
            if (IsSupported(fallback)) return fallback;
            return current;
        }

        private static bool IsSupported(Material material)
            => material != null && material.shader != null && material.shader.isSupported &&
               material.shader.name != "Hidden/InternalErrorShader";
    }
}
