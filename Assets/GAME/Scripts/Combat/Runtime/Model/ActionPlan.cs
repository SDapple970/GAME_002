namespace Game.Combat.Model
{
    public readonly struct ActionPlan
    {
        public readonly PlannedAction Slot1;
        public readonly PlannedAction Slot2;

        public ActionPlan(PlannedAction slot1, PlannedAction slot2)
        {
            Slot1 = slot1;
            Slot2 = slot2;
        }
    }

    public readonly struct PlannedAction
    {
        public static PlannedAction None => new(isNone: true);

        public readonly bool IsNone;
        public readonly SkillId SkillId;
        public readonly SkillTag Tag;
        public readonly TargetingRule Targeting;
        public readonly CombatantId TargetCombatantId; // 단일 타겟일 때
        public readonly int PlannedSpeed;              // 정렬 기준
        public readonly bool ConsumesTurn;             // 무료 행동이면 false

        private PlannedAction(bool isNone)
        {
            IsNone = isNone;
            SkillId = default;
            Tag = SkillTag.Utility;
            Targeting = TargetingRule.None;
            TargetCombatantId = default;
            PlannedSpeed = 0;
            ConsumesTurn = true;
        }

        public PlannedAction(
            SkillId skillId,
            SkillTag tag,
            TargetingRule targeting,
            CombatantId targetCombatantId,
            int plannedSpeed,
            bool consumesTurn)
        {
            IsNone = false;
            SkillId = skillId;
            Tag = tag;
            Targeting = targeting;
            TargetCombatantId = targetCombatantId;
            PlannedSpeed = plannedSpeed;
            ConsumesTurn = consumesTurn;
        }
    }
}
