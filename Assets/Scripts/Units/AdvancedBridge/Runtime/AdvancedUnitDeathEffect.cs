using UnityEngine;

/// <summary>
/// UnitManager destroi a unidade no mesmo instante em que a vida chega a zero, sem hook de animacao. Este
/// componente instancia, no lugar, um prefab de morte/destruicao (visual com animacao Death/Destroy) e o remove
/// depois de alguns segundos. Nada acontece quando o objeto e destruido por troca de cena ou fim do jogo.
/// </summary>
public sealed class AdvancedUnitDeathEffect : MonoBehaviour
{
    public GameObject deathPrefab;
    public float lifetime = 8f;

    private UnitManager _manager;
    private bool _quitting;

    public static int DeathsSpawned { get; private set; }

    private void Awake()
    {
        _manager = GetComponent<UnitManager>();
    }

    private void OnApplicationQuit()
    {
        _quitting = true;
    }

    private void OnDestroy()
    {
        if (_quitting || deathPrefab == null || !gameObject.scene.isLoaded) return;
        if (_manager == null || _manager.Unit == null || _manager.Unit.HP > 0) return;

        GameObject instance = Instantiate(deathPrefab, transform.position, transform.rotation);
        DeathsSpawned++;
        Destroy(instance, lifetime);
    }
}
