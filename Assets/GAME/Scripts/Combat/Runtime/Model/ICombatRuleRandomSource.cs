namespace Game.Combat.Model
{
    public interface ICombatRuleRandomSource
    {
        int NextInclusive(int minInclusive, int maxInclusive);
    }
}
