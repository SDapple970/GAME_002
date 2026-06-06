namespace Game.NonCombat.Interaction
{
    public interface INonCombatInteractable
    {
        bool CanInteract { get; }
        void Interact();
    }
}
