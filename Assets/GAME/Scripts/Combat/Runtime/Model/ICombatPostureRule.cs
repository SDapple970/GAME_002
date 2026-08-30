namespace Game.Combat.Model
{
    public interface ICombatPostureRule
    {
        int ResolvePostureDelta(CombatPostureRequest request);
    }
}
