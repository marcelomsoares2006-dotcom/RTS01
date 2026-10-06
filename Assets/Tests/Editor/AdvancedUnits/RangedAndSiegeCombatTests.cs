using System.Collections.Generic;
using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Crossbow;
using EraImperial.Units.Mage;
using EraImperial.Units.Siege;
using NUnit.Framework;
using UnityEngine;

namespace EraImperial.AdvancedUnits.Tests
{
    /// <summary>4. Alcance, cooldown e dano da catapulta e da balestra (e do mago).</summary>
    public class RangedAndSiegeCombatTests
    {
        private ContractSet _set;

        [SetUp] public void SetUp() { _set = new ContractSet(); }
        [TearDown] public void TearDown() { _set.Dispose(); }

        // ---- Catapulta ----

        [Test]
        public void Catapult_FiresOnlyBetweenMinimumAndMaximumRange_BoundariesIncluded()
        {
            foreach (CatapultEraTier t in _set.Catapult.tiers)
            {
                float ready = t.attackCooldown;
                Assert.That(SiegeAttackProfile.CanFire(t, t.minimumRange - 0.01f, ready), Is.False, $"{t.era} perto demais");
                Assert.That(SiegeAttackProfile.CanFire(t, t.minimumRange, ready), Is.True, $"{t.era} no mínimo");
                Assert.That(SiegeAttackProfile.CanFire(t, (t.minimumRange + t.attackRange) / 2f, ready), Is.True, $"{t.era} no meio");
                Assert.That(SiegeAttackProfile.CanFire(t, t.attackRange, ready), Is.True, $"{t.era} no máximo");
                Assert.That(SiegeAttackProfile.CanFire(t, t.attackRange + 0.01f, ready), Is.False, $"{t.era} longe demais");
                Assert.That(SiegeAttackProfile.CanFire(t, -1f, ready), Is.False, $"{t.era} distância inválida");
            }
        }

        [Test]
        public void Catapult_ReloadTimeGatesEveryShot()
        {
            CatapultEraTier t = _set.Catapult.tiers[0];
            float d = (t.minimumRange + t.attackRange) / 2f;
            Assert.That(SiegeAttackProfile.CanFire(t, d, t.attackCooldown - 0.01f), Is.False);
            Assert.That(SiegeAttackProfile.CanFire(t, d, t.attackCooldown), Is.True);
            Assert.That(SiegeAttackProfile.CanFire(t, d, 0f), Is.False);
        }

        [Test]
        public void Catapult_HasLongerReachAndStrongerHitsThanEveryOtherAdvancedUnit()
        {
            CatapultEraTier cat = _set.Catapult.tiers[0];
            Assert.That(cat.attackRange, Is.GreaterThan(_set.Crossbow.tiers[3].attackRange), "alcance mínimo da era mais fraca já supera a balestra imperial");
            Assert.That(cat.attackDamage, Is.GreaterThan(_set.Mage.tiers[0].attackDamage));
            Assert.That(cat.attackCooldown, Is.GreaterThan(_set.Crossbow.tiers[0].attackCooldown), "artilharia pesada recarrega mais devagar");
        }

        [Test]
        public void Catapult_ImpactHitsTheDirectTargetAndRadialFallsOffLinearly()
        {
            CatapultEraTier t = _set.Catapult.tiers[2]; // Medieval: radial 45, raio 4
            Vector3 point = new Vector3(10f, 0f, 10f);
            SiegeTarget[] targets =
            {
                new SiegeTarget(1, 1, point),                                   // alvo direto
                new SiegeTarget(2, 1, point + new Vector3(0f, 0f, 0f)),         // no centro, não-direto
                new SiegeTarget(3, 1, point + new Vector3(t.blastRadius / 2f, 0f, 0f)),
                new SiegeTarget(4, 1, point + new Vector3(t.blastRadius, 0f, 0f)),       // na borda: zero
                new SiegeTarget(5, 1, point + new Vector3(t.blastRadius + 1f, 0f, 0f))   // fora
            };

            List<SiegeHit> hits = SiegeAttackProfile.ResolveImpact(t, point, 1, 0, targets);

            Assert.That(hits.Single(h => h.Id == 1).Damage, Is.EqualTo(t.attackDamage));
            Assert.That(hits.Single(h => h.Id == 1).IsDirect, Is.True);
            Assert.That(hits.Single(h => h.Id == 2).Damage, Is.EqualTo(t.radialDamage), "centro da explosão");
            Assert.That(hits.Single(h => h.Id == 3).Damage, Is.EqualTo(Mathf.RoundToInt(t.radialDamage / 2f)), "metade do raio");
            Assert.That(hits.Any(h => h.Id == 4), Is.False, "borda não causa dano");
            Assert.That(hits.Any(h => h.Id == 5), Is.False, "fora do raio");
            Assert.That(hits.Count(h => h.Id == 1), Is.EqualTo(1), "o alvo direto não leva o radial por cima");
        }

