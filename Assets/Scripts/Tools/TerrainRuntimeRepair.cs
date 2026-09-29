using EraImperial.Compatibility;
using UnityEngine;

[DisallowMultipleComponent]
public class TerrainRuntimeRepair : MonoBehaviour
{
    private void Awake() => RepairAllTerrains();

    public static void RepairAllTerrains()
    {
        Material fallback = Resources.Load<Material>("Terrain/EraImperialTerrain");
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null) continue;
            // Entering Play must never overwrite shared TerrainData.
            Material material = TerrainCompatibility.SelectMaterial(terrain.materialTemplate, fallback);
            if (material != terrain.materialTemplate) terrain.materialTemplate = material;
        }
    }
}
