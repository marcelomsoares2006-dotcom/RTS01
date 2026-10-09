using UnityEngine;

/// <summary>
/// Permite a uma unidade substituir o ataque instantaneo de UnitManager.Attack
/// (por exemplo, disparar um projetil no momento certo da animacao).
/// Retorne true quando o ataque foi tratado e o dano imediato nao deve ser aplicado.
/// </summary>
public interface IAttackHandler
{
    bool TryHandleAttack(Transform target);
}
