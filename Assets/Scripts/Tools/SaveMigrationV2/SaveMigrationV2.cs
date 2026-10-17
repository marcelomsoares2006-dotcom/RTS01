using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace EraImperial.SaveMigrationV2
{
    public enum SaveMigrationV2Source
    {
        None,
        V2,
        Legacy
    }

    public enum SaveMigrationV2Status
    {
        Loaded,
        RecoveredFromBackup,
        Missing,
        Corrupt,
        Unsupported,
        Invalid,
        AlreadyV2
    }

    public sealed class SaveMigrationV2ReadResult
    {
        public SaveMigrationV2Status Status;
        public SaveMigrationV2Source Source;
        public GameData Data;
        public SaveMigrationV2Document Document;
        public readonly List<string> Warnings = new List<string>();
        public string Error;

        public bool Succeeded
        {
            get { return Status == SaveMigrationV2Status.Loaded || Status == SaveMigrationV2Status.RecoveredFromBackup; }
        }
    }

    /// <summary>
    /// Explicit, detached save document. It contains no Unity object, prefab,
    /// scene reference or Dictionary&lt;string, object&gt;. Enum values are stored
    /// as integers only where the existing runtime contract already uses an
    /// enum; resource arrays use stable names instead of positional indexes.
    /// </summary>
    [Serializable]
    public sealed class SaveMigrationV2Document
    {
        public int schemaVersion = SaveMigrationV2Store.SchemaVersion;
        public string gameUid;
        public SaveMigrationV2Player[] players = new SaveMigrationV2Player[0];
        public SaveMigrationV2Vector3 cameraPosition;
        public string[] unlockedTechnologyNodeCodes = new string[0];
        public SaveMigrationV2ReplayCommand[] constructionReplayCommands = new SaveMigrationV2ReplayCommand[0];
        public SaveMigrationV2ReplaySnapshot constructionReplayState;
    }

    [Serializable]
    public sealed class SaveMigrationV2Player
    {
        public SaveMigrationV2Resource[] resources = new SaveMigrationV2Resource[0];
        public SaveMigrationV2Economy economy;
        public SaveMigrationV2Unit[] units = new SaveMigrationV2Unit[0];
    }

    [Serializable]
    public sealed class SaveMigrationV2Resource
    {
        public string code;
        public int amount;
    }

    [Serializable]
    public sealed class SaveMigrationV2Economy
    {
        public SaveMigrationV2EconomyResource[] stockpile = new SaveMigrationV2EconomyResource[0];
        public int population;
        public int populationCapacity;
        public SaveMigrationV2Collector[] collectors = new SaveMigrationV2Collector[0];
    }

    [Serializable]
    public sealed class SaveMigrationV2EconomyResource
    {
        public string code;
        public int amount;
    }

    [Serializable]
    public sealed class SaveMigrationV2Collector
    {
        public string collectorId;
        public int ownerId;
        public string resource;
        public float ratePerSecond;
        public float carryCapacity;
        public float carried;
    }

    [Serializable]
    public sealed class SaveMigrationV2Unit
    {
        public string unitType;
        public string code;
        public string uid;
        public SaveMigrationV2Vector3 position;
        public int currentHealth;
        public int level;
        public SaveMigrationV2Resource[] production = new SaveMigrationV2Resource[0];
        public int attackDamage;
        public float attackRange;
        public int constructionHP;
        public int buildingLifecycle;
        public string[] constructorIds = new string[0];
        public SaveMigrationV2Construction construction;
        public SaveMigrationV2Maintenance maintenance;
        public SaveMigrationV2ProductionQueue productionQueue;
    }

    [Serializable]
    public sealed class SaveMigrationV2Construction
    {
        public string buildingCode;
        public int ownerId;
        public SaveMigrationV2Vector3 position;
        public int constructionHP;
        public int maxHP;
        public int lifecycle;
        public string[] constructorIds = new string[0];
        public SaveMigrationV2Cost[] reservedCosts = new SaveMigrationV2Cost[0];
        public string reservationId;
        public int reservedPopulation;
        public int assetOrigin;
        public bool contentRuntimeReady;
    }

    [Serializable]
    public sealed class SaveMigrationV2Maintenance
    {
        public string buildingId;
        public int ownerId;
        public int currentHealth;
        public int maxHealth;
        public int lifecycle;
        public int operation;
        public float progress;
        public bool busy;
        public SaveMigrationV2MaintenanceState operationState;
    }

    [Serializable]
    public sealed class SaveMigrationV2MaintenanceState
    {
        public int operation;
        public float elapsed;
        public float duration;
        public int initialHealth;
        public int initialLifecycle;
        public SaveMigrationV2Cost[] reservedCost = new SaveMigrationV2Cost[0];
    }

    [Serializable]
    public sealed class SaveMigrationV2ProductionQueue
    {
        public string buildingCode;
        public int ownerId;
        public int era;
        public int assetOrigin;
        public string[] unlockedTechnologyCodes = new string[0];
        public SaveMigrationV2ProductionOrder[] orders = new SaveMigrationV2ProductionOrder[0];
    }

    [Serializable]
    public sealed class SaveMigrationV2ProductionOrder
    {
        public string productCode;
        public float duration;
        public float elapsed;
        public int populationCost;
        public SaveMigrationV2LegacyResource[] cost = new SaveMigrationV2LegacyResource[0];
    }

    [Serializable]
    public sealed class SaveMigrationV2LegacyResource
    {
        public string code;
        public int amount;
    }

    [Serializable]
    public sealed class SaveMigrationV2Cost
    {
        public string resource;
        public int amount;
    }

    [Serializable]
    public sealed class SaveMigrationV2ReplayCommand
    {
        public long simulationTick;
        public long issuedAtMilliseconds;
        public int priority;
        public string commandId;
        public int ownerId;
        public int kind;
        public string targetCode;
        public string reservationId;
        public int quantity;
        public int populationCost;
        public SaveMigrationV2Cost[] costs = new SaveMigrationV2Cost[0];
    }

    [Serializable]
    public sealed class SaveMigrationV2ReplaySnapshot
    {
        public int era;
        public string[] activeBuildings = new string[0];
        public string[] producedUnits = new string[0];
        public string[] reservations = new string[0];
        public string fingerprint;
    }

    [Serializable]
    public sealed class SaveMigrationV2Vector3
    {
        public float x;
        public float y;
        public float z;

        public SaveMigrationV2Vector3() { }

        public SaveMigrationV2Vector3(Vector3 value)
        {
            x = value.x;
            y = value.y;
            z = value.z;
        }

        public Vector3 ToUnity()
        {
            return new Vector3(x, y, z);
        }
    }

    /// <summary>
    /// V2 writer/reader. New writes use only JsonUtility and the explicit
    /// envelope below; the legacy BinaryFormatter bridge lives in its own file
    /// and is reached only when a file is not a V2 document.
    /// </summary>
    public static class SaveMigrationV2Store
    {
        public const int SchemaVersion = 2;
        private const int EnvelopeVersion = 2;
        private const long MaxFileBytes = 256L * 1024L * 1024L;
        private const int MaxJsonBytes = 64 * 1024 * 1024;
        private static readonly byte[] Magic = { (byte)'R', (byte)'T', (byte)'S', (byte)'A', (byte)'V', (byte)'E', 2, 0 };

        public static void Save(string path, GameData data)
        {
            string fullPath = ValidatePath(path);
            SaveMigrationV2Document document = SaveMigrationV2Mapper.FromGameData(data);
            List<string> errors = SaveMigrationV2Validator.Validate(document);
            if (errors.Count > 0)
                throw new InvalidDataException("V2 save rejected: " + string.Join("; ", errors.ToArray()));

            byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(document, false));
            if (payload.Length < 1 || payload.Length > MaxJsonBytes)
                throw new InvalidDataException("V2 payload is outside the accepted range.");

            string directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory);
            string temporaryPath = fullPath + ".tmp";
            string backupPath = fullPath + ".bak";
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(Magic);
                    writer.Write(EnvelopeVersion);
                    writer.Write(SchemaVersion);
                    writer.Write(payload.Length);
                    writer.Write(Sha256(payload));
                    writer.Write(payload);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(fullPath))
                {
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    File.Move(fullPath, backupPath);
                }
                File.Move(temporaryPath, fullPath);
                // Keep the last known-good file. A later interrupted
                // replacement can therefore be recovered by Load().
            }
            catch
            {
                if (!File.Exists(fullPath) && File.Exists(backupPath)) File.Move(backupPath, fullPath);
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                throw;
            }
        }

        public static SaveMigrationV2ReadResult Load(string path)
        {
            string fullPath = ValidatePath(path);
            SaveMigrationV2ReadResult primary = TryLoadCandidate(fullPath);
            if (primary.Succeeded)
                return primary;

            string backupPath = fullPath + ".bak";
            if (!File.Exists(backupPath)) return primary;
            SaveMigrationV2ReadResult backup = TryLoadCandidate(backupPath);
            if (backup.Succeeded)
            {
                backup.Status = SaveMigrationV2Status.RecoveredFromBackup;
                backup.Warnings.Insert(0, "O arquivo principal falhou; backup carregado: " + backupPath);
                return backup;
            }
            primary.Warnings.Add("O backup também não pôde ser carregado: " + backup.Error);
            return primary;
        }

        public static SaveMigrationV2ReadResult MigrateLegacyToV2(string legacyPath, string destinationPath)
        {
            string source = ValidatePath(legacyPath);
            string destination = ValidatePath(destinationPath);
            if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A migração exige um destino separado para preservar o arquivo legado.");

            SaveMigrationV2ReadResult result = Load(source);
            if (!result.Succeeded) return result;
            if (result.Source == SaveMigrationV2Source.V2)
            {
                result.Status = SaveMigrationV2Status.AlreadyV2;
                return result;
            }

            Save(destination, result.Data);
            result.Document = SaveMigrationV2Mapper.FromGameData(result.Data);
            result.Warnings.Insert(0, "Arquivo legado convertido para V2 em destino separado.");
            return result;
        }

        private static SaveMigrationV2ReadResult TryLoadCandidate(string path)
        {
            if (!File.Exists(path))
                return new SaveMigrationV2ReadResult { Status = SaveMigrationV2Status.Missing };

            try
            {
                FileInfo info = new FileInfo(path);
                if (info.Length <= 0 || info.Length > MaxFileBytes)
                    return Failure(SaveMigrationV2Status.Invalid, "Tamanho do save fora do limite.");

                byte[] bytes = File.ReadAllBytes(path);
                if (!HasV2Magic(bytes))
                    return SaveMigrationV2LegacyReader.TryRead(path);
                return ReadV2(bytes);
            }
            catch (Exception exception)
            {
                return Failure(SaveMigrationV2Status.Corrupt, exception.Message);
            }
        }

        private static SaveMigrationV2ReadResult ReadV2(byte[] bytes)
        {
            try
            {
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(stream))
                {
                    byte[] magic = reader.ReadBytes(Magic.Length);
                    int envelopeVersion = reader.ReadInt32();
                    int schemaVersion = reader.ReadInt32();
                    int payloadLength = reader.ReadInt32();
                    byte[] expectedHash = reader.ReadBytes(32);
                    if (!BytesEqual(magic, Magic) || envelopeVersion != EnvelopeVersion
                        || schemaVersion != SchemaVersion || payloadLength < 1
                        || payloadLength > MaxJsonBytes || expectedHash.Length != 32)
                        return Failure(SaveMigrationV2Status.Unsupported, "Envelope V2 desconhecido ou inválido.");

                    long remaining = stream.Length - stream.Position;
                    if (remaining != payloadLength)
                        return Failure(SaveMigrationV2Status.Corrupt, "Payload truncado ou com bytes excedentes.");
                    byte[] payload = reader.ReadBytes(payloadLength);
                    if (!BytesEqual(expectedHash, Sha256(payload)))
                        return Failure(SaveMigrationV2Status.Corrupt, "Hash SHA-256 do payload não confere.");

                    string json = new UTF8Encoding(false, true).GetString(payload);
                    SaveMigrationV2Document document = JsonUtility.FromJson<SaveMigrationV2Document>(json);
                    List<string> errors = SaveMigrationV2Validator.Validate(document);
                    if (errors.Count > 0)
                        return Failure(SaveMigrationV2Status.Invalid, string.Join("; ", errors.ToArray()));

                    return new SaveMigrationV2ReadResult {
                        Status = SaveMigrationV2Status.Loaded,
                        Source = SaveMigrationV2Source.V2,
                        Document = document,
                        Data = SaveMigrationV2Mapper.ToGameData(document)
                    };
                }
            }
            catch (Exception exception)
            {
                return Failure(SaveMigrationV2Status.Corrupt, exception.Message);
            }
        }

        private static SaveMigrationV2ReadResult Failure(SaveMigrationV2Status status, string error)
        {
            return new SaveMigrationV2ReadResult { Status = status, Error = error };
        }

        private static bool HasV2Magic(byte[] bytes)
        {
            if (bytes == null || bytes.Length < Magic.Length) return false;
            for (int i = 0; i < Magic.Length; i++)
                if (bytes[i] != Magic[i]) return false;
            return true;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            int difference = 0;
            for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }

        private static byte[] Sha256(byte[] payload)
        {
            using (SHA256 sha = SHA256.Create()) return sha.ComputeHash(payload);
        }

        private static string ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Save path is empty.", nameof(path));
            string fullPath = Path.GetFullPath(path);
            string root = Path.GetFullPath(Application.persistentDataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string testRoot = Path.IsPathRooted(BinarySerializable.DATA_DIRECTORY)
                ? Path.GetFullPath(BinarySerializable.DATA_DIRECTORY).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar
                : null;
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                && (testRoot == null || !fullPath.StartsWith(testRoot, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Save path must be inside persistentDataPath.");
            return fullPath;
        }
    }

    internal static class SaveMigrationV2Mapper
    {
        public static SaveMigrationV2Document FromGameData(GameData source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            SaveMigrationV2Document document = new SaveMigrationV2Document {
                gameUid = GameData.gameUid,
                cameraPosition = new SaveMigrationV2Vector3(source.camPosition),
                unlockedTechnologyNodeCodes = Copy(source.unlockedTechnologyNodeCodes),
                constructionReplayState = FromReplaySnapshot(source.constructionReplayState)
            };
            document.players = new SaveMigrationV2Player[source.players == null ? 0 : source.players.Length];
            for (int i = 0; i < document.players.Length; i++)
                document.players[i] = FromPlayer(source.players[i]);

            List<SaveMigrationV2ReplayCommand> commands = new List<SaveMigrationV2ReplayCommand>();
            if (source.constructionReplayCommands != null)
                for (int i = 0; i < source.constructionReplayCommands.Count; i++)
                    commands.Add(FromReplayCommand(source.constructionReplayCommands[i]));
            document.constructionReplayCommands = commands.ToArray();
            return document;
        }

        public static GameData ToGameData(SaveMigrationV2Document source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            GameData.gameUid = source.gameUid;
            GameData result = new GameData {
                camPosition = source.cameraPosition == null ? default(Vector3) : source.cameraPosition.ToUnity(),
                unlockedTechnologyNodeCodes = Copy(source.unlockedTechnologyNodeCodes),
                constructionReplayCommands = new List<ConstructionReplayCommand>(),
                constructionReplayState = ToReplaySnapshot(source.constructionReplayState)
            };
            result.players = new GamePlayerData[source.players == null ? 0 : source.players.Length];
            for (int i = 0; i < result.players.Length; i++) result.players[i] = ToPlayer(source.players[i]);
            if (source.constructionReplayCommands != null)
                for (int i = 0; i < source.constructionReplayCommands.Length; i++)
                    result.constructionReplayCommands.Add(ToReplayCommand(source.constructionReplayCommands[i]));
            return result;
        }

        private static SaveMigrationV2Player FromPlayer(GamePlayerData source)
        {
            if (source == null) return new SaveMigrationV2Player();
            SaveMigrationV2Player result = new SaveMigrationV2Player {
                resources = FromLegacyResources(source.resources),
                economy = FromEconomy(source.economy)
            };
            result.units = new SaveMigrationV2Unit[source.units == null ? 0 : source.units.Length];
            for (int i = 0; i < result.units.Length; i++) result.units[i] = FromUnit(source.units[i]);
            return result;
        }

        private static GamePlayerData ToPlayer(SaveMigrationV2Player source)
        {
            if (source == null) return new GamePlayerData { units = new GamePlayerUnitData[0], resources = new int[0] };
            GamePlayerData result = new GamePlayerData { resources = ToLegacyResources(source.resources), economy = ToEconomy(source.economy) };
            result.units = new GamePlayerUnitData[source.units == null ? 0 : source.units.Length];
            for (int i = 0; i < result.units.Length; i++) result.units[i] = ToUnit(source.units[i]);
            return result;
        }

        private static SaveMigrationV2Unit FromUnit(GamePlayerUnitData source)
        {
            if (source == null) return new SaveMigrationV2Unit();
            return new SaveMigrationV2Unit {
                unitType = source.unitType, code = source.code, uid = source.uid,
                position = new SaveMigrationV2Vector3(source.position), currentHealth = source.currentHealth,
                level = source.level, production = FromLegacyResources(source.production),
                attackDamage = source.attackDamage, attackRange = source.attackRange,
                constructionHP = source.constructionHP, buildingLifecycle = source.buildingLifecycle,
                constructorIds = Copy(source.constructorIds), construction = FromConstruction(source.construction),
                maintenance = FromMaintenance(source.maintenance), productionQueue = FromProductionQueue(source.productionQueue)
            };
        }

        private static GamePlayerUnitData ToUnit(SaveMigrationV2Unit source)
        {
            if (source == null) return new GamePlayerUnitData();
            return new GamePlayerUnitData {
                unitType = source.unitType, code = source.code, uid = source.uid,
                position = source.position == null ? default(Vector3) : source.position.ToUnity(),
                currentHealth = source.currentHealth, level = source.level, production = ToLegacyResources(source.production),
                attackDamage = source.attackDamage, attackRange = source.attackRange,
                constructionHP = source.constructionHP, buildingLifecycle = source.buildingLifecycle,
                constructorIds = Copy(source.constructorIds), construction = ToConstruction(source.construction),
                maintenance = ToMaintenance(source.maintenance), productionQueue = ToProductionQueue(source.productionQueue)
            };
        }

        private static SaveMigrationV2Resource[] FromLegacyResources(int[] values)
        {
            if (values == null) return new SaveMigrationV2Resource[0];
            List<SaveMigrationV2Resource> result = new List<SaveMigrationV2Resource>();
            for (int i = 0; i < values.Length; i++)
            {
                InGameResource code = i < Globals.GAME_RESOURCE_KEYS.Length ? Globals.GAME_RESOURCE_KEYS[i] : (InGameResource)i;
                result.Add(new SaveMigrationV2Resource { code = code.ToString(), amount = values[i] });
            }
            return result.ToArray();
        }

        private static int[] ToLegacyResources(SaveMigrationV2Resource[] values)
        {
            if (values == null) return new int[0];
            int[] result = new int[Globals.GAME_RESOURCE_KEYS.Length];
            for (int i = 0; i < values.Length; i++)
            {
                InGameResource code;
                if (!TryParse(values[i] == null ? null : values[i].code, out code)) continue;
                for (int j = 0; j < Globals.GAME_RESOURCE_KEYS.Length; j++)
                    if (Globals.GAME_RESOURCE_KEYS[j] == code) result[j] = values[i].amount;
            }
            return result;
        }

        private static SaveMigrationV2Economy FromEconomy(EconomyPlayerSaveData source)
        {
            if (source == null) return null;
            SaveMigrationV2Economy result = new SaveMigrationV2Economy {
                population = source.population, populationCapacity = source.populationCapacity
            };
            List<SaveMigrationV2EconomyResource> stockpile = new List<SaveMigrationV2EconomyResource>();
            if (source.stockpile != null)
                for (int i = 0; i < source.stockpile.Length; i++)
                    if (source.stockpile[i] != null)
                        stockpile.Add(new SaveMigrationV2EconomyResource {
                            code = ((EconomyResourceType)source.stockpile[i].resource).ToString(), amount = source.stockpile[i].amount
                        });
            result.stockpile = stockpile.ToArray();
            List<SaveMigrationV2Collector> collectors = new List<SaveMigrationV2Collector>();
            if (source.collectors != null)
                for (int i = 0; i < source.collectors.Length; i++)
                    if (source.collectors[i] != null)
                        collectors.Add(new SaveMigrationV2Collector {
                            collectorId = source.collectors[i].collectorId, ownerId = source.collectors[i].ownerId,
                            resource = ((EconomyResourceType)source.collectors[i].resource).ToString(),
                            ratePerSecond = source.collectors[i].ratePerSecond, carryCapacity = source.collectors[i].carryCapacity,
                            carried = source.collectors[i].carried
                        });
            result.collectors = collectors.ToArray();
            return result;
        }

        private static EconomyPlayerSaveData ToEconomy(SaveMigrationV2Economy source)
        {
            if (source == null) return null;
            EconomyPlayerSaveData result = new EconomyPlayerSaveData {
                population = source.population, populationCapacity = source.populationCapacity
            };
            List<EconomyResourceSaveData> stockpile = new List<EconomyResourceSaveData>();
            if (source.stockpile != null)
                for (int i = 0; i < source.stockpile.Length; i++)
                {
                    EconomyResourceType code;
                    if (source.stockpile[i] != null && TryParse(source.stockpile[i].code, out code))
                        stockpile.Add(new EconomyResourceSaveData((int)code, source.stockpile[i].amount));
                }
            result.stockpile = stockpile.ToArray();
            List<EconomyCollectorSaveData> collectors = new List<EconomyCollectorSaveData>();
            if (source.collectors != null)
                for (int i = 0; i < source.collectors.Length; i++)
                {
                    SaveMigrationV2Collector item = source.collectors[i];
                    EconomyResourceType code;
                    if (item != null && TryParse(item.resource, out code))
                        collectors.Add(new EconomyCollectorSaveData {
                            collectorId = item.collectorId, ownerId = item.ownerId, resource = (int)code,
                            ratePerSecond = item.ratePerSecond, carryCapacity = item.carryCapacity, carried = item.carried
                        });
                }
            result.collectors = collectors.ToArray();
            return result;
        }

        private static SaveMigrationV2Construction FromConstruction(BuildingConstructionSaveSnapshot source)
        {
            if (source == null) return null;
            return new SaveMigrationV2Construction {
                buildingCode = source.buildingCode, ownerId = source.ownerId, position = new SaveMigrationV2Vector3(source.position),
                constructionHP = source.constructionHP, maxHP = source.maxHP, lifecycle = (int)source.lifecycle,
                constructorIds = Copy(source.constructorIds), reservedCosts = FromCosts(source.reservedCosts),
                reservationId = source.reservationId, reservedPopulation = source.reservedPopulation,
                assetOrigin = (int)source.assetOrigin, contentRuntimeReady = source.contentRuntimeReady
            };
        }

        private static BuildingConstructionSaveSnapshot ToConstruction(SaveMigrationV2Construction source)
        {
            if (source == null || string.IsNullOrEmpty(source.buildingCode) && source.maxHP <= 0) return null;
            return new BuildingConstructionSaveSnapshot {
                buildingCode = source.buildingCode, ownerId = source.ownerId,
                position = source.position == null ? default(Vector3) : source.position.ToUnity(), constructionHP = source.constructionHP,
                maxHP = source.maxHP, lifecycle = (BuildingLifecycleState)source.lifecycle, constructorIds = Copy(source.constructorIds),
                reservedCosts = ToCosts(source.reservedCosts), reservationId = source.reservationId,
                reservedPopulation = source.reservedPopulation, assetOrigin = (BuildingContentAssetStage)source.assetOrigin,
                contentRuntimeReady = source.contentRuntimeReady
            };
        }

        private static SaveMigrationV2Maintenance FromMaintenance(BuildingMaintenanceRuntimeSnapshot source)
        {
            if (source == null) return null;
            return new SaveMigrationV2Maintenance {
                buildingId = source.buildingId, ownerId = source.ownerId, currentHealth = source.currentHealth,
                maxHealth = source.maxHealth, lifecycle = (int)source.lifecycle, operation = (int)source.operation,
                progress = source.progress, busy = source.busy, operationState = FromMaintenanceState(source.operationState)
            };
        }

        private static BuildingMaintenanceRuntimeSnapshot ToMaintenance(SaveMigrationV2Maintenance source)
        {
            // Unity JsonUtility may materialize an omitted reference as an
            // empty object. Treat that shape as the legacy/null state rather
            // than rejecting an otherwise valid save.
            if (source == null || source.maxHealth <= 0) return null;
            return new BuildingMaintenanceRuntimeSnapshot(
                source.buildingId, source.ownerId, source.currentHealth, source.maxHealth,
                (BuildingLifecycleState)source.lifecycle, (BuildingMaintenanceOperation)source.operation,
                source.progress, source.busy, ToMaintenanceState(source.operationState));
        }

        private static SaveMigrationV2MaintenanceState FromMaintenanceState(BuildingMaintenanceOperationState source)
        {
            if (source == null) return null;
            return new SaveMigrationV2MaintenanceState {
                operation = (int)source.operation, elapsed = source.elapsed, duration = source.duration,
                initialHealth = source.initialHealth, initialLifecycle = (int)source.initialLifecycle,
                reservedCost = FromCosts(source.reservedCost)
            };
        }

        private static BuildingMaintenanceOperationState ToMaintenanceState(SaveMigrationV2MaintenanceState source)
        {
            if (source == null) return null;
            return new BuildingMaintenanceOperationState {
                operation = (BuildingMaintenanceOperation)source.operation, elapsed = source.elapsed, duration = source.duration,
                initialHealth = source.initialHealth, initialLifecycle = (BuildingLifecycleState)source.initialLifecycle,
                reservedCost = ToCosts(source.reservedCost)
            };
        }

        private static SaveMigrationV2ProductionQueue FromProductionQueue(BuildingProductionQueueSnapshot source)
        {
            if (source == null) return null;
            SaveMigrationV2ProductionQueue result = new SaveMigrationV2ProductionQueue {
                buildingCode = source.buildingCode, ownerId = source.ownerId, era = (int)source.era,
                assetOrigin = (int)source.assetOrigin,
                unlockedTechnologyCodes = Copy(source.unlockedTechnologyCodes == null
                    ? null : source.unlockedTechnologyCodes.ToArray())
            };
            List<SaveMigrationV2ProductionOrder> orders = new List<SaveMigrationV2ProductionOrder>();
            if (source.orders != null)
                for (int i = 0; i < source.orders.Count; i++)
                    if (source.orders[i] != null)
                        orders.Add(new SaveMigrationV2ProductionOrder {
                            productCode = source.orders[i].productCode, duration = source.orders[i].duration,
                            elapsed = source.orders[i].elapsed, populationCost = source.orders[i].populationCost,
                            cost = FromLegacyResourceValues(source.orders[i].cost)
                        });
            result.orders = orders.ToArray();
            return result;
        }

        private static BuildingProductionQueueSnapshot ToProductionQueue(SaveMigrationV2ProductionQueue source)
        {
            if (source == null) return null;
            BuildingProductionQueueSnapshot result = new BuildingProductionQueueSnapshot {
                buildingCode = source.buildingCode, ownerId = source.ownerId, era = (BuildingEra)source.era,
                assetOrigin = (BuildingContentAssetStage)source.assetOrigin, unlockedTechnologyCodes = ToList(source.unlockedTechnologyCodes)
            };
            if (source.orders != null)
                for (int i = 0; i < source.orders.Length; i++)
                    if (source.orders[i] != null)
                        result.orders.Add(new BuildingProductionOrderSnapshot {
                            productCode = source.orders[i].productCode, duration = source.orders[i].duration,
                            elapsed = source.orders[i].elapsed, populationCost = source.orders[i].populationCost,
                            cost = ToLegacyResourceValues(source.orders[i].cost)
                        });
            return result;
        }

        private static SaveMigrationV2ReplayCommand FromReplayCommand(ConstructionReplayCommand source)
        {
            if (source == null) return new SaveMigrationV2ReplayCommand();
            return new SaveMigrationV2ReplayCommand {
                simulationTick = source.simulationTick, issuedAtMilliseconds = source.issuedAtMilliseconds,
                priority = source.priority, commandId = source.commandId, ownerId = source.ownerId, kind = (int)source.kind,
                targetCode = source.targetCode, reservationId = source.reservationId, quantity = source.quantity,
                populationCost = source.populationCost, costs = FromCosts(source.costs)
            };
        }

        private static ConstructionReplayCommand ToReplayCommand(SaveMigrationV2ReplayCommand source)
        {
            if (source == null) return new ConstructionReplayCommand();
            return new ConstructionReplayCommand {
                simulationTick = source.simulationTick, issuedAtMilliseconds = source.issuedAtMilliseconds,
                priority = source.priority, commandId = source.commandId, ownerId = source.ownerId,
                kind = (ConstructionReplayCommandKind)source.kind, targetCode = source.targetCode,
                reservationId = source.reservationId, quantity = source.quantity, populationCost = source.populationCost,
                costs = ToCosts(source.costs)
            };
        }

        private static SaveMigrationV2ReplaySnapshot FromReplaySnapshot(ConstructionReplaySnapshot source)
        {
            if (source == null) return null;
            return new SaveMigrationV2ReplaySnapshot {
                era = (int)source.era,
                activeBuildings = Copy(source.activeBuildings == null ? null : source.activeBuildings.ToArray()),
                producedUnits = Copy(source.producedUnits == null ? null : source.producedUnits.ToArray()),
                reservations = Copy(source.reservations == null ? null : source.reservations.ToArray()),
                fingerprint = source.fingerprint
            };
        }

        private static ConstructionReplaySnapshot ToReplaySnapshot(SaveMigrationV2ReplaySnapshot source)
        {
            if (source == null) return null;
            return new ConstructionReplaySnapshot {
                era = (BuildingEra)source.era, activeBuildings = ToList(source.activeBuildings),
                producedUnits = ToList(source.producedUnits), reservations = ToList(source.reservations), fingerprint = source.fingerprint
            };
        }

        private static SaveMigrationV2Cost[] FromCosts(List<BuildingMaintenanceCost> source)
        {
            if (source == null) return new SaveMigrationV2Cost[0];
            List<SaveMigrationV2Cost> result = new List<SaveMigrationV2Cost>();
            for (int i = 0; i < source.Count; i++)
                if (source[i] != null) result.Add(new SaveMigrationV2Cost { resource = source[i].resource.ToString(), amount = source[i].amount });
            return result.ToArray();
        }

        private static List<BuildingMaintenanceCost> ToCosts(SaveMigrationV2Cost[] source)
        {
            List<BuildingMaintenanceCost> result = new List<BuildingMaintenanceCost>();
            if (source == null) return result;
            for (int i = 0; i < source.Length; i++)
            {
                EconomyResourceType resource;
                if (source[i] != null && TryParse(source[i].resource, out resource))
                    result.Add(new BuildingMaintenanceCost(resource, source[i].amount));
            }
            return result;
        }

        private static SaveMigrationV2LegacyResource[] FromLegacyResourceValues(List<ResourceValue> source)
        {
            if (source == null) return new SaveMigrationV2LegacyResource[0];
            List<SaveMigrationV2LegacyResource> result = new List<SaveMigrationV2LegacyResource>();
            for (int i = 0; i < source.Count; i++)
                if (source[i] != null) result.Add(new SaveMigrationV2LegacyResource { code = source[i].code.ToString(), amount = source[i].amount });
            return result.ToArray();
        }

        private static List<ResourceValue> ToLegacyResourceValues(SaveMigrationV2LegacyResource[] source)
        {
            List<ResourceValue> result = new List<ResourceValue>();
            if (source == null) return result;
            for (int i = 0; i < source.Length; i++)
            {
                InGameResource code;
                if (source[i] != null && TryParse(source[i].code, out code)) result.Add(new ResourceValue(code, source[i].amount));
            }
            return result;
        }

        private static bool TryParse<T>(string value, out T parsed) where T : struct
        {
            return Enum.TryParse(value, true, out parsed) && Enum.IsDefined(typeof(T), parsed);
        }

        private static T[] Copy<T>(T[] source)
        {
            if (source == null) return new T[0];
            T[] result = new T[source.Length];
            Array.Copy(source, result, source.Length);
            return result;
        }

        private static List<T> ToList<T>(T[] source)
        {
            return source == null ? new List<T>() : new List<T>(source);
        }
    }

    internal static class SaveMigrationV2Validator
    {
        private const int MaxPlayers = 64;
        private const int MaxUnitsPerPlayer = 100000;
        private const int MaxCollectionItems = 100000;

        public static List<string> Validate(SaveMigrationV2Document document)
        {
            List<string> errors = new List<string>();
            if (document == null) { errors.Add("document ausente"); return errors; }
            if (document.schemaVersion != SaveMigrationV2Store.SchemaVersion) errors.Add("schemaVersion inválido");
            if (string.IsNullOrWhiteSpace(document.gameUid)) errors.Add("gameUid ausente");
            if (document.players == null || document.players.Length > MaxPlayers) errors.Add("players fora do limite");
            if (document.cameraPosition == null || !Finite(document.cameraPosition.x) || !Finite(document.cameraPosition.y) || !Finite(document.cameraPosition.z)) errors.Add("cameraPosition inválida");
            ValidateStrings(document.unlockedTechnologyNodeCodes, "unlockedTechnologyNodeCodes", errors);
            if (document.constructionReplayCommands == null || document.constructionReplayCommands.Length > MaxCollectionItems) errors.Add("replay commands fora do limite");
            if (document.constructionReplayCommands != null)
                for (int i = 0; i < document.constructionReplayCommands.Length; i++) ValidateReplay(document.constructionReplayCommands[i], "replay[" + i + "]", errors);
            ValidateReplaySnapshot(document.constructionReplayState, errors);
            if (document.players != null)
                for (int i = 0; i < document.players.Length; i++) ValidatePlayer(document.players[i], i, errors);
            return errors;
        }

        private static void ValidatePlayer(SaveMigrationV2Player player, int index, List<string> errors)
        {
            string prefix = "players[" + index + "]";
            if (player == null) { errors.Add(prefix + " ausente"); return; }
            ValidateResources(player.resources, prefix + ".resources", errors, false);
            if (player.units == null || player.units.Length > MaxUnitsPerPlayer) errors.Add(prefix + ".units fora do limite");
            if (player.economy != null) ValidateEconomy(player.economy, prefix + ".economy", errors);
            if (player.units != null)
                for (int i = 0; i < player.units.Length; i++) ValidateUnit(player.units[i], prefix + ".units[" + i + "]", errors);
        }

        private static void ValidateUnit(SaveMigrationV2Unit unit, string prefix, List<string> errors)
        {
            if (unit == null) { errors.Add(prefix + " ausente"); return; }
            RequireString(unit.code, prefix + ".code", errors);
            if (!Finite(unit.attackRange)) errors.Add(prefix + ".attackRange inválido");
            ValidateResources(unit.production, prefix + ".production", errors, false);
            ValidateStrings(unit.constructorIds, prefix + ".constructorIds", errors);
            ValidateConstruction(unit.construction, prefix + ".construction", errors);
            ValidateMaintenance(unit.maintenance, prefix + ".maintenance", errors);
            ValidateProductionQueue(unit.productionQueue, prefix + ".productionQueue", errors);
        }

        private static void ValidateResources(SaveMigrationV2Resource[] values, string prefix, List<string> errors, bool economy)
        {
            if (values == null || values.Length > MaxCollectionItems) { errors.Add(prefix + " fora do limite"); return; }
            if (values == null) return;
            for (int i = 0; i < values.Length; i++)
            {
                SaveMigrationV2Resource value = values[i];
                if (value == null || !TryEnum(value.code, out InGameResource resource)) errors.Add(prefix + "[" + i + "] recurso inválido");
                if (value != null && value.amount < 0) errors.Add(prefix + "[" + i + "] amount negativo");
            }
        }

        private static void ValidateEconomy(SaveMigrationV2Economy economy, string prefix, List<string> errors)
        {
            if (economy.population < 0 || economy.populationCapacity < 0 || economy.population > economy.populationCapacity)
                errors.Add(prefix + " população inválida");
            if (economy.stockpile == null || economy.stockpile.Length > MaxCollectionItems) errors.Add(prefix + ".stockpile fora do limite");
            if (economy.collectors == null || economy.collectors.Length > MaxCollectionItems) errors.Add(prefix + ".collectors fora do limite");
            if (economy.stockpile != null)
                for (int i = 0; i < economy.stockpile.Length; i++)
                    if (economy.stockpile[i] == null || !TryEnum(economy.stockpile[i].code, out EconomyResourceType resource) || economy.stockpile[i].amount < 0)
                        errors.Add(prefix + ".stockpile[" + i + "] inválido");
            if (economy.collectors != null)
                for (int i = 0; i < economy.collectors.Length; i++)
                {
                    SaveMigrationV2Collector item = economy.collectors[i];
                    EconomyResourceType resource;
                    if (item == null || !TryEnum(item == null ? null : item.resource, out resource)
                        || item.ownerId < 0 || !Finite(item.ratePerSecond) || !Finite(item.carryCapacity) || !Finite(item.carried)
                        || item.ratePerSecond < 0f || item.carryCapacity < 0f || item.carried < 0f || item.carried > item.carryCapacity)
                        errors.Add(prefix + ".collectors[" + i + "] inválido");
                }
        }

        private static void ValidateConstruction(SaveMigrationV2Construction value, string prefix, List<string> errors)
        {
            if (value == null) return;
            if (string.IsNullOrEmpty(value.buildingCode) && value.maxHP <= 0 && value.constructionHP == 0) return;
            RequireString(value.buildingCode, prefix + ".buildingCode", errors);
            if (value.ownerId < 0 || value.maxHP <= 0 || value.constructionHP < 0 || value.constructionHP > value.maxHP
                || !TryEnum(value.lifecycle, out BuildingLifecycleState lifecycle) || !TryEnum(value.assetOrigin, out BuildingContentAssetStage stage))
                errors.Add(prefix + " estado inválido");
            ValidateStrings(value.constructorIds, prefix + ".constructorIds", errors);
            ValidateCosts(value.reservedCosts, prefix + ".reservedCosts", errors);
        }

        private static void ValidateMaintenance(SaveMigrationV2Maintenance value, string prefix, List<string> errors)
        {
            if (value == null) return;
            // JsonUtility/BinaryFormatter can materialize an omitted legacy
            // reference as an empty object. It represents "no maintenance"
            // and must remain compatible with older saves.
            if (value.maxHealth <= 0 && value.currentHealth == 0
                && string.IsNullOrEmpty(value.buildingId)) return;
            if (value.ownerId < 0 || value.maxHealth <= 0 || value.currentHealth < 0 || value.currentHealth > value.maxHealth
                || !TryEnum(value.lifecycle, out BuildingLifecycleState lifecycle) || !TryEnum(value.operation, out BuildingMaintenanceOperation operation)
                || !Finite(value.progress) || value.progress < 0f || value.progress > 1f)
                errors.Add(prefix + " estado inválido");
            if (value.operationState != null)
            {
                SaveMigrationV2MaintenanceState state = value.operationState;
                if (state.duration <= 0f && state.elapsed == 0f && state.initialHealth == 0
                    && (state.reservedCost == null || state.reservedCost.Length == 0)) return;
                if (!TryEnum(state.operation, out BuildingMaintenanceOperation stateOperation)
                    || !TryEnum(state.initialLifecycle, out BuildingLifecycleState initialLifecycle)
                    || !Finite(state.elapsed) || !Finite(state.duration) || state.elapsed < 0f || state.duration <= 0f || state.elapsed > state.duration
                    || state.initialHealth < 0)
                    errors.Add(prefix + ".operationState inválido");
                ValidateCosts(state.reservedCost, prefix + ".operationState.reservedCost", errors);
            }
        }

        private static void ValidateProductionQueue(SaveMigrationV2ProductionQueue value, string prefix, List<string> errors)
        {
            if (value == null) return;
            RequireString(value.buildingCode, prefix + ".buildingCode", errors);
            if (value.ownerId < 0 || !TryEnum(value.era, out BuildingEra era) || !TryEnum(value.assetOrigin, out BuildingContentAssetStage stage))
                errors.Add(prefix + " estado inválido");
            ValidateStrings(value.unlockedTechnologyCodes, prefix + ".unlockedTechnologyCodes", errors);
            if (value.orders == null || value.orders.Length > MaxCollectionItems) errors.Add(prefix + ".orders fora do limite");
            if (value.orders != null)
                for (int i = 0; i < value.orders.Length; i++)
                {
                    SaveMigrationV2ProductionOrder item = value.orders[i];
                    if (item == null || string.IsNullOrWhiteSpace(item.productCode) || !Finite(item.duration) || !Finite(item.elapsed)
                        || item.duration <= 0f || item.elapsed < 0f || item.elapsed > item.duration || item.populationCost < 0)
                        errors.Add(prefix + ".orders[" + i + "] inválido");
                    if (item != null) ValidateLegacyResources(item.cost, prefix + ".orders[" + i + "].cost", errors);
                }
        }

        private static void ValidateLegacyResources(SaveMigrationV2LegacyResource[] values, string prefix, List<string> errors)
        {
            if (values == null || values.Length > MaxCollectionItems) { errors.Add(prefix + " fora do limite"); return; }
            for (int i = 0; i < values.Length; i++)
            {
                InGameResource code;
                if (values[i] == null || !TryEnum(values[i].code, out code) || values[i].amount < 0) errors.Add(prefix + "[" + i + "] inválido");
            }
        }

        private static void ValidateCosts(SaveMigrationV2Cost[] values, string prefix, List<string> errors)
        {
            if (values == null || values.Length > MaxCollectionItems) { errors.Add(prefix + " fora do limite"); return; }
            if (values == null) return;
            for (int i = 0; i < values.Length; i++)
            {
                EconomyResourceType resource;
                if (values[i] == null || !TryEnum(values[i].resource, out resource) || values[i].amount < 0) errors.Add(prefix + "[" + i + "] inválido");
            }
        }

        private static void ValidateReplay(SaveMigrationV2ReplayCommand value, string prefix, List<string> errors)
        {
            if (value == null || value.simulationTick < 0 || value.issuedAtMilliseconds < 0 || value.ownerId < 0
                || string.IsNullOrWhiteSpace(value.commandId) || value.quantity <= 0 || value.populationCost < 0
                || !TryEnum(value.kind, out ConstructionReplayCommandKind kind)) errors.Add(prefix + " inválido");
            if (value != null) ValidateCosts(value.costs, prefix + ".costs", errors);
        }

        private static void ValidateReplaySnapshot(SaveMigrationV2ReplaySnapshot value, List<string> errors)
        {
            if (value == null) return;
            if (!TryEnum(value.era, out BuildingEra era)) errors.Add("constructionReplayState.era inválida");
            ValidateStrings(value.activeBuildings, "constructionReplayState.activeBuildings", errors);
            ValidateStrings(value.producedUnits, "constructionReplayState.producedUnits", errors);
            ValidateStrings(value.reservations, "constructionReplayState.reservations", errors);
        }

        private static void ValidateStrings(string[] values, string prefix, List<string> errors)
        {
            if (values == null || values.Length > MaxCollectionItems) { errors.Add(prefix + " fora do limite"); return; }
            for (int i = 0; i < values.Length; i++) if (values[i] != null && values[i].Length > 1024) errors.Add(prefix + "[" + i + "] longa demais");
        }

        private static void RequireString(string value, string prefix, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 1024) errors.Add(prefix + " inválida");
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        private static bool TryEnum<T>(int value, out T parsed) where T : struct
        {
            parsed = (T)Enum.ToObject(typeof(T), value);
            return Enum.IsDefined(typeof(T), parsed);
        }

        private static bool TryEnum<T>(string value, out T parsed) where T : struct
        {
            return Enum.TryParse(value, true, out parsed) && Enum.IsDefined(typeof(T), parsed);
        }
    }
}
