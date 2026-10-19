using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class CharacterManager : UnitManager
{
    public NavMeshAgent agent;

    public string Uid => Unit == null ? string.Empty : Unit.Uid;

    private Character _character;
    public override Unit Unit
    {
        get { return _character; }
        set { _character = value is Character ? (Character)value : null; }
    }

    public BoxCollider characterCollider;

    private bool _isConstructor = false;
    public bool IsConstructor => _isConstructor;

    private void Start()
    {
        _character.Place();
    }

    public bool MoveTo(Vector3 targetPosition, bool playSound = true)
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return false;
        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(targetPosition, path))
            return false;
        if (path.status != NavMeshPathStatus.PathComplete)
        {
            AudioClip invalidSound = Unit != null && Unit.Data is CharacterData characterData
                ? characterData.onMoveInvalidSound
                : null;
            if (playSound && contextualSource != null && invalidSound != null)
                contextualSource.PlayOneShot(invalidSound);
            return false;
        }

        if (!agent.SetDestination(targetPosition))
            return false;
        AudioClip validSound = Unit != null && Unit.Data is CharacterData validCharacterData
            ? validCharacterData.onMoveValidSound
            : null;
        if (playSound && contextualSource != null && validSound != null)
            contextualSource.PlayOneShot(validSound);
        return true;
    }

    public void SetRendererVisibility(bool on)
    {
        if (on && !meshRenderer.enabled)
        {
            meshRenderer.enabled = true;
            characterCollider.enabled = true;
        }
        else if (!on && meshRenderer.enabled)
        {
            meshRenderer.enabled = false;
            characterCollider.enabled = false;
        }
    }

    public void SetIsConstructor(bool on)
    {
        _isConstructor = on;
        if (_isConstructor) Deselect();
    }

    protected override bool IsActive()
    {
        return !_isConstructor;
    }
}
