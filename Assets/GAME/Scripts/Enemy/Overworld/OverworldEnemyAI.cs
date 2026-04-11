// GAME/Scripts/Enemy/Overworld/OverworldEnemyAI.cs
using System.Collections;
using UnityEngine;
using Game.Core;

namespace Game.Enemy.Overworld
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class OverworldEnemyAI : MonoBehaviour
    {
        public enum AIState { Patrol, Chase }

        [Header("Movement Settings")]
        [SerializeField] private float patrolSpeed = 2f;
        [SerializeField] private float chaseSpeed = 4f;
        [SerializeField] private Transform[] waypoints;

        [Header("Detection Settings")]
        [SerializeField] private float detectionRadius = 5f;
        [SerializeField] private LayerMask playerLayer;

        [Header("Optimization")]
        [SerializeField] private float aiTickRate = 0.15f;

        private Rigidbody2D _rb;
        private AIState _currentState = AIState.Patrol;
        private int _currentWaypointIndex = 0;
        private Transform _targetPlayer;
        private Coroutine _aiRoutine;

        public AIState CurrentState => _currentState;
        public bool IsChasing => _currentState == AIState.Chase;
        public Transform TargetPlayer => _targetPlayer;

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
        }

        private IEnumerator AITickRoutine()
        {
            var wait = new WaitForSeconds(aiTickRate);

            while (true)
            {
                if (GameStateMachine.Instance != null && !GameStateMachine.Instance.Is(GameState.Exploration))
                {
                    _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                    yield return wait;
                    continue;
                }

                DetectPlayer();

                switch (_currentState)
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

        private void DetectPlayer()
        {
            Collider2D col = Physics2D.OverlapCircle(transform.position, detectionRadius, playerLayer);

            if (col != null)
            {
                _targetPlayer = col.transform;
                _currentState = AIState.Chase;
            }
            else
            {
                _targetPlayer = null;
                _currentState = AIState.Patrol;
            }
        }

        private void DoPatrol()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            Transform targetPoint = waypoints[_currentWaypointIndex];
            MoveTowards(targetPoint.position.x, patrolSpeed);

            if (Mathf.Abs(transform.position.x - targetPoint.position.x) < 0.5f)
            {
                _currentWaypointIndex = (_currentWaypointIndex + 1) % waypoints.Length;
            }
        }

        private void DoChase()
        {
            if (_targetPlayer == null) return;
            MoveTowards(_targetPlayer.position.x, chaseSpeed);
        }

        private void MoveTowards(float targetX, float speed)
        {
            float direction = Mathf.Sign(targetX - transform.position.x);

            if (direction != 0)
            {
                transform.localScale = new Vector3(
                    Mathf.Abs(transform.localScale.x) * direction,
                    transform.localScale.y,
                    transform.localScale.z);
            }

            _rb.linearVelocity = new Vector2(direction * speed, _rb.linearVelocity.y);
        }
    }
}