using System.Collections;
using System.Collections.Generic;
using System;
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
        private int _attackGeneration;
        private GameStateMachine _subscribedStateMachine;

        private void Awake()
        {
            AutoBindReferences();
        }

        private void Start()
        {
            AutoBindReferences();
            RefreshStateSubscription();
        }

        private void OnEnable()
        {
            RefreshStateSubscription();
            if (CanAttack())
                ResetAttackState();
        }

        private void Update()
        {
            RefreshStateSubscription();
        }

        private void OnDisable()
        {
            UnsubscribeStateMachine();
            CancelPendingAttack();
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
            int generation = ++_attackGeneration;
            StartCoroutine(Co_Attack(generation));
        }

        private IEnumerator Co_Attack(int generation)
        {
            _attackRunning = true;
            _combatStarted = false;

            animationController?.PlayAttack();

            float delay = Mathf.Max(0f, hitDelay);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (generation == _attackGeneration && CanAttack())
                TryResolveHit(generation);
            _attackRunning = false;
        }

        private void TryResolveHit(int generation)
        {
            if (generation != _attackGeneration || !CanAttack() || attackOrigin == null || entryPoint == null)
                return;

            Vector2 center = (Vector2)attackOrigin.position + hitBoxOffset;
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, hitBoxSize, 0f, targetMask);
            HashSet<GameObject> resolvedEncounterRoots = new HashSet<GameObject>();

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                    continue;

                GameObject enemyRoot = ResolveEnemyRoot(hit);
                if (enemyRoot == null || !resolvedEncounterRoots.Add(enemyRoot))
                    continue;

                ICombatEncounterRuntimeOwner encounterOwner = ResolveEncounterOwner(hit, enemyRoot);
                if (encounterOwner != null && !encounterOwner.TryReserve(this))
                    continue;

                List<GameObject> enemies = ResolveEnemies(enemyRoot);
                enemies = CreateActiveUniqueSnapshot(enemies);

                if (enemies.Count == 0)
                {
                    encounterOwner?.ReleaseReservation(this);
                    continue;
                }

                List<GameObject> allies = new List<GameObject>(1) { gameObject };

                CombatStartRequest request = new CombatStartRequest(
                    startReason,
                    initiativeSide,
                    0,
                    -1,
                    openingEffectOrNull
                );
                request.AllyFieldObjects.AddRange(allies);
                request.EnemyFieldObjects.AddRange(enemies);
                request.EncounterOwnerOrNull = encounterOwner as UnityEngine.Object;

                bool started = false;
                try
                {
                    if (generation == _attackGeneration && CanAttack())
                        started = entryPoint.StartCombat(request);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[PlayerFieldAttackController] Combat start threw and the encounter reservation was released. {exception}", this);
                }
                finally
                {
                    if (!started)
                        encounterOwner?.ReleaseReservation(this);
                }

                if (!started)
                    return;

                encounterOwner?.CommitReservation(entryPoint.ActiveSession != null
                    ? entryPoint.ActiveSession.CompletionId
                    : encounterOwner.ActiveCompletionId);
                _combatStarted = true;
                Debug.Log($"[PlayerFieldAttackController] Combat started by field attack. Reason={startReason}, Initiative={initiativeSide}", this);
                return;
            }
        }

        private static List<GameObject> CreateActiveUniqueSnapshot(List<GameObject> source)
        {
            int capacity = source != null ? source.Count : 0;
            List<GameObject> result = new List<GameObject>(capacity);
            HashSet<GameObject> seen = new HashSet<GameObject>();
            if (source == null)
                return result;

            for (int i = 0; i < source.Count; i++)
            {
                GameObject candidate = source[i];
                if (candidate != null && candidate.activeInHierarchy && seen.Add(candidate))
                    result.Add(candidate);
            }

            return result;
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

        private static ICombatEncounterRuntimeOwner ResolveEncounterOwner(Collider2D hit, GameObject enemyRoot)
        {
            CombatEncounterGroup group = hit != null ? hit.GetComponentInParent<CombatEncounterGroup>() : null;
            if (group == null && enemyRoot != null)
                group = enemyRoot.GetComponentInParent<CombatEncounterGroup>();
            if (group != null)
                return group;

            CombatEncounterTrigger2D trigger = hit != null ? hit.GetComponentInParent<CombatEncounterTrigger2D>() : null;
            if (trigger == null && enemyRoot != null)
                trigger = enemyRoot.GetComponentInParent<CombatEncounterTrigger2D>();
            return trigger != null ? trigger.RuntimeOwner : null;
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

        private void RefreshStateSubscription()
        {
            GameStateMachine current = GameStateMachine.Instance;
            if (_subscribedStateMachine == current)
                return;

            UnsubscribeStateMachine();
            _subscribedStateMachine = current;
            if (_subscribedStateMachine != null)
                _subscribedStateMachine.OnStateChanged += HandleGameStateChanged;
        }

        private void UnsubscribeStateMachine()
        {
            if (_subscribedStateMachine != null)
                _subscribedStateMachine.OnStateChanged -= HandleGameStateChanged;
            _subscribedStateMachine = null;
        }

        private void HandleGameStateChanged(GameState previous, GameState next)
        {
            if (next == GameState.Exploration)
            {
                ResetAttackState();
                return;
            }

            CancelPendingAttack();
        }

        private void CancelPendingAttack()
        {
            _attackGeneration++;
            StopAllCoroutines();
            _attackRunning = false;
        }

        private void ResetAttackState()
        {
            CancelPendingAttack();
            _combatStarted = false;
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
