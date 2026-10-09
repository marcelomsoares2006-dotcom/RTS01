using EraImperial.Units.Advanced;
using UnityEngine;

/// <summary>
/// Ataque corpo a corpo do Paladino com o cooldown da ERA atual. O behavior tree tenta atacar a cada
/// AdvancedUnitDataBridge.AttackPollSeconds; este IAttackHandler so deixa o golpe acontecer quando o cooldown do
/// contrato venceu (e cancela o trigger "Hit" que o BT ja acionou, para a animacao nao reiniciar a cada tentativa).
/// O dano e o instantaneo padrao do jogo (UnitManager.TakeHit com Unit.AttackDamage, que ja inclui era e tecnologia).
/// </summary>
public sealed class PaladinMeleeAttackController : MonoBehaviour, IAttackHandler
{
    public static readonly int HitHash = Animator.StringToHash("Hit");

    public Animator animator;

    private UnitManager _manager;
    private AdvancedUnitEraBinding _binding;
    private float _lastSwing = float.NegativeInfinity;
    private bool _hasHitParameter;

    public int Swings { get; private set; }

    private void Awake()
    {
        _manager = GetComponent<UnitManager>();
        _binding = GetComponent<AdvancedUnitEraBinding>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        _hasHitParameter = AdvancedAttackController.HasParameter(animator, "Hit");
    }

    private float Cooldown
    {
        get
        {
            IEraTier tier = _binding != null ? _binding.CurrentTier : null;
            return tier != null ? tier.AttackCooldown : 1.5f;
        }
    }

    public bool TryHandleAttack(Transform target)
    {
        if (target == null) return true;
        if (Time.time - _lastSwing < Cooldown)
        {
            if (animator != null && _hasHitParameter) animator.ResetTrigger(HitHash);
            return true;
        }

        _lastSwing = Time.time;
        Swings++;
        UnitManager victim = target.GetComponent<UnitManager>();
        if (victim != null && _manager != null && _manager.Unit != null) victim.TakeHit(_manager.Unit.AttackDamage);
        return true;
    }
}
