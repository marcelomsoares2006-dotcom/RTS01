using UnityEngine;

/// <summary>
/// Recebe os eventos de animacao do clipe Fire da catapulta (fica no GameObject do Animator, na raiz do visual).
/// CatapultRelease: instante em que a pedra sai da concha; toca o efeito de disparo (poeira/fumaca) no socket ProjectileSpawn.
/// O projetil e o dano pertencem ao CatapultCombatController (AdvancedBridge); aqui so ha o efeito visual.
/// </summary>
public sealed class CatapultAnimationEvents : MonoBehaviour
{
    public Transform projectileSpawn;
    public GameObject fireEffectPrefab;
    public float effectLifetime = 3f;

    /// <summary>Contadores estaticos usados pela verificacao em Play Mode.</summary>
    public static int ReleaseEvents { get; private set; }
    public static int FireEffectsSpawned { get; private set; }
    public static Vector3 LastReleasePosition { get; private set; }

    // Chamado pelo evento de animacao "CatapultRelease" do clipe Fire.
    public void CatapultRelease()
    {
        ReleaseEvents++;
        Vector3 position = projectileSpawn != null ? projectileSpawn.position : transform.position + Vector3.up * 1.2f;
        LastReleasePosition = position;
        if (fireEffectPrefab == null) return;
        GameObject instance = Instantiate(fireEffectPrefab, position, Quaternion.identity);
        instance.name = fireEffectPrefab.name;
        FireEffectsSpawned++;
        Destroy(instance, effectLifetime);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ReleaseEvents = 0;
        FireEffectsSpawned = 0;
        LastReleasePosition = Vector3.zero;
    }
}
