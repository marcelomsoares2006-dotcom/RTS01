using System;
using System.Collections.Generic;
using EraImperial.Units.Advanced;
using UnityEngine;

namespace EraImperial.Units.Mage
{
    /// <summary>Nível de era do Mago de combate. attackDamage é dano mágico; attackRange é o alcance de conjuração.</summary>
    [Serializable]
    public struct MageEraTier : IEraTier
    {
        public ImperialEra era;
        public int healthpoints;
        public int attackDamage;
        public float attackRange;
        /// <summary>Segundos entre conjurações.</summary>
        public float attackCooldown;

        public ImperialEra Era => era;
        public int HealthPoints => healthpoints;
        public int AttackDamage => attackDamage;
        public float AttackRange => attackRange;
        public float AttackCooldown => attackCooldown;

        public IEnumerable<EraMetric> ExtraMetrics() { yield break; }
    }

    /// <summary>
    /// Mago de combate (combat_mage): ataque mágico à distância, com projétil separado e efeitos visuais
    /// declarados (conjuração e impacto). Sem malha, prefab ou material: o visual fica pendente até existir asset real.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatMage", menuName = "Era Imperial/Units/Advanced/Combat Mage")]
    public sealed class CombatMageContract : AdvancedUnitContract
    {
        public MageEraTier[] tiers = new MageEraTier[0];
        [SerializeField] private ProjectileContract projectile;

        public override string ExpectedCode => AdvancedUnitCodes.CombatMage;
        public override ProjectileContract Projectile => projectile;

        public void SetProjectile(ProjectileContract value) { projectile = value; }

        public override IReadOnlyList<IEraTier> GetTiers()
        {
            List<IEraTier> list = new List<IEraTier>();
            if (tiers != null)
                foreach (MageEraTier t in tiers) list.Add(t);
            return list;
        }

        protected override void ValidateSpecific(ValidationReport report)
        {
            if (projectile == null) report.Add("combat_mage exige um ProjectileContract separado");
            else if (projectile.damageType != DamageType.Magic) report.Add("o projétil do combat_mage deve causar dano Magic");

            if (tiers == null) return;
            foreach (MageEraTier t in tiers)
                if (t.attackRange <= AdvancedUnitRules.MeleeRangeCeiling)
                    report.Add($"{t.era}: alcance mágico ({t.attackRange}) deve superar o corpo a corpo ({AdvancedUnitRules.MeleeRangeCeiling})");
        }
    }

    /// <summary>Regras de ataque do mago, sem estado e sem dependência de cena.</summary>
    public static class MageAttackProfile
    {
        public static bool CanCast(MageEraTier tier, float distance, float secondsSinceLastCast)
        {
            return CombatGates.IsInRange(distance, 0f, tier.attackRange)
                && CombatGates.IsCooledDown(secondsSinceLastCast, tier.attackCooldown);
        }

        /// <summary>Dano mágico após a resistência mágica do alvo (0 = nenhuma, 1 = imune). Mínimo 1 se não for imune.</summary>
        public static int ComputeDamage(MageEraTier tier, float targetMagicResistance)
        {
            float resistance = Mathf.Clamp01(targetMagicResistance);
            if (resistance >= 1f) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(tier.attackDamage * (1f - resistance)));
        }
    }
}
