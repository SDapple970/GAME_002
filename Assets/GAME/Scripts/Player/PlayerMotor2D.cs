using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMotor2D : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 7f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundRadius = 0.2f;
    [SerializeField] private LayerMask groundMask;

    private Rigidbody2D rb;
    private float moveInput;

    public bool IsGrounded { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // 바닥 체크
        if (groundCheck != null)
        {
            IsGrounded = Physics2D.OverlapCircle(
                groundCheck.position,
                groundRadius,
                groundMask
            );
        }
    }

    private void FixedUpdate()
    {
        // 좌우 이동
        Vector2 velocity = rb.linearVelocity;
        velocity.x = moveInput * moveSpeed;
        rb.linearVelocity = velocity;
    }

    // 🔹 외부에서 호출
    public void Move(float input)
    {
        moveInput = Mathf.Clamp(input, -1f, 1f);
    }

    public void Jump()
    {
        if (!IsGrounded) return;

        Vector2 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;

        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
    }
#endif
}
