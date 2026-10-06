using System.Collections.Generic;
using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Advanced.Editor;
using NUnit.Framework;
using UnityEditor;

namespace EraImperial.AdvancedUnits.Tests
{
    /// <summary>Os assets de contrato gravados em Assets/Data/AdvancedUnits (gerados pelo AdvancedUnitAssetGenerator).</summary>
    public class GeneratedContractAssetsTests
    {
        private static List<AdvancedUnitContract> LoadContracts()
        {
            return AssetDatabase.FindAssets("t:AdvancedUnitContract", new[] { AdvancedUnitAssetGenerator.RootFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<AdvancedUnitContract>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToList();
        }

        [Test]
        public void FourContractAssetsExist_WithTheRequiredUniqueCodes()
        {
            List<AdvancedUnitContract> contracts = LoadContracts();
            Assert.That(contracts.Select(c => c.code), Is.EquivalentTo(AdvancedUnitCodes.All));
            Assert.That(contracts.Select(c => c.code), Is.Unique);
        }

        [Test]
        public void ContractAssets_AreValid_ProgressiveAndHaveSeparateProjectileAssets()
        {
            foreach (AdvancedUnitContract contract in LoadContracts())
            {
                Assert.That(contract.Validate().IsValid, Is.True, $"{contract.code}: {contract.Validate()}");
                Assert.That(contract.GetTiers().Select(t => t.Era), Is.EqualTo(ImperialEras.Ordered), contract.code);

                if (contract.code == AdvancedUnitCodes.PaladinHealer)
                {
                    Assert.That(contract.Projectile, Is.Null);
                    continue;
                }
                Assert.That(contract.Projectile, Is.Not.Null, contract.code);
                Assert.That(AssetDatabase.GetAssetPath(contract.Projectile), Does.StartWith(AdvancedUnitAssetGenerator.ProjectileFolder), contract.code);
            }
        }

        [Test]
        public void ContractAssets_ArePendingVisuallyAndNotApprovedForBuild()
        {
            foreach (AdvancedUnitContract contract in LoadContracts())
            {
                Assert.That(contract.visual.prefab.asset == null, Is.True, contract.code);
                Assert.That(contract.IsVisualReady, Is.False, contract.code);
                Assert.That(contract.buildSettingsApproved, Is.False, contract.code);
                Assert.That(contract.CanBeAddedToBuildSettings, Is.False, contract.code);
            }
        }
    }
}
