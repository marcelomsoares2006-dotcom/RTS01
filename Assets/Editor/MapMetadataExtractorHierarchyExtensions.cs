using UnityEditor;
using UnityEngine.SceneManagement;
public static class MapMetadataExtractorHierarchyExtensions
{
    // Public API instead of obsolete instance IDs and private reflection.
    [MenuItem("Tools/Era Imperial/Extract Active Map Metadata")]
    private static void ExtractActiveMap()
        => MapMetadataExtractor.Extract(SceneManager.GetActiveScene());
}
