// Scripts/Core/GameState.cs
namespace Game.Core
{
    public enum GameState
    {
        Boot,
        Title,
        Exploration,
        Dialogue,
        Choice,
        CombatTransition,
        CombatPlanning,
        CombatResolving,
        Reward,
        Cutscene,
        UIOnly,
        Loading,
        Paused,

        // Compatibility alias for older scripts. New code should use
        // CombatPlanning or CombatResolving explicitly.
        Combat = CombatPlanning
    }
}
