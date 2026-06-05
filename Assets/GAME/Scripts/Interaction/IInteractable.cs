using UnityEngine;

namespace Game.Interaction
{
    public interface IInteractable
    {
        string PromptText { get; }
        bool CanInteract { get; }
        void Interact(GameObject interactor);
    }
}
