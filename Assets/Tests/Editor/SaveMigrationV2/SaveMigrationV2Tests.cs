using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using EraImperial.SaveMigrationV2;

public sealed class SaveMigrationV2Tests
{
    private string _path;

    [SetUp]
    public void SetUp()
    {
        _path = Path.Combine(Application.persistentDataPath, "Data_Dev", "Tests", "SaveMigrationV2Tests.data");
        Cleanup();
    }

    [TearDown]
    public void TearDown() { Cleanup(); }

    [Test]
    public void V2WritesExplicitEnvelopeAndRoundTripsNamedState()
    {
        GameData source = CreateGameData();
        GameData.gameUid = "save-v2-roundtrip";

        SaveMigrationV2Store.Save(_path, source);
        byte[] bytes = File.ReadAllBytes(_path);
        string raw = System.Text.Encoding.UTF8.GetString(bytes);
        SaveMigrationV2ReadResult loaded = SaveMigrationV2Store.Load(_path);

        Assert.That(bytes[0], Is.EqualTo((byte)'R'));
        Assert.That(raw.Contains("BinaryFormatter"), Is.False);
        Assert.That(loaded.Succeeded, Is.True, loaded.Error);
        Assert.That(loaded.Source, Is.EqualTo(SaveMigrationV2Source.V2));
        Assert.That(loaded.Data.players[0].resources[0], Is.EqualTo(123));
        Assert.That(loaded.Data.players[0].units[0].productionQueue.orders[0].productCode, Is.EqualTo("spearman"));
        Assert.That(loaded.Data.players[0].units[0].construction.reservedPopulation, Is.EqualTo(2));
        Assert.That(loaded.Data.constructionReplayState.fingerprint, Is.EqualTo("ABC123"));
    }

    [Test]
    public void GameDataSaveAndLoadUsesV2WithoutLegacyWriter()
    {
        const string uid = "save-v2-gamedata-api";
        GameData.gameUid = uid;
        string gamePath = Path.Combine(
            Application.persistentDataPath, BinarySerializable.DATA_DIRECTORY,
            "Games", uid, GameData.DATA_FILE_NAME);
        if (File.Exists(gamePath)) File.Delete(gamePath);
        if (File.Exists(gamePath + ".bak")) File.Delete(gamePath + ".bak");

        GameData.Save(CreateGameData());
        byte[] bytes = File.ReadAllBytes(gamePath);
        GameData loaded = GameData.Load();

        Assert.That(bytes[0], Is.EqualTo((byte)'R'));
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded.players[0].units[0].uid, Is.EqualTo("unit-1"));
        Assert.That(File.Exists(gamePath + ".legacy"), Is.False);

