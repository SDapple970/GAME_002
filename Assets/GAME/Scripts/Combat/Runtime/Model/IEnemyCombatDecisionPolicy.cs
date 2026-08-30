namespace Game.Combat.Model
{
    public interface IEnemyCombatDecisionPolicy
    {
        bool TryCreateDeclaration(
            EnemyCombatDecisionRequest request,
            out CombatAttackDeclaration declaration);
    }
}
