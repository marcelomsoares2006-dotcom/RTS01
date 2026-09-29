using UnityEngine;

namespace BehaviorTree
{
    public abstract class RuntimeBehaviorTree : MonoBehaviour
    {
        private Node _root;

        protected void Start()
        {
            _root = SetupTree();
        }

        private void Update()
        {
            if (_root != null)
                _root.Evaluate();
        }

        public Node Root => _root;
        protected abstract Node SetupTree();
    }
}
