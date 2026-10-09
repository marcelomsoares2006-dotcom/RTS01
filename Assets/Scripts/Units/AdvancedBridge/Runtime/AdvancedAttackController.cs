using EraImperial.Units.Advanced;
using EraImperial.Units.Crossbow;
using EraImperial.Units.Mage;
using EraImperial.Units.Siege;
using UnityEngine;

/// <summary>
/// Base do ataque proprio das unidades avancadas, ligada ao fluxo existente: o behavior tree chama
/// UnitManager.Attack(alvo) a cada periodo e este componente (IAttackHandler) assume o ataque. Ele valida alcance e
/// recarga pelas regras do contrato, gira a unidade, aciona a animacao "Fire" e solta o projetil apos
/// releaseDelay (instante do disparo dentro do clipe). Nunca aplica dano instantaneo.
/// Parametros do Animator usados: Fire (trigger). O trigger "Hit" do BT e ignorado (nao e a reacao de dano).
/// </summary>
public abstract class AdvancedAttackController : MonoBehaviour, IAttackHandler
{
    public static readonly int FireHash = Animator.StringToHash("Fire");

    [Header("Visual")]
    public Animator animator;
    [Tooltip("Origem do disparo (socket do projetil). Sem ela usa a posicao da unidade + sourceHeight.")]
    public Transform projectileSpawn;
    public float sourceHeight = 1.5f;

    [Header("Disparo")]
    public AdvancedUnitContract contract;
    [Tooltip("Segundos entre o inicio da animacao Fire e a saida do projetil.")]
    public float releaseDelay = 0.6f;
    public float turnSpeed = 240f;

    protected UnitManager Manager;
    private AdvancedUnitEraBinding _binding;
    private Transform _target;
    private Vector3 _aimPoint;
    private float _lastShotTime = float.NegativeInfinity;
    private bool _firing;

    public bool IsFiring => _firing;
    public int ShotsFired { get; private set; }
    public float SecondsSinceLastShot => Time.time - _lastShotTime;

    protected virtual void Awake()
    {
        Manager = GetComponent<UnitManager>();
        _binding = GetComponent<AdvancedUnitEraBinding>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    protected IEraTier CurrentTier
    {
        get
        {
            if (_binding != null && _binding.CurrentTier != null) return _binding.CurrentTier;
            return contract != null && contract.TryGetTier(AdvancedEraState.Current, out IEraTier tier) ? tier : null;
        }
    }

    /// <summary>distance = distancia horizontal centro a centro; effective = distancia descontado o tamanho do alvo.</summary>
    protected abstract bool CanFire(IEraTier tier, float distance, float effectiveDistance, float secondsSinceLastShot);

    protected abstract ProjectileContract ProjectileContract { get; }

    /// <summary>Instante do disparo: cria o projetil e liga o impacto ao dano.</summary>
    protected abstract void Release(IEraTier tier, Vector3 origin, Vector3 aimPoint, Transform target);

    public bool TryHandleAttack(Transform target)
    {
        // sempre "tratado": estas unidades nunca causam o dano instantaneo padrao de UnitManager.Attack
        if (target == null || _firing) return true;
        UnitManager targetManager = AdvancedCombat.ManagerOf(target);
        if (!AdvancedCombat.IsAlive(targetManager)) return true;

        IEraTier tier = CurrentTier;
        if (tier == null) return true;

        float distance = AdvancedCombat.FlatDistance(transform.position, target.position);
        float effective = Mathf.Max(0f, distance - AdvancedCombat.TargetSize(targetManager));
        if (!CanFire(tier, distance, effective, SecondsSinceLastShot)) return true;

        BeginFire(target);
        return true;
    }

    /// <summary>Inicia um disparo ja validado (tambem usado por testes de Play Mode).</summary>
    public void BeginFire(Transform target)
    {
        _lastShotTime = Time.time;
        _firing = true;
        _target = target;
        _aimPoint = target.position;
        if (animator != null && HasParameter(animator, "Fire")) animator.SetTrigger(FireHash);
        CancelInvoke(nameof(DoRelease));
        Invoke(nameof(DoRelease), Mathf.Max(0f, releaseDelay));
    }

    private void DoRelease()
    {
        _firing = false;
        IEraTier tier = CurrentTier;
        if (tier == null) return;

        Transform target = _target;
        _target = null;
        bool alive = target != null && AdvancedCombat.IsAlive(AdvancedCombat.ManagerOf(target));
        Vector3 aim = alive ? target.position : _aimPoint;
        Vector3 origin = projectileSpawn != null ? projectileSpawn.position : transform.position + Vector3.up * sourceHeight;
        Release(tier, origin, aim, alive ? target : null);
        ShotsFired++;
    }

    protected virtual void Update()
    {
        if (!_firing || _target == null) return;
        Vector3 flat = _target.position - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), turnSpeed * Time.deltaTime);
    }

    protected GameObject ProjectilePrefab
    {
        get
        {
            ProjectileContract p = ProjectileContract;
            return p != null && p.prefab != null ? p.prefab.asset as GameObject : null;
        }
    }

    protected int CurrentAttackDamage => Manager != null && Manager.Unit != null ? Manager.Unit.AttackDamage : 0;

    public static bool HasParameter(Animator a, string name)
    {
        if (a == null || a.runtimeAnimatorController == null) return false;
        foreach (AnimatorControllerParameter p in a.parameters)
            if (p.name == name) return true;
        return false;
    }
}