        [Test]
        public void Catapult_ImpactSparesAlliesAndTheDeadUnlessFriendlyFireIsOn()
        {
            CatapultEraTier t = _set.Catapult.tiers[0];
            Vector3 point = Vector3.zero;
            SiegeTarget[] targets =
            {
                new SiegeTarget(1, 0, point),                    // aliado do atirador
                new SiegeTarget(2, 1, point, isAlive: false),    // inimigo morto
                new SiegeTarget(3, 1, point)                     // inimigo vivo
            };

            Assert.That(SiegeAttackProfile.ResolveImpact(t, point, -1, 0, targets).Select(h => h.Id), Is.EqualTo(new[] { 3 }));
            Assert.That(SiegeAttackProfile.ResolveImpact(t, point, -1, 0, targets, friendlyFire: true).Select(h => h.Id), Is.EquivalentTo(new[] { 1, 3 }));
        }

        [Test]
        public void Catapult_LaterErasHitHarderWiderAndFartherAndReloadFaster()
        {
            CatapultEraTier a = _set.Catapult.tiers[0];
            CatapultEraTier i = _set.Catapult.tiers[3];
            Vector3 p = Vector3.zero;
            SiegeTarget[] ring = { new SiegeTarget(1, 1, new Vector3(a.blastRadius + 0.2f, 0f, 0f)) };

            Assert.That(SiegeAttackProfile.ResolveImpact(a, p, -1, 0, ring), Is.Empty);
            Assert.That(SiegeAttackProfile.ResolveImpact(i, p, -1, 0, ring), Is.Not.Empty);
            Assert.That(i.attackDamage, Is.GreaterThan(a.attackDamage));
            Assert.That(i.attackRange, Is.GreaterThan(a.attackRange));
            Assert.That(i.attackCooldown, Is.LessThan(a.attackCooldown));
        }

        [Test]
        public void Catapult_InvalidSiegeDataIsRejected()
        {
            _set.Catapult.tiers[0].minimumRange = _set.Catapult.tiers[0].attackRange;
            Assert.That(_set.Catapult.Validate().ToString(), Does.Contain("alcance mínimo"));
            _set.Catapult.tiers[0].minimumRange = 8f;

            _set.Catapult.tiers[1].radialDamage = _set.Catapult.tiers[1].attackDamage;
            Assert.That(_set.Catapult.Validate().IsValid, Is.False, "radial deve ser menor que o impacto");
        }

        [Test]
        public void Catapult_ProjectileIsSeparate_Ballistic_AndSiegeDamage()
        {
            Assert.That(_set.Boulder.ballistic, Is.True);
            Assert.That(_set.Boulder.damageType, Is.EqualTo(DamageType.Siege));
            Assert.That(_set.Boulder.arcHeight, Is.GreaterThan(0f));

            _set.Boulder.ballistic = false;
            Assert.That(_set.Catapult.Validate().IsValid, Is.False);
            _set.Boulder.ballistic = true;

            _set.Catapult.SetProjectile(null);
            Assert.That(_set.Catapult.Validate().ToString(), Does.Contain("ProjectileContract"));
        }

        // ---- Balestra ----

        [Test]
        public void Crossbow_HasLongRange_AndFiresOnlyInsideItWithCooldownReady()
        {
            foreach (CrossbowEraTier t in _set.Crossbow.tiers)
            {
                Assert.That(t.attackRange, Is.GreaterThanOrEqualTo(AdvancedUnitRules.LongRangeFloor), t.era.ToString());
                Assert.That(CrossbowAttackProfile.CanFire(t, t.attackRange, t.attackCooldown), Is.True, $"{t.era} no limite");
                Assert.That(CrossbowAttackProfile.CanFire(t, 0f, t.attackCooldown), Is.True, $"{t.era} sem alcance mínimo");
                Assert.That(CrossbowAttackProfile.CanFire(t, t.attackRange + 0.01f, t.attackCooldown), Is.False, $"{t.era} além do alcance");
                Assert.That(CrossbowAttackProfile.CanFire(t, 5f, t.attackCooldown - 0.01f), Is.False, $"{t.era} em recarga");
                Assert.That(CrossbowAttackProfile.CanFire(t, -1f, t.attackCooldown), Is.False, $"{t.era} distância inválida");
            }
        }

        [Test]
        public void Crossbow_OutrangesMage_AndRangeIsNotMeleeLike()
        {
            for (int i = 0; i < 4; i++)
                Assert.That(_set.Crossbow.tiers[i].attackRange, Is.GreaterThan(_set.Mage.tiers[i].attackRange), ((ImperialEra)i).ToString());
        }

