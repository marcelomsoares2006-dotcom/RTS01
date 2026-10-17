using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DTO de schema v2 para GameData. Os campos são deliberadamente explícitos;
/// nenhuma referência de cena, prefab, GameObject ou Transform é persistida.
/// Os contratos aninhados existentes são dados puros e mantêm seus campos
/// atuais para que a conversão não descarte construção, manutenção, produção,
/// economia ou replay.
/// </summary>
[Serializable]
public sealed class SaveMigrationV2GameDataDocument
{
    public int schemaVersion;
    public string gameVersion;
    public string source;
    public string gameUid;
    public GamePlayerData[] players;
    public Vector3 camPosition;
    public string[] unlockedTechnologyNodeCodes;
    public List<ConstructionReplayCommand> constructionReplayCommands =
        new List<ConstructionReplayCommand>();
    public ConstructionReplaySnapshot constructionReplayState;

    public static SaveMigrationV2GameDataDocument From(GameData data, string origin)
    {
        return new SaveMigrationV2GameDataDocument
        {
            schemaVersion = SaveMigrationV2.SchemaVersion,
            gameVersion = Application.version,
            source = string.IsNullOrEmpty(origin) ? "game-data-v2" : origin,
            gameUid = GameData.gameUid,
            players = data.players,
            camPosition = data.camPosition,
            unlockedTechnologyNodeCodes = data.unlockedTechnologyNodeCodes,
            constructionReplayCommands = data.constructionReplayCommands,
            constructionReplayState = data.constructionReplayState
        };
    }

    public GameData ToGameData()
    {
        if (schemaVersion != SaveMigrationV2.SchemaVersion)
            throw new InvalidOperationException("Unsupported GameData schema version.");
        if (players == null)
            throw new InvalidOperationException("GameData v2 requires the players array.");
        if (players.Length > 10000)
            throw new InvalidOperationException("GameData v2 contains too many players.");

        GameData data = new GameData
        {
            players = players,
            camPosition = camPosition,
            unlockedTechnologyNodeCodes = unlockedTechnologyNodeCodes,
            constructionReplayCommands = constructionReplayCommands
                ?? new List<ConstructionReplayCommand>(),
            constructionReplayState = constructionReplayState
        };
        if (!string.IsNullOrEmpty(gameUid)) GameData.gameUid = gameUid;
        Validate(data);
        return data;
    }

    private static void Validate(GameData data)
    {
        if (float.IsNaN(data.camPosition.x) || float.IsInfinity(data.camPosition.x)
            || float.IsNaN(data.camPosition.y) || float.IsInfinity(data.camPosition.y)
            || float.IsNaN(data.camPosition.z) || float.IsInfinity(data.camPosition.z))
            throw new InvalidOperationException("GameData v2 contains a non-finite camera position.");

        for (int p = 0; p < data.players.Length; p++)
        {
            if (data.players[p] == null)
                throw new InvalidOperationException("GameData v2 contains a null player.");
            if (data.players[p].units == null) data.players[p].units = new GamePlayerUnitData[0];
            if (data.players[p].units.Length > 100000)
                throw new InvalidOperationException("GameData v2 contains too many units.");
            for (int u = 0; u < data.players[p].units.Length; u++)
            {
                GamePlayerUnitData unit = data.players[p].units[u];
                if (unit == null) throw new InvalidOperationException("GameData v2 contains a null unit.");
                if (float.IsNaN(unit.attackRange) || float.IsInfinity(unit.attackRange))
                    throw new InvalidOperationException("GameData v2 contains a non-finite attack range.");
            }
        }
    }
}
