// GAME/Scripts/Player/OverworldAttack2D.cs
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Common;

public sealed class OverworldAttack2D : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference attack;
    [SerializeField] private bool useDirectInputAction = true;

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
    private bool _attackQueued;
    private bool _loggedDuplicateInputWarning;
    private readonly Collider2D[] _buffer = new Collider2D[16];

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        if (!useDirectInputAction || attack == null || attack.action == null)
            return;

        WarnIfDuplicateInputPathIsLikely();
        attack.action.performed += OnAttack;
        attack.action.Enable();
    }

    private void OnDisable()
    {
        if (!useDirectInputAction || attack == null || attack.action == null)
            return;

        attack.action.performed -= OnAttack;
        attack.action.Disable();
    }

    private void Update()
    {
        if (!_attackQueued)
            return;

        _attackQueued = false;
        TryAttack();
    }

    private void OnAttack(InputAction.CallbackContext ctx)
    {
        _attackQueued = true;
    }

    public void RequestAttack()
    {
        // Preferred runtime path: GameInputInstaller -> OverworldPlayerController -> RequestAttack().
        TryAttack();
    }

    private void WarnIfDuplicateInputPathIsLikely()
    {
        if (_loggedDuplicateInputWarning)
            return;

        if (GameInputInstaller.Instance == null)
            return;

        _loggedDuplicateInputWarning = true;
        Debug.LogWarning(
            "[OverworldAttack2D] Direct InputActionReference is enabled while GameInputInstaller exists. " +
            "If OverworldPlayerController also calls RequestAttack(), one input can trigger duplicate attack attempts. " +
            "Leave the attack reference empty or disable direct input after confirming the scene uses GameInputInstaller.",
            this
        );
    }

    private void TryAttack()
    {
        if (Time.time < _nextTime)
            return;

        _nextTime = Time.time + cooldown;

        if (animator != null && !string.IsNullOrEmpty(attackTrigger))
            animator.SetTrigger(attackTrigger);

        DoHitboxDamage();
    }

    private void DoHitboxDamage()
    {
        if (hitOrigin == null)
            return;

        Vector2 center = (Vector2)hitOrigin.position + hitBoxOffset;

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        filter.SetLayerMask(targetMask);

        int count = Physics2D.OverlapBox(center, hitBoxSize, 0f, filter, _buffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D col = _buffer[i];
            if (col == null)
                continue;

            if (TryGetDamageable(col, out IDamageable damageable))
            {
                damageable.TakeDamage(damage);
            }
        }
    }

    private static bool TryGetDamageable(Collider2D col, out IDamageable damageable)
    {
        damageable = null;

        MonoBehaviour[] behaviours = col.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IDamageable candidate)
            {
                damageable = candidate;
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (hitOrigin == null)
            return;

        Vector3 center = hitOrigin.position + (Vector3)hitBoxOffset;
        Gizmos.DrawWireCube(center, new Vector3(hitBoxSize.x, hitBoxSize.y, 0f));
    }
#endif
}
