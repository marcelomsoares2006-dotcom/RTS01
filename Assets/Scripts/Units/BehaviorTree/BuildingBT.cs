using System.Collections.Generic;

using BehaviorTree;

[UnityEngine.RequireComponent(typeof(BuildingManager))]
public class BuildingBT : RuntimeBehaviorTree
{
    BuildingManager manager;

    private void Awake()
    {
        manager = GetComponent<BuildingManager>();
    }

    protected override Node SetupTree()
    {
        Node _root;

        _root = new Parallel();

        if (manager.Unit.AttackDamage > 0 && manager.Unit.AttackRange > 0f)
        {
            Sequence attackSequence = new Sequence(new List<Node> {
                new CheckUnitInRange(manager, true),
                new Timer(
                    manager.Unit.AttackRate > 0f ? manager.Unit.AttackRate : 0.1f,
                    new List<Node>()
                    {
                        new TaskAttack(manager),
                    }
                ),
            });

            _root.Attach(attackSequence);
            _root.Attach(new CheckEnemyInFOVRange(manager));
        }

        _root.Attach(new Sequence(new List<Node> {
            new Timer(
                GameManager.instance.producingRate,
                new List<Node>()
                {
                    new TaskProduceResources(manager)
                },
                delegate
                {
                    EventManager.TriggerEvent("UpdatedResources");
                }
            )
        }));

        return _root;
    }
}
