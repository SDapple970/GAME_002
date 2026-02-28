namespace Game.Combat.Model
{
    public readonly struct CombatantId
    {
        public readonly int Value;
        public CombatantId(int value) => Value = value;
        public override string ToString() => Value.ToString();
    }

    public readonly struct SkillId
    {
        public readonly int Value;
        public SkillId(int value) => Value = value;
        public override string ToString() => Value.ToString();
    }
}
