using System;
using System.Collections.Generic;
using EraImperial.Units.Advanced;
using UnityEngine;

namespace EraImperial.Units.Paladin
{
    /// <summary>Nível de era do Paladino curador: combate corpo a corpo + pulso de cura em área.</summary>
    [Serializable]
    public struct PaladinEraTier : IEraTier
    {
        public ImperialEra era;
        public int healthpoints;
        /// <summary>Dano do golpe corpo a corpo.</summary>
        public int attackDamage;
        /// <summary>Alcance do golpe (corpo a corpo).</summary>
        public float attackRange;
        public float attackCooldown;

        /// <summary>Vida restaurada por aliado em cada pulso.</summary>
        public int healAmount;
        /// <summary>Raio horizontal (plano XZ) do pulso de cura.</summary>
        public float healRadius;
        /// <summary>Segundos entre pulsos de cura.</summary>
        public float healCooldown;
        /// <summary>Máximo de vida que um mesmo alvo recebe deste paladino dentro de uma janela de tempo.</summary>
        public int maxHealPerTarget;

        public ImperialEra Era => era;
        public int HealthPoints => healthpoints;
        public int AttackDamage => attackDamage;
        public float AttackRange => attackRange;
        public float AttackCooldown => attackCooldown;

        public IEnumerable<EraMetric> ExtraMetrics()
        {
            yield return new EraMetric("cura", healAmount, EraTrend.StrictlyIncreasing);
            yield return new EraMetric("raio de cura", healRadius, EraTrend.StrictlyIncreasing);
            yield return new EraMetric("cooldown de cura", healCooldown, EraTrend.NonIncreasing);
            yield return new EraMetric("limite de cura por alvo", maxHealPerTarget, EraTrend.StrictlyIncreasing);
        }
    }

    /// <summary>
    /// Paladino curador (paladin_healer): guerreiro corpo a corpo que cura aliados ao redor.
    /// Sockets e animações são apenas declarados; nenhuma malha foi inventada.
    /// </summary>
    [CreateAssetMenu(fileName = "PaladinHealer", menuName = "Era Imperial/Units/Advanced/Paladin Healer")]
    public sealed class PaladinHealerContract : AdvancedUnitContract
    {
        public PaladinEraTier[] tiers = new PaladinEraTier[0];
        /// <summary>Janela (s) em que o limite de cura por alvo é contado.</summary>
        [Min(0.1f)] public float healCapWindowSeconds = 10f;
        public bool canHealSelf;

        public override string ExpectedCode => AdvancedUnitCodes.PaladinHealer;

        public override IReadOnlyList<IEraTier> GetTiers()
        {
            List<IEraTier> list = new List<IEraTier>();
            if (tiers != null)
                foreach (PaladinEraTier t in tiers) list.Add(t);
            return list;
        }

        protected override void ValidateSpecific(ValidationReport report)
        {
            if (healCapWindowSeconds <= 0f) report.Add("janela do limite de cura deve ser > 0");
            if (tiers == null) return;
            foreach (PaladinEraTier t in tiers)
            {
                if (t.attackRange > AdvancedUnitRules.MeleeRangeCeiling)
                    report.Add($"{t.era}: o ataque do paladino deve ser corpo a corpo (alcance {t.attackRange})");
                if (t.healAmount <= 0) report.Add($"{t.era}: cura deve ser > 0");
                if (t.healRadius <= 0f) report.Add($"{t.era}: raio de cura deve ser > 0");
                if (t.healCooldown <= 0f) report.Add($"{t.era}: cooldown de cura deve ser > 0");
                if (t.maxHealPerTarget < t.healAmount)
                    report.Add($"{t.era}: limite por alvo ({t.maxHealPerTarget}) não pode ser menor que uma cura ({t.healAmount})");
            }
        }
    }
}
