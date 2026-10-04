using UnityEngine;

public sealed class MountedSockets : MonoBehaviour
{
    public Transform saddleRoot;
    public Transform riderRoot;
    public Transform leftFootTarget;
    public Transform rightFootTarget;
    public Transform leftHandTarget;
    public Transform rightHandTarget;
    public Transform weaponMount;

    public bool IsConfigured => saddleRoot != null && riderRoot != null;
}
