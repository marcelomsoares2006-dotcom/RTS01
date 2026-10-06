namespace EraImperial.Units.Advanced
{
    /// <summary>Limiares de referência para as regras de design. Soldier tem alcance 1 e Spearman 1,6.</summary>
    public static class AdvancedUnitRules
    {
        /// <summary>Alcance máximo ainda considerado corpo a corpo.</summary>
        public const float MeleeRangeCeiling = 2f;

        /// <summary>Alcance mínimo para ser considerado longo alcance (RangedAttackProfile do Arqueiro usa 12).</summary>
        public const float LongRangeFloor = 12f;
    }

    /// <summary>Portões simples de combate, sem estado e sem dependência de cena.</summary>
    public static class CombatGates
    {
        /// <summary>Distância dentro da faixa [min, max] (extremos incluídos). Distância negativa é inválida.</summary>
        public static bool IsInRange(float distance, float minimumRange, float maximumRange)
        {
            return distance >= 0f && distance >= minimumRange && distance <= maximumRange;
        }

        /// <summary>Recarga concluída quando o tempo desde o último uso alcança o cooldown (> 0).</summary>
        public static bool IsCooledDown(float secondsSinceLastUse, float cooldown)
        {
            return cooldown > 0f && secondsSinceLastUse >= cooldown;
        }
    }
}
