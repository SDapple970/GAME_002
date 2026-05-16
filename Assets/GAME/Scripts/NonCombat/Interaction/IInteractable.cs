namespace Game.NonCombat.Interaction
{
    public interface IInteractable
    {
        bool CanInteract { get; }
        void Interact();
    }
}