        if (File.Exists(gamePath)) File.Delete(gamePath);
        if (File.Exists(gamePath + ".bak")) File.Delete(gamePath + ".bak");
        string directory = Path.GetDirectoryName(gamePath);
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [Test]
    public void V2RejectsTruncationAndHashTamperingWithoutLegacyFallback()
    {
        GameData.gameUid = "save-v2-integrity";
        SaveMigrationV2Store.Save(_path, CreateGameData());
        byte[] bytes = File.ReadAllBytes(_path);
        bytes[bytes.Length - 1] ^= 0x01;
        File.WriteAllBytes(_path, bytes);

        SaveMigrationV2ReadResult result = SaveMigrationV2Store.Load(_path);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Status, Is.EqualTo(SaveMigrationV2Status.Corrupt));
        Assert.That(result.Source, Is.EqualTo(SaveMigrationV2Source.None));
        Assert.That(result.Error, Does.Contain("Hash"));
    }

    [Test]
    public void LegacyReadIsCompatibleAndMigrationKeepsOriginalSeparate()
    {
        GameData.gameUid = "save-v2-legacy";
        GameData legacyData = CreateGameData();
        string destination = _path + ".v2";
        string legacyBytesPath = _path + ".legacy";
        BinarySerializable.Save(legacyBytesPath, legacyData);
        byte[] original = File.ReadAllBytes(legacyBytesPath);

        SaveMigrationV2ReadResult read = SaveMigrationV2Store.Load(legacyBytesPath);
        SaveMigrationV2ReadResult migration = SaveMigrationV2Store.MigrateLegacyToV2(legacyBytesPath, destination);
        SaveMigrationV2ReadResult converted = SaveMigrationV2Store.Load(destination);

        Assert.That(read.Succeeded, Is.True, read.Error);
        Assert.That(read.Source, Is.EqualTo(SaveMigrationV2Source.Legacy));
        Assert.That(migration.Succeeded, Is.True, migration.Error);
        Assert.That(File.ReadAllBytes(legacyBytesPath), Is.EqualTo(original));
        Assert.That(converted.Source, Is.EqualTo(SaveMigrationV2Source.V2), converted.Error);
        Assert.That(converted.Data.players[0].units[0].uid, Is.EqualTo("unit-1"));
    }

    [Test]
    public void RawLegacyPayloadIsAcceptedByTheSameIsolatedFallback()
    {
        GameData.gameUid = "save-v2-raw";
        string wrappedPath = _path + ".wrapped";
        string rawPath = _path + ".raw";
        BinarySerializable.Save(wrappedPath, CreateGameData());

        byte[] wrapped = File.ReadAllBytes(wrappedPath);
        int payloadLength = BitConverter.ToInt32(wrapped, 12);
        byte[] rawPayload = new byte[payloadLength];
        Buffer.BlockCopy(wrapped, 16, rawPayload, 0, payloadLength);
        File.WriteAllBytes(rawPath, rawPayload);

        SaveMigrationV2ReadResult result = SaveMigrationV2Store.Load(rawPath);

        Assert.That(result.Succeeded, Is.True, result.Error);
        Assert.That(result.Source, Is.EqualTo(SaveMigrationV2Source.Legacy));
        Assert.That(result.Data.players[0].units[0].code, Is.EqualTo("barracks"));
    }

    [Test]
    public void V2FallsBackToBakWhenPrimaryIsInterrupted()
    {
        GameData.gameUid = "save-v2-backup";
        SaveMigrationV2Store.Save(_path, CreateGameData());
        SaveMigrationV2Store.Save(_path, CreateGameData());
        Assert.That(File.Exists(_path + ".bak"), Is.True);
        byte[] primary = File.ReadAllBytes(_path);
        primary[primary.Length - 1] ^= 0x01;
        File.WriteAllBytes(_path, primary);

        SaveMigrationV2ReadResult result = SaveMigrationV2Store.Load(_path);

        Assert.That(result.Succeeded, Is.True, result.Error);
        Assert.That(result.Status, Is.EqualTo(SaveMigrationV2Status.RecoveredFromBackup));
        Assert.That(result.Source, Is.EqualTo(SaveMigrationV2Source.V2));
    }

    private static GameData CreateGameData()
    {
        return new GameData {
            camPosition = new Vector3(1f, 2f, 3f),
            players = new[] {
                new GamePlayerData {
                    resources = new[] { 123, 45, 6 },
                    economy = new EconomyPlayerSaveData {
                        population = 4, populationCapacity = 10,
                        stockpile = new[] { new EconomyResourceSaveData((int)EconomyResourceType.Wood, 77) },
                        collectors = new EconomyCollectorSaveData[0]
                    },
                    units = new[] {
                        new GamePlayerUnitData {
                            unitType = "building", code = "barracks", uid = "unit-1",
                            position = new Vector3(4f, 5f, 6f), currentHealth = 90, level = 2,
                            production = new[] { 9, 8, 7 }, attackDamage = 10, attackRange = 3.5f,
                            constructionHP = 90, buildingLifecycle = (int)BuildingLifecycleState.UnderConstruction,
                            constructorIds = new[] { "worker-1" },
                            construction = new BuildingConstructionSaveSnapshot {
                                buildingCode = "barracks", ownerId = 0, position = new Vector3(4f, 5f, 6f),
                                constructionHP = 90, maxHP = 100, lifecycle = BuildingLifecycleState.UnderConstruction,
                                constructorIds = new[] { "worker-1" },
                                reservedCosts = new List<BuildingMaintenanceCost> { new BuildingMaintenanceCost(EconomyResourceType.Wood, 30) },
                                reservedPopulation = 2, assetOrigin = BuildingContentAssetStage.RuntimeReady, contentRuntimeReady = true
                            },
                            productionQueue = new BuildingProductionQueueSnapshot {
                                buildingCode = "barracks", ownerId = 0, era = BuildingEra.Dark,
                                assetOrigin = BuildingContentAssetStage.RuntimeReady,
                                orders = new List<BuildingProductionOrderSnapshot> {
                                    new BuildingProductionOrderSnapshot {
                                        productCode = "spearman", duration = 15f, elapsed = 3f, populationCost = 1,
                                        cost = new List<ResourceValue> { new ResourceValue(InGameResource.Wood, 30) }
                                    }
                                }
                            }
                        }
                    }
                }
            },
            unlockedTechnologyNodeCodes = new[] { "root", "fletching" },
            constructionReplayCommands = new List<ConstructionReplayCommand> {
                new ConstructionReplayCommand {
                    simulationTick = 1, issuedAtMilliseconds = 2, priority = 1, commandId = "cmd-1", ownerId = 0,
                    kind = ConstructionReplayCommandKind.ConstructBuilding, targetCode = "barracks", quantity = 1
                }
            },
            constructionReplayState = new ConstructionReplaySnapshot {
                era = BuildingEra.Dark, activeBuildings = new List<string> { "barracks" },
                producedUnits = new List<string>(), reservations = new List<string>(), fingerprint = "ABC123"
            }
        };
    }

    private void Cleanup()
    {
        string[] paths = {
            _path, _path + ".bak", _path + ".tmp", _path + ".v2", _path + ".legacy",
            _path + ".wrapped", _path + ".raw"
        };
        for (int i = 0; i < paths.Length; i++) if (File.Exists(paths[i])) File.Delete(paths[i]);
    }
}
