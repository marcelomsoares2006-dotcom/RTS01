using System.Collections.Generic;
using EraImperial.Units.Advanced;
using UnityEngine;

namespace EraImperial.Units.Siege
{
    /// <summary>Alvo possível de uma explosão de cerco.</summary>
    public readonly struct SiegeTarget
    {
        public readonly int Id;
        public readonly int Owner;
        public readonly Vector3 Position;
        public readonly bool IsAlive;

        public SiegeTarget(int id, int owner, Vector3 position, bool isAlive = true)
        {
            Id = id;
            Owner = owner;
            Position = position;
            IsAlive = isAlive;
        }
    }

    public readonly struct SiegeHit
    {
        public readonly int Id;
        public readonly int Damage;
        /// <summary>Verdadeiro para o alvo atingido diretamente (dano de impacto); falso para dano radial.</summary>
        public readonly bool IsDirect;

        public SiegeHit(int id, int damage, bool isDirect)
        {
            Id = id;
            Damage = damage;
            IsDirect = isDirect;
        }
    }

    /// <summary>Regras de disparo e dano de área da catapulta, sem estado e sem dependência de cena.</summary>
    public static class SiegeAttackProfile
    {
        /// <summary>Dentro de [alcance mínimo, alcance máximo] e com a recarga concluída.</summary>
        public static bool CanFire(CatapultEraTier tier, float distance, float secondsSinceLastShot)
        {
            return CombatGates.IsInRange(distance, tier.minimumRange, tier.attackRange)
                && CombatGates.IsCooledDown(secondsSinceLastShot, tier.attackCooldown);
        }

        /// <summary>
        /// Resolve uma explosão. O alvo direto (se vivo e inimigo) recebe o dano de impacto; os demais inimigos vivos
        /// dentro do raio recebem o dano radial com queda linear (centro = total, borda = 0). Aliados do atirador
        /// não sofrem dano, salvo friendlyFire. Quem já recebeu o impacto direto não recebe o radial por cima.
        /// </summary>
        public static List<SiegeHit> ResolveImpact(
            CatapultEraTier tier,
            Vector3 impactPoint,
            int directTargetId,
            int attackerOwner,
            IReadOnlyList<SiegeTarget> targets,
            bool friendlyFire = false)
        {
            List<SiegeHit> hits = new List<SiegeHit>();
            if (targets == null) return hits;

            for (int i = 0; i < targets.Count; i++)
            {
                SiegeTarget t = targets[i];
                if (!t.IsAlive) continue;
                if (!friendlyFire && t.Owner == attackerOwner) continue;

                if (t.Id == directTargetId)
                {
                    hits.Add(new SiegeHit(t.Id, tier.attackDamage, true));
                    continue;
                }

                float distance = Vector3.Distance(impactPoint, t.Position);
                if (distance >= tier.blastRadius) continue;
                int damage = Mathf.RoundToInt(tier.radialDamage * (1f - distance / tier.blastRadius));
                if (damage > 0) hits.Add(new SiegeHit(t.Id, damage, false));
            }
            return hits;
        }
    }
}
