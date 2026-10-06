using System.Collections.Generic;

namespace EraImperial.Units.Advanced
{
    /// <summary>Códigos únicos das unidades avançadas (chave usada por UnitData.code).</summary>
    public static class AdvancedUnitCodes
    {
        public const string CombatMage = "combat_mage";
        public const string PaladinHealer = "paladin_healer";
        public const string Catapult = "catapult";
        public const string Crossbow = "crossbow";

        public static readonly IReadOnlyList<string> All = new[] { CombatMage, PaladinHealer, Catapult, Crossbow };
    }
}
