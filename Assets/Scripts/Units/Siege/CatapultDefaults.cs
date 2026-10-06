using EraImperial.Units.Advanced;
using UnityEngine;

namespace EraImperial.Units.Siege
{
    /// <summary>
    /// Valores iniciais da Catapulta. Todos os caminhos visuais são apenas destinos esperados:
    /// nenhuma malha, prefab, material ou clipe foi criado.
    /// </summary>
    public static class CatapultDefaults
    {
        public const string ProjectileCode = "catapult_boulder";

        public static CatapultEraTier[] Tiers()
        {
            return new[]
            {
                new CatapultEraTier { era = ImperialEra.Ancient,   healthpoints = 60,  attackDamage = 45,  attackRange = 20f, attackCooldown = 6.0f, minimumRange = 8f, radialDamage = 25, blastRadius = 3f },
                new CatapultEraTier { era = ImperialEra.Classical, healthpoints = 80,  attackDamage = 60,  attackRange = 22f, attackCooldown = 5.5f, minimumRange = 8f, radialDamage = 34, blastRadius = 3.5f },
                new CatapultEraTier { era = ImperialEra.Medieval,  healthpoints = 105, attackDamage = 78,  attackRange = 24f, attackCooldown = 5.0f, minimumRange = 8f, radialDamage = 45, blastRadius = 4f },
                new CatapultEraTier { era = ImperialEra.Imperial,  healthpoints = 135, attackDamage = 100, attackRange = 26f, attackCooldown = 4.5f, minimumRange = 8f, radialDamage = 58, blastRadius = 4.5f }
            };
        }

        public static ProjectileContract CreateProjectile()
        {
            ProjectileContract projectile = ScriptableObject.CreateInstance<ProjectileContract>();
            projectile.name = "CatapultBoulder";
            projectile.code = ProjectileCode;
            projectile.damageType = DamageType.Siege;
            projectile.speed = 14f;
            projectile.maxLifetime = 8f;
            projectile.ballistic = true;
            projectile.arcHeight = 8f;
            projectile.prefab = new VisualAssetReference("projectile prefab", "Assets/Resources/Prefabs/Projectiles/CatapultBoulder.prefab");
            projectile.impactEffect = new VisualAssetReference("impact effect", "Assets/Resources/Prefabs/Effects/CatapultImpact.prefab");
            return projectile;
        }

        public static CatapultContract Create(ProjectileContract projectile)
        {
            CatapultContract contract = ScriptableObject.CreateInstance<CatapultContract>();
            contract.name = "Catapult";
            contract.code = AdvancedUnitCodes.Catapult;
            contract.unitName = "Catapulta";
            contract.description = "Artilharia pesada de cerco, com ataque de área e alcance mínimo.";
            contract.dataKind = UnitDataKind.Character;
            contract.cost = new[] { new ResourceCost("Gold", 120), new ResourceCost("Wood", 100), new ResourceCost("Stone", 40) };
            contract.fieldOfView = 22f;
            contract.tiers = Tiers();
            contract.SetProjectile(projectile);
            contract.visual = new AdvancedVisualContract
            {
                prefab = new VisualAssetReference("prefab", "Assets/Resources/Prefabs/Units/Characters/Catapult.prefab"),
                meshes = new[] { new VisualAssetReference("malha-fonte da catapulta", "Assets/CharacterRigging/Catapult/Catapult.fbx") },
                effects = new[] { new VisualAssetReference("efeito de disparo", "Assets/Resources/Prefabs/Effects/CatapultFire.prefab") },
                sockets = new[]
                {
                    new SocketRequirement("ProjectileSpawn", "Sockets/ProjectileSpawn"),
                    new SocketRequirement("ArmPivot", "Sockets/ArmPivot")
                },
                animations = new[]
                {
                    new AnimationRequirement("Idle", "Running"),
                    new AnimationRequirement("Move", "Running"),
                    new AnimationRequirement("Fire", "Attacking"),
                    new AnimationRequirement("Reload", "Reloading")
                }
            };
            return contract;
        }
    }
}
