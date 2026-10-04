using Game.Combat.Data;
using Game.Combat.Model;

namespace Game.Combat.Actions
{
    public sealed class SoSkill : ISkill, ICombatMpCostProvider, IFinalCombatSkillStats, ICombatStatusEffectProvider
    {
        private readonly SkillDefinitionSO _so;

        public SoSkill(SkillDefinitionSO so) => _so = so;
        public SkillDefinitionSO Definition => _so;

        public SkillId Id => new SkillId(_so.skillId);
        public string Name => _so.displayName;

        public int InspirationCost => _so.inspirationCost;
        public int MpCost => _so.MpCost;
        public int FinalMpCost => _so.FinalMpCost;
        public int ClashPower => _so.ClashPower;
        public System.Collections.Generic.IReadOnlyList<CombatStatusDefinitionSO> AppliedStatusEffects =>
            _so.AppliedStatusEffects;
        public KeywordMask Keywords => _so.keywords;
        public SkillTag Tag => _so.tag;
        public TargetingRule Targeting => _so.targeting;
        public SkillMovementMode MovementMode => _so.MovementMode;
        public float DesiredTargetDistance => _so.DesiredTargetDistance;
        public float MoveSpeed => _so.MoveSpeed;
        public float ActionDelayAfterMove => _so.ActionDelayAfterMove;

        public int BaseDamage => _so.baseDamage;
        public int BaseStagger => _so.baseStagger;
        public int WeaknessStaggerBonus => _so.weaknessStaggerBonus;
        public int Speed => _so.speed;
        public bool ConsumesTurn => _so.consumesTurn;
    }
}
