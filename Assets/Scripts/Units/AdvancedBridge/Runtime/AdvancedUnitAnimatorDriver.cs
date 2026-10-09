using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Alimenta o Animator das unidades avancadas: "Speed" (velocidade do NavMeshAgent) e o trigger "Damaged"
/// (reacao a dano, detectada pela queda de vida). Parametros ausentes no controller sao ignorados.
/// O behavior tree tambem escreve Running (bool), Attacking (bool) e Hit (trigger); os controllers das unidades
/// avancadas devem declarar esses tres parametros (sem transicoes) para nao gerar avisos.
/// </summary>
public sealed class AdvancedUnitAnimatorDriver : MonoBehaviour
{
    public static readonly int SpeedHash = Animator.StringToHash("Speed");
    public static readonly int DamagedHash = Animator.StringToHash("Damaged");

    public Animator animator;
    [Tooltip("Somente testes/demonstracao: forca Speed (>= 0). -1 usa o NavMeshAgent.")]
    public float debugSpeed = -1f;
    public float damageReactionCooldown = 0.6f;

    private NavMeshAgent _agent;
    private UnitManager _manager;
    private float _speedSmoothed;
    private int _lastHealth = -1;
    private float _lastReaction = float.NegativeInfinity;
    private bool _hasSpeed;
    private bool _hasDamaged;

    public int DamageReactions { get; private set; }

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _manager = GetComponent<UnitManager>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            _hasSpeed = AdvancedAttackController.HasParameter(animator, "Speed");
            _hasDamaged = AdvancedAttackController.HasParameter(animator, "Damaged");
        }
    }

    private void Update()
    {
        if (animator == null) return;

        if (_hasSpeed)
        {
            float speed = debugSpeed >= 0f
                ? debugSpeed
                : _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh ? _agent.velocity.magnitude : 0f;
            _speedSmoothed = Mathf.Lerp(_speedSmoothed, speed, 1f - Mathf.Exp(-12f * Time.deltaTime));
            animator.SetFloat(SpeedHash, _speedSmoothed);
        }

        if (_manager != null && _manager.Unit != null)
        {
            int hp = _manager.Unit.HP;
            if (_lastHealth >= 0 && hp < _lastHealth && hp > 0 && Time.time - _lastReaction >= damageReactionCooldown)
            {
                _lastReaction = Time.time;
                DamageReactions++;
                if (_hasDamaged) animator.SetTrigger(DamagedHash);
            }
            _lastHealth = hp;
        }
    }
}
