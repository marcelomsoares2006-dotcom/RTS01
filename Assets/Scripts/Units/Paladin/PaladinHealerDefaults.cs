using EraImperial.Units.Advanced;
using UnityEngine;

namespace EraImperial.Units.Paladin
{
    /// <summary>
    /// Valores iniciais do Paladino curador. Todos os caminhos visuais são apenas destinos esperados:
    /// nenhuma malha, prefab, material ou clipe foi criado.
    /// </summary>
    public static class PaladinHealerDefaults
    {
        public static PaladinEraTier[] Tiers()
        {
            return new[]
            {
                new PaladinEraTier { era = ImperialEra.Ancient,   healthpoints = 70,  attackDamage = 16, attackRange = 1.5f, attackCooldown = 1.8f, healAmount = 6,  healRadius = 4f,   healCooldown = 3.0f, maxHealPerTarget = 18 },
                new PaladinEraTier { era = ImperialEra.Classical, healthpoints = 90,  attackDamage = 20, attackRange = 1.5f, attackCooldown = 1.7f, healAmount = 9,  healRadius = 4.5f, healCooldown = 2.8f, maxHealPerTarget = 27 },
                new PaladinEraTier { era = ImperialEra.Medieval,  healthpoints = 115, attackDamage = 25, attackRange = 1.5f, attackCooldown = 1.6f, healAmount = 13, healRadius = 5f,   healCooldown = 2.5f, maxHealPerTarget = 39 },
                new PaladinEraTier { era = ImperialEra.Imperial,  healthpoints = 145, attackDamage = 31, attackRange = 1.5f, attackCooldown = 1.5f, healAmount = 18, healRadius = 6f,   healCooldown = 2.2f, maxHealPerTarget = 54 }
            };
        }

        public static PaladinHealerContract Create()
        {
            PaladinHealerContract contract = ScriptableObject.CreateInstance<PaladinHealerContract>();
            contract.name = "PaladinHealer";
            contract.code = AdvancedUnitCodes.PaladinHealer;
            contract.unitName = "Paladino Curador";
            contract.description = "Guerreiro corpo a corpo que cura aliados ao redor.";
            contract.dataKind = UnitDataKind.Character;
            contract.cost = new[] { new ResourceCost("Gold", 80), new ResourceCost("Wood", 20) };
            contract.fieldOfView = 16f;
            contract.tiers = Tiers();
            contract.healCapWindowSeconds = 10f;
            contract.canHealSelf = false;
            contract.visual = new AdvancedVisualContract
            {
                prefab = new VisualAssetReference("prefab", "Assets/Resources/Prefabs/Units/Characters/PaladinHealer.prefab"),
                meshes = new[] { new VisualAssetReference("malha-fonte do corpo", "Assets/CharacterRigging/PaladinHealer/PaladinHealer.fbx") },
                effects = new[] { new VisualAssetReference("efeito de aura de cura", "Assets/Resources/Prefabs/Effects/PaladinHealAura.prefab") },
                sockets = new[]
                {
                    new SocketRequirement("WeaponMount", "Sockets/WeaponMount"),
                    new SocketRequirement("ShieldMount", "Sockets/ShieldMount"),
                    new SocketRequirement("HealAuraCenter", "Sockets/HealAuraCenter")
                },
                animations = new[]
                {
                    new AnimationRequirement("Idle", "Running"),
                    new AnimationRequirement("Run", "Running"),
                    new AnimationRequirement("Attack", "Attacking"),
                    new AnimationRequirement("Hit", "Hit"),
                    new AnimationRequirement("Heal", "Healing")
                }
            };
            return contract;
        }
    }
}
