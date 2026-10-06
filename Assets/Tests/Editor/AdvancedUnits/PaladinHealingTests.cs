using System.Collections.Generic;
using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Paladin;
using NUnit.Framework;
using UnityEngine;

namespace EraImperial.AdvancedUnits.Tests
{
    /// <summary>3. Cura em área do paladino: aliados no raio, nunca inimigos, cooldown e limite por alvo.</summary>
    public class PaladinHealingTests
    {
        private const int PaladinId = 100;
        private const int AllyOwner = 0;
        private const int EnemyOwner = 1;

        private ContractSet _set;
        private PaladinEraTier _ancient;
        private PaladinEraTier _imperial;

        [SetUp]
        public void SetUp()
        {
            _set = new ContractSet();
            _ancient = _set.Paladin.tiers[0];
            _imperial = _set.Paladin.tiers[3];
        }

        [TearDown] public void TearDown() { _set.Dispose(); }

        private static HealCandidate Wounded(int id, int owner, Vector3 pos, int health = 10, int max = 100, bool alive = true)
        {
            return new HealCandidate(id, owner, pos, health, max, alive);
        }

        private static List<HealResult> Pulse(PaladinEraTier tier, IReadOnlyList<HealCandidate> group, float now = 0f, TargetHealLedger ledger = null, bool self = false)
        {
            return PaladinHealing.ComputePulse(tier, PaladinId, AllyOwner, Vector3.zero, group, ledger ?? new TargetHealLedger(10f), now, self);
        }

