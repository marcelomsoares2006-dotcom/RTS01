using System;
using System.IO;
using EraImperial.Units.Crossbow;
using EraImperial.Units.Mage;
using EraImperial.Units.Paladin;
using EraImperial.Units.Siege;
using UnityEditor;
using UnityEngine;

namespace EraImperial.Units.Advanced.Editor
{
    /// <summary>
    /// Gera os assets de contrato das quatro unidades avançadas (e dos três projéteis) a partir dos *Defaults.
    /// Os assets ficam em Assets/Data/AdvancedUnits, fora de Resources, e NÃO são adicionados ao Build Settings.
    /// Só cria o que ainda não existe: nunca sobrescreve um asset já ajustado no Inspector.
    /// Menu: Era Imperial > Advanced Units > Criar assets de contrato.
    /// </summary>
    public static class AdvancedUnitAssetGenerator
    {
        public const string RootFolder = "Assets/Data/AdvancedUnits";
        public const string ProjectileFolder = RootFolder + "/Projectiles";

        [MenuItem("Era Imperial/Advanced Units/Criar assets de contrato")]
        public static void Generate()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder(RootFolder);
            EnsureFolder(ProjectileFolder);

            ProjectileContract mageBolt = CreateOrLoad(ProjectileFolder + "/CombatMageBolt.asset", CombatMageDefaults.CreateProjectile);
            ProjectileContract boulder = CreateOrLoad(ProjectileFolder + "/CatapultBoulder.asset", CatapultDefaults.CreateProjectile);
            ProjectileContract bolt = CreateOrLoad(ProjectileFolder + "/CrossbowBolt.asset", CrossbowDefaults.CreateProjectile);

            CreateOrLoad(RootFolder + "/CombatMage.asset", () => CombatMageDefaults.Create(mageBolt));
            CreateOrLoad(RootFolder + "/PaladinHealer.asset", PaladinHealerDefaults.Create);
            CreateOrLoad(RootFolder + "/Catapult.asset", () => CatapultDefaults.Create(boulder));
            CreateOrLoad(RootFolder + "/Crossbow.asset", () => CrossbowDefaults.Create(bolt));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AdvancedUnits] Assets de contrato verificados em " + RootFolder + ". Nenhuma malha, prefab ou material foi criado.");
        }

        /// <summary>Ponto de entrada para execução em batch (-executeMethod).</summary>
        public static void GenerateBatch()
        {
            try
            {
                Generate();
            }
            catch (Exception exception)
            {
                Debug.LogError("[AdvancedUnits] Falha ao gerar assets: " + exception);
                EditorApplication.Exit(1);
            }
        }

        private static T CreateOrLoad<T>(string path, Func<T> factory) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            T created = factory();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
