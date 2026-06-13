// GAME/Scripts/Player/OverworldAttack2D.cs
using UnityEngine;
using Game.Core;
using Game.Common;
using Game.Player;

public sealed class OverworldAttack2D : MonoBehaviour
{
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
    [SerializeField] private FieldSkillCaster fieldSkillCaster;

    private float _nextTime;
    private readonly Collider2D[] _buffer = new Collider2D[16];

    private void Awake()
    {
        if (fieldSkillCaster == null)
            fieldSkillCaster = GetComponent<FieldSkillCaster>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    public void RequestAttack()
    {
        if (fieldSkillCaster != null)
        {
            fieldSkillCaster.RequestPrimaryAttack();
            return;
        }

        TryAttack();
    }

    private void TryAttack()
    {
        if (!CanAttack())
            return;

        if (Time.time < _nextTime)
            return;

        _nextTime = Time.time + cooldown;

        if (animator != null && !string.IsNullOrEmpty(attackTrigger))
            animator.SetTrigger(attackTrigger);

        DoHitboxDamage();
    }

    private static bool CanAttack()
    {
        return GameStateMachine.Instance == null ||
               GameStateMachine.Instance.Is(GameState.Exploration);
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
