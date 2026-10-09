using UnityEngine;

/// <summary>
/// Efeito visual do pulso de cura do Paladino. O prefab e autorado com 1 unidade de diametro em XZ: o
/// PaladinHealBehaviour o escala para (2*raio, 1, 2*raio). Os aneis expandem do centro ate a borda do raio e somem;
/// o disco pulsa; as particulas (escala "Shape") sobem dentro da area. Somente visual, nao altera dados de jogo.
/// </summary>
public sealed class PaladinHealAuraEffect : MonoBehaviour
{
    static readonly int TintColor = Shader.PropertyToID("_TintColor");

    public Renderer ring;
    public Renderer ringTrail;
    public Renderer disc;
    public float duration = 1.4f;
    public float expandTime = 0.75f;
    public float trailDelay = 0.18f;

    private MaterialPropertyBlock _block;
    private Color _ringColor, _trailColor, _discColor;
    private float _age;

    public float Age => _age;
    public bool Finished => _age >= duration;

    private static Color BaseTint(Renderer r)
    {
        return r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty(TintColor) ? r.sharedMaterial.GetColor(TintColor) : Color.white;
    }

    private bool _ready;

    private void EnsureReady()
    {
        if (_ready) return;
        _ready = true;
        _block = new MaterialPropertyBlock();
        _ringColor = BaseTint(ring);
        _trailColor = BaseTint(ringTrail);
        _discColor = BaseTint(disc);
    }

    private void Awake()
    {
        EnsureReady();
        Apply(0f);
    }

    /// <summary>Avalia o efeito no instante t (s). Usado por auditorias em Edit Mode; em jogo o Update avanca sozinho.</summary>
    public void Evaluate(float t)
    {
        EnsureReady();
        _age = t;
        Apply(t);
    }

    private void Update()
    {
        EnsureReady();
        _age += Time.deltaTime;
        Apply(_age);
    }

    private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    private static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - Mathf.Pow(1f - t, 3f); }

    private void Tint(Renderer r, Color c, float alpha)
    {
        if (r == null) return;
        r.GetPropertyBlock(_block);
        c.a *= Mathf.Clamp01(alpha);
        _block.SetColor(TintColor, c);
        r.SetPropertyBlock(_block);
        r.enabled = alpha > 0.002f;
    }

    private void Wave(Renderer r, Color baseColor, float t)
    {
        if (r == null) return;
        float e = EaseOut(t / expandTime);
        float s = Mathf.Lerp(0.10f, 1f, e);
        r.transform.localScale = new Vector3(s, 1f, s);
        float fadeIn = Smooth(t / 0.08f);
        float fadeOut = 1f - Smooth((t - expandTime * 0.55f) / (duration - expandTime * 0.55f));
        Tint(r, baseColor, t < 0f ? 0f : fadeIn * fadeOut);
    }

    private void Apply(float t)
    {
        Wave(ring, _ringColor, t);
        Wave(ringTrail, _trailColor, t - trailDelay);
        if (disc != null)
        {
            float rise = Smooth(t / 0.25f);
            float fall = 1f - Smooth((t - 0.35f) / (duration - 0.35f));
            float pulse = 0.85f + 0.15f * Mathf.Sin(t * 14f);
            float s = Mathf.Lerp(0.55f, 1f, EaseOut(t / expandTime));
            disc.transform.localScale = new Vector3(s, 1f, s);
            Tint(disc, _discColor, rise * fall * pulse);
        }
    }
}
