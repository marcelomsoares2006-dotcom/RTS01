using UnityEngine;

/// <summary>Alvo de teste/demonstracao do Archer (recebe TakeHit como as unidades do jogo).</summary>
public sealed class ArcherTargetDummy : MonoBehaviour
{
    public int hitPoints = 100;
    public int hits;
    public int lastDamage;

    public void TakeHit(int damage)
    {
        hits++;
        lastDamage = damage;
        hitPoints -= damage;
    }
}
