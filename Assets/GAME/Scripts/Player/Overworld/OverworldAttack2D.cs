using UnityEngine;
using System;
using UnityEngine.InputSystem;

public class OverworldAttack2D : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference attack; // ← 추가 (InputActions의 Attack 연결)

    [Header("References")]
    [SerializeField] private Transform hitOrigin;
    [SerializeField] private Vector2 hitBoxSize = new(1.2f, 0.8f);
    [SerializeField] private LayerMask targetMask;
    [SerializeField] private float cooldown = 0.25f;

    [Header("Damage")]
    [SerializeField] private int damage = 1;

    [Header("Animation (Optional)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string attackTrigger = "Attack";

    [SerializeField] private Vector2 hitBoxOffset = new(1.2f, 0f);

    private float _nextTime;
    private readonly Collider2D[] _buffer = new Collider2D[16];

    private bool _attackQueued;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }


    private void OnEnable()
    {
        if (attack == null) return;
        attack.action.Enable();
        attack.action.performed += OnAttack;
    }

    private void OnDisable()
    {
        if (attack == null) return;
        attack.action.performed -= OnAttack;
        attack.action.Disable();
    }

    private void OnAttack(InputAction.CallbackContext ctx)
    {
        _attackQueued = true;
    }

    public void RequestAttack()
    {
        TryAttack();
    }


    private void TryAttack()
    {
        if (Time.time < _nextTime) return;
        _nextTime = Time.time + cooldown;

        if (animator != null && !string.IsNullOrEmpty(attackTrigger))
            animator.SetTrigger(attackTrigger);

        DoHitboxDamage();
    }

    private void DoHitboxDamage()
    {
        if (hitOrigin == null) return;

        // OverlapBox로 히트박스 판정 (2D 쿼리 패턴) :contentReference[oaicite:3]{index=3}
        var center = (Vector2)hitOrigin.position + hitBoxOffset;

        var filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.SetLayerMask(targetMask);
        filter.useTriggers = true; // 트리거도 맞아야 하면

        int count = Physics2D.OverlapBox(center, hitBoxSize, 0f, filter, _buffer);

        for (int i = 0; i < count; i++)
        {
            var col = _buffer[i];
            if (col == null) continue;

            if (col.TryGetComponent<IDamageable>(out var dmg))
                dmg.TakeDamage(damage);
            else if (col.TryGetComponent<SimpleDamageable>(out var simple))
                simple.TakeDamage(damage);
        }
        
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (hitOrigin == null) return;
        Gizmos.matrix = Matrix4x4.TRS(hitOrigin.position, Quaternion.identity, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(hitBoxSize.x, hitBoxSize.y, 0f));
    }
#endif
}

public interface IDamageable
{
    void TakeDamage(int amount);
}

/// <summary>
/// 적 쪽에 아무것도 없으면 테스트용으로 붙일 수 있는 최소 구현.
/// (이미 너가 적 체력/피격 스크립트가 있으면 이건 안 써도 됨)
/// </summary>
public sealed class SimpleDamageable : MonoBehaviour, IDamageable
{
    [SerializeField] private int hp = 3;

    public void TakeDamage(int amount)
    {
        hp -= amount;
        if (hp <= 0) Destroy(gameObject);
    }
}
