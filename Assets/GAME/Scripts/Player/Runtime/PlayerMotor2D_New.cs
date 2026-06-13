using UnityEngine;

namespace Game.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMotor2D_New : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float jumpForce = 13f;
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundRadius = 0.2f;
        [SerializeField] private LayerMask groundMask;

        private Rigidbody2D _rb;
        private float _moveInput;
        private bool _jumpRequested;

        public bool IsGrounded { get; private set; }
        public float FacingSign { get; private set; } = 1f;
        public Vector2 Velocity => _rb != null ? _rb.linearVelocity : Vector2.zero;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            RefreshGrounded();
        }

        private void FixedUpdate()
        {
            if (_rb == null)
                return;

            RefreshGrounded();

            Vector2 velocity = _rb.linearVelocity;
            velocity.x = _moveInput * moveSpeed;

            if (_jumpRequested && IsGrounded)
                velocity.y = jumpForce;

            _jumpRequested = false;
            _rb.linearVelocity = velocity;
        }

        public void SetMoveInput(float x)
        {
            _moveInput = Mathf.Clamp(x, -1f, 1f);

            if (Mathf.Abs(_moveInput) > 0.01f)
                FacingSign = Mathf.Sign(_moveInput);
        }

        public void RequestJump()
        {
            _jumpRequested = true;
        }

        public void ForceStopHorizontal()
        {
            _moveInput = 0f;
            _jumpRequested = false;

            if (_rb == null)
                return;

            Vector2 velocity = _rb.linearVelocity;
            velocity.x = 0f;
            _rb.linearVelocity = velocity;
        }

        private void RefreshGrounded()
        {
            if (groundCheck == null)
            {
                IsGrounded = false;
                return;
            }

            IsGrounded = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundMask) != null;
        }
    }
}
