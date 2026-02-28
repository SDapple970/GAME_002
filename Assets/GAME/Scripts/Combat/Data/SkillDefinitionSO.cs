using UnityEngine;
using Game.Combat.Model;

namespace Game.Combat.Data
{
    [CreateAssetMenu(menuName = "Game/Combat/Skill Definition")]
    public sealed class SkillDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int skillId;
        public string displayName;

        [Header("Costs / Tags")]
        public int inspirationCost;
        public SkillTag tag = SkillTag.Attack;
        public TargetingRule targeting = TargetingRule.SingleEnemy;
        public bool consumesTurn = true;

        [Header("Keywords")]
        public KeywordMask keywords = KeywordMask.None;

        [Header("MVP Numbers")]
        public int baseDamage = 1;
        public int baseStagger = 1;
        public int weaknessStaggerBonus = 3;
        public int speed = 5;
    }
}
