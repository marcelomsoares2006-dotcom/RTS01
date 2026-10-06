using EraImperial.Units.Advanced;
using UnityEngine;

namespace EraImperial.Units.Mage
{
    /// <summary>
    /// Valores iniciais de balanceamento do Mago de combate (referência: Soldier 30 PV/dano 20/alcance 1,
    /// Spearman 45 PV/dano 24/alcance 1,6). Os assets .asset são gerados a partir daqui e depois ajustáveis no Inspector.
    /// Todos os caminhos visuais abaixo são apenas destinos esperados: nenhum asset foi criado.
    /// </summary>
    public static class CombatMageDefaults
    {
        public const string ProjectileCode = "combat_mage_bolt";

        public static MageEraTier[] Tiers()
        {
            return new[]
            {
                new MageEraTier { era = ImperialEra.Ancient,   healthpoints = 22, attackDamage = 18, attackRange = 8f,  attackCooldown = 2.4f },
                new MageEraTier { era = ImperialEra.Classical, healthpoints = 28, attackDamage = 24, attackRange = 9f,  attackCooldown = 2.2f },
                new MageEraTier { era = ImperialEra.Medieval,  healthpoints = 34, attackDamage = 31, attackRange = 10f, attackCooldown = 2.0f },
                new MageEraTier { era = ImperialEra.Imperial,  healthpoints = 42, attackDamage = 40, attackRange = 11f, attackCooldown = 1.8f }
            };
        }

        public static ProjectileContract CreateProjectile()
        {
            ProjectileContract projectile = ScriptableObject.CreateInstance<ProjectileContract>();
            projectile.name = "CombatMageBolt";
            projectile.code = ProjectileCode;
            projectile.damageType = DamageType.Magic;
            projectile.speed = 16f;
            projectile.maxLifetime = 4f;
            projectile.ballistic = false;
            projectile.prefab = new VisualAssetReference("projectile prefab", "Assets/Resources/Prefabs/Projectiles/CombatMageBolt.prefab");
            projectile.impactEffect = new VisualAssetReference("impact effect", "Assets/Resources/Prefabs/Effects/CombatMageImpact.prefab");
            return projectile;
        }

        public static CombatMageContract Create(ProjectileContract projectile)
        {
            CombatMageContract contract = ScriptableObject.CreateInstance<CombatMageContract>();
            contract.name = "CombatMage";
            contract.code = AdvancedUnitCodes.CombatMage;
            contract.unitName = "Mago de Combate";
            contract.description = "Conjurador de ataque mágico à distância.";
            contract.dataKind = UnitDataKind.Character;
            contract.cost = new[] { new ResourceCost("Gold", 70), new ResourceCost("Stone", 30) };
            contract.fieldOfView = 16f;
            contract.tiers = Tiers();
            contract.SetProjectile(projectile);
            contract.visual = new AdvancedVisualContract
            {
                prefab = new VisualAssetReference("prefab", "Assets/Resources/Prefabs/Units/Characters/CombatMage.prefab"),
                meshes = new[] { new VisualAssetReference("malha-fonte do corpo", "Assets/CharacterRigging/CombatMage/CombatMage.fbx") },
                effects = new[]
                {
                    new VisualAssetReference("efeito de conjuração", "Assets/Resources/Prefabs/Effects/CombatMageCast.prefab"),
                    new VisualAssetReference("efeito de impacto", "Assets/Resources/Prefabs/Effects/CombatMageImpact.prefab")
                },
                sockets = new[]
                {
                    new SocketRequirement("CastPoint", "Sockets/CastPoint"),
                    new SocketRequirement("StaffMount", "Sockets/StaffMount")
                },
                animations = new[]
                {
                    new AnimationRequirement("Idle", "Running"),
                    new AnimationRequirement("Run", "Running"),
                    new AnimationRequirement("Cast", "Attacking"),
                    new AnimationRequirement("Hit", "Hit")
                }
            };
            return contract;
        }
    }
}
