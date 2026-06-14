using UnityEngine;

namespace Game.Enemies
{
    public sealed class FieldEnemyAnimator2D : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private FieldEnemyMotor2D motor;
        [SerializeField] private string speedParam = "Speed";

        private int _speedHash;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (motor == null)
                motor = GetComponentInParent<FieldEnemyMotor2D>();

            _speedHash = Animator.StringToHash(speedParam);
        }

        private void Update()
        {
            if (animator == null || motor == null)
                return;

            animator.SetFloat(_speedHash, Mathf.Abs(motor.CurrentSpeed));
        }
    }
}
