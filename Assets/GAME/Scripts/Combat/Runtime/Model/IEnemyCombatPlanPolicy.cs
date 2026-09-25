namespace Game.Combat.Model
{
    /// <summary>
    /// Selects an enemy plan from session-owned runtime state. Turn timing and plan
    /// submission remain the responsibility of the combat flow orchestrator.
    /// </summary>
    internal interface IEnemyCombatPlanPolicy
    {
        bool TryCreatePlan(EnemyCombatPlanRequest request, out ActionPlan plan);
    }
}
