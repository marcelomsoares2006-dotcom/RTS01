using EraImperial.Units.Advanced;
using EraImperial.Units.Crossbow;
using EraImperial.Units.Mage;
using EraImperial.Units.Siege;
using UnityEngine;

/// <summary>Mago e balestra: projetil reto, dano segundo o perfil de ataque da unidade.</summary>
public sealed class ProjectileAttackController : AdvancedAttackController
{
    public int LastDamage { get; private set; }

    protected override ProjectileContract ProjectileContract => contract != null ? contract.Projectile : null;

    protected override bool CanFire(IEraTier tier, float distance, float effectiveDistance, float secondsSinceLastShot)
    {
        return CombatGates.IsInRange(effectiveDistance, 0f, tier.AttackRange)
            && CombatGates.IsCooledDown(secondsSinceLastShot, tier.AttackCooldown);
    }

    protected override void Release(IEraTier tier, Vector3 origin, Vector3 aimPoint, Transform target)
    {
        int damage = ComputeDamage(tier);
        Vector3 aim = target != null ? target.position : aimPoint;
        AdvancedProjectile.Spawn(ProjectilePrefab, origin, aim, target, ProjectileContract, (point, hit) => Apply(damage, hit));
    }

    // usa o ataque atual da unidade (era + tecnologia); perfurante/magico passam pelas funcoes do contrato
    private int ComputeDamage(IEraTier tier)
    {
        int attack = CurrentAttackDamage > 0 ? CurrentAttackDamage : tier.AttackDamage;
        if (tier is CrossbowEraTier crossbow)
        {
            crossbow.attackDamage = attack;
            return CrossbowAttackProfile.ComputeDamage(crossbow, 0f);
        }
        if (tier is MageEraTier mage)
        {
            mage.attackDamage = attack;
            return MageAttackProfile.ComputeDamage(mage, 0f);
        }
        return attack;
    }

    private void Apply(int damage, Transform hit)
    {
        UnitManager m = AdvancedCombat.ManagerOf(hit);
        if (!AdvancedCombat.IsAlive(m)) return;
        int attackerOwner = Manager != null && Manager.Unit != null ? Manager.Unit.Owner : -1;
        if (m.Unit.Owner == attackerOwner) return;
        LastDamage = damage;
        m.TakeHit(damage);
    }
}
