using System.Collections.Generic;
using System.IO;
using System.Linq;
using EraImperial.Units.Advanced;
using NUnit.Framework;
using UnityEngine;

namespace EraImperial.AdvancedUnits.Tests
{
    /// <summary>5. Nenhuma referência visual inexistente pode ser tratada como pronta.</summary>
    public class VisualReadinessTests
    {
        private ContractSet _set;
        private readonly List<UnityEngine.Object> _doubles = new List<UnityEngine.Object>();

        [SetUp] public void SetUp() { _set = new ContractSet(); }

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object o in _doubles) UnityEngine.Object.DestroyImmediate(o);
            _doubles.Clear();
            _set.Dispose();
        }

        private T Track<T>(T o) where T : UnityEngine.Object { _doubles.Add(o); return o; }

        [Test]
        public void ShippedDefaults_ReferenceNoRealAsset_SoNoUnitIsVisuallyReady()
        {
            foreach (AdvancedUnitContract contract in _set.All)
            {
                VisualReadinessReport report = contract.EvaluateVisuals();
                Assert.That(report.IsReady, Is.False, contract.code);
                Assert.That(contract.IsVisualReady, Is.False, contract.code);
                Assert.That(report.ToString(), Does.Contain("prefab ausente"), contract.code);
                Assert.That(report.ToString(), Does.Contain("socket"), contract.code);
                Assert.That(report.ToString(), Does.Contain("animação"), contract.code);
            }
        }

        [Test]
        public void Defaults_DeclareExpectedPaths_AndExistingShippedAssetsAreIntegrated()
        {
            foreach (AdvancedUnitContract contract in _set.All)
            {
                List<VisualAssetReference> references = new List<VisualAssetReference> { contract.visual.prefab };
                references.AddRange(contract.visual.meshes);
                references.AddRange(contract.visual.effects);
                if (contract.Projectile != null)
                {
                    references.Add(contract.Projectile.prefab);
                    references.Add(contract.Projectile.impactEffect);
                }

                foreach (VisualAssetReference r in references)
                {
                    Assert.That(r.IsResolved, Is.False, $"{contract.code}: {r.role}");
                    Assert.That(r.asset == null, Is.True, $"{contract.code}: {r.role}");
                    Assert.That(r.expectedAssetPath, Does.StartWith("Assets/"), $"{contract.code}: {r.role}");
                    string onDisk = Path.GetFullPath(Path.Combine(Application.dataPath, "..", r.expectedAssetPath));
                    // Os Defaults são referências de proposta, portanto permanecem não resolvidos em memória.
                    // Quando um asset já foi criado no projeto, o contrato gravado precisa apontar para o visual integrado.
                    if (File.Exists(onDisk))
                    {
                        string contractPath = "Assets/Data/AdvancedUnits/" + contract.name + ".asset";
                        AdvancedUnitContract shipped = UnityEditor.AssetDatabase.LoadAssetAtPath<AdvancedUnitContract>(contractPath);
                        Assert.That(shipped, Is.Not.Null, $"{contract.code}: '{r.expectedAssetPath}' existe mas o contrato gravado nao foi encontrado");
                        Assert.That(shipped.IsVisualReady, Is.True,
                            $"{contract.code}: '{r.expectedAssetPath}' existe em disco mas o contrato gravado nao esta com o visual pronto");
                    }
                }
            }
        }

        [Test]
        public void ProjectileVisuals_AreReportedPendingSeparatelyFromTheUnit()
        {
            Assert.That(_set.Boulder.EvaluateVisuals().IsReady, Is.False);
            Assert.That(_set.Boulder.EvaluateVisuals().ToString(), Does.Contain("catapult_boulder"));
            Assert.That(_set.Catapult.EvaluateVisuals().ToString(), Does.Contain("catapult_boulder"));
            Assert.That(_set.Paladin.EvaluateVisuals().ToString(), Does.Not.Contain("projétil"), "o paladino não tem projétil");
        }

        [Test]
        public void ApprovalFlagAloneNeverMakesAnUnitBuildReady()
        {
            foreach (AdvancedUnitContract contract in _set.All)
            {
                contract.buildSettingsApproved = true;
                Assert.That(contract.IsGameplayValid, Is.True, contract.code);
                Assert.That(contract.CanBeAddedToBuildSettings, Is.False, contract.code + ": visual pendente");
            }
        }

        [Test]
        public void NullAssignedPathStringOrDestroyedObject_IsNotAResolvedReference()
        {
            VisualAssetReference declaredOnly = new VisualAssetReference("prefab", "Assets/NaoExiste/Fantasma.prefab");
            Assert.That(declaredOnly.IsResolved, Is.False);

            GameObject temporary = new GameObject("temporario");
            VisualAssetReference reference = new VisualAssetReference("prefab", "Assets/Qualquer.prefab") { asset = temporary };
            Assert.That(reference.IsResolved, Is.True);
            UnityEngine.Object.DestroyImmediate(temporary);
            Assert.That(reference.IsResolved, Is.False, "referência quebrada (objeto destruído) não é pronta");

            AdvancedVisualContract contract = new AdvancedVisualContract { prefab = reference };
            Assert.That(contract.Evaluate().IsReady, Is.False);
        }

        [Test]
        public void PrefabSlotRejectsNonGameObjectAssets()
        {
            AdvancedVisualContract contract = new AdvancedVisualContract();
            contract.prefab = new VisualAssetReference("prefab", "x") { asset = Track(new AnimationClip()) };
            Assert.That(contract.Evaluate().IsReady, Is.False, "um AnimationClip não é um prefab");
        }

        [Test]
        public void SocketsAreOnlyReadyWhenTheRealPrefabContainsThem_AndAnimationsNeedClips()
        {
            // Duplo de teste em memória (não é asset do projeto): só prova que o portão consegue abrir e voltar a fechar.
            GameObject root = Track(new GameObject("TestDoubleRoot"));
            GameObject sockets = new GameObject("Sockets");
            sockets.transform.SetParent(root.transform);
            GameObject cast = new GameObject("CastPoint");
            cast.transform.SetParent(sockets.transform);

            AdvancedVisualContract contract = new AdvancedVisualContract
            {
                prefab = new VisualAssetReference("prefab", "x") { asset = root },
                sockets = new[] { new SocketRequirement("CastPoint", "Sockets/CastPoint") },
                animations = new[] { new AnimationRequirement("Cast", "Attacking") }
            };

            Assert.That(contract.Evaluate().ToString(), Does.Contain("animação 'Cast'"), "sem clipe ainda não está pronto");
            Assert.That(contract.Evaluate().ToString(), Does.Not.Contain("socket"), "o socket existe no prefab");

            contract.animations[0].clip = Track(new AnimationClip { name = "TestClip" });
            Assert.That(contract.Evaluate().IsReady, Is.True, "com prefab, socket e clipe reais o contrato abre");

            contract.sockets = new[] { new SocketRequirement("CastPoint", "Sockets/CastPoint"), new SocketRequirement("Staff", "Sockets/Staff") };
            Assert.That(contract.Evaluate().IsReady, Is.False, "um socket que não existe no prefab bloqueia");
            Assert.That(contract.Evaluate().ToString(), Does.Contain("Staff"));

            contract.sockets = new[] { new SocketRequirement("Optional", "Sockets/Optional", required: false) };
            Assert.That(contract.Evaluate().IsReady, Is.True, "itens opcionais não bloqueiam");
        }

        [Test]
        public void ContractWithoutPrefab_IsNeverReadyEvenWithAClipAndOptionalEverything()
        {
            AdvancedVisualContract contract = new AdvancedVisualContract
            {
                animations = new[] { new AnimationRequirement("Idle", "Running") { clip = Track(new AnimationClip()) } }
            };
            Assert.That(contract.Evaluate().IsReady, Is.False);
            Assert.That(new AdvancedVisualContract { prefab = null }.Evaluate().IsReady, Is.False);
        }

        [Test]
        public void ReferencesAreNotSharedBetweenUnits()
        {
            Assert.That(_set.Mage.visual.prefab, Is.Not.SameAs(_set.Catapult.visual.prefab));
            Assert.That(_set.Mage.visual.prefab.expectedAssetPath, Is.Not.EqualTo(_set.Catapult.visual.prefab.expectedAssetPath));
            string[] prefabPaths = _set.All.Select(c => c.visual.prefab.expectedAssetPath).ToArray();
            Assert.That(prefabPaths, Is.Unique);
        }
    }
}
