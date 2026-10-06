using System;
using System.Collections.Generic;
using EraImperial.Units.Advanced;
using EraImperial.Units.Crossbow;
using EraImperial.Units.Mage;
using EraImperial.Units.Paladin;
using EraImperial.Units.Siege;
using UnityEngine;

namespace EraImperial.AdvancedUnits.Tests
{
    /// <summary>As quatro unidades e os três projéteis criados a partir dos Defaults, em memória e descartáveis.</summary>
    public sealed class ContractSet : IDisposable
    {
        public readonly ProjectileContract MageBolt = CombatMageDefaults.CreateProjectile();
        public readonly ProjectileContract Boulder = CatapultDefaults.CreateProjectile();
        public readonly ProjectileContract Bolt = CrossbowDefaults.CreateProjectile();
        public readonly CombatMageContract Mage;
        public readonly PaladinHealerContract Paladin;
        public readonly CatapultContract Catapult;
        public readonly CrossbowContract Crossbow;

        public ContractSet()
        {
            Mage = CombatMageDefaults.Create(MageBolt);
            Paladin = PaladinHealerDefaults.Create();
            Catapult = CatapultDefaults.Create(Boulder);
            Crossbow = CrossbowDefaults.Create(Bolt);
        }

        public IEnumerable<AdvancedUnitContract> All
        {
            get
            {
                yield return Mage;
                yield return Paladin;
                yield return Catapult;
                yield return Crossbow;
            }
        }

        public void Dispose()
        {
            foreach (UnityEngine.Object o in new UnityEngine.Object[] { Mage, Paladin, Catapult, Crossbow, MageBolt, Boulder, Bolt })
                UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
