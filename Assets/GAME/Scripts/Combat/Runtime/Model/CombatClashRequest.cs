namespace Game.Combat.Model
{
    public sealed class CombatClashRequest
    {
        public CombatAttackDeclaration AttackDeclaration { get; }
        public CombatResponseState ResponseState { get; }
        public CombatResponseDeclaration ResponseDeclaration { get; }
        public ICombatant Attacker => AttackDeclaration?.Attacker;
        public ICombatant Target => AttackDeclaration?.Target;
        public ISkill AttackSkill => AttackDeclaration?.Skill;
        public ISkill ResponseSkill => ResponseDeclaration?.Skill;

        public CombatClashRequest(
            CombatAttackDeclaration attackDeclaration,
            CombatResponseState responseState,
            CombatResponseDeclaration responseDeclaration)
        {
            AttackDeclaration = attackDeclaration;
            ResponseState = responseState;
            ResponseDeclaration = responseDeclaration;
        }
    }
}
