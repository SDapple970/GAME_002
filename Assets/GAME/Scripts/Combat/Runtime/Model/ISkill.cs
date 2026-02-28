namespace Game.Combat.Model
{
    public interface ISkill
    {
        SkillId Id { get; }
        string Name { get; }

        int InspirationCost { get; }
        KeywordMask Keywords { get; }
        SkillTag Tag { get; }
        TargetingRule Targeting { get; }

        int BaseDamage { get; }       // MVP용
        int BaseStagger { get; }      // MVP용
        int WeaknessStaggerBonus { get; } // 약점 공격 시 추가 그로기
        int Speed { get; }            // 행동 속도(정렬용)
        bool ConsumesTurn { get; }    // 무료 행동이면 false
    }
}
