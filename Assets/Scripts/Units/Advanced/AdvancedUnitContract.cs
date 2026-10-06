using System.Collections.Generic;
using UnityEngine;

namespace EraImperial.Units.Advanced
{
    /// <summary>Qual abstração do jogo receberá os dados: CharacterData (unidade) ou BuildingData (estrutura).</summary>
    public enum UnitDataKind
    {
        Character = 0,
        Building = 1
    }

    /// <summary>
    /// Campos de UnitData calculados a partir de um contrato e de uma era. Os nomes espelham UnitData
    /// (code, unitName, description, healthpoints, cost, fieldOfView, attackRange, attackDamage, attackRate).
    /// </summary>
    public struct UnitDataFields
    {
        public string code;
        public string unitName;
        public string description;
        public int healthpoints;
        public ResourceCost[] cost;
        public float fieldOfView;
        public float attackRange;
        public int attackDamage;
        public float attackRate;
    }

    /// <summary>
    /// Base dos contratos de dados das unidades avançadas. Não referencia UnitData (que vive no Assembly-CSharp):
    /// produz UnitDataFields, e a ponte em Assets/Scripts/Units/AdvancedBridge copia para UnitData/CharacterData/BuildingData.
    /// </summary>
    public abstract class AdvancedUnitContract : ScriptableObject
    {
        public string code;
        public string unitName;
        [TextArea] public string description;
        public UnitDataKind dataKind = UnitDataKind.Character;
        public ResourceCost[] cost = new ResourceCost[0];
        [Min(0f)] public float fieldOfView = 16f;
        public AdvancedVisualContract visual = new AdvancedVisualContract();
        /// <summary>Marcado pelo responsável somente após validação visual e de jogo; nunca é presumido.</summary>
        public bool buildSettingsApproved;

        /// <summary>Código obrigatório desta unidade (AdvancedUnitCodes).</summary>
        public abstract string ExpectedCode { get; }

        /// <summary>Níveis por era, na ordem Ancient, Classical, Medieval, Imperial.</summary>
        public abstract IReadOnlyList<IEraTier> GetTiers();

        /// <summary>Projétil separado usado por esta unidade; nulo para unidades corpo a corpo.</summary>
        public virtual ProjectileContract Projectile => null;

        /// <summary>Regras específicas da unidade, somadas à validação comum.</summary>
        protected virtual void ValidateSpecific(ValidationReport report) { }

        public bool TryGetTier(ImperialEra era, out IEraTier tier)
        {
            IReadOnlyList<IEraTier> tiers = GetTiers();
            if (tiers != null)
            {
                for (int i = 0; i < tiers.Count; i++)
                {
                    if (tiers[i].Era == era)
                    {
                        tier = tiers[i];
                        return true;
                    }
                }
            }
            tier = null;
            return false;
        }

        public UnitDataFields BuildUnitDataFields(ImperialEra era)
        {
            if (!TryGetTier(era, out IEraTier tier))
                throw new System.InvalidOperationException($"'{code}' não define a era {era}.");

            return new UnitDataFields
            {
                code = code,
                unitName = unitName,
                description = description,
                healthpoints = tier.HealthPoints,
                cost = cost != null ? (ResourceCost[])cost.Clone() : new ResourceCost[0],
                fieldOfView = fieldOfView,
                attackRange = tier.AttackRange,
                attackDamage = tier.AttackDamage,
                attackRate = tier.AttackCooldown
            };
        }

        /// <summary>Validação de dados de jogo (independente de malha/prefab).</summary>
        public ValidationReport Validate()
        {
            ValidationReport report = new ValidationReport();
            if (string.IsNullOrWhiteSpace(code)) report.Add("código vazio");
            else if (code != ExpectedCode) report.Add($"código '{code}' difere do esperado '{ExpectedCode}'");
            if (string.IsNullOrWhiteSpace(unitName)) report.Add($"'{code}': nome vazio");

            if (cost == null || cost.Length == 0) report.Add($"'{code}': custo de recursos vazio");
            else
            {
                foreach (ResourceCost c in cost)
                    if (!c.IsValid) report.Add($"'{code}': custo inválido ({c.resource} x{c.amount})");
            }

            report.AddRange(EraProgressionRules.Validate(GetTiers()).Problems);

            if (Projectile != null) report.AddRange(Projectile.Validate().Problems);
            ValidateSpecific(report);
            return report;
        }

        /// <summary>Prontidão visual: unidade (prefab, malhas, sockets, animações) e projétil, se houver.</summary>
        public VisualReadinessReport EvaluateVisuals()
        {
            VisualReadinessReport report = new VisualReadinessReport();
            report.AddRange((visual ?? new AdvancedVisualContract()).Evaluate().Missing);
            if (Projectile != null) report.AddRange(Projectile.EvaluateVisuals().Missing);
            return report;
        }

        public bool IsGameplayValid => Validate().IsValid;
        public bool IsVisualReady => EvaluateVisuals().IsReady;

        /// <summary>Só é verdadeiro com dados válidos, visual real completo e aprovação explícita.</summary>
        public bool CanBeAddedToBuildSettings => IsGameplayValid && IsVisualReady && buildSettingsApproved;
    }
}
