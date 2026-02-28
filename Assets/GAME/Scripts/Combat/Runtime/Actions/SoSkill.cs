using Game.Combat.Data;
using Game.Combat.Model;

namespace Game.Combat.Actions
{
    public sealed class SoSkill : ISkill
    {
        private readonly SkillDefinitionSO _so;

        public SoSkill(SkillDefinitionSO so) => _so = so;

        public SkillId Id => new SkillId(_so.skillId);
        public string Name => _so.displayName;

        public int InspirationCost => _so.inspirationCost;
        public KeywordMask Keywords => _so.keywords;
        public SkillTag Tag => _so.tag;
        public TargetingRule Targeting => _so.targeting;

        public int BaseDamage => _so.baseDamage;
        public int BaseStagger => _so.baseStagger;
        public int WeaknessStaggerBonus => _so.weaknessStaggerBonus;
        public int Speed => _so.speed;
        public bool ConsumesTurn => _so.consumesTurn;
    }
}
