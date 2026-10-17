using System;

// Compatibility types for the two pre-existing, untracked authoring files in
// this folder. They are intentionally independent of the namespaced V2 store;
// keeping this small adapter here avoids modifying or deleting concurrent work.
public enum SaveMigrationV2ReadStatus
{
    Loaded,
    LoadedLegacyFallback,
    LegacyUnavailable
}

public sealed class SaveMigrationV2ReadResult<T> where T : class
{
    public SaveMigrationV2ReadStatus Status;
    public string Path;
    public T Value;
    public string Error;

    public static SaveMigrationV2ReadResult<T> Create(
        SaveMigrationV2ReadStatus status, string path, T value, string error)
    {
        return new SaveMigrationV2ReadResult<T> { Status = status, Path = path, Value = value, Error = error };
    }
}

public static class SaveMigrationV2
{
    public const int SchemaVersion = 2;
}
