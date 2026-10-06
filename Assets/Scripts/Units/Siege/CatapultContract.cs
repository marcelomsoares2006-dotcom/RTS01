using System;
using System.Collections.Generic;
using EraImperial.Units.Advanced;
using UnityEngine;

namespace EraImperial.Units.Siege
{
    /// <summary>Nível de era da Catapulta: dano de impacto, dano radial, alcance mínimo/máximo e recarga.</summary>
    [Serializable]
    public struct CatapultEraTier : IEraTier
    {
        public ImperialEra era;
        public int healthpoints;
        /// <summary>Dano de impacto no alvo atingido diretamente.</summary>
        public int attackDamage;
        /// <summary>Alcance máximo.</summary>
        public float attackRange;
        /// <summary>Tempo de recarga entre disparos (s).</summary>
        public float attackCooldown;

        /// <summary>Alcance mínimo: alvos mais próximos não podem ser atacados.</summary>
        public float minimumRange;
        /// <summary>Dano radial no centro da explosão (cai linearmente até zero na borda do raio).</summary>
        public int radialDamage;
        public float blastRadius;

        public ImperialEra Era => era;
        public int HealthPoints => healthpoints;
        public int AttackDamage => attackDamage;
        public float AttackRange => attackRange;
        public float AttackCooldown => attackCooldown;

        public IEnumerable<EraMetric> ExtraMetrics()
        {
            yield return new EraMetric("dano radial", radialDamage, EraTrend.StrictlyIncreasing);
            yield return new EraMetric("raio da explosão", blastRadius, EraTrend.NonDecreasing);
        }
    }

    /// <summary>
    /// Catapulta (catapult): artilharia pesada de cerco. É modelada como CharacterData por padrão (unidade de cerco móvel);
    /// dataKind = Building permite usá-la como estrutura de cerco via BuildingData. O projétil é um contrato separado.
    /// Não deve entrar no Build Settings antes da validação (ver CanBeAddedToBuildSettings).
    /// </summary>
    [CreateAssetMenu(fileName = "Catapult", menuName = "Era Imperial/Units/Advanced/Catapult")]
    public sealed class CatapultContract : AdvancedUnitContract
    {
        public CatapultEraTier[] tiers = new CatapultEraTier[0];
        [SerializeField] private ProjectileContract projectile;

        public override string ExpectedCode => AdvancedUnitCodes.Catapult;
        public override ProjectileContract Projectile => projectile;

        public void SetProjectile(ProjectileContract value) { projectile = value; }

        public override IReadOnlyList<IEraTier> GetTiers()
        {
            List<IEraTier> list = new List<IEraTier>();
            if (tiers != null)
                foreach (CatapultEraTier t in tiers) list.Add(t);
            return list;
        }

        protected override void ValidateSpecific(ValidationReport report)
        {
            if (projectile == null) report.Add("catapult exige um ProjectileContract separado");
            else
            {
                if (projectile.damageType != DamageType.Siege) report.Add("o projétil da catapulta deve causar dano Siege");
                if (!projectile.ballistic) report.Add("o projétil da catapulta deve ter trajetória balística");
            }

            if (tiers == null) return;
            foreach (CatapultEraTier t in tiers)
            {
                if (t.minimumRange <= 0f) report.Add($"{t.era}: alcance mínimo deve ser > 0");
                if (t.minimumRange >= t.attackRange) report.Add($"{t.era}: alcance mínimo ({t.minimumRange}) deve ser menor que o máximo ({t.attackRange})");
                if (t.radialDamage <= 0) report.Add($"{t.era}: dano radial deve ser > 0");
                if (t.radialDamage >= t.attackDamage) report.Add($"{t.era}: dano radial ({t.radialDamage}) deve ser menor que o de impacto ({t.attackDamage})");
                if (t.blastRadius <= 0f) report.Add($"{t.era}: raio da explosão deve ser > 0");
            }
        }
    }
}
