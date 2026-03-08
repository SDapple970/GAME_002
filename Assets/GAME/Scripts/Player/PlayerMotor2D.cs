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

    // [field: SerializeField]를 추가해 게임 실행 중 인스펙터에서 실시간으로 true/false를 확인할 수 있게 합니다.
    [field: Header("Debug")]
    [field: SerializeField] public bool IsGrounded { get; private set; }

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

        // 바닥에 닿아있으면 초록색, 공중에 있으면 빨간색으로 표시하여 디버깅을 돕습니다.
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
    }
#endif
}