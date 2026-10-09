using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Combate a distancia do Archer. Ligado ao fluxo existente: o BehaviorTree chama
/// SetAnimatorTriggerVariable("Hit") e UnitManager.Attack(alvo); este componente (IAttackHandler) assume o ataque,
/// gira o corpo para o alvo e so dispara a flecha quando a animacao Fire chega ao evento de soltura.
/// Tambem alimenta o parametro Speed do Animator (locomocao) e posiciona a flecha na mao que puxa.
/// </summary>
public sealed class ArcherCombatController : MonoBehaviour, IAttackHandler
{
    public static readonly int SpeedHash = Animator.StringToHash("Speed");
    public static readonly int HitHash = Animator.StringToHash("Hit");

    [Header("Visual")]
    public Animator animator;
    public Transform bow;
    public Transform heldArrow;
    public ArcherBowString bowString;

    [Header("Combate")]
    public GameObject arrowProjectilePrefab;
    public RangedAttackProfile profile;
    public float turnSpeed = 540f;
    public float maxAttackSeconds = 6f;
    [Tooltip("Somente testes/demonstracao: forca o parametro Speed do Animator (>= 0). -1 usa o NavMeshAgent.")]
    public float debugSpeed = -1f;

    private NavMeshAgent _agent;
    private UnitManager _unit;
    private Transform _leftHand;
    private Transform _rightHand;
    private Transform _target;
    private bool _attacking;
    private bool _nocked;
    private float _attackStarted;
    private float _speedSmoothed;
    private Quaternion _bowRestLocalRotation = Quaternion.identity;
    private float _bowUpright;

    public bool IsAttacking => _attacking;
    public int ArrowsFired { get; private set; }
    public bool HeldArrowVisible => heldArrow != null && heldArrow.gameObject.activeSelf;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _unit = GetComponent<UnitManager>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        ResolveHands();
        if (bow != null) _bowRestLocalRotation = bow.localRotation;
        SetHeldArrow(false);
    }

    private void ResolveHands()
    {
        if (animator == null || !animator.isHuman) return;
        _leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        _rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
    }

    public bool TryHandleAttack(Transform target)
    {
        if (target == null) return false;
        BeginAttack(target, false);
        return true;
    }

    /// <summary>Inicia o ciclo puxar, mirar, disparar. setTrigger=false quando o BehaviorTree ja acionou "Hit".</summary>
    public void BeginAttack(Transform target, bool setTrigger)
    {
        if (_attacking)
        {
            if (animator != null) animator.ResetTrigger(HitHash);
            return;
        }
        _target = target;
        _attacking = true;
        _attackStarted = Time.time;
        if (setTrigger && animator != null) animator.SetTrigger(HitHash);
    }

    private void Update()
    {
        UpdateLocomotion();
        if (_attacking)
        {
            if (Time.time - _attackStarted > maxAttackSeconds) OnAttackEnd();
            else FaceTarget();
        }
    }

    private void UpdateLocomotion()
    {
        if (animator == null) return;
        float speed = debugSpeed >= 0f
            ? debugSpeed
            : _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh ? _agent.velocity.magnitude : 0f;
        _speedSmoothed = Mathf.Lerp(_speedSmoothed, speed, 1f - Mathf.Exp(-12f * Time.deltaTime));
        animator.SetFloat(SpeedHash, _speedSmoothed);
    }

    private void FaceTarget()
    {
        if (_target == null) return;
        Vector3 flat = _target.position - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), turnSpeed * Time.deltaTime);
    }

    private void LateUpdate()
    {
        UpdateBowOrientation();
        if (!HeldArrowVisible || _rightHand == null) return;
        Vector3 forward;
        if (_nocked && _leftHand != null) forward = (_leftHand.position - _rightHand.position).normalized;
        else forward = (transform.forward + Vector3.up * 0.8f).normalized;
        if (forward.sqrMagnitude < 0.5f) forward = transform.forward;
        Quaternion rotation = Quaternion.LookRotation(forward);
        heldArrow.SetPositionAndRotation(_rightHand.position, Quaternion.Slerp(heldArrow.rotation, rotation, _nocked ? 1f : 0.35f));
    }

    // durante o ataque o arco fica na vertical apontando para o alvo; fora dele segue a rotacao da mao
    private void UpdateBowOrientation()
    {
        if (bow == null || bow.parent == null) return;
        _bowUpright = Mathf.MoveTowards(_bowUpright, _attacking ? 1f : 0f, Time.deltaTime * 6f);
        if (_bowUpright <= 0f) { bow.localRotation = _bowRestLocalRotation; return; }
        Quaternion upright = Quaternion.LookRotation(transform.forward, Vector3.up);
        Quaternion local = Quaternion.Inverse(bow.parent.rotation) * upright;
        bow.localRotation = Quaternion.Slerp(_bowRestLocalRotation, local, _bowUpright);
    }

    private void SetHeldArrow(bool visible)
    {
        if (heldArrow != null) heldArrow.gameObject.SetActive(visible);
    }

    // --- animation events ---------------------------------------------------------------------------
    public void OnArrowGrab()
    {
        SetHeldArrow(true);
    }

    public void OnArrowNock()
    {
        SetHeldArrow(true);
        _nocked = true;
        if (bowString != null)
        {
            bowString.pullPoint = _rightHand;
            bowString.Drawing = true;
        }
    }

    public void OnArrowRelease()
    {
        Vector3 origin = heldArrow != null ? heldArrow.position : transform.position + Vector3.up * 1.4f;
        Transform target = _target;
        _nocked = false;
        SetHeldArrow(false);
        if (bowString != null) bowString.Drawing = false;
        if (target == null) return;

        int damage = profile != null ? profile.damage : 1;
        if (_unit != null && _unit.Unit != null) damage = _unit.Unit.AttackDamage;
        float speed = profile != null ? profile.projectileSpeed : 18f;
        ArcherProjectile.Spawn(arrowProjectilePrefab, origin, target, speed, damage);
        ArrowsFired++;
    }

    public void OnAttackEnd()
    {
        _attacking = false;
        _nocked = false;
        _target = null;
        SetHeldArrow(false);
        if (bowString != null) bowString.Drawing = false;
        if (animator != null) animator.ResetTrigger(HitHash);
    }
}
