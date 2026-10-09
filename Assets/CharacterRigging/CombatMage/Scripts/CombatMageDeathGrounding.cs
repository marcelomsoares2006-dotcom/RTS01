using UnityEngine;

/// <summary>
/// Prefab de morte do Mago: o jogo instancia o corpo na posicao da unidade (altura do NavMesh), que em alguns mapas fica
/// abaixo da superficie visivel do terreno. Ao nascer, projeta um raio para baixo nas camadas Terrain/FlatTerrain e
/// alinha a origem ao chao encontrado; o clipe Death ja mantem todos os vertices acima de y = 0.02 m da origem.
/// </summary>
public sealed class CombatMageDeathGrounding : MonoBehaviour
{
    public float maxAdjust = 1.5f;
    public LayerMask groundMask = (1 << 8) | (1 << 10);   // Terrain, FlatTerrain (Globals.TERRAIN_LAYER_MASK | FLAT_TERRAIN_LAYER_MASK)

    public float LastAdjust { get; private set; }

    private void Start()
    {
        Ground();
    }

    public void Ground()
    {
        Vector3 origin = transform.position + Vector3.up * (maxAdjust + 1f);
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2f * maxAdjust + 2f, groundMask, QueryTriggerInteraction.Ignore)) return;
        float delta = hit.point.y - transform.position.y;
        if (Mathf.Abs(delta) > maxAdjust) return;
        transform.position += Vector3.up * delta;
        LastAdjust = delta;
    }
}
