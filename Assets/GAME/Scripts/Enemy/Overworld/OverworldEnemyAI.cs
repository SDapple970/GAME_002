// À§Ä¡: GAME/Scripts/Enemy/Overworld/OverworldEnemyAI.cs
using System.Collections;
using UnityEngine;
using Game.Core;

namespace Game.Enemy.Overworld
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class OverworldEnemyAI : MonoBehaviour
    {
        public enum AIState
        {
            Patrol,
            Chase
        }

        [Header("Movement Settings")]
        [SerializeField] private float patrolSpeed = 2f;
        [SerializeField] private float chaseSpeed = 4f;
        [SerializeField] private Transform[] waypoints;

        [Header("Detection Settings")]
        [SerializeField] private float detectionRadius = 5f;
        [SerializeField] private LayerMask playerLayer;

        [Header("Optimization")]
        [SerializeField] private float aiTickRate = 0.15f;

        [Header("Debug")]
        [SerializeField] private bool drawDebugGizmos = true;

        private Rigidbody2D _rb;
        private int _currentWaypointIndex;
        private Transform _targetPlayer;
        private Coroutine _aiRoutine;

        public AIState CurrentState { get; private set; } = AIState.Patrol;
        public bool HasTarget => _targetPlayer != null;
        public float FacingDirection { get; private set; } = 1f;
        public float CurrentHorizontalSpeed => Mathf.Abs(_rb != null ? _rb.linearVelocity.x : 0f);

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            if (_aiRoutine == null)
                _aiRoutine = StartCoroutine(AITickRoutine());
        }

        private void OnDisable()
        {
            if (_aiRoutine != null)
            {
                StopCoroutine(_aiRoutine);
                _aiRoutine = null;
            }

            StopHorizontalMovement();
        }

        private IEnumerator AITickRoutine()
        {
            var wait = new WaitForSeconds(aiTickRate);

            while (true)
            {
                if (!CanThinkAndMove())
                {
                    CurrentState = AIState.Patrol;
                    _targetPlayer = null;
                    StopHorizontalMovement();
                    yield return wait;
                    continue;
                }

                DetectPlayer();

                switch (CurrentState)
                {
                    case AIState.Patrol:
                        DoPatrol();
                        break;

                    case AIState.Chase:
                        DoChase();
                        break;
                }

                yield return wait;
            }
        }

        private bool CanThinkAndMove()
        {
            if (GameStateMachine.Instance == null)
                return true;

            return GameStateMachine.Instance.Is(GameState.Exploration);
        }

        private void DetectPlayer()
        {
            Collider2D col = Physics2D.OverlapCircle(transform.position, detectionRadius, playerLayer);

            if (col != null)
            {
                _targetPlayer = col.transform;
                CurrentState = AIState.Chase;
            }
            else
            {
                _targetPlayer = null;
                CurrentState = AIState.Patrol;
            }
        }

        private void DoPatrol()
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                StopHorizontalMovement();
                return;
            }

            Transform targetPoint = waypoints[_currentWaypointIndex];
            if (targetPoint == null)
            {
                AdvanceWaypoint();
                return;
            }

            MoveTowardsX(targetPoint.position.x, patrolSpeed);

            if (Mathf.Abs(transform.position.x - targetPoint.position.x) < 0.25f)
            {
                AdvanceWaypoint();
            }
        }

        private void DoChase()
        {
            if (_targetPlayer == null)
            {
                CurrentState = AIState.Patrol;
                return;
            }

            MoveTowardsX(_targetPlayer.position.x, chaseSpeed);
        }

        private void MoveTowardsX(float targetX, float speed)
        {
            if (_rb == null) return;

            float deltaX = targetX - transform.position.x;

            if (Mathf.Abs(deltaX) < 0.05f)
            {
                StopHorizontalMovement();
                return;
            }

            float direction = Mathf.Sign(deltaX);
            FacingDirection = direction;

            _rb.linearVelocity = new Vector2(direction * speed, _rb.linearVelocity.y);
        }

        private void StopHorizontalMovement()
        {
            if (_rb == null) return;

            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
        }

        private void AdvanceWaypoint()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            _currentWaypointIndex = (_currentWaypointIndex + 1) % waypoints.Length;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
#endif
    }
}