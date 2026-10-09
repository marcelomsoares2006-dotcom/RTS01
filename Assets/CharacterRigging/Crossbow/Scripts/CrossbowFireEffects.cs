using UnityEngine;

/// <summary>
/// Efeitos de disparo da Balestra, acionados por eventos de animacao do clipe Fire (instante em que a corda e solta).
/// Fica no GameObject do Animator do visual. O projetil e criado a parte, por ProjectileAttackController.
/// </summary>
public sealed class CrossbowFireEffects : MonoBehaviour
{
    [Tooltip("Fumaca/poeira leve na ponta do trilho.")]
    public ParticleSystem muzzle;

    /// <summary>Quantas vezes a corda foi solta (todas as balestras, para testes).</summary>
    public static int ReleaseCount { get; private set; }

    public int Releases { get; private set; }

    /// <summary>Evento de animacao do clipe Fire.</summary>
    public void CrossbowRelease()
    {
        Releases++;
        ReleaseCount++;
        if (muzzle != null) muzzle.Play(true);
    }
}
