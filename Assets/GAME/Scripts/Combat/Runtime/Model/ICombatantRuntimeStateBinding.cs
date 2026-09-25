namespace Game.Combat.Model
{
    /// <summary>
    /// Lets compatibility combatants project session-owned runtime state through the
    /// existing ICombatant API without keeping a second mutable HP owner.
    /// </summary>
    public interface ICombatantRuntimeStateBinding
    {
        void BindCombatState(CombatantCombatState state);
    }
}
