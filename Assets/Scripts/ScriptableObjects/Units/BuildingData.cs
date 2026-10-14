using UnityEngine;

[CreateAssetMenu(fileName = "Building", menuName = "Scriptable Objects/Building", order = 2)]
public class BuildingData : UnitData
{

    [Header("Building Sounds")]
    public AudioClip ambientSound;

    [Header("Construction")]
    public Mesh[] constructionMeshes;

    // Construction rules belong to the building asset so each building can
    // scale independently as the roster grows. Existing assets retain the
    // previous behaviour through these defaults.
    [Min(1)] public int maxConstructors = 3;
    [Range(1, 4)] public int minimumSupportedCorners = 2;
    [Min(0.1f)] public float terrainProbeDistance = 2f;
    [Min(0f)] public float terrainProbeStartOffset = 0.5f;

    [Header("Placement")]
    [Tooltip("Optional world-space grid size. Zero keeps free placement.")]
    [Min(0f)] public float placementGridSize = 0f;
    [Tooltip("Rotation increment used by the placement preview.")]
    [Range(1f, 360f)] public float rotationStepDegrees = 15f;
    [Tooltip("Maximum allowed terrain slope in degrees at the supported probe corners.")]
    [Range(0f, 90f)] public float maximumTerrainSlopeDegrees = 25f;
    [Tooltip("Minimum distance from another building. Zero disables the rule.")]
    [Min(0f)] public float minimumDistanceFromBuildings = 0f;

}
