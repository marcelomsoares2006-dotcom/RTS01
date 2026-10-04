using UnityEngine;

public sealed class MountedIKController : MonoBehaviour
{
    public Animator riderAnimator;
    public MountedSockets sockets;
    [Range(0f, 1f)] public float feetWeight = 1f;
    [Range(0f, 1f)] public float handsWeight = 0.75f;

    void OnAnimatorIK(int layerIndex)
    {
        if (riderAnimator == null || sockets == null || !riderAnimator.isHuman)
            return;
        ApplyGoal(AvatarIKGoal.LeftFoot, sockets.leftFootTarget, feetWeight);
        ApplyGoal(AvatarIKGoal.RightFoot, sockets.rightFootTarget, feetWeight);
        ApplyGoal(AvatarIKGoal.LeftHand, sockets.leftHandTarget, handsWeight);
        ApplyGoal(AvatarIKGoal.RightHand, sockets.rightHandTarget, handsWeight);
    }

    void ApplyGoal(AvatarIKGoal goal, Transform target, float weight)
    {
        if (target == null) return;
        riderAnimator.SetIKPositionWeight(goal, weight);
        riderAnimator.SetIKRotationWeight(goal, weight);
        riderAnimator.SetIKPosition(goal, target.position);
        riderAnimator.SetIKRotation(goal, target.rotation);
    }
}