        [Test]
        public void HealsAlliesInsideRadius_AndOnlyThem()
        {
            float r = _ancient.healRadius;
            HealCandidate[] group =
            {
                Wounded(1, AllyOwner, new Vector3(1f, 0f, 0f)),
                Wounded(2, AllyOwner, new Vector3(0f, 0f, r - 0.1f)),
                Wounded(3, AllyOwner, new Vector3(r + 0.1f, 0f, 0f)),
                Wounded(4, AllyOwner, new Vector3(50f, 0f, 50f))
            };
            List<HealResult> healed = Pulse(_ancient, group);
            Assert.That(healed.Select(h => h.Id), Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(healed.All(h => h.Amount == _ancient.healAmount), Is.True);
        }

        [Test]
        public void NeverHealsEnemies_EvenAdjacentAndWounded()
        {
            HealCandidate[] group =
            {
                Wounded(1, EnemyOwner, new Vector3(0.5f, 0f, 0f)),
                Wounded(2, 2, new Vector3(0f, 0f, 1f)),
                Wounded(3, AllyOwner, new Vector3(1f, 0f, 1f))
            };
            List<HealResult> healed = Pulse(_imperial, group);
            Assert.That(healed.Select(h => h.Id), Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void RadiusBoundaryIsInclusive_AndHeightIsIgnored()
        {
            HealCandidate[] group =
            {
                Wounded(1, AllyOwner, new Vector3(_ancient.healRadius, 0f, 0f)),
                Wounded(2, AllyOwner, new Vector3(0f, 25f, 1f))
            };
            Assert.That(Pulse(_ancient, group).Select(h => h.Id), Is.EquivalentTo(new[] { 1, 2 }));
        }

        [Test]
        public void SkipsDeadFullHealthAndSelf_UnlessSelfHealingIsEnabled()
        {
            HealCandidate[] group =
            {
                Wounded(1, AllyOwner, Vector3.one, alive: false),
                Wounded(2, AllyOwner, Vector3.one, health: 100, max: 100),
                Wounded(PaladinId, AllyOwner, Vector3.zero),
                Wounded(3, AllyOwner, Vector3.one)
            };
            Assert.That(Pulse(_ancient, group).Select(h => h.Id), Is.EqualTo(new[] { 3 }));
            Assert.That(Pulse(_ancient, group, self: true).Select(h => h.Id), Is.EquivalentTo(new[] { PaladinId, 3 }));
        }

        [Test]
        public void HealNeverExceedsMissingHealth()
        {
            HealCandidate[] group = { Wounded(1, AllyOwner, Vector3.one, health: 98, max: 100) };
            Assert.That(Pulse(_imperial, group).Single().Amount, Is.EqualTo(2));
        }

        [Test]
        public void HealCooldown_BlocksEarlyPulsesAndDoesNotRestartWhenBlocked()
        {
            PaladinHealController controller = new PaladinHealController(10f);
            HealCandidate[] group = { Wounded(1, AllyOwner, Vector3.one, health: 1, max: 1000) };

            Assert.That(controller.TryPulse(_ancient, PaladinId, AllyOwner, Vector3.zero, group, 0f), Is.Not.Null, "primeiro pulso");
            Assert.That(controller.TryPulse(_ancient, PaladinId, AllyOwner, Vector3.zero, group, _ancient.healCooldown - 0.01f), Is.Null, "ainda em cooldown");
            Assert.That(controller.CanPulse(_ancient, _ancient.healCooldown), Is.True, "o pulso bloqueado não reiniciou a contagem");
            Assert.That(controller.TryPulse(_ancient, PaladinId, AllyOwner, Vector3.zero, group, _ancient.healCooldown), Is.Not.Null, "cooldown concluído");
        }

        [Test]
        public void PerTargetCap_StopsHealingUntilTheWindowExpires()
        {
            TargetHealLedger ledger = new TargetHealLedger(10f);
            HealCandidate[] group = { Wounded(1, AllyOwner, Vector3.one, health: 1, max: 1000) };
            int cap = _ancient.maxHealPerTarget;
            int total = 0;

            // pulsos a cada healCooldown até esgotar o limite dentro da janela de 10 s
            for (float t = 0f; t < 10f; t += _ancient.healCooldown)
                total += Pulse(_ancient, group, t, ledger).Sum(h => h.Amount);

            Assert.That(total, Is.LessThanOrEqualTo(cap));
            Assert.That(total, Is.EqualTo(Mathf.Min(cap, _ancient.healAmount * Mathf.CeilToInt(10f / _ancient.healCooldown))));

            // cap atingido de fato com um pulso extra forçado ainda na janela
            int extra = 0;
            for (int i = 0; i < 20; i++) extra += Pulse(_ancient, group, 9.9f, ledger).Sum(h => h.Amount);
            Assert.That(total + extra, Is.EqualTo(cap), "o alvo recebe exatamente o limite na janela");
            Assert.That(Pulse(_ancient, group, 9.9f, ledger), Is.Empty);

            // nova janela: volta a curar
            Assert.That(Pulse(_ancient, group, 20.1f, ledger).Single().Amount, Is.EqualTo(_ancient.healAmount));
        }

        [Test]
        public void PerTargetCap_IsIndependentBetweenTargets()
        {
            TargetHealLedger ledger = new TargetHealLedger(10f);
            HealCandidate[] one = { Wounded(1, AllyOwner, Vector3.one, 1, 1000) };
            for (int i = 0; i < 50; i++) Pulse(_ancient, one, 0f, ledger);
            Assert.That(Pulse(_ancient, one, 0f, ledger), Is.Empty);

            HealCandidate[] other = { Wounded(2, AllyOwner, Vector3.one, 1, 1000) };
            Assert.That(Pulse(_ancient, other, 0f, ledger).Single().Amount, Is.EqualTo(_ancient.healAmount));
        }

        [Test]
        public void LaterEras_HealMoreAndReachFarther()
        {
            HealCandidate[] group =
            {
                Wounded(1, AllyOwner, new Vector3(1f, 0f, 0f), 1, 1000),
                Wounded(2, AllyOwner, new Vector3(_ancient.healRadius + 0.5f, 0f, 0f), 1, 1000)
            };
            List<HealResult> ancient = Pulse(_ancient, group);
            List<HealResult> imperial = Pulse(_imperial, group);

            Assert.That(ancient.Select(h => h.Id), Is.EqualTo(new[] { 1 }), "o aliado distante fica fora do raio antigo");
            Assert.That(imperial.Select(h => h.Id), Is.EquivalentTo(new[] { 1, 2 }), "e dentro do raio imperial");
            Assert.That(imperial.First().Amount, Is.GreaterThan(ancient.First().Amount));
        }

        [Test]
        public void PaladinFightsMelee_WithMeleeRangeInEveryEra()
        {
            foreach (PaladinEraTier tier in _set.Paladin.tiers)
            {
                Assert.That(tier.attackRange, Is.LessThanOrEqualTo(AdvancedUnitRules.MeleeRangeCeiling));
                Assert.That(tier.attackDamage, Is.GreaterThan(0));
            }

            _set.Paladin.tiers[0].attackRange = 6f;
            Assert.That(_set.Paladin.Validate().ToString(), Does.Contain("corpo a corpo"));
        }
    }
}
