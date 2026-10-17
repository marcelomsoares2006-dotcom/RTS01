using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EraImperial.SaveMigrationV2;
using EraImperial.Units.Advanced;
using EraImperial.Units.Crossbow;
using EraImperial.Units.Mage;
using EraImperial.Units.Paladin;
using EraImperial.Units.Siege;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Ciclo 4 isolado de combate e cerco.
///
/// Exercita somente contratos e regras determinísticas já existentes. Não cria
/// prefabs, colliders, projéteis GameObject, alvos BuildingManager, UI,
/// animações ou medições de FPS. Esses gates são reportados explicitamente como
/// BLOCKED, nunca como PASS.
/// </summary>
[InitializeOnLoad]
public static class CombatSiegePlayModeHarness
{
    private const string RunningKey = "EraImperial.CombatSiegeCycle4.Running";
    private const string FinishedKey = "EraImperial.CombatSiegeCycle4.Finished";
    private static bool _finished;

    static CombatSiegePlayModeHarness()
    {
        EditorApplication.update += Tick;
    }

    /// <summary>Executa o ciclo em uma cena vazia e encerra com código 2 por gates bloqueados conhecidos.</summary>
    public static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("CombatSiegePlayModeHarness.Run exige o Editor fora do PlayMode.");

        _finished = false;
        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(FinishedKey, false);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("EventManager").AddComponent<EventManager>();
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (_finished || !SessionState.GetBool(RunningKey, false)
            || !EditorApplication.isPlaying || EditorApplication.isCompiling)
            return;

