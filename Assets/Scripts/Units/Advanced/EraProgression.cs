using System.Collections.Generic;

namespace EraImperial.Units.Advanced
{
    public enum EraTrend
    {
        StrictlyIncreasing = 0,
        NonDecreasing = 1,
        NonIncreasing = 2
    }

    /// <summary>Métrica nomeada de um nível de era, com a tendência exigida entre eras consecutivas.</summary>
    public readonly struct EraMetric
    {
        public readonly string Name;
        public readonly float Value;
        public readonly EraTrend Trend;

        public EraMetric(string name, float value, EraTrend trend)
        {
            Name = name;
            Value = value;
            Trend = trend;
        }
    }

    /// <summary>Estatísticas de uma unidade em uma era. Cada unidade avançada define seu próprio tier.</summary>
    public interface IEraTier
    {
        ImperialEra Era { get; }
        int HealthPoints { get; }
        /// <summary>Dano principal (UnitData.attackDamage). Impacto direto na catapulta.</summary>
        int AttackDamage { get; }
        /// <summary>Alcance máximo (UnitData.attackRange).</summary>
        float AttackRange { get; }
        /// <summary>Segundos entre ataques (UnitData.attackRate, usado como período do Timer da árvore de comportamento).</summary>
        float AttackCooldown { get; }
        /// <summary>Métricas específicas da unidade (cura, raio, perfuração...), sempre na mesma ordem.</summary>
        IEnumerable<EraMetric> ExtraMetrics();
    }

    /// <summary>Regras de progressão: as quatro eras, uma vez cada, em ordem, e métricas coerentes.</summary>
    public static class EraProgressionRules
    {
        public static ValidationReport Validate(IReadOnlyList<IEraTier> tiers)
        {
            ValidationReport report = new ValidationReport();
            if (tiers == null || tiers.Count != ImperialEras.Ordered.Length)
            {
                report.Add($"a progressão precisa de exatamente {ImperialEras.Ordered.Length} eras (encontradas {(tiers == null ? 0 : tiers.Count)})");
                return report;
            }

            for (int i = 0; i < tiers.Count; i++)
            {
                if (tiers[i].Era != ImperialEras.Ordered[i])
                    report.Add($"posição {i} deve ser {ImperialEras.Ordered[i]} mas é {tiers[i].Era}");
            }
            if (!report.IsValid) return report;

            for (int i = 0; i < tiers.Count; i++)
            {
                IEraTier t = tiers[i];
                if (t.HealthPoints <= 0) report.Add($"{t.Era}: vida deve ser > 0");
                if (t.AttackDamage <= 0) report.Add($"{t.Era}: dano deve ser > 0");
                if (t.AttackRange <= 0f) report.Add($"{t.Era}: alcance deve ser > 0");
                if (t.AttackCooldown <= 0f) report.Add($"{t.Era}: cooldown deve ser > 0");
            }

            for (int i = 1; i < tiers.Count; i++)
            {
                IEraTier previous = tiers[i - 1];
                IEraTier current = tiers[i];
                Compare(report, previous.Era, current.Era, "vida", previous.HealthPoints, current.HealthPoints, EraTrend.StrictlyIncreasing);
                Compare(report, previous.Era, current.Era, "dano", previous.AttackDamage, current.AttackDamage, EraTrend.StrictlyIncreasing);
                Compare(report, previous.Era, current.Era, "alcance", previous.AttackRange, current.AttackRange, EraTrend.NonDecreasing);
                Compare(report, previous.Era, current.Era, "cooldown", previous.AttackCooldown, current.AttackCooldown, EraTrend.NonIncreasing);

                List<EraMetric> before = new List<EraMetric>(previous.ExtraMetrics());
                List<EraMetric> after = new List<EraMetric>(current.ExtraMetrics());
                for (int m = 0; m < before.Count && m < after.Count; m++)
                    Compare(report, previous.Era, current.Era, before[m].Name, before[m].Value, after[m].Value, before[m].Trend);
            }
            return report;
        }

        private static void Compare(ValidationReport report, ImperialEra from, ImperialEra to, string name, float a, float b, EraTrend trend)
        {
            bool ok;
            switch (trend)
            {
                case EraTrend.StrictlyIncreasing: ok = b > a; break;
                case EraTrend.NonDecreasing: ok = b >= a; break;
                default: ok = b <= a; break;
            }
            if (!ok) report.Add($"{name} de {to} ({b}) viola a tendência {trend} em relação a {from} ({a})");
        }
    }
}
