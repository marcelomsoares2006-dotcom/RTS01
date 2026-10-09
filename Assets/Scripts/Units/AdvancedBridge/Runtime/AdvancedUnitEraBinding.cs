using EraImperial.Units.Advanced;
using UnityEngine;

/// <summary>
/// Liga uma unidade avancada ao seu contrato: aplica vida maxima, dano e alcance da era atual em Unit
/// (Unit.SetEraStats) ao nascer e sempre que AdvancedEraState muda.
/// Executa antes do behavior tree (Start) para que o alcance de ataque lido por CheckUnitInRange ja seja o da era.
/// Limite conhecido: o behavior tree le alcance e periodo de ataque uma unica vez; mudar de era depois do spawn
/// atualiza vida/dano/regras dos controladores, mas o alcance de aproximacao do BT so muda em unidades novas.
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class AdvancedUnitEraBinding : MonoBehaviour
{
    public AdvancedUnitContract contract;

    private UnitManager _manager;
    private bool _subscribed;

    public ImperialEra AppliedEra { get; private set; } = ImperialEra.Ancient;
    public bool HasApplied { get; private set; }

    public IEraTier CurrentTier
    {
        get
        {
            if (contract != null && contract.TryGetTier(HasApplied ? AppliedEra : AdvancedEraState.Current, out IEraTier tier))
                return tier;
            return null;
        }
    }

    private void Start()
    {
        _manager = GetComponent<UnitManager>();
        Apply(AdvancedEraState.Current);
        AdvancedEraState.Changed += OnEraChanged;
        _subscribed = true;
    }

    private void OnDestroy()
    {
        if (_subscribed) AdvancedEraState.Changed -= OnEraChanged;
    }

    private void OnEraChanged(ImperialEra era)
    {
        Apply(era);
    }

    public bool Apply(ImperialEra era)
    {
        if (contract == null) return false;
        if (_manager == null) _manager = GetComponent<UnitManager>();
        if (_manager == null || _manager.Unit == null) return false;
        if (!contract.TryGetTier(era, out IEraTier tier)) return false;

        _manager.Unit.SetEraStats(tier.HealthPoints, tier.AttackDamage, tier.AttackRange);
        AppliedEra = era;
        HasApplied = true;
        return true;
    }
}
