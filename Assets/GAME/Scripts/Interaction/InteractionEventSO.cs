using UnityEngine;

namespace Game.Interaction
{
    public readonly struct InteractionContext
    {
        public InteractionContext(GameObject interactor, InteractableObject target, InteractionController controller)
        {
            Interactor = interactor;
            Target = target;
            Controller = controller;
        }

        public GameObject Interactor { get; }
        public InteractableObject Target { get; }
        public InteractionController Controller { get; }
    }

    public abstract class InteractionEventSO : ScriptableObject
    {
        public abstract void Execute(InteractionContext context);
    }
}
