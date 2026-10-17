using System;
using System.Collections.Generic;
using Game.Combat.Data;

namespace Game.Combat.Model
{
    /// <summary>
    /// Immutable, value-only FinalExchange behavior data captured at combat entry.
    /// It intentionally contains no scene or ScriptableObject reference.
    /// </summary>
    public sealed class EnemyCombatBehaviorSnapshot
    {
        public IReadOnlyList<EnemyCombatAttackPrioritySnapshot> AttackPriorities { get; }
        public bool FeintCueEnabled { get; }
        public bool DeclarationFeintEnabled { get; }
        public bool HasBehavior => AttackPriorities.Count > 0 || FeintCueEnabled || DeclarationFeintEnabled;

        public EnemyCombatBehaviorSnapshot(
            IEnumerable<EnemyCombatAttackPrioritySnapshot> attackPriorities,
            bool feintCueEnabled = false,
            bool declarationFeintEnabled = false)
        {
            List<EnemyCombatAttackPrioritySnapshot> copied = new();
            if (attackPriorities != null)
                foreach (EnemyCombatAttackPrioritySnapshot priority in attackPriorities)
                    copied.Add(priority);

            AttackPriorities = copied.AsReadOnly();
            FeintCueEnabled = feintCueEnabled;
            DeclarationFeintEnabled = declarationFeintEnabled;
        }

        public static EnemyCombatBehaviorSnapshot FromProfile(FinalCombatEnemyBehaviorProfileSO profile)
        {
            if (profile == null)
                return null;

            List<EnemyCombatAttackPrioritySnapshot> snapshots = new();
            for (int i = 0; profile.AttackPriorities != null && i < profile.AttackPriorities.Count; i++)
            {
                FinalCombatEnemyAttackPriority priority = profile.AttackPriorities[i];
                if (priority == null)
                    continue;

                snapshots.Add(new EnemyCombatAttackPrioritySnapshot(
                    priority.SkillId,
                    priority.MaximumHpRatio));
            }

            return snapshots.Count == 0 && !profile.FeintCueEnabled && !profile.DeclarationFeintEnabled
                ? null
                : new EnemyCombatBehaviorSnapshot(snapshots, profile.FeintCueEnabled, profile.DeclarationFeintEnabled);
        }
    }

    public readonly struct EnemyCombatAttackPrioritySnapshot
    {
        public int SkillId { get; }
        public float MaximumHpRatio { get; }

        public EnemyCombatAttackPrioritySnapshot(int skillId, float maximumHpRatio)
        {
            SkillId = skillId;
            MaximumHpRatio = Math.Clamp(maximumHpRatio, 0f, 1f);
        }

        public bool Matches(CombatantCombatState state)
        {
            return state != null && state.MaxHp > 0 &&
                   (float)state.CurrentHp / state.MaxHp <= MaximumHpRatio;
        }
    }
}
