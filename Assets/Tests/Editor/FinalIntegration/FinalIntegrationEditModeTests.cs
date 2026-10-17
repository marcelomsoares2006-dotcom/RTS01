using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Crossbow;
using EraImperial.Units.Mage;
using EraImperial.Units.Paladin;
using EraImperial.Units.Siege;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Matriz EditMode isolada da cadeia produção militar -> unidade avançada ->
/// combate/cerco -> save/load -> quatro eras. Os testes usam contratos e dados
/// em memória; o catálogo é lido de Resources e nunca é salvo.
/// </summary>
[TestFixture]
public sealed class FinalIntegrationEditModeTests
{
    [SetUp]
    public void SetUp()
    {
        ConstructionReplayRuntime.Restore(new List<ConstructionReplayCommand>(), null);
        AdvancedEraState.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        ConstructionReplayRuntime.Restore(new List<ConstructionReplayCommand>(), null);
        AdvancedEraState.Reset();
    }

    [Test]
    public void MilitaryCatalog_DeclaresRuntimeReadyProducersAndAdvancedOrSiegeProducts()
    {
        BuildingContentCatalog catalog = FinalIntegrationFixtures.LoadMilitaryCatalog();
        Assert.That(catalog, Is.Not.Null, "catálogo de conteúdo militar não foi carregado");

        AssertProducer(catalog, "barracks", BuildingEra.Dark,
            "spearman", "combat_mage", "paladin_healer");
        AssertProducer(catalog, "archery_range", BuildingEra.Feudal, "archer", "crossbow");
        AssertProducer(catalog, "stable", BuildingEra.Feudal, "horseman");
        AssertProducer(catalog, "siege_workshop", BuildingEra.Castle,
            "battering_ram", "catapult");

        Assert.That(BuildingContentProgressionRules.ValidateEraContracts(catalog), Is.Empty,
            "o grafo de eras do catálogo militar possui violações");
    }

    [Test]
    public void MilitaryProduction_ResolvesQueuesAndCompletesAdvancedAndSiegeProducts()
    {
        BuildingContentCatalog catalog = FinalIntegrationFixtures.LoadMilitaryCatalog();
        Assert.That(catalog, Is.Not.Null);

        ProductionCase[] cases =
        {
            new ProductionCase("barracks", "combat_mage", BuildingEra.Dark),
            new ProductionCase("barracks", "paladin_healer", BuildingEra.Dark),
            new ProductionCase("archery_range", "crossbow", BuildingEra.Feudal, "fletching"),
            new ProductionCase("siege_workshop", "catapult", BuildingEra.Castle)
        };

        foreach (ProductionCase testCase in cases)
        {
            StrategicPlanningState state = FinalIntegrationFixtures.PlanningState(
                testCase.era, testCase.technologies);
            BuildingProductionEntry entry;
            BuildingProductionCatalogResult resolveResult;
            Assert.That(
                BuildingProductionCatalogRules.TryCreateLegacyQueueEntry(
                    catalog, testCase.buildingCode, testCase.productCode, state,
                    out entry, out resolveResult),
                Is.True, $"{testCase.buildingCode}/{testCase.productCode}: {resolveResult}");

            EconomyLedger economy = FinalIntegrationFixtures.RichEconomy();
            PopulationLedger population = FinalIntegrationFixtures.Population();
            BuildingProductionQueue queue = new BuildingProductionQueue(5);
            Assert.That(queue.TryEnqueue(entry, economy, population),
                Is.EqualTo(BuildingProductionQueueResult.Accepted), testCase.productCode);

            List<string> completed = queue.Advance(entry.productionTime);
            CollectionAssert.AreEqual(new[] { testCase.productCode }, completed,
                testCase.productCode + " não completou a fila FIFO");
            Assert.That(queue.Count, Is.EqualTo(0), testCase.productCode);
            Assert.That(population.current, Is.EqualTo(entry.populationCost), testCase.productCode);
        }
    }

