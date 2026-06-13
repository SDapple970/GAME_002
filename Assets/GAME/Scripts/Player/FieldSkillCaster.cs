using System.Collections;
using Game.Battle;
using Game.Combat.Animation;
using Game.Combat.Data;
using Game.Core;
using Game.Common;
using UnityEngine;

namespace Game.Player
{
    public sealed class FieldSkillCaster : MonoBehaviour
    {
        [Header("Skill")]
        [SerializeField] private SkillDefinitionSO defaultSkill;

        [Header("References")]
        [SerializeField] private Transform hitOrigin;
        [SerializeField] private LayerMask targetMask;
        [SerializeField] private CombatantAnimationDriver animationDriver;
        [SerializeField] private bool subscribeToGameInput;

        private readonly Collider2D[] _hitBuffer = new Collider2D[16];
        private float _nextAttackTime;
        private bool _attackRunning;
        private bool _battleRequested;
        private GameInputInstaller _input;
        private bool _subscribedToInput;

        private void Awake()
        {
            AutoBindReferences();
        }

        private void OnEnable()
        {
            TrySubscribeInput();
        }

        private void Start()
        {
            AutoBindReferences();
            TrySubscribeInput();
        }

        private void OnDisable()
        {
            UnsubscribeInput();
            _attackRunning = false;
        }

        public void RequestPrimaryAttack()
        {
            if (!CanAttack())
                return;

            if (defaultSkill == null)
            {
                Debug.LogWarning("[FieldSkillCaster] Default skill is missing.", this);
                return;
            }

            if (!defaultSkill.FieldUsable)
                return;

            if (Time.time < _nextAttackTime || _attackRunning)
                return;

            _battleRequested = false;
            _nextAttackTime = Time.time + defaultSkill.FieldCooldown;
            StartCoroutine(Co_CastPrimaryAttack(defaultSkill));
        }

        private IEnumerator Co_CastPrimaryAttack(SkillDefinitionSO skill)
        {
            _attackRunning = true;

            animationDriver?.PlaySkill(skill, combatMode: false);
            Spawn(skill.CastVfxPrefab, transform.position);
            PlayClip(skill.CastSfx, transform.position);

            float delay = Mathf.Max(0f, skill.FieldHitTiming);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            PerformHit(skill);
            _attackRunning = false;
        }

        private void PerformHit(SkillDefinitionSO skill)
        {
            if (!CanAttack() || hitOrigin == null || skill == null)
                return;

            Vector2 center = (Vector2)hitOrigin.position + skill.FieldHitBoxOffset;
            ContactFilter2D filter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = true
            };
            filter.SetLayerMask(targetMask);

            int count = Physics2D.OverlapBox(center, skill.FieldHitBoxSize, 0f, filter, _hitBuffer);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _hitBuffer[i];
                if (hit == null)
                    continue;

                Vector3 impactPosition = hit.bounds.center;
                FieldEnemy fieldEnemy = hit.GetComponentInParent<FieldEnemy>();
                if (fieldEnemy != null)
                {
                    if (_battleRequested)
                        return;

                    _battleRequested = true;
                    Spawn(skill.ImpactVfxPrefab, impactPosition);
                    PlayClip(skill.ImpactSfx, impactPosition);
                    fieldEnemy.TakeDamage(Mathf.Max(0, skill.baseDamage));
                    return;
                }

                if (TryGetDamageable(hit, out IDamageable damageable))
                {
                    Spawn(skill.ImpactVfxPrefab, impactPosition);
                    PlayClip(skill.ImpactSfx, impactPosition);
                    damageable.TakeDamage(Mathf.Max(0, skill.baseDamage));
                }
            }
        }

        private void AutoBindReferences()
        {
            if (animationDriver == null)
                animationDriver = GetComponent<CombatantAnimationDriver>();

            if (animationDriver == null)
                animationDriver = GetComponentInChildren<CombatantAnimationDriver>();

            if (hitOrigin == null)
                hitOrigin = transform;
        }

        private void TrySubscribeInput()
        {
            if (!subscribeToGameInput || _subscribedToInput)
                return;

            _input = GameInputInstaller.Instance;
            if (_input == null)
                return;

            _input.Attack += RequestPrimaryAttack;
            _subscribedToInput = true;
        }

        private void UnsubscribeInput()
        {
            if (!_subscribedToInput || _input == null)
            {
                _subscribedToInput = false;
                _input = null;
                return;
            }

            _input.Attack -= RequestPrimaryAttack;
            _subscribedToInput = false;
            _input = null;
        }

        private static bool TryGetDamageable(Collider2D col, out IDamageable damageable)
        {
            damageable = null;
            if (col == null)
                return false;

            MonoBehaviour[] behaviours = col.GetComponentsInParent<MonoBehaviour>();
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

        private static bool CanAttack()
        {
            return GameStateMachine.Instance == null ||
                   GameStateMachine.Instance.Is(GameState.Exploration);
        }

        private static void Spawn(GameObject prefab, Vector3 position)
        {
            if (prefab != null)
                Instantiate(prefab, position, Quaternion.identity);
        }

        private static void PlayClip(AudioClip clip, Vector3 position)
        {
            if (clip != null)
                AudioSource.PlayClipAtPoint(clip, position);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (defaultSkill == null)
                return;

            Transform origin = hitOrigin != null ? hitOrigin : transform;
            Vector3 center = origin.position + (Vector3)defaultSkill.FieldHitBoxOffset;
            Gizmos.DrawWireCube(center, new Vector3(defaultSkill.FieldHitBoxSize.x, defaultSkill.FieldHitBoxSize.y, 0f));
        }
#endif
    }
}
