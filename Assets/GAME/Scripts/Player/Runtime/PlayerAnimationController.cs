using UnityEngine;

namespace Game.Player
{
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private PlayerMotor2D_New motor;
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private string speedParam = "Speed";
        [SerializeField] private string groundedParam = "IsGrounded";
        [SerializeField] private string airSpeedYParam = "AirSpeedY";
        [SerializeField] private string attackTrigger = "Attack";
        [SerializeField] private string hitTrigger = "Hit";
        [SerializeField] private string dieTrigger = "Die";

        private void Awake()
        {
            AutoBindReferences();
        }

        private void Update()
        {
            if (motor != null)
                ApplyFacing(motor.FacingSign);

            if (animator == null)
                return;

            Vector2 velocity = motor != null ? motor.Velocity : rb != null ? rb.linearVelocity : Vector2.zero;

            SetFloat(speedParam, Mathf.Abs(velocity.x));
            SetBool(groundedParam, motor != null && motor.IsGrounded);
            SetFloat(airSpeedYParam, velocity.y);
        }

        public void PlayAttack()
        {
            PlayTrigger(attackTrigger);
        }

        public void PlayHit()
        {
            PlayTrigger(hitTrigger);
        }

        public void PlayDie()
        {
            PlayTrigger(dieTrigger);
        }

        public void PlayTrigger(string triggerName)
        {
            if (string.IsNullOrWhiteSpace(triggerName))
                return;

            if (animator == null)
            {
                Debug.LogWarning($"[PlayerAnimationController] Animator is missing for trigger '{triggerName}'.", this);
                return;
            }

            animator.SetTrigger(Animator.StringToHash(triggerName));
        }

        private void AutoBindReferences()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor2D_New>();

            if (rb == null)
                rb = GetComponent<Rigidbody2D>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        private void ApplyFacing(float facingSign)
        {
            if (visualRoot == null || Mathf.Abs(facingSign) < 0.01f)
                return;

            Vector3 scale = visualRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(facingSign);
            visualRoot.localScale = scale;
        }

        private void SetFloat(string paramName, float value)
        {
            if (!string.IsNullOrWhiteSpace(paramName))
                animator.SetFloat(Animator.StringToHash(paramName), value);
        }

        private void SetBool(string paramName, bool value)
        {
            if (!string.IsNullOrWhiteSpace(paramName))
                animator.SetBool(Animator.StringToHash(paramName), value);
        }
    }
}
