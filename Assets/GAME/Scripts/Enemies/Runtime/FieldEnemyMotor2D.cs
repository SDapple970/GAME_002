using UnityEngine;

namespace Game.Enemies
{
    public sealed class FieldEnemyMotor2D : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private bool faceMoveDirection = true;

        private bool _missingRigidbodyWarned;
        private float _currentDirection;

        public float CurrentSpeed { get; private set; }
        public float FacingSign { get; private set; } = 1f;

        private void Awake()
        {
            if (rb == null)
                rb = GetComponent<Rigidbody2D>();

            WarnIfMissingRigidbody();
            ApplyFacing();
        }

        private void OnDisable()
        {
            Stop();
        }

        public void Move(float direction)
        {
            _currentDirection = Mathf.Clamp(direction, -1f, 1f);
            CurrentSpeed = _currentDirection * moveSpeed;

            if (!Mathf.Approximately(_currentDirection, 0f))
            {
                FacingSign = Mathf.Sign(_currentDirection);
                ApplyFacing();
            }

            if (rb == null)
            {
                WarnIfMissingRigidbody();
                return;
            }

            if (rb.bodyType == RigidbodyType2D.Kinematic)
            {
                Vector2 nextPosition = rb.position + new Vector2(CurrentSpeed * Time.deltaTime, 0f);
                rb.MovePosition(nextPosition);
                return;
            }

            Vector2 velocity = rb.linearVelocity;
            velocity.x = CurrentSpeed;
            rb.linearVelocity = velocity;
        }

        public void MoveToward(Vector2 targetPosition)
        {
            float deltaX = targetPosition.x - transform.position.x;
            Move(Mathf.Approximately(deltaX, 0f) ? 0f : Mathf.Sign(deltaX));
        }

        public void Stop()
        {
            _currentDirection = 0f;
            CurrentSpeed = 0f;

            if (rb == null)
            {
                WarnIfMissingRigidbody();
                return;
            }

            if (rb.bodyType == RigidbodyType2D.Kinematic)
                rb.MovePosition(rb.position);

            rb.linearVelocity = Vector2.zero;
        }

        private void ApplyFacing()
        {
            if (!faceMoveDirection || visualRoot == null)
                return;

            Vector3 scale = visualRoot.localScale;
            float absX = Mathf.Abs(scale.x);
            if (Mathf.Approximately(absX, 0f))
                absX = 1f;

            scale.x = absX * FacingSign;
            visualRoot.localScale = scale;
        }

        private void WarnIfMissingRigidbody()
        {
            if (rb != null || _missingRigidbodyWarned)
                return;

            _missingRigidbodyWarned = true;
            Debug.LogWarning("[FieldEnemyMotor2D] Rigidbody2D is missing. Field enemy movement will be skipped.", this);
        }
    }
}
