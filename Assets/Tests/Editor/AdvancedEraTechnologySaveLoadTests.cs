using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using EraImperial.Units.Advanced;

/// <summary>
/// Prova dedicada do contrato de progressão: a política de age-up exige a
/// tecnologia, o replay aplica a próxima era e o save/load restaura a era
/// avançada. O teste não carrega nem altera assets visuais.
/// </summary>
public sealed class AdvancedEraTechnologySaveLoadTests
{
    private string _gameUid;
    private string _saveFolder;

    [SetUp]
    public void SetUp()
    {
        _gameUid = "advanced-era-proof-" + Guid.NewGuid().ToString("N");
        GameData.gameUid = _gameUid;
        _saveFolder = GameData.GetFolderPath();
        ConstructionReplayRuntime.Restore(new List<ConstructionReplayCommand>(), null);
    }

    [TearDown]
    public void TearDown()
    {
        ConstructionReplayRuntime.Restore(new List<ConstructionReplayCommand>(), null);
        if (!string.IsNullOrEmpty(_saveFolder) && Directory.Exists(_saveFolder))
            Directory.Delete(_saveFolder, true);
    }

    [Test]
    public void TechnologyTreeAgeUpThenSaveLoadRestoresAdvancedEra()
    {
        StrategicPlanningState planning = PlanningState(BuildingEra.Dark);

        AdvanceThroughTechnology(
            planning,
            new TechnologyDefinition { code = "age_up_feudal_technology", minimumEra = BuildingEra.Dark },
            new EraTransitionRequirement(BuildingEra.Feudal),
            1,
            "age-feudal");
        Assert.AreEqual(ImperialEra.Classical, AdvancedEraState.Current);

        AdvanceThroughTechnology(
            planning,
            new TechnologyDefinition {
                code = "age_up_castle_technology",
                minimumEra = BuildingEra.Feudal,
                prerequisiteTechnologyCodes = new List<string> { "age_up_feudal_technology" }
            },
            new EraTransitionRequirement(BuildingEra.Castle),
            2,
            "age-castle");
        Assert.AreEqual(ImperialEra.Medieval, AdvancedEraState.Current);

        GameData saved = new GameData {
            unlockedTechnologyNodeCodes = new[] {
                "root", "age_up_feudal_technology", "age_up_castle_technology"
            },
            constructionReplayCommands = ConstructionReplayRuntime.ExportCommands(),
            constructionReplayState = ConstructionReplayRuntime.State
        };
        GameData.Save(saved);

        ConstructionReplayRuntime.Restore(new List<ConstructionReplayCommand>(), null);
        AdvancedEraState.Reset();
        Assert.AreEqual(ImperialEra.Ancient, AdvancedEraState.Current);

        GameData loaded = GameData.Load();
        Assert.IsNotNull(loaded);
        Assert.AreEqual(2, loaded.constructionReplayCommands.Count);
        Assert.IsNotNull(loaded.constructionReplayState);
        Assert.AreEqual(BuildingEra.Castle, loaded.constructionReplayState.era);
        Assert.Contains("age_up_castle_technology", loaded.unlockedTechnologyNodeCodes);

        Assert.AreEqual(
            ConstructionReplayResult.Accepted,
            ConstructionReplayRuntime.Restore(
                loaded.constructionReplayCommands,
                loaded.constructionReplayState));
        Assert.AreEqual(BuildingEra.Castle, ConstructionReplayRuntime.State.era);
        Assert.AreEqual(ImperialEra.Medieval, AdvancedEraState.Current);
    }

    private static void AdvanceThroughTechnology(
        StrategicPlanningState planning,
        TechnologyDefinition technology,
        EraTransitionRequirement ageUp,
        long tick,
        string eraCommandId)
    {
        ageUp.requiredTechnologyCodes.Add(technology.code);

        StrategicActionIntent intent;
        string rationale;
        Assert.IsTrue(
            AgeUpTechnologyPlanningRules.TryCreateIntent(
                planning, ageUp, new[] { technology }, out intent, out rationale),
            rationale);
        Assert.AreEqual(StrategicActionKind.ResearchTechnology, intent.action);
        Assert.AreEqual(technology.code, intent.targetCode);
        Assert.IsTrue(TechnologyProgressionRules.TryUnlock(planning, technology));

        Assert.IsTrue(
            AgeUpTechnologyPlanningRules.TryCreateIntent(
                planning, ageUp, new[] { technology }, out intent, out rationale),
            rationale);
        Assert.AreEqual(StrategicActionKind.AdvanceEra, intent.action);
        Assert.AreEqual("age:" + ageUp.targetEra.ToString().ToLowerInvariant(), intent.targetCode);

        Assert.AreEqual(
            ConstructionReplayResult.Accepted,
            ConstructionReplayRuntime.TryAdvanceEra(
                planning.ownerId, ageUp.targetEra, tick, tick, 100, eraCommandId));
        planning.era = ageUp.targetEra;
    }

    private static StrategicPlanningState PlanningState(BuildingEra era)
    {
        return new StrategicPlanningState {
            ownerId = 0,
            era = era,
            population = 10,
            buildings = new List<StrategicBuildingObservation> {
                new StrategicBuildingObservation {
                    ownerId = 0,
                    code = "town_center",
                    lifecycle = BuildingLifecycleState.Operational
                }
            },
            unlockedTechnologyCodes = new List<string>()
        };
    }
}
