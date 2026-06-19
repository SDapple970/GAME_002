using System.Collections;
using System.Collections.Generic;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    public sealed class PlayerFieldAttackController : MonoBehaviour
    {
        [SerializeField] private PlayerAnimationController animationController;
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private Vector2 hitBoxSize = new Vector2(1.2f, 0.8f);
        [SerializeField] private Vector2 hitBoxOffset = Vector2.zero;
        [SerializeField] private LayerMask targetMask;
        [SerializeField] private float hitDelay = 0.12f;
        [SerializeField] private float cooldown = 0.35f;
        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private Game.Combat.Adapters.OpeningEffectSO openingEffectOrNull;
        [SerializeField] private StartReason startReason = StartReason.PlayerFirstHit;
        [SerializeField] private Side initiativeSide = Side.Allies;
        [SerializeField] private string enemyTag = "Enemy";

        private float _nextAttackTime;
        private bool _attackRunning;
        private bool _combatStarted;

        private void Awake()
        {
            AutoBindReferences();
        }

        private void Start()
        {
            AutoBindReferences();
        }

        public void RequestPrimaryAttack()
        {
            if (!CanAttack())
                return;

            if (_combatStarted && entryPoint != null && entryPoint.ActiveStateMachine != null)
                return;

            if (entryPoint == null || entryPoint.ActiveStateMachine == null)
                _combatStarted = false;

            if (_attackRunning || Time.time < _nextAttackTime)
                return;

            _nextAttackTime = Time.time + Mathf.Max(0f, cooldown);
            StartCoroutine(Co_Attack());
        }

        private IEnumerator Co_Attack()
        {
            _attackRunning = true;
            _combatStarted = false;

            animationController?.PlayAttack();

            float delay = Mathf.Max(0f, hitDelay);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            TryResolveHit();
            _attackRunning = false;
        }

        private void TryResolveHit()
        {
            if (!CanAttack() || attackOrigin == null || entryPoint == null)
                return;

            Vector2 center = (Vector2)attackOrigin.position + hitBoxOffset;
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, hitBoxSize, 0f, targetMask);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                    continue;

                GameObject enemyRoot = ResolveEnemyRoot(hit);
                if (enemyRoot == null)
                    continue;

                List<GameObject> enemies = ResolveEnemies(enemyRoot);
                enemies.RemoveAll(enemy => enemy == null || !enemy.activeInHierarchy);

                if (enemies.Count == 0)
                    continue;

                List<GameObject> allies = new List<GameObject>(1) { gameObject };

                bool started = entryPoint.StartCombatFromField(
                    allies,
                    enemies,
                    startReason,
                    initiativeSide,
                    openingEffectOrNull
                );

                if (!started)
                    continue;

                _combatStarted = true;
                Debug.Log($"[PlayerFieldAttackController] Combat started by field attack. Reason={startReason}, Initiative={initiativeSide}", this);
                return;
            }
        }

        private GameObject ResolveEnemyRoot(Collider2D hit)
        {
            CombatEncounterGroup group = hit.GetComponentInParent<CombatEncounterGroup>();
            if (group != null)
                return group.gameObject;

            CombatEncounterTrigger2D trigger = hit.GetComponentInParent<CombatEncounterTrigger2D>();
            if (trigger != null)
                return trigger.gameObject;

            if (hit.CompareTag(enemyTag))
                return hit.gameObject;

            Transform current = hit.transform;
            while (current != null)
            {
                if (current.CompareTag(enemyTag))
                    return current.gameObject;

                current = current.parent;
            }

            return hit.attachedRigidbody != null ? hit.attachedRigidbody.gameObject : hit.gameObject;
        }

        private static List<GameObject> ResolveEnemies(GameObject enemyRoot)
        {
            CombatEncounterGroup group = enemyRoot.GetComponentInParent<CombatEncounterGroup>();
            if (group != null)
                return group.GetActiveEnemies();

            return new List<GameObject>(1) { enemyRoot };
        }

        private void AutoBindReferences()
        {
            if (animationController == null)
                animationController = GetComponent<PlayerAnimationController>();

            if (attackOrigin == null)
                attackOrigin = transform;

            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();
        }

        private static bool CanAttack()
        {
            return GameStateMachine.Instance == null ||
                   GameStateMachine.Instance.AllowsExplorationInput();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Transform origin = attackOrigin != null ? attackOrigin : transform;
            Vector3 center = origin.position + (Vector3)hitBoxOffset;
            Gizmos.DrawWireCube(center, new Vector3(hitBoxSize.x, hitBoxSize.y, 0f));
        }
#endif
    }
}
