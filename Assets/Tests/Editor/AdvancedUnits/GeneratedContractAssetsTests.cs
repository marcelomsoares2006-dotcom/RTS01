using System.Collections.Generic;
using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Advanced.Editor;
using NUnit.Framework;
using UnityEngine;
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
        public void ContractAssets_VisualReadinessIsRealOrExplicitlyPending()
        {
            foreach (AdvancedUnitContract contract in LoadContracts())
            {
                VisualReadinessReport report = contract.EvaluateVisuals();
                // consistencia: "pronto" so existe quando o prefab do contrato e um asset real em disco
                GameObject prefab = contract.visual.prefab.asset as GameObject;
                if (report.IsReady)
                {
                    Assert.That(prefab != null, Is.True, contract.code + ": pronto sem prefab");
                    string path = AssetDatabase.GetAssetPath(prefab);
                    Assert.That(path, Does.StartWith("Assets/Resources/Prefabs/Units/Characters/"), contract.code);
                    Assert.That(System.IO.File.Exists(path), Is.True, contract.code + ": prefab inexistente em disco");
                    Assert.That(contract.visual.prefab.expectedAssetPath, Is.EqualTo(path), contract.code);
                }
                else
                {
                    Assert.That(report.Missing, Is.Not.Empty, contract.code);
                    Assert.That(contract.CanBeAddedToBuildSettings, Is.False, contract.code);
                }
                // um contrato so pode ser aprovado para o Build Settings se estiver realmente pronto
                if (contract.buildSettingsApproved)
                    Assert.That(contract.IsGameplayValid && report.IsReady, Is.True, contract.code + ": aprovado sem estar pronto");
            }
        }
    }
}
