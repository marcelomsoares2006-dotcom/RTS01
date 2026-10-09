using System.Collections.Generic;
using EraImperial.Units.Advanced;
using EraImperial.Units.Paladin;
using UnityEngine;

/// <summary>
/// Cura em area do Paladino curador. A cada frame verifica se ha aliado ferido no raio; se houver e o cooldown de
/// cura permitir, executa um pulso (PaladinHealController): so aliados do mesmo dono, vivos, feridos, dentro do
/// raio, limitados a vida faltante e ao limite por alvo. Inimigos nunca sao curados. Pulso sem alvo ferido nao gasta
/// o cooldown. O ataque corpo a corpo continua sendo o padrao do behavior tree.
/// Animator: trigger "Heal". Efeito: prefab de aura (contrato) instanciado no paladino com escala = raio.
/// </summary>
public sealed class PaladinHealBehaviour : MonoBehaviour
{
    public static readonly int HealHash = Animator.StringToHash("Heal");

    public PaladinHealerContract contract;
    public Animator animator;
    public Transform effectOrigin;
    public GameObject healEffectPrefab;

    private UnitManager _manager;
    private AdvancedUnitEraBinding _binding;
    private PaladinHealController _controller;

    public int PulsesPerformed { get; private set; }
    public int TotalHealed { get; private set; }
    public int LastPulseTargets { get; private set; }

    private void Awake()
    {
        _manager = GetComponent<UnitManager>();
        _binding = GetComponent<AdvancedUnitEraBinding>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        _controller = new PaladinHealController(contract != null ? contract.healCapWindowSeconds : 10f);
    }

    private PaladinEraTier? CurrentTier()
    {
        IEraTier tier = _binding != null ? _binding.CurrentTier : null;
        if (tier == null && contract != null) contract.TryGetTier(AdvancedEraState.Current, out tier);
        return tier is PaladinEraTier t ? t : (PaladinEraTier?)null;
    }

    private void Update()
    {
        if (contract == null || !AdvancedCombat.IsAlive(_manager)) return;
        PaladinEraTier? maybe = CurrentTier();
        if (!maybe.HasValue) return;
        PaladinEraTier tier = maybe.Value;
        if (!_controller.CanPulse(tier, Time.time)) return;

        Vector3 position = transform.position;
        int owner = _manager.Unit.Owner;
        int selfId = AdvancedCombat.IdOf(_manager);
        List<UnitManager> nearby = AdvancedCombat.UnitsInRadius(position, tier.healRadius + 1f);
        List<HealCandidate> candidates = new List<HealCandidate>();
        Dictionary<int, UnitManager> byId = new Dictionary<int, UnitManager>();
        bool anyWounded = false;
        foreach (UnitManager m in nearby)
        {
            if (!(m is CharacterManager) || !AdvancedCombat.IsAlive(m)) continue;
            int id = AdvancedCombat.IdOf(m);
            byId[id] = m;
            candidates.Add(new HealCandidate(id, m.Unit.Owner, m.transform.position, m.Unit.HP, m.Unit.MaxHP, true));
            if (m.Unit.Owner == owner && m.Unit.HP < m.Unit.MaxHP && (id != selfId || contract.canHealSelf)) anyWounded = true;
        }
        if (!anyWounded) return;

        List<HealResult> results = _controller.TryPulse(tier, selfId, owner, position, candidates, Time.time, contract.canHealSelf);
        if (results == null || results.Count == 0) return;

        foreach (HealResult r in results)
        {
            if (byId.TryGetValue(r.Id, out UnitManager target) && target != null)
            {
                target.TakeHit(-r.Amount); // dano negativo = cura (atualiza a barra de vida; nunca excede a vida faltante)
                TotalHealed += r.Amount;
            }
        }
        PulsesPerformed++;
        LastPulseTargets = results.Count;
        PlayFeedback(tier);
    }

    private void PlayFeedback(PaladinEraTier tier)
    {
        if (animator != null && AdvancedAttackController.HasParameter(animator, "Heal")) animator.SetTrigger(HealHash);
        if (healEffectPrefab == null) return;
        Transform origin = effectOrigin != null ? effectOrigin : transform;
        GameObject fx = Instantiate(healEffectPrefab, origin.position, Quaternion.identity);
        fx.transform.localScale = new Vector3(tier.healRadius * 2f, 1f, tier.healRadius * 2f);
        Destroy(fx, 3f);
    }
}
