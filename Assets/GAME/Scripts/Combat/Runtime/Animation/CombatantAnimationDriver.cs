using Game.Combat.Data;
using UnityEngine;

namespace Game.Combat.Animation
{
    public sealed class CombatantAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string defaultAttackTrigger = "Attack";
        [SerializeField] private string hitTrigger = "Hit";
        [SerializeField] private string staggerTrigger = "Stagger";
        [SerializeField] private string dieTrigger = "Die";
        [SerializeField] private bool warnWhenAnimatorMissing = true;

        private void Awake()
        {
            AutoBindAnimator();
        }

        private void Reset()
        {
            AutoBindAnimator();
        }

        public void PlayAttack()
        {
            PlaySkill(defaultAttackTrigger);
        }

        public void PlaySkill(string triggerName)
        {
            SetTrigger(string.IsNullOrWhiteSpace(triggerName) ? defaultAttackTrigger : triggerName);
        }

        public void PlaySkill(SkillDefinitionSO skill, bool combatMode)
        {
            if (skill == null)
            {
                PlayAttack();
                return;
            }

            PlaySkill(combatMode ? skill.CombatAnimationTrigger : skill.FieldAnimationTrigger);
        }

        public void PlayHit()
        {
            SetTrigger(hitTrigger);
        }

        public void PlayStagger()
        {
            SetTrigger(staggerTrigger);
        }

        public void PlayDie()
        {
            SetTrigger(dieTrigger);
        }

        private void SetTrigger(string triggerName)
        {
            if (string.IsNullOrWhiteSpace(triggerName))
                return;

            if (animator == null)
                AutoBindAnimator();

            if (animator == null)
            {
                if (warnWhenAnimatorMissing)
                    Debug.LogWarning($"[CombatantAnimationDriver] Animator is missing for trigger '{triggerName}'.", this);

                return;
            }

            animator.SetTrigger(Animator.StringToHash(triggerName));
        }

        private void AutoBindAnimator()
        {
            if (animator != null)
                return;

            animator = GetComponent<Animator>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }
    }
}
