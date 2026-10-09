using UnityEngine;

/// <summary>
/// Efeitos do cajado do Mago de combate, puramente visuais: o orbe pulsa em repouso, carrega durante o estado "Cast"
/// do Animator (particulas convergindo no CastPoint) e solta um clarao no instante do disparo.
/// Fica no mesmo GameObject do Animator para receber o evento de animacao "CombatMageRelease" do clipe Cast.
/// Nao altera dano, alcance nem tempos de jogo (isso e feito pelos controladores compartilhados).
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatMageStaffEffects : MonoBehaviour
{
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    public Animator animator;
    public Renderer orbRenderer;
    [Tooltip("Particulas de carga (loop) e nucleo, tocadas enquanto o estado Cast roda ate o disparo.")]
    public ParticleSystem[] chargeEffects = new ParticleSystem[0];
    [Tooltip("Clarao do disparo, tocado uma vez no evento CombatMageRelease.")]
    public ParticleSystem releaseBurst;
    public Color emission = new Color(0.30f, 0.60f, 1f, 1f) * 1.25f;
    public float chargeBoost = 2.2f;

    private MaterialPropertyBlock _block;
    private bool _wasCasting;
    private float _boost = 1f;
    private float _releaseGlow;

    public int ReleaseEvents { get; private set; }
    public int CastsStarted { get; private set; }

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        _block = new MaterialPropertyBlock();
    }

    private void OnDisable()
    {
        foreach (ParticleSystem ps in chargeEffects)
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private bool IsCasting()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        if (animator.IsInTransition(0)) return animator.GetNextAnimatorStateInfo(0).IsName("Cast");
        return animator.GetCurrentAnimatorStateInfo(0).IsName("Cast");
    }

    private void Update()
    {
        bool casting = IsCasting();
        if (casting != _wasCasting)
        {
            _wasCasting = casting;
            if (casting) CastsStarted++;
            foreach (ParticleSystem ps in chargeEffects)
            {
                if (ps == null) continue;
                if (casting) ps.Play(true);
                else ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        float target = casting ? chargeBoost : 1f;
        _boost = Mathf.MoveTowards(_boost, target, (casting ? 2.0f : 3.5f) * Time.deltaTime);
        _releaseGlow = Mathf.MoveTowards(_releaseGlow, 0f, 3f * Time.deltaTime);
        float pulse = 0.88f + 0.12f * Mathf.Sin(Time.time * 2.4f);
        if (orbRenderer != null)
        {
            orbRenderer.GetPropertyBlock(_block);
            _block.SetColor(EmissionColor, emission * (pulse * _boost + _releaseGlow));
            orbRenderer.SetPropertyBlock(_block);
        }
    }

    /// <summary>Evento de animacao do clipe Cast: instante em que a esfera deixa o cajado.</summary>
    public void CombatMageRelease()
    {
        ReleaseEvents++;
        _releaseGlow = 2.0f;
        _boost = 1f;
        foreach (ParticleSystem ps in chargeEffects)
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (releaseBurst != null) releaseBurst.Play(true);
    }
}
