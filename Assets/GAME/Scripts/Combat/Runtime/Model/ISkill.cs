using Game.Combat.Data;

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
        SkillMovementMode MovementMode { get; }
        float DesiredTargetDistance { get; }
        float MoveSpeed { get; }
        float ActionDelayAfterMove { get; }

        int BaseDamage { get; }
        int BaseStagger { get; }
        int WeaknessStaggerBonus { get; }
        int Speed { get; }
        bool ConsumesTurn { get; }
    }
}
