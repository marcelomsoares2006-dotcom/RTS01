using System;
using System.Collections.Generic;
using EraImperial.Units.Advanced;
using UnityEngine;

namespace EraImperial.Units.Crossbow
{
    /// <summary>Nível de era da Balestra: dano perfurante, longo alcance e cooldown.</summary>
    [Serializable]
    public struct CrossbowEraTier : IEraTier
    {
        public ImperialEra era;
        public int healthpoints;
        /// <summary>Dano perfurante do virote.</summary>
        public int attackDamage;
        public float attackRange;
        public float attackCooldown;
        /// <summary>Fração (0..1) da armadura do alvo que o virote ignora.</summary>
        [Range(0f, 1f)] public float armorPenetration;

        public ImperialEra Era => era;
        public int HealthPoints => healthpoints;
        public int AttackDamage => attackDamage;
        public float AttackRange => attackRange;
        public float AttackCooldown => attackCooldown;

        public IEnumerable<EraMetric> ExtraMetrics()
        {
            yield return new EraMetric("penetração de armadura", armorPenetration, EraTrend.StrictlyIncreasing);
        }
    }

    /// <summary>
    /// Balestra (crossbow): ataque perfurante de longo alcance, com projétil separado. Por padrão é uma unidade
    /// (CharacterData); dataKind = Building permite usá-la como estrutura de defesa via BuildingData.
    /// </summary>
    [CreateAssetMenu(fileName = "Crossbow", menuName = "Era Imperial/Units/Advanced/Crossbow")]
    public sealed class CrossbowContract : AdvancedUnitContract
    {
        public CrossbowEraTier[] tiers = new CrossbowEraTier[0];
        [SerializeField] private ProjectileContract projectile;

        public override string ExpectedCode => AdvancedUnitCodes.Crossbow;
        public override ProjectileContract Projectile => projectile;

        public void SetProjectile(ProjectileContract value) { projectile = value; }

        public override IReadOnlyList<IEraTier> GetTiers()
        {
            List<IEraTier> list = new List<IEraTier>();
            if (tiers != null)
                foreach (CrossbowEraTier t in tiers) list.Add(t);
            return list;
        }

        protected override void ValidateSpecific(ValidationReport report)
        {
            if (projectile == null) report.Add("crossbow exige um ProjectileContract separado");
            else if (projectile.damageType != DamageType.Piercing) report.Add("o projétil da balestra deve causar dano Piercing");

            if (tiers == null) return;
            foreach (CrossbowEraTier t in tiers)
            {
                if (t.attackRange < AdvancedUnitRules.LongRangeFloor)
                    report.Add($"{t.era}: alcance ({t.attackRange}) abaixo do piso de longo alcance ({AdvancedUnitRules.LongRangeFloor})");
                if (t.armorPenetration < 0f || t.armorPenetration > 1f)
                    report.Add($"{t.era}: penetração de armadura deve estar entre 0 e 1");
            }
        }
    }

    /// <summary>Regras de ataque da balestra, sem estado e sem dependência de cena.</summary>
    public static class CrossbowAttackProfile
    {
        public static bool CanFire(CrossbowEraTier tier, float distance, float secondsSinceLastShot)
        {
            return CombatGates.IsInRange(distance, 0f, tier.attackRange)
                && CombatGates.IsCooledDown(secondsSinceLastShot, tier.attackCooldown);
        }

        /// <summary>
        /// Dano perfurante contra armadura plana: a armadura efetiva é reduzida pela penetração.
        /// Sempre causa pelo menos 1 de dano.
        /// </summary>
        public static int ComputeDamage(CrossbowEraTier tier, float targetArmor)
        {
            float effectiveArmor = Mathf.Max(0f, targetArmor) * (1f - Mathf.Clamp01(tier.armorPenetration));
            return Mathf.Max(1, Mathf.RoundToInt(tier.attackDamage - effectiveArmor));
        }
    }
}
