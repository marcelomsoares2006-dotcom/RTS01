using System.Collections.Generic;
using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Crossbow;
using EraImperial.Units.Mage;
using EraImperial.Units.Paladin;
using EraImperial.Units.Siege;
using NUnit.Framework;

namespace EraImperial.AdvancedUnits.Tests
{
    public class UnitCodesAndProgressionTests
    {
        private ContractSet _set;

        [SetUp] public void SetUp() { _set = new ContractSet(); }
        [TearDown] public void TearDown() { _set.Dispose(); }

        // ---- 1. códigos únicos ----

        [Test]
        public void UnitCodes_AreTheFourRequiredValuesAndUnique()
        {
            Assert.That(AdvancedUnitCodes.All, Is.EquivalentTo(new[] { "combat_mage", "paladin_healer", "catapult", "crossbow" }));
            Assert.That(AdvancedUnitCodes.All, Is.Unique);
        }

        [Test]
        public void Contracts_CarryTheirExpectedCodes_AllDistinct()
        {
            string[] codes = _set.All.Select(c => c.code).ToArray();
            Assert.That(codes, Is.Unique);
            Assert.That(codes, Is.EquivalentTo(AdvancedUnitCodes.All));
            foreach (AdvancedUnitContract contract in _set.All)
                Assert.That(contract.code, Is.EqualTo(contract.ExpectedCode), contract.name);
        }

        [Test]
        public void Projectiles_HaveDistinctCodes_AndAreSeparateFromUnits()
        {
            string[] projectileCodes = { _set.MageBolt.code, _set.Boulder.code, _set.Bolt.code };
            Assert.That(projectileCodes, Is.Unique);
            Assert.That(projectileCodes.Intersect(AdvancedUnitCodes.All), Is.Empty);
            Assert.That(_set.Mage.Projectile, Is.SameAs(_set.MageBolt));
            Assert.That(_set.Catapult.Projectile, Is.SameAs(_set.Boulder));
            Assert.That(_set.Crossbow.Projectile, Is.SameAs(_set.Bolt));
            Assert.That(_set.Paladin.Projectile, Is.Null, "o paladino é corpo a corpo e não usa projétil");
        }

        [Test]
        public void ContractWithWrongCode_IsRejected()
        {
            _set.Catapult.code = "combat_mage";
            ValidationReport report = _set.Catapult.Validate();
            Assert.That(report.IsValid, Is.False);
            Assert.That(report.ToString(), Does.Contain("difere do esperado"));
        }

        // ---- 2. progressão crescente por eras ----

        [Test]
        public void DefaultContracts_PassGameplayValidation()
        {
            foreach (AdvancedUnitContract contract in _set.All)
                Assert.That(contract.Validate().IsValid, Is.True, $"{contract.code}: {contract.Validate()}");
        }

        [Test]
        public void EveryUnit_HasExplicitProgression_AncientClassicalMedievalImperial()
        {
            foreach (AdvancedUnitContract contract in _set.All)
            {
                IReadOnlyList<IEraTier> tiers = contract.GetTiers();
                Assert.That(tiers.Select(t => t.Era), Is.EqualTo(new[]
                {
                    ImperialEra.Ancient, ImperialEra.Classical, ImperialEra.Medieval, ImperialEra.Imperial
                }), contract.code);
            }
        }

        [Test]
        public void EveryUnit_HealthAndDamageGrowStrictlyByEra_RangeNeverShrinks_CooldownNeverGrows()
        {
            foreach (AdvancedUnitContract contract in _set.All)
            {
                IReadOnlyList<IEraTier> tiers = contract.GetTiers();
                for (int i = 1; i < tiers.Count; i++)
                {
                    string where = $"{contract.code}: {tiers[i - 1].Era} -> {tiers[i].Era}";
                    Assert.That(tiers[i].HealthPoints, Is.GreaterThan(tiers[i - 1].HealthPoints), where + " vida");
                    Assert.That(tiers[i].AttackDamage, Is.GreaterThan(tiers[i - 1].AttackDamage), where + " dano");
                    Assert.That(tiers[i].AttackRange, Is.GreaterThanOrEqualTo(tiers[i - 1].AttackRange), where + " alcance");
                    Assert.That(tiers[i].AttackCooldown, Is.LessThanOrEqualTo(tiers[i - 1].AttackCooldown), where + " cooldown");
                }
            }
        }