    [Test]
    public void AdvancedUnits_ExposeAllEras_AndCombatAndSiegeResolveFromProducedCodes()
    {
        ProjectileContract mageBolt = CombatMageDefaults.CreateProjectile();
        ProjectileContract boulder = CatapultDefaults.CreateProjectile();
        ProjectileContract crossbowBolt = CrossbowDefaults.CreateProjectile();
        CombatMageContract mage = CombatMageDefaults.Create(mageBolt);
        PaladinHealerContract paladin = PaladinHealerDefaults.Create();
        CatapultContract catapult = CatapultDefaults.Create(boulder);
        CrossbowContract crossbow = CrossbowDefaults.Create(crossbowBolt);

        try
        {
            AdvancedUnitContract[] units = { mage, paladin, catapult, crossbow };
            foreach (AdvancedUnitContract unit in units)
            {
                Assert.That(unit.Validate().IsValid, Is.True, unit.code);
                Assert.That(unit.GetTiers().Select(tier => tier.Era), Is.EqualTo(
                    new[] { ImperialEra.Ancient, ImperialEra.Classical,
                        ImperialEra.Medieval, ImperialEra.Imperial }), unit.code);
            }

            UnitDataFields mageImperial = mage.BuildUnitDataFields(ImperialEra.Imperial);
            CombatCooldownState cooldown = new CombatCooldownState();
            CombatProjectilePayload projectile;
            Assert.That(CombatAttackResolver.TryCreateProjectile(
                Vector3.zero, Vector3.right * mageImperial.attackRange,
                mageImperial.attackRange, mageImperial.attackDamage,
                mageImperial.attackRate, 0f, cooldown, out projectile), Is.True);
            CombatImpact impact = CombatImpact.FromProjectile(projectile);
            Assert.That(CombatDamageProfile.ResolveDamage(impact.RawDamage, 5),
                Is.EqualTo(mageImperial.attackDamage - 5));

            CatapultEraTier siegeTier = catapult.tiers[(int)ImperialEra.Medieval];
            Assert.That(catapult.Projectile.damageType, Is.EqualTo(DamageType.Siege));
            Assert.That(SiegeAttackProfile.CanFire(siegeTier, 12f, siegeTier.attackCooldown), Is.True);
            List<SiegeHit> hits = SiegeAttackProfile.ResolveImpact(
                siegeTier, Vector3.zero, 1, 0,
                new[]
                {
                    new SiegeTarget(1, 1, Vector3.zero),
                    new SiegeTarget(2, 1, Vector3.right * 2f),
                    new SiegeTarget(3, 0, Vector3.right)
                });
            Assert.That(hits.Single(hit => hit.Id == 1).IsDirect, Is.True);
            Assert.That(hits.Single(hit => hit.Id == 1).Damage, Is.EqualTo(siegeTier.attackDamage));
            Assert.That(hits.Any(hit => hit.Id == 2), Is.True);
            Assert.That(hits.Any(hit => hit.Id == 3), Is.False, "cerco atingiu aliado");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mage);
            UnityEngine.Object.DestroyImmediate(paladin);
            UnityEngine.Object.DestroyImmediate(catapult);
            UnityEngine.Object.DestroyImmediate(crossbow);
            UnityEngine.Object.DestroyImmediate(mageBolt);
            UnityEngine.Object.DestroyImmediate(boulder);
            UnityEngine.Object.DestroyImmediate(crossbowBolt);
        }
    }

    [Test]
    public void IntegratedMilitarySiege_SaveLoadRestoresQueueReplayAndImperialEra()
    {
        BuildingContentCatalog catalog = FinalIntegrationFixtures.LoadMilitaryCatalog();
        Assert.That(catalog, Is.Not.Null);
        StrategicPlanningState state = FinalIntegrationFixtures.PlanningState(BuildingEra.Imperial);
        FinalIntegrationFixtures.AddOperationalBuilding(state, "siege_workshop");

        BuildingProductionEntry entry;
        BuildingProductionCatalogResult result;
        Assert.That(BuildingProductionCatalogRules.TryCreateLegacyQueueEntry(
            catalog, "siege_workshop", "catapult", state,
            out entry, out result), Is.True, result.ToString());

        EconomyLedger productionEconomy = FinalIntegrationFixtures.RichEconomy();
        PopulationLedger productionPopulation = FinalIntegrationFixtures.Population();
        BuildingProductionQueue queue = new BuildingProductionQueue(3);
        Assert.That(queue.TryEnqueue(entry, productionEconomy, productionPopulation),
            Is.EqualTo(BuildingProductionQueueResult.Accepted));
        queue.Advance(entry.productionTime * 0.4f);

        BuildingProductionQueueSnapshot queueSnapshot;
        Assert.That(BuildingProductionSaveRules.Capture(
            catalog, state, queue, "siege_workshop", out queueSnapshot),
            Is.EqualTo(BuildingProductionSnapshotResult.Captured));

        ConstructionReplayLog log = new ConstructionReplayLog();
        Assert.That(log.TryAppend(FinalIntegrationFixtures.Command(
            "town-center", 0, ConstructionReplayCommandKind.ConstructBuilding, "town_center")),
            Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(log.TryAppend(FinalIntegrationFixtures.Command(
            "age-classical", 1, ConstructionReplayCommandKind.AdvanceEra, "age:feudal")),
            Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(log.TryAppend(FinalIntegrationFixtures.Command(
            "age-medieval", 2, ConstructionReplayCommandKind.AdvanceEra, "age:castle")),
            Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(log.TryAppend(FinalIntegrationFixtures.Command(
            "age-imperial", 3, ConstructionReplayCommandKind.AdvanceEra, "age:imperial")),
            Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(log.TryAppend(FinalIntegrationFixtures.Command(
            "siege-workshop", 4, ConstructionReplayCommandKind.ConstructBuilding,
            "siege_workshop")), Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(log.TryAppend(FinalIntegrationFixtures.Command(
            "produce-catapult", 5, ConstructionReplayCommandKind.ProduceUnit,
            "catapult", 1, entry.populationCost,
            FinalIntegrationFixtures.ToReplayCosts(entry.cost).ToArray())),
            Is.EqualTo(ConstructionReplayResult.Accepted));

        ConstructionReplaySnapshot replaySnapshot;
        Assert.That(ConstructionReplayExecutor.Replay(
            log, FinalIntegrationFixtures.RichEconomy(),
            FinalIntegrationFixtures.Population(), null, out replaySnapshot),
            Is.EqualTo(ConstructionReplayResult.Accepted));
        Assert.That(replaySnapshot.era, Is.EqualTo(BuildingEra.Imperial));
        CollectionAssert.Contains(replaySnapshot.producedUnits, "catapult");

        string uid = "final-integration-" + Guid.NewGuid().ToString("N");
        string path = Path.Combine(Application.persistentDataPath, "FinalIntegration", uid + ".data");
        try
        {
            FinalIntegrationSavePayload saved = new FinalIntegrationSavePayload
            {
                queue = queueSnapshot,
                commands = new List<ConstructionReplayCommand>(log.Commands),
                replay = replaySnapshot
            };
            SaveFileStore.Save(path, saved);

            FinalIntegrationSavePayload loaded = SaveFileStore.Load<FinalIntegrationSavePayload>(path);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.queue.orders[0].productCode, Is.EqualTo("catapult"));
            Assert.That(loaded.queue.orders[0].elapsed,
                Is.EqualTo(queueSnapshot.orders[0].elapsed).Within(0.001f));

            ConstructionReplayRuntime.Restore(new List<ConstructionReplayCommand>(), null);
            AdvancedEraState.Reset();
            Assert.That(ConstructionReplayRuntime.Restore(loaded.commands, loaded.replay),
                Is.EqualTo(ConstructionReplayResult.Accepted));
            Assert.That(AdvancedEraState.Current, Is.EqualTo(ImperialEra.Imperial));

            BuildingProductionQueue restoredQueue = new BuildingProductionQueue(3);
            Assert.That(BuildingProductionSaveRules.Restore(
                catalog, state, restoredQueue, loaded.queue),
                Is.EqualTo(BuildingProductionSnapshotResult.Restored));
            Assert.That(restoredQueue.Advance(restoredQueue.Peek.Remaining),
                Is.EqualTo(new[] { "catapult" }));
        }
        finally
        {
            DeleteSaveArtifacts(path);
        }
    }

    private static void AssertProducer(
        BuildingContentCatalog catalog,
        string buildingCode,
        BuildingEra era,
        params string[] products)
    {
        BuildingContentSpec entry = catalog.Find(buildingCode);
        Assert.That(entry, Is.Not.Null, buildingCode);
        Assert.That(entry.IsRuntimeReady, Is.True, buildingCode + " não está RuntimeReady");
        Assert.That(entry.minimumEra, Is.EqualTo(era), buildingCode);
        Assert.That(entry.supportsProduction, Is.True, buildingCode);
        CollectionAssert.AreEquivalent(
            products, entry.production.Select(item => item.productCode).ToArray(), buildingCode);
    }

    private static void DeleteSaveArtifacts(string path)
    {
        foreach (string candidate in new[] { path, path + ".tmp", path + ".bak" })
            if (File.Exists(candidate)) File.Delete(candidate);
        string folder = Path.GetDirectoryName(path);
        if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
            Directory.Delete(folder);
    }

    private sealed class ProductionCase
    {
        public readonly string buildingCode;
        public readonly string productCode;
        public readonly BuildingEra era;
        public readonly string[] technologies;

        public ProductionCase(string buildingCode, string productCode, BuildingEra era,
            params string[] technologies)
        {
            this.buildingCode = buildingCode;
            this.productCode = productCode;
            this.era = era;
            this.technologies = technologies;
        }
    }

    [Serializable]
    private sealed class FinalIntegrationSavePayload
    {
        public BuildingProductionQueueSnapshot queue;
        public List<ConstructionReplayCommand> commands;
        public ConstructionReplaySnapshot replay;
    }
}
