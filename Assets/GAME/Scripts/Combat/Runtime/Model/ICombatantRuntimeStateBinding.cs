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

    internal interface ICombatantDamageReceipt
    {
        // Execute damage once and report only this call's direct mutation, excluding callbacks.
        // Null means the damage path ran but cannot report an exact amount.
        int? ApplyDamageWithReceipt(int amount);
    }
}
