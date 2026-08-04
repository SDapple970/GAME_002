namespace Game.Interaction
{
    public enum InteractionUsePolicy
    {
        LegacyCompatibility = 0,
        Repeatable = 10,
        OncePerSession = 20,
        PersistentOnce = 30
    }
}
