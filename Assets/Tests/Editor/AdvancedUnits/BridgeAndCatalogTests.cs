using System;
using System.Linq;
using System.Reflection;
using EraImperial.Units.Advanced;
using EraImperial.Units.Advanced.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EraImperial.AdvancedUnits.Tests
{
    /// <summary>
    /// Compatibilidade com UnitData/CharacterData/BuildingData (Assembly-CSharp, acessado por reflexão porque o
    /// asmdef de testes não referencia o Assembly-CSharp) e garantias de catálogo/Build Settings.
    /// </summary>
    public class BridgeAndCatalogTests
    {
        private const string BridgeType = "AdvancedUnitDataBridge, Assembly-CSharp";
        private ContractSet _set;
        private ScriptableObject _characterData;
        private ScriptableObject _buildingData;

        [SetUp]
        public void SetUp()
        {
            _set = new ContractSet();
            _characterData = ScriptableObject.CreateInstance("CharacterData");
            _buildingData = ScriptableObject.CreateInstance("BuildingData");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_characterData);
            UnityEngine.Object.DestroyImmediate(_buildingData);
            _set.Dispose();
        }

        private static Type Bridge()
        {
            Type type = Type.GetType(BridgeType);
            Assert.That(type, Is.Not.Null, "AdvancedUnitDataBridge não encontrado no Assembly-CSharp");
            return type;
        }

        [Test]
        public void UnitDataAndBuildingData_ExposeEveryFieldTheContractsMapTo()
        {
            string[] mapped = { "code", "unitName", "description", "healthpoints", "cost", "fieldOfView", "attackRange", "attackDamage", "attackRate", "prefab" };
            foreach (ScriptableObject data in new[] { _characterData, _buildingData })
            {
                Assert.That(data, Is.Not.Null);
                SerializedObject serialized = new SerializedObject(data);
                foreach (string field in mapped)
                    Assert.That(serialized.FindProperty(field), Is.Not.Null, $"{data.GetType().Name}.{field}");
            }
        }

        [Test]
        public void Bridge_CopiesEraStatsIntoCharacterData_AndMapsCostToResourceValues()
        {
            MethodInfo applyStats = Bridge().GetMethod("ApplyStats");
            UnitDataFields fields = _set.Catapult.BuildUnitDataFields(ImperialEra.Medieval);
            applyStats.Invoke(null, new object[] { _characterData, fields });

            SerializedObject s = new SerializedObject(_characterData);
            Assert.That(s.FindProperty("code").stringValue, Is.EqualTo("catapult"));
            Assert.That(s.FindProperty("unitName").stringValue, Is.EqualTo("Catapulta"));
            Assert.That(s.FindProperty("healthpoints").intValue, Is.EqualTo(105));
            Assert.That(s.FindProperty("attackDamage").intValue, Is.EqualTo(78));
            Assert.That(s.FindProperty("attackRange").floatValue, Is.EqualTo(24f));
            Assert.That(s.FindProperty("attackRate").floatValue, Is.EqualTo(5f));
            SerializedProperty cost = s.FindProperty("cost");
            Assert.That(cost.arraySize, Is.EqualTo(3));
            Assert.That(cost.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue, Is.EqualTo(120));
            Assert.That(cost.GetArrayElementAtIndex(0).FindPropertyRelative("code").enumValueIndex, Is.EqualTo(0), "Gold");
            Assert.That(cost.GetArrayElementAtIndex(1).FindPropertyRelative("code").enumValueIndex, Is.EqualTo(1), "Wood");
            Assert.That(cost.GetArrayElementAtIndex(2).FindPropertyRelative("code").enumValueIndex, Is.EqualTo(2), "Stone");
            Assert.That(s.FindProperty("prefab").objectReferenceValue, Is.Null, "ApplyStats nunca liga prefab");
        }

        [Test]
        public void Bridge_RefusesToBindContractsWhoseVisualsAreNotReal()
        {
            Type bridge = Bridge();
            MethodInfo canBind = bridge.GetMethod("CanBind");
            MethodInfo apply = bridge.GetMethod("Apply");

            foreach (AdvancedUnitContract contract in _set.All)
            {
                object[] args = { contract, null };
                bool result = (bool)canBind.Invoke(null, args);
                Assert.That(result, Is.False, contract.code);
                Assert.That((string)args[1], Does.Contain("visual"), contract.code);

                ScriptableObject target = contract.dataKind == UnitDataKind.Character ? _characterData : _buildingData;
                TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(
                    () => apply.Invoke(null, new object[] { target, contract, ImperialEra.Ancient }), contract.code);
                Assert.That(thrown.InnerException, Is.TypeOf<InvalidOperationException>(), contract.code);
            }

            SerializedObject untouched = new SerializedObject(_characterData);
            Assert.That(untouched.FindProperty("code").stringValue, Is.Empty, "nenhum UnitData foi preenchido por contrato sem visual");
        }

        [Test]
        public void Bridge_RefusesInvalidGameplayDataBeforeLookingAtVisuals()
        {
            _set.Crossbow.tiers[1].attackDamage = 1;
            object[] args = { _set.Crossbow, null };
            Assert.That((bool)Bridge().GetMethod("CanBind").Invoke(null, args), Is.False);
            Assert.That((string)args[1], Does.Contain("dados de jogo inválidos"));
        }

        [Test]
        public void NoAdvancedUnitIsRegisteredInTheGameCatalog()
        {
            string[] codes = AdvancedUnitCodes.All.ToArray();
            foreach (string guid in AssetDatabase.FindAssets("t:UnitData"))
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                string code = new SerializedObject(asset).FindProperty("code").stringValue;
                Assert.That(codes, Does.Not.Contain(code), $"{AssetDatabase.GUIDToAssetPath(guid)} registrou '{code}' no catálogo sem visual validado");
            }
            Assert.That(AssetDatabase.FindAssets("t:AdvancedUnitContract", new[] { "Assets/Resources" }), Is.Empty, "contratos ficam fora de Resources");
        }

        [Test]
        public void BuildSettings_DoNotReachAnyAdvancedUnitAsset()
        {
            string[] advancedPaths = AssetDatabase.FindAssets("t:AdvancedUnitContract", new[] { AdvancedUnitAssetGenerator.RootFolder })
                .Concat(AssetDatabase.FindAssets("t:ProjectileContract", new[] { AdvancedUnitAssetGenerator.RootFolder }))
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                string[] dependencies = AssetDatabase.GetDependencies(scene.path, true);
                foreach (string path in advancedPaths)
                    Assert.That(dependencies, Does.Not.Contain(path), $"{scene.path} depende de {path}");
                Assert.That(scene.path, Does.Not.Contain("Catapult"));
            }
        }
    }
}
