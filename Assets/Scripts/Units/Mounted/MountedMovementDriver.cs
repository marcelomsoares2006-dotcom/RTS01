using UnityEngine;
using UnityEngine.AI;

/// <summary>Optional root movement layer for the mounted visual prototype.</summary>
public sealed class MountedMovementDriver : MonoBehaviour
{
    public NavMeshAgent agent;
    public MountedAnimatorSync animatorSync;
    public bool acceptCommands = true;

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animatorSync == null) animatorSync = GetComponent<MountedAnimatorSync>();
    }

    void Update()
    {
        if (agent == null || animatorSync == null) return;
        animatorSync.speed = agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
        animatorSync.isMoving = agent.isOnNavMesh && agent.velocity.sqrMagnitude > .0025f;
    }

    public bool MoveTo(Vector3 destination)
    {
        if (!acceptCommands || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return false;
        return agent.SetDestination(destination);
    }

    public bool TryPlace(Vector3 position)
    {
        return Globals.TryWarpToNavMesh(agent, position, 8f);
    }
}
