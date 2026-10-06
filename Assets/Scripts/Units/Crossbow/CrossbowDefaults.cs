using EraImperial.Units.Advanced;
using UnityEngine;

namespace EraImperial.Units.Crossbow
{
    /// <summary>
    /// Valores iniciais da Balestra. Todos os caminhos visuais são apenas destinos esperados:
    /// nenhuma malha, prefab, material ou clipe foi criado.
    /// </summary>
    public static class CrossbowDefaults
    {
        public const string ProjectileCode = "crossbow_bolt";

        public static CrossbowEraTier[] Tiers()
        {
            return new[]
            {
                new CrossbowEraTier { era = ImperialEra.Ancient,   healthpoints = 28, attackDamage = 22, attackRange = 14f,   attackCooldown = 3.2f, armorPenetration = 0.15f },
                new CrossbowEraTier { era = ImperialEra.Classical, healthpoints = 35, attackDamage = 29, attackRange = 15.5f, attackCooldown = 2.9f, armorPenetration = 0.25f },
                new CrossbowEraTier { era = ImperialEra.Medieval,  healthpoints = 43, attackDamage = 37, attackRange = 17f,   attackCooldown = 2.6f, armorPenetration = 0.35f },
                new CrossbowEraTier { era = ImperialEra.Imperial,  healthpoints = 52, attackDamage = 47, attackRange = 18.5f, attackCooldown = 2.3f, armorPenetration = 0.5f }
            };
        }

        public static ProjectileContract CreateProjectile()
        {
            ProjectileContract projectile = ScriptableObject.CreateInstance<ProjectileContract>();
            projectile.name = "CrossbowBolt";
            projectile.code = ProjectileCode;
            projectile.damageType = DamageType.Piercing;
            projectile.speed = 30f;
            projectile.maxLifetime = 3f;
            projectile.ballistic = false;
            projectile.prefab = new VisualAssetReference("projectile prefab", "Assets/Resources/Prefabs/Projectiles/CrossbowBolt.prefab");
            projectile.impactEffect = new VisualAssetReference("impact effect", "Assets/Resources/Prefabs/Effects/CrossbowImpact.prefab", false);
            return projectile;
        }

        public static CrossbowContract Create(ProjectileContract projectile)
        {
            CrossbowContract contract = ScriptableObject.CreateInstance<CrossbowContract>();
            contract.name = "Crossbow";
            contract.code = AdvancedUnitCodes.Crossbow;
            contract.unitName = "Balestra";
            contract.description = "Ataque perfurante de longo alcance.";
            contract.dataKind = UnitDataKind.Character;
            contract.cost = new[] { new ResourceCost("Gold", 60), new ResourceCost("Wood", 40) };
            contract.fieldOfView = 20f;
            contract.tiers = Tiers();
            contract.SetProjectile(projectile);
            contract.visual = new AdvancedVisualContract
            {
                prefab = new VisualAssetReference("prefab", "Assets/Resources/Prefabs/Units/Characters/Crossbow.prefab"),
                meshes = new[] { new VisualAssetReference("malha-fonte da balestra", "Assets/CharacterRigging/Crossbow/Crossbow.fbx") },
                effects = new VisualAssetReference[0],
                sockets = new[]
                {
                    new SocketRequirement("BoltSpawn", "Sockets/BoltSpawn"),
                    new SocketRequirement("WeaponMount", "Sockets/WeaponMount")
                },
                animations = new[]
                {
                    new AnimationRequirement("Idle", "Running"),
                    new AnimationRequirement("Run", "Running"),
                    new AnimationRequirement("Shoot", "Attacking"),
                    new AnimationRequirement("Reload", "Reloading")
                }
            };
            return contract;
        }
    }
}
