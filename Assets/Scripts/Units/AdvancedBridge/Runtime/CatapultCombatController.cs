using EraImperial.Units.Advanced;
using EraImperial.Units.Crossbow;
using EraImperial.Units.Mage;
using EraImperial.Units.Siege;
using UnityEngine;

/// <summary>Catapulta: ataque de area com alcance minimo/maximo, recarga, projetil balistico e dano radial.</summary>
public sealed class CatapultCombatController : AdvancedAttackController
{
    public int LastDirectDamage { get; private set; }
    public int LastRadialHits { get; private set; }
    public int TotalImpacts { get; private set; }

    protected override ProjectileContract ProjectileContract => contract != null ? contract.Projectile : null;

    protected override bool CanFire(IEraTier tier, float distance, float effectiveDistance, float secondsSinceLastShot)
    {
        CatapultEraTier t = (CatapultEraTier)tier;
        // minimo medido centro a centro; maximo descontando o tamanho do alvo (como CheckUnitInRange)
        return distance >= t.minimumRange
            && CombatGates.IsInRange(effectiveDistance, 0f, t.attackRange)
            && CombatGates.IsCooledDown(secondsSinceLastShot, t.attackCooldown);
    }

    protected override void Release(IEraTier tier, Vector3 origin, Vector3 aimPoint, Transform target)
    {
        CatapultEraTier t = Scaled((CatapultEraTier)tier);
        Vector3 ground = aimPoint;
        AdvancedProjectile.Spawn(ProjectilePrefab, origin, ground, target, ProjectileContract, (point, hitTarget) => Detonate(t, point, hitTarget));
    }

    /// <summary>Dano de impacto e radial seguem o ataque atual da unidade (era + bonus de tecnologia).</summary>
    private CatapultEraTier Scaled(CatapultEraTier t)
    {
        int attack = CurrentAttackDamage;
        if (attack <= 0 || t.attackDamage <= 0) return t;
        float scale = attack / (float)t.attackDamage;
        t.attackDamage = attack;
        t.radialDamage = Mathf.Max(1, Mathf.RoundToInt(t.radialDamage * scale));
        return t;
    }

    private void Detonate(CatapultEraTier t, Vector3 point, Transform directTarget)
    {
        int attackerOwner = Manager != null && Manager.Unit != null ? Manager.Unit.Owner : -1;
        UnitManager direct = AdvancedCombat.ManagerOf(directTarget);
        int directId = direct != null ? AdvancedCombat.IdOf(direct) : int.MinValue;

        System.Collections.Generic.List<UnitManager> inBlast = AdvancedCombat.UnitsInRadius(point, t.blastRadius + 1.5f);
        if (direct != null && !inBlast.Contains(direct)) inBlast.Add(direct);

        System.Collections.Generic.Dictionary<int, UnitManager> byId = new System.Collections.Generic.Dictionary<int, UnitManager>();
        System.Collections.Generic.List<SiegeTarget> targets = new System.Collections.Generic.List<SiegeTarget>();
        foreach (UnitManager m in inBlast)
        {
            if (!AdvancedCombat.IsAlive(m)) continue;
            int id = AdvancedCombat.IdOf(m);
            byId[id] = m;
            targets.Add(new SiegeTarget(id, m.Unit.Owner, m.transform.position, true));
        }

        System.Collections.Generic.List<SiegeHit> hits = SiegeAttackProfile.ResolveImpact(t, point, directId, attackerOwner, targets);
        TotalImpacts++;
        LastRadialHits = 0;
        LastDirectDamage = 0;
        foreach (SiegeHit hit in hits)
        {
            if (!byId.TryGetValue(hit.Id, out UnitManager m) || m == null) continue;
            if (hit.IsDirect) LastDirectDamage = hit.Damage; else LastRadialHits++;
            m.TakeHit(hit.Damage);
        }
    }
}
