using UnityEngine;

/// <summary>
/// Parâmetros de ataque à distância, sem referência a Animator, prefab ou malha.
/// Pode ser integrado ao sistema de combate quando existir uma unidade visual válida.
/// </summary>
[CreateAssetMenu(fileName = "RangedAttackProfile", menuName = "Era Imperial/Combat/Ranged Attack Profile")]
public sealed class RangedAttackProfile : ScriptableObject
{
    [Min(0f)] public float projectileSpeed = 18f;
    [Min(0f)] public float maximumRange = 12f;
    [Min(0f)] public float attackCooldown = 1f;
    [Min(0)] public int damage = 1;

    public bool CanAttack(float distance, float elapsedSinceLastAttack)
    {
        return distance >= 0f && distance <= maximumRange && elapsedSinceLastAttack >= attackCooldown;
    }
}
