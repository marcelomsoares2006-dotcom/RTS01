using UnityEngine;

using BehaviorTree;

public class TaskBuild: Node
{
    CharacterManager _manager;
    int _buildPower;

    public TaskBuild(UnitManager manager) : base()
    {
        _manager = (CharacterManager) manager;
        _buildPower = ((CharacterData) manager.Unit.Data).buildPower;
    }

    public override NodeState Evaluate()
    {
        object currentTarget = GetData("currentTarget");
        Transform target = currentTarget as Transform;
        if (target == null)
        {
            _state = NodeState.FAILURE;
            return _state;
        }
        BuildingManager bm = target.GetComponent<BuildingManager>();
        if (bm == null || bm.Unit == null)
        {
            ClearData("currentTarget");
            _state = NodeState.FAILURE;
            return _state;
        }
        bool finishedBuilding = bm.Build(_buildPower);
        if (finishedBuilding)
        {
            _manager.SetIsConstructor(false);
            _manager.SetRendererVisibility(true);
            ClearData("currentTarget");
        }

        _state = NodeState.SUCCESS;
        return _state;
    }
}
