using System;
using UnityEngine;

/// <summary>
/// Ponte de compatibilidade somente-leitura. Este é o único ponto da migração
/// que chama o armazenamento legado; a v2 não delega escrita para ele.
/// </summary>
internal static class SaveMigrationV2LegacyFallback
{
    public static SaveMigrationV2ReadResult<T> Load<T>(string path) where T : class
    {
        try
        {
            T value = SaveFileStore.Load<T>(path);
            if (value != null)
                return SaveMigrationV2ReadResult<T>.Create(
                    SaveMigrationV2ReadStatus.LoadedLegacyFallback, path, value, null);
            return SaveMigrationV2ReadResult<T>.Create(
                SaveMigrationV2ReadStatus.LegacyUnavailable,
                path,
                null,
                "The legacy reader could not produce the requested type.");
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Legacy save fallback failed: " + exception.Message);
            return SaveMigrationV2ReadResult<T>.Create(
                SaveMigrationV2ReadStatus.LegacyUnavailable, path, null, exception.Message);
        }
    }
}
