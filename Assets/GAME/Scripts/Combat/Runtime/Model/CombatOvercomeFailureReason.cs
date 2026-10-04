namespace Game.Combat.Model
{
    public enum CombatOvercomeFailureReason
    {
        None = 0,
        InvalidSession,
        InvalidPhase,
        StaleRequest,
        InvalidActor,
        NotActionAuthority,
        NotPanicked,
        Stunned,
        CombatEnded
    }
}
