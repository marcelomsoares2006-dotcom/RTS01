using UnityEngine;

public sealed class MountedAnimatorSync : MonoBehaviour
{
    public Animator horseAnimator;
    public Animator riderAnimator;
    public float speed;
    public bool isMoving;
    public bool isAttacking;
    public int attackIndex;

    void Update()
    {
        SetFloat("Speed", speed);
        SetBool("IsMoving", isMoving);
        SetBool("IsAttacking", isAttacking);
        SetInteger("AttackIndex", attackIndex);
    }

    void SetFloat(string parameter, float value)
    {
        if (Has(horseAnimator, parameter)) horseAnimator.SetFloat(parameter, value);
        if (Has(riderAnimator, parameter)) riderAnimator.SetFloat(parameter, value);
    }

    void SetBool(string parameter, bool value)
    {
        if (Has(horseAnimator, parameter)) horseAnimator.SetBool(parameter, value);
        if (Has(riderAnimator, parameter)) riderAnimator.SetBool(parameter, value);
    }

    void SetInteger(string parameter, int value)
    {
        if (Has(horseAnimator, parameter)) horseAnimator.SetInteger(parameter, value);
        if (Has(riderAnimator, parameter)) riderAnimator.SetInteger(parameter, value);
    }

    static bool Has(Animator animator, string parameter)
    {
        if (animator == null) return false;
        foreach (var item in animator.parameters)
            if (item.name == parameter) return true;
        return false;
    }
}
