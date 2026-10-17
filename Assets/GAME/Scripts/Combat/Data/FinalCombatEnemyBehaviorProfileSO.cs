using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat.Data
{
    /// <summary>
    /// Authored, optional FinalExchange attack preferences for one enemy definition.
    /// Runtime combat state is never stored here.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Combat/Final Combat Enemy Behavior Profile")]
    public sealed class FinalCombatEnemyBehaviorProfileSO : ScriptableObject
    {
        [SerializeField] private FinalCombatEnemyAttackPriority[] attackPriorities = Array.Empty<FinalCombatEnemyAttackPriority>();
        [SerializeField] private bool feintCueEnabled;
        [SerializeField] private bool declarationFeintEnabled;

        public IReadOnlyList<FinalCombatEnemyAttackPriority> AttackPriorities =>
            Array.AsReadOnly(attackPriorities ?? Array.Empty<FinalCombatEnemyAttackPriority>());

        /// <summary>
        /// Allows this authored profile to opt in to an explicitly requested, presentation-only feint cue.
        /// It does not schedule cues or alter combat rules.
        /// </summary>
        public bool FeintCueEnabled => feintCueEnabled;
        public bool DeclarationFeintEnabled => declarationFeintEnabled;

        private void OnValidate()
        {
            if (attackPriorities == null)
                return;

            for (int i = 0; i < attackPriorities.Length; i++)
                if (attackPriorities[i] != null)
                    attackPriorities[i].Clamp();
        }
    }

    /// <summary>
    /// Earlier array entries have higher priority when their HP condition is met.
    /// </summary>
    [Serializable]
    public sealed class FinalCombatEnemyAttackPriority
    {
        [SerializeField] private int skillId;
        [SerializeField, Range(0f, 1f)] private float maximumHpRatio = 1f;

        public int SkillId => skillId;
        public float MaximumHpRatio => maximumHpRatio;

        internal void Clamp()
        {
            maximumHpRatio = Mathf.Clamp01(maximumHpRatio);
        }
    }
}
