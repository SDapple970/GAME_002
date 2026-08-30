namespace Game.Combat.Model
{
    public interface ICombatClashRule
    {
        CombatClashOutcome Resolve(CombatClashRequest request);
    }
}