        _finished = true;
        try
        {
            Require(Application.isPlaying, "o runner não entrou em PlayMode");
            CombatSiegeCycle4Result result = RunScenario();
            if (result.Blocked.Count > 0)
            {
                Debug.LogWarning("COMBAT_SIEGE_CYCLE4_BLOCKED: "
                    + string.Join(" | ", result.Blocked.ToArray()));
                SessionState.SetBool(FinishedKey, false);
                StopAndExit(2);
                return;
            }

            Debug.Log("COMBAT_SIEGE_CYCLE4_PASS: contratos de combate, cerco e SaveMigrationV2");
            SessionState.SetBool(FinishedKey, true);
            StopAndExit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError("COMBAT_SIEGE_CYCLE4_FAIL: " + exception);
            SessionState.SetBool(FinishedKey, false);
            StopAndExit(1);
        }
    }

    private static CombatSiegeCycle4Result RunScenario()
    {
        CombatSiegeCycle4Result result = new CombatSiegeCycle4Result();
        ContractFixture contracts = new ContractFixture();
        try
        {
            ValidateContracts(contracts);
            ValidateCatapult(contracts.Catapult.tiers);
            ValidateCrossbow(contracts.Crossbow.tiers);
            ValidateMage(contracts.Mage.tiers);
            ValidatePaladin(contracts.Paladin.tiers);
            ValidateSaveV2(result);

            // O escopo pedido separa regras de cena do contrato. Estes gates
            // ficam bloqueados até existir uma execução PlayMode real com os
            // objetos e sistemas correspondentes.
            result.Blocked.Add("produção real da catapulta via prefab não é exercitada");
            result.Blocked.Add("collider e camada UNIT_MASK não são exercitados");
            result.Blocked.Add("projétil GameObject balístico não é instanciado");
            result.Blocked.Add("alvo BuildingManager e destruição estrutural não são exercitados");
            result.Blocked.Add("balestra/mago/paladino não são produzidos em prefab durante uma partida");
            result.Blocked.Add("UI, animações e feedback audiovisual não são exercitados");
            result.Blocked.Add("FPS e orçamento de performance não são medidos neste harness");
        }
        finally
        {
            contracts.Dispose();
        }
        return result;
    }

    private static void ValidateContracts(ContractFixture c)
    {
        Require(c.Catapult.Validate().IsValid, "contrato da catapulta inválido: " + c.Catapult.Validate());
        Require(c.Crossbow.Validate().IsValid, "contrato da balestra inválido: " + c.Crossbow.Validate());
        Require(c.Mage.Validate().IsValid, "contrato do mago inválido: " + c.Mage.Validate());
        Require(c.Paladin.Validate().IsValid, "contrato do paladino inválido: " + c.Paladin.Validate());
        Require(c.Boulder.Validate().IsValid && c.Bolt.Validate().IsValid && c.MageBolt.Validate().IsValid,
            "um contrato de projétil de combate é inválido");
        Require(c.Catapult.GetTiers().Count == 4 && c.Crossbow.GetTiers().Count == 4
            && c.Mage.GetTiers().Count == 4 && c.Paladin.GetTiers().Count == 4,
            "alguma unidade não possui as quatro eras");
    }

    private static void ValidateCatapult(CatapultEraTier[] tiers)
    {
        Require(tiers != null && tiers.Length == 4, "catapulta sem quatro tiers");
        for (int i = 0; i < tiers.Length; i++)
        {
            CatapultEraTier t = tiers[i];
            float middle = (t.minimumRange + t.attackRange) * 0.5f;
            Require(!SiegeAttackProfile.CanFire(t, t.minimumRange - 0.01f, t.attackCooldown), t.era + " aceitou distância menor que o mínimo");
            Require(SiegeAttackProfile.CanFire(t, t.minimumRange, t.attackCooldown), t.era + " rejeitou alcance mínimo válido");
            Require(SiegeAttackProfile.CanFire(t, middle, t.attackCooldown), t.era + " rejeitou alcance intermediário");
            Require(SiegeAttackProfile.CanFire(t, t.attackRange, t.attackCooldown), t.era + " rejeitou alcance máximo válido");
            Require(!SiegeAttackProfile.CanFire(t, t.attackRange + 0.01f, t.attackCooldown), t.era + " aceitou distância maior que o máximo");
            Require(!SiegeAttackProfile.CanFire(t, middle, t.attackCooldown - 0.01f), t.era + " ignorou cooldown");
        }

        CatapultEraTier medieval = tiers[2];
        Vector3 point = new Vector3(10f, 0f, 10f);
        SiegeTarget[] targets =
        {
            new SiegeTarget(1, 1, point),
            new SiegeTarget(2, 1, point),
            new SiegeTarget(3, 1, point + new Vector3(medieval.blastRadius * 0.5f, 0f, 0f)),
            new SiegeTarget(4, 1, point + new Vector3(medieval.blastRadius, 0f, 0f)),
            new SiegeTarget(5, 0, point),
            new SiegeTarget(6, 1, point, false)
        };
        List<SiegeHit> hits = SiegeAttackProfile.ResolveImpact(medieval, point, 1, 0, targets);
        Require(hits.Any(h => h.Id == 1 && h.IsDirect && h.Damage == medieval.attackDamage), "impacto direto da catapulta incorreto");
        Require(hits.Any(h => h.Id == 2 && !h.IsDirect && h.Damage == medieval.radialDamage), "dano radial central incorreto");
        Require(hits.Any(h => h.Id == 3 && !h.IsDirect && h.Damage == Mathf.RoundToInt(medieval.radialDamage * 0.5f)), "falloff radial incorreto");
        Require(!hits.Any(h => h.Id == 4) && !hits.Any(h => h.Id == 5) && !hits.Any(h => h.Id == 6),
            "catapulta atingiu borda, aliado ou unidade morta");
        Require(hits.Count(h => h.Id == 1) == 1, "alvo direto recebeu dano radial duplicado");

        List<SiegeHit> friendlyFire = SiegeAttackProfile.ResolveImpact(medieval, point, -1, 0, targets, true);
        Require(friendlyFire.Any(h => h.Id == 5), "friendlyFire explícito não permitiu aliado");
        Require(!friendlyFire.Any(h => h.Id == 6), "friendlyFire atingiu unidade morta");
        Require(tiers[3].attackDamage > tiers[0].attackDamage
            && tiers[3].attackRange > tiers[0].attackRange
            && tiers[3].blastRadius > tiers[0].blastRadius
            && tiers[3].attackCooldown < tiers[0].attackCooldown,
            "progressão da catapulta entre eras inválida");
    }

    private static void ValidateCrossbow(CrossbowEraTier[] tiers)
    {
        Require(tiers != null && tiers.Length == 4, "balestra sem quatro tiers");
        for (int i = 0; i < tiers.Length; i++)
        {
            CrossbowEraTier t = tiers[i];
            Require(CrossbowAttackProfile.CanFire(t, t.attackRange, t.attackCooldown), t.era + " balestra rejeitou alcance válido");
            Require(!CrossbowAttackProfile.CanFire(t, t.attackRange + 0.01f, t.attackCooldown), t.era + " balestra aceitou alcance excessivo");
            Require(!CrossbowAttackProfile.CanFire(t, 5f, t.attackCooldown - 0.01f), t.era + " balestra ignorou cooldown");
            Require(CrossbowAttackProfile.ComputeDamage(t, 10f) <= t.attackDamage
                && CrossbowAttackProfile.ComputeDamage(t, 10f) >= 1,
                t.era + " dano perfurante inválido");
        }
        Require(tiers[3].armorPenetration > tiers[0].armorPenetration, "progressão de penetração da balestra inválida");
    }

    private static void ValidateMage(MageEraTier[] tiers)
    {
        Require(tiers != null && tiers.Length == 4, "mago sem quatro tiers");
        for (int i = 0; i < tiers.Length; i++)
        {
            MageEraTier t = tiers[i];
            Require(MageAttackProfile.CanCast(t, t.attackRange, t.attackCooldown), t.era + " mago rejeitou alcance válido");
            Require(!MageAttackProfile.CanCast(t, t.attackRange + 0.01f, t.attackCooldown), t.era + " mago aceitou alcance excessivo");
            Require(!MageAttackProfile.CanCast(t, 3f, t.attackCooldown - 0.01f), t.era + " mago ignorou cooldown");
            Require(MageAttackProfile.ComputeDamage(t, 0f) == t.attackDamage
                && MageAttackProfile.ComputeDamage(t, 1f) == 0,
                t.era + " resistência mágica não foi aplicada");
        }
    }

    private static void ValidatePaladin(PaladinEraTier[] tiers)
    {
        Require(tiers != null && tiers.Length == 4, "paladino sem quatro tiers");
        PaladinEraTier t = tiers[0];
        HealCandidate[] candidates =
        {
            new HealCandidate(1, 0, new Vector3(1f, 0f, 0f), 10, 100),
            new HealCandidate(2, 0, new Vector3(t.healRadius + 0.1f, 0f, 0f), 10, 100),
            new HealCandidate(3, 1, Vector3.zero, 10, 100),
            new HealCandidate(4, 0, Vector3.zero, 100, 100),
            new HealCandidate(5, 0, Vector3.zero, 10, 100, false)
        };
        List<HealResult> pulse = PaladinHealing.ComputePulse(t, 99, 0, Vector3.zero, candidates, new TargetHealLedger(10f), 0f);
        Require(pulse.Count == 1 && pulse[0].Id == 1 && pulse[0].Amount == t.healAmount,
            "paladino não curou exatamente o aliado válido");
        PaladinHealController controller = new PaladinHealController(10f);
        Require(controller.TryPulse(t, 99, 0, Vector3.zero, candidates, 0f) != null, "primeiro pulso do paladino falhou");
        Require(controller.TryPulse(t, 99, 0, Vector3.zero, candidates, t.healCooldown - 0.01f) == null,
            "paladino ignorou cooldown");
        Require(controller.TryPulse(t, 99, 0, Vector3.zero, candidates, t.healCooldown) != null,
            "paladino não voltou após cooldown");
        Require(tiers[3].healAmount > tiers[0].healAmount && tiers[3].healRadius > tiers[0].healRadius,
            "progressão de cura do paladino inválida");
    }

    private static void ValidateSaveV2(CombatSiegeCycle4Result result)
    {
        string path = Path.Combine(Application.persistentDataPath, "Data_Dev", "Tests",
            "combat-siege-cycle4-" + Guid.NewGuid().ToString("N") + ".data");
        try
        {
            GameData source = new GameData {
                camPosition = Vector3.zero,
                players = new GamePlayerData[0],
                unlockedTechnologyNodeCodes = new[] { "cycle4_siege" },
                constructionReplayCommands = new List<ConstructionReplayCommand>(),
                constructionReplayState = null
            };
            SaveMigrationV2Store.Save(path, source);
            SaveMigrationV2ReadResult loaded = SaveMigrationV2Store.Load(path);
            Require(loaded != null && loaded.Succeeded, "SaveMigrationV2 não carregou o save de combate");
            Require(loaded.Source == SaveMigrationV2Source.V2, "save de combate não retornou fonte V2");
            Require(loaded.Data != null && loaded.Data.unlockedTechnologyNodeCodes != null
                && loaded.Data.unlockedTechnologyNodeCodes.Contains("cycle4_siege"),
                "save V2 perdeu o desbloqueio de cerco");
        }
        finally
        {
            DeleteIfExists(path);
            DeleteIfExists(path + ".bak");
            DeleteIfExists(path + ".tmp");
        }
    }

    private sealed class ContractFixture
    {
        public readonly ProjectileContract Boulder;
        public readonly ProjectileContract Bolt;
        public readonly ProjectileContract MageBolt;
        public readonly CatapultContract Catapult;
        public readonly CrossbowContract Crossbow;
        public readonly CombatMageContract Mage;
        public readonly PaladinHealerContract Paladin;

        public ContractFixture()
        {
            Boulder = CatapultDefaults.CreateProjectile();
            Bolt = CrossbowDefaults.CreateProjectile();
            MageBolt = CombatMageDefaults.CreateProjectile();
            Catapult = CatapultDefaults.Create(Boulder);
            Crossbow = CrossbowDefaults.Create(Bolt);
            Mage = CombatMageDefaults.Create(MageBolt);
            Paladin = PaladinHealerDefaults.Create();
        }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(Catapult);
            UnityEngine.Object.DestroyImmediate(Crossbow);
            UnityEngine.Object.DestroyImmediate(Mage);
            UnityEngine.Object.DestroyImmediate(Paladin);
            UnityEngine.Object.DestroyImmediate(Boulder);
            UnityEngine.Object.DestroyImmediate(Bolt);
            UnityEngine.Object.DestroyImmediate(MageBolt);
        }
    }

    private sealed class CombatSiegeCycle4Result
    {
        public readonly List<string> Blocked = new List<string>();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private static void StopAndExit(int code)
    {
        SessionState.SetBool(RunningKey, false);
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(code);
    }
}
