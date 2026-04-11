// GAME/Scripts/Enemy/Overworld/EnemyAnimator2D.cs
using UnityEngine;

namespace Game.Enemy.Overworld
{
    public sealed class EnemyAnimator2D : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private OverworldEnemyAI enemyAI;

        [Header("Tuning")]
        [SerializeField] private float moveThreshold = 0.05f;
        [SerializeField] private bool useRandomAttackVariant = true;
        [SerializeField] private int attackVariantCount = 3;

        private bool _wasChasing;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int DetectHash = Animator.StringToHash("Detect");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int AttackVariantHash = Animator.StringToHash("AttackVariant");
        private static readonly int IsChasingHash = Animator.StringToHash("IsChasing");

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (rb == null)
                rb = GetComponent<Rigidbody2D>();

            if (enemyAI == null)
                enemyAI = GetComponent<OverworldEnemyAI>();
        }

        private void Update()
        {
            if (animator == null) return;

            float speed = 0f;
            if (rb != null)
                speed = Mathf.Abs(rb.linearVelocity.x);

            animator.SetFloat(SpeedHash, speed);

            bool isChasing = enemyAI != null && enemyAI.IsChasing;
            animator.SetBool(IsChasingHash, isChasing);

            if (!_wasChasing && isChasing)
            {
                animator.ResetTrigger(DetectHash);
                animator.SetTrigger(DetectHash);
            }

            _wasChasing = isChasing;
        }

        public void PlayAttack()
        {
            if (animator == null) return;

            if (useRandomAttackVariant && attackVariantCount > 1)
            {
                int variant = Random.Range(0, attackVariantCount);
                animator.SetInteger(AttackVariantHash, variant);
            }

            animator.ResetTrigger(AttackHash);
            animator.SetTrigger(AttackHash);
        }
    }
}