        [Test]
        public void Crossbow_PiercingDamageIgnoresPartOfTheArmorAndGrowsByEra()
        {
            CrossbowEraTier a = _set.Crossbow.tiers[0]; // 22 dano, 15% penetração
            CrossbowEraTier i = _set.Crossbow.tiers[3]; // 47 dano, 50% penetração

            Assert.That(CrossbowAttackProfile.ComputeDamage(a, 0f), Is.EqualTo(a.attackDamage));
            Assert.That(CrossbowAttackProfile.ComputeDamage(a, 10f), Is.EqualTo(Mathf.RoundToInt(a.attackDamage - 10f * (1f - a.armorPenetration))));
            Assert.That(CrossbowAttackProfile.ComputeDamage(i, 10f), Is.EqualTo(i.attackDamage - 5), "50% de penetração: armadura 10 vira 5");
            Assert.That(CrossbowAttackProfile.ComputeDamage(a, 10f), Is.LessThan(CrossbowAttackProfile.ComputeDamage(a, 0f)));
            Assert.That(CrossbowAttackProfile.ComputeDamage(i, 10f), Is.GreaterThan(CrossbowAttackProfile.ComputeDamage(a, 10f)));
            Assert.That(CrossbowAttackProfile.ComputeDamage(a, 9999f), Is.EqualTo(1), "mínimo de 1");
        }

        [Test]
        public void Crossbow_ProjectileIsSeparateAndPiercing()
        {
            Assert.That(_set.Bolt.damageType, Is.EqualTo(DamageType.Piercing));
            Assert.That(_set.Bolt.ballistic, Is.False);
            Assert.That(_set.Bolt.speed, Is.GreaterThan(_set.Boulder.speed), "o virote é mais rápido que a pedra");

            _set.Bolt.damageType = DamageType.Physical;
            Assert.That(_set.Crossbow.Validate().ToString(), Does.Contain("Piercing"));
        }

        [Test]
        public void Crossbow_ShortRangeIsRejected()
        {
            _set.Crossbow.tiers[0].attackRange = 6f;
            Assert.That(_set.Crossbow.Validate().ToString(), Does.Contain("longo alcance"));
        }

        // ---- Mago ----

        [Test]
        public void Mage_OutrangesEveryMeleeUnit_AndUsesMagicDamage()
        {
            foreach (MageEraTier t in _set.Mage.tiers)
                Assert.That(t.attackRange, Is.GreaterThan(AdvancedUnitRules.MeleeRangeCeiling), t.era.ToString());
            Assert.That(_set.Mage.tiers[0].attackRange, Is.GreaterThan(_set.Paladin.tiers[3].attackRange));
            Assert.That(_set.MageBolt.damageType, Is.EqualTo(DamageType.Magic));

            _set.Mage.tiers[1].attackRange = 1.5f;
            Assert.That(_set.Mage.Validate().ToString(), Does.Contain("alcance mágico"));
        }

        [Test]
        public void Mage_CastRespectsRangeAndCooldown()
        {
            MageEraTier t = _set.Mage.tiers[0];
            Assert.That(MageAttackProfile.CanCast(t, t.attackRange, t.attackCooldown), Is.True);
            Assert.That(MageAttackProfile.CanCast(t, t.attackRange + 0.01f, t.attackCooldown), Is.False);
            Assert.That(MageAttackProfile.CanCast(t, 3f, t.attackCooldown - 0.01f), Is.False);
            Assert.That(MageAttackProfile.CanCast(t, -1f, t.attackCooldown), Is.False);
        }

        [Test]
        public void Mage_MagicDamageIsConfigurableAndReducedByResistance()
        {
            MageEraTier t = _set.Mage.tiers[2];
            Assert.That(MageAttackProfile.ComputeDamage(t, 0f), Is.EqualTo(t.attackDamage));
            Assert.That(MageAttackProfile.ComputeDamage(t, 0.5f), Is.EqualTo(Mathf.RoundToInt(t.attackDamage * 0.5f)));
            Assert.That(MageAttackProfile.ComputeDamage(t, 1f), Is.EqualTo(0), "imune");
            Assert.That(MageAttackProfile.ComputeDamage(t, 0.999f), Is.GreaterThanOrEqualTo(1));

            t.attackDamage = 99;
            Assert.That(MageAttackProfile.ComputeDamage(t, 0f), Is.EqualTo(99), "o dano vem do dado configurado");
        }

        [Test]
        public void Projectile_FlightTimeFollowsSpeed()
        {
            Assert.That(_set.Bolt.FlightTime(30f), Is.EqualTo(30f / _set.Bolt.speed).Within(1e-4f));
            Assert.That(_set.Bolt.FlightTime(0f), Is.EqualTo(0f));
            _set.Bolt.speed = 0f;
            Assert.That(_set.Bolt.FlightTime(10f), Is.EqualTo(0f));
            Assert.That(_set.Bolt.Validate().IsValid, Is.False);
        }
    }
}
