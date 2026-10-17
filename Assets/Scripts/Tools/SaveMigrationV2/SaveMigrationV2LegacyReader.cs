using System;

namespace EraImperial.SaveMigrationV2
{
    /// <summary>
    /// The only legacy bridge. It is read-only and deliberately isolated from
    /// the V2 writer. SaveMigrationV2Store never calls BinaryFormatter itself.
    /// Removal of this file after the compatibility window removes the legacy
    /// deserialization path without changing the V2 document.
    /// </summary>
    internal static class SaveMigrationV2LegacyReader
    {
        public static SaveMigrationV2ReadResult TryRead(string path)
        {
            try
            {
                BinarySerializable legacy = BinarySerializable.Load(path);
                GameData data = legacy as GameData;
                if (data == null)
                    return new SaveMigrationV2ReadResult {
                        Status = SaveMigrationV2Status.Unsupported,
                        Source = SaveMigrationV2Source.Legacy,
                        Error = "O arquivo legado não contém GameData suportado."
                    };

                SaveMigrationV2Document document = SaveMigrationV2Mapper.FromGameData(data);
                var errors = SaveMigrationV2Validator.Validate(document);
                if (errors.Count > 0)
                    return new SaveMigrationV2ReadResult {
                        Status = SaveMigrationV2Status.Invalid,
                        Source = SaveMigrationV2Source.Legacy,
                        Error = string.Join("; ", errors.ToArray())
                    };

                SaveMigrationV2ReadResult result = new SaveMigrationV2ReadResult {
                    Status = SaveMigrationV2Status.Loaded,
                    Source = SaveMigrationV2Source.Legacy,
                    Data = data,
                    Document = document
                };
                result.Warnings.Add("Leitura compatível: o arquivo legado foi convertido em memória; o original não foi sobrescrito.");
                return result;
            }
            catch (Exception exception)
            {
                return new SaveMigrationV2ReadResult {
                    Status = SaveMigrationV2Status.Corrupt,
                    Source = SaveMigrationV2Source.Legacy,
                    Error = exception.Message
                };
            }
        }
    }
}
