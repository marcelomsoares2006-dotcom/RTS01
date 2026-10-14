using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

/// <summary>
/// Versioned, atomic storage for the game's existing save objects.
/// The BinaryFormatter call is intentionally isolated here so future formats can
/// replace it without changing gameplay save code. Legacy unwrapped files remain readable.
/// </summary>
public static class SaveFileStore
{
    private static readonly byte[] Magic = { (byte)'R', (byte)'T', (byte)'S', (byte)'A', (byte)'V', (byte)'E', 1, 0 };
    private const int FormatVersion = 1;
    private const long MaxFileBytes = 256L * 1024L * 1024L;

    public static void Save(string path, object value)
    {
        string fullPath = ValidatePath(path);
        string directory = Path.GetDirectoryName(fullPath);
        Directory.CreateDirectory(directory);
        string temporaryPath = fullPath + ".tmp";
        string backupPath = fullPath + ".bak";

        byte[] payload;
        using (var memory = new MemoryStream())
        {
            SerializeLegacy(memory, value);
            payload = memory.ToArray();
        }

        using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Magic);
            writer.Write(FormatVersion);
            writer.Write(payload.Length);
            writer.Write(payload);
            writer.Flush();
            stream.Flush(true);
        }

        // Keep a recoverable copy while replacing the active save.
        if (File.Exists(fullPath))
        {
            if (File.Exists(backupPath)) File.Delete(backupPath);
            File.Move(fullPath, backupPath);
        }
        File.Move(temporaryPath, fullPath);
        if (File.Exists(backupPath)) File.Delete(backupPath);
    }

    public static T Load<T>(string path) where T : class
    {
        string fullPath = ValidatePath(path);
        string backupPath = fullPath + ".bak";
        Exception failure;

        if (TryLoad(fullPath, out T value, out failure)) return value;
        if (File.Exists(backupPath) && TryLoad(backupPath, out value, out failure))
        {
            Debug.LogWarning($"Recovered save from backup: {fullPath}");
            return value;
        }

        if (failure != null)
            Debug.LogWarning($"Could not load save '{fullPath}': {failure.Message}");
        return null;
    }

    private static bool TryLoad<T>(string path, out T value, out Exception failure) where T : class
    {
        value = null;
        failure = null;
        try
        {
            if (!File.Exists(path)) return false;
            FileInfo info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > MaxFileBytes)
                throw new InvalidDataException("Save size is outside the accepted range.");

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(stream))
            {
                byte[] header = reader.ReadBytes(Magic.Length);
                bool wrapped = header.Length == Magic.Length;
                for (int i = 0; wrapped && i < Magic.Length; i++) wrapped = header[i] == Magic[i];

                if (wrapped)
                {
                    int version = reader.ReadInt32();
                    int payloadLength = reader.ReadInt32();
                    if (version != FormatVersion || payloadLength < 1 || payloadLength > MaxFileBytes)
                        throw new InvalidDataException("Unsupported or invalid save format.");
                    value = DeserializeLegacy<T>(new MemoryStream(reader.ReadBytes(payloadLength)));
                }
                else
                {
                    // Saves produced before the versioned envelope are still supported.
                    stream.Position = 0;
                    value = DeserializeLegacy<T>(stream);
                }
            }
            return value != null;
        }
        catch (Exception exception)
        {
            failure = exception;
            return false;
        }
    }

    private static void SerializeLegacy(Stream stream, object value)
    {
#pragma warning disable SYSLIB0011
        new BinaryFormatter().Serialize(stream, value);
#pragma warning restore SYSLIB0011
    }

    private static T DeserializeLegacy<T>(Stream stream) where T : class
    {
#pragma warning disable SYSLIB0011
        return new BinaryFormatter().Deserialize(stream) as T;
#pragma warning restore SYSLIB0011
    }

    private static string ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Save path is empty.", nameof(path));
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetFullPath(Application.persistentDataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Save path must be inside persistentDataPath.");
        return fullPath;
    }
}