        [Test]
        public void Paladin_HealAndRadiusGrowStrictlyByEra()
        {
            PaladinEraTier[] t = _set.Paladin.tiers;
            for (int i = 1; i < t.Length; i++)
            {
                Assert.That(t[i].healAmount, Is.GreaterThan(t[i - 1].healAmount));
                Assert.That(t[i].healRadius, Is.GreaterThan(t[i - 1].healRadius));
                Assert.That(t[i].maxHealPerTarget, Is.GreaterThan(t[i - 1].maxHealPerTarget));
                Assert.That(t[i].healCooldown, Is.LessThanOrEqualTo(t[i - 1].healCooldown));
            }
        }

        [Test]
        public void Catapult_RadialDamageAndBlastGrowByEra()
        {
            CatapultEraTier[] t = _set.Catapult.tiers;
            for (int i = 1; i < t.Length; i++)
            {
                Assert.That(t[i].radialDamage, Is.GreaterThan(t[i - 1].radialDamage));
                Assert.That(t[i].blastRadius, Is.GreaterThanOrEqualTo(t[i - 1].blastRadius));
            }
        }

        [Test]
        public void Crossbow_ArmorPenetrationGrowsByEra()
        {
            CrossbowEraTier[] t = _set.Crossbow.tiers;
            for (int i = 1; i < t.Length; i++)
                Assert.That(t[i].armorPenetration, Is.GreaterThan(t[i - 1].armorPenetration));
        }

        [Test]
        public void ProgressionRules_RejectRegressionMissingEraAndDuplicateEra()
        {
            _set.Mage.tiers[2].attackDamage = _set.Mage.tiers[1].attackDamage;
            Assert.That(_set.Mage.Validate().IsValid, Is.False, "dano igual entre eras deve ser rejeitado");
            _set.Mage.tiers[2].attackDamage = 31;
            Assert.That(_set.Mage.Validate().IsValid, Is.True);

            _set.Paladin.tiers = _set.Paladin.tiers.Take(3).ToArray();
            Assert.That(_set.Paladin.Validate().ToString(), Does.Contain("exatamente 4 eras"));

            _set.Crossbow.tiers[3].era = ImperialEra.Medieval;
            Assert.That(_set.Crossbow.Validate().IsValid, Is.False, "era duplicada/fora de ordem");

            _set.Catapult.tiers[1].radialDamage = _set.Catapult.tiers[0].radialDamage;
            Assert.That(_set.Catapult.Validate().IsValid, Is.False, "dano radial deve crescer");
        }

        [Test]
        public void BuildUnitDataFields_UsesTheRequestedEra()
        {
            UnitDataFields ancient = _set.Crossbow.BuildUnitDataFields(ImperialEra.Ancient);
            UnitDataFields imperial = _set.Crossbow.BuildUnitDataFields(ImperialEra.Imperial);
            Assert.That(ancient.code, Is.EqualTo("crossbow"));
            Assert.That(imperial.attackDamage, Is.GreaterThan(ancient.attackDamage));
            Assert.That(imperial.healthpoints, Is.GreaterThan(ancient.healthpoints));
            Assert.That(imperial.attackRange, Is.GreaterThan(ancient.attackRange));
            Assert.That(imperial.attackRate, Is.LessThan(ancient.attackRate), "attackRate é o período entre ataques");
            Assert.That(ancient.cost, Is.Not.Empty);

            _set.Crossbow.tiers = _set.Crossbow.tiers.Take(2).ToArray();
            Assert.Throws<System.InvalidOperationException>(() => _set.Crossbow.BuildUnitDataFields(ImperialEra.Imperial));
        }

        [Test]
        public void Cost_RejectsUnknownResourceAndNonPositiveAmounts()
        {
            _set.Mage.cost = new[] { new ResourceCost("Mana", 10) };
            Assert.That(_set.Mage.Validate().IsValid, Is.False);
            _set.Mage.cost = new[] { new ResourceCost("Gold", 0) };
            Assert.That(_set.Mage.Validate().IsValid, Is.False);
            _set.Mage.cost = new ResourceCost[0];
            Assert.That(_set.Mage.Validate().IsValid, Is.False);
        }
    }
}
