using System;
using UnityEngine;
using Unity.Profiling;

using BehaviorTree;

public class CheckEnemyInFOVRange : Node
{
    UnitManager _manager;
    float _fovRadius;
    int _unitOwner;

    Vector3 _pos;
    // Reused by every evaluation of this node. The buffer grows only when a
    // dense scene fills it, so the normal behavior avoids the managed array
    // allocation performed by Physics.OverlapSphere.
    private Collider[] _overlapBuffer = new Collider[32];

    private const int MaxOverlapBufferSize = 4096;
    private static readonly ProfilerMarker EvaluationMarker =
        new ProfilerMarker("RTS01.CheckEnemyInFOVRange.Evaluate");

    public CheckEnemyInFOVRange(UnitManager manager) : base()
    {
        _manager = manager;
        _fovRadius = _manager.Unit.Data.fieldOfView;
        _unitOwner = _manager.Unit.Owner;
    }

    public override NodeState Evaluate()
    {
        using (EvaluationMarker.Auto())
        {
            _pos = _manager.transform.position;
            Transform nearestEnemy = FindNearestEnemy();
            if (nearestEnemy != null)
            {
                Parent.SetData("currentTarget", nearestEnemy);
                Parent.SetData("currentTargetOffset", Vector2.zero);
                _state = NodeState.SUCCESS;
                return _state;
            }

            _state = NodeState.FAILURE;
            return _state;
        }
    }

    private Transform FindNearestEnemy()
    {
        int hitCount;
        do
        {
            hitCount = Physics.OverlapSphereNonAlloc(
                _pos,
                _fovRadius,
                _overlapBuffer,
                Globals.UNIT_MASK);

            // NonAlloc returns the buffer length when the result set may have
            // been truncated. Grow and retry to preserve the old query's
            // completeness in dense battles.
            if (hitCount < _overlapBuffer.Length)
                break;

            if (_overlapBuffer.Length >= MaxOverlapBufferSize)
            {
                // Exceptional safety valve: preserve correctness if an
                // extreme scene exceeds the bounded buffer.
                return FindNearestEnemy(Physics.OverlapSphere(
                    _pos,
                    _fovRadius,
                    Globals.UNIT_MASK));
            }

            Array.Resize(ref _overlapBuffer, _overlapBuffer.Length * 2);
        }
        while (true);

        return FindNearestEnemy(_overlapBuffer, hitCount);
    }

    private Transform FindNearestEnemy(Collider[] colliders, int count)
    {
        Transform nearest = null;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
                continue;

            UnitManager unitManager = collider.GetComponent<UnitManager>();
            if (unitManager == null || unitManager.Unit == null ||
                unitManager.Unit.Owner == _unitOwner)
                continue;

            float distance = (collider.transform.position - _pos).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = collider.transform;
            }
        }

        return nearest;
    }
}
