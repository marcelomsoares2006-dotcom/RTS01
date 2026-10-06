using System.Collections.Generic;
using EraImperial.Units.Advanced;
using UnityEngine;

namespace EraImperial.Units.Paladin
{
    /// <summary>Dados mínimos de uma unidade candidata à cura. Id é a identidade estável da unidade (Unit.Uid, hash, índice...).</summary>
    public readonly struct HealCandidate
    {
        public readonly int Id;
        public readonly int Owner;
        public readonly Vector3 Position;
        public readonly int Health;
        public readonly int MaxHealth;
        public readonly bool IsAlive;

        public HealCandidate(int id, int owner, Vector3 position, int health, int maxHealth, bool isAlive = true)
        {
            Id = id;
            Owner = owner;
            Position = position;
            Health = health;
            MaxHealth = maxHealth;
            IsAlive = isAlive;
        }
    }

    public readonly struct HealResult
    {
        public readonly int Id;
        public readonly int Amount;

        public HealResult(int id, int amount)
        {
            Id = id;
            Amount = amount;
        }
    }

    /// <summary>Quanto cada alvo já recebeu em uma janela de tempo (limite de cura por alvo).</summary>
    public sealed class TargetHealLedger
    {
        private struct Entry
        {
            public float windowStart;
            public int healed;
        }

        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly float _windowSeconds;

        public TargetHealLedger(float windowSeconds)
        {
            _windowSeconds = windowSeconds;
        }

        /// <summary>Quanto o alvo ainda pode receber agora, dado o limite.</summary>
        public int Remaining(int id, float now, int cap)
        {
            if (!_entries.TryGetValue(id, out Entry entry) || now - entry.windowStart >= _windowSeconds)
                return Mathf.Max(0, cap);
            return Mathf.Max(0, cap - entry.healed);
        }

        public void Record(int id, int amount, float now)
        {
            if (amount <= 0) return;
            if (!_entries.TryGetValue(id, out Entry entry) || now - entry.windowStart >= _windowSeconds)
                entry = new Entry { windowStart = now, healed = 0 };
            entry.healed += amount;
            _entries[id] = entry;
        }
    }

    /// <summary>
    /// Pulso de cura em área do Paladino. Regras: só aliados (mesmo dono), vivos, feridos, dentro do raio
    /// (distância no plano XZ, incluindo o limite), sem exceder a vida máxima nem o limite por alvo.
    /// O paladino só cura a si mesmo se canHealSelf; inimigos nunca recebem cura.
    /// </summary>
    public static class PaladinHealing
    {
        public static List<HealResult> ComputePulse(
            PaladinEraTier tier,
            int paladinId,
            int paladinOwner,
            Vector3 paladinPosition,
            IReadOnlyList<HealCandidate> candidates,
            TargetHealLedger ledger,
            float now,
            bool canHealSelf = false)
        {
            List<HealResult> results = new List<HealResult>();
            if (candidates == null || tier.healAmount <= 0 || tier.healRadius <= 0f) return results;

            for (int i = 0; i < candidates.Count; i++)
            {
                HealCandidate c = candidates[i];
                if (!c.IsAlive) continue;
                if (c.Owner != paladinOwner) continue;
                if (c.Id == paladinId && !canHealSelf) continue;
                if (c.Health >= c.MaxHealth) continue;
                if (HorizontalDistance(paladinPosition, c.Position) > tier.healRadius) continue;

                int missing = c.MaxHealth - c.Health;
                int allowance = ledger != null ? ledger.Remaining(c.Id, now, tier.maxHealPerTarget) : tier.maxHealPerTarget;
                int amount = Mathf.Min(tier.healAmount, Mathf.Min(missing, allowance));
                if (amount <= 0) continue;

                ledger?.Record(c.Id, amount, now);
                results.Add(new HealResult(c.Id, amount));
            }
            return results;
        }

        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }

    /// <summary>
    /// Estado de cura de um paladino: cooldown entre pulsos + limite por alvo. Um pulso em cooldown não cura ninguém
    /// e não consome o cooldown.
    /// </summary>
    public sealed class PaladinHealController
    {
        private readonly TargetHealLedger _ledger;
        private float _lastPulseTime = float.NegativeInfinity;

        public PaladinHealController(float capWindowSeconds)
        {
            _ledger = new TargetHealLedger(capWindowSeconds);
        }

        public bool CanPulse(PaladinEraTier tier, float now)
        {
            return CombatGates.IsCooledDown(now - _lastPulseTime, tier.healCooldown);
        }

        /// <summary>Executa um pulso se o cooldown permitir; retorna null quando em cooldown.</summary>
        public List<HealResult> TryPulse(
            PaladinEraTier tier,
            int paladinId,
            int paladinOwner,
            Vector3 paladinPosition,
            IReadOnlyList<HealCandidate> candidates,
            float now,
            bool canHealSelf = false)
        {
            if (!CanPulse(tier, now)) return null;
            _lastPulseTime = now;
            return PaladinHealing.ComputePulse(tier, paladinId, paladinOwner, paladinPosition, candidates, _ledger, now, canHealSelf);
        }
    }
}
