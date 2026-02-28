using System.Collections;
using UnityEngine;
using Game.Core;

namespace Game.Enemy.Overworld
{
    // 물리 이동을 위해 Rigidbody2D를 필수로 요구함
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class OverworldEnemyAI : MonoBehaviour
    {
        public enum AIState { Patrol, Chase }

        [Header("Movement Settings")]
        [Tooltip("배회할 때의 이동 속도")]
        [SerializeField] private float patrolSpeed = 2f;
        [Tooltip("플레이어를 쫓을 때의 이동 속도")]
        [SerializeField] private float chaseSpeed = 4f;
        [Tooltip("순찰할 지점(Transform)들. (빈 오브젝트들을 할당)")]
        [SerializeField] private Transform[] waypoints;

        [Header("Detection Settings")]
        [Tooltip("플레이어를 감지하는 반경")]
        [SerializeField] private float detectionRadius = 5f;
        [Tooltip("플레이어의 레이어 (Inspector에서 설정)")]
        [SerializeField] private LayerMask playerLayer;

        [Header("Optimization")]
        [Tooltip("AI 판단 주기 (초 단위). Update 대신 사용하여 연산 절약")]
        [SerializeField] private float aiTickRate = 0.15f;

        private Rigidbody2D _rb;
        private AIState _currentState = AIState.Patrol;
        private int _currentWaypointIndex = 0;
        private Transform _targetPlayer;
        private Coroutine _aiRoutine;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            // 오브젝트가 활성화될 때 AI 루틴 시작
            if (_aiRoutine == null)
                _aiRoutine = StartCoroutine(AITickRoutine());
        }

        private void OnDisable()
        {
            // 오브젝트가 비활성화되면 코루틴 정지 (오브젝트 풀링 대비)
            if (_aiRoutine != null)
            {
                StopCoroutine(_aiRoutine);
                _aiRoutine = null;
            }
        }

        private IEnumerator AITickRoutine()
        {
            // 주기적으로 상태를 판단하는 루프
            var wait = new WaitForSeconds(aiTickRate);

            while (true)
            {
                // 게임 상태가 '탐험(Exploration)'이 아닐 때는 움직이거나 판단하지 않음
                if (GameStateMachine.Instance != null && !GameStateMachine.Instance.Is(GameState.Exploration))
                {
                    _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                    yield return wait;
                    continue;
                }

                // 주변 플레이어 탐색 (물리 연산 최소화)
                DetectPlayer();

                // 현재 상태에 따른 행동 실행
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
            // Physics2D.OverlapCircle을 통해 범위 내 플레이어 탐색
            Collider2D col = Physics2D.OverlapCircle(transform.position, detectionRadius, playerLayer);

            if (col != null)
            {
                _targetPlayer = col.transform;
                _currentState = AIState.Chase; // 발견 시 추적 상태로 전환
            }
            else
            {
                _targetPlayer = null;
                _currentState = AIState.Patrol; // 놓치면 다시 배회 상태로
            }
        }

        private void DoPatrol()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            Transform targetPoint = waypoints[_currentWaypointIndex];
            MoveTowards(targetPoint.position.x, patrolSpeed);

            // 목표 지점에 도달했는지 확인 (x축 거리만 계산)
            if (Mathf.Abs(transform.position.x - targetPoint.position.x) < 0.5f)
            {
                // 다음 지점으로 인덱스 업데이트 (마지막 지점이면 처음으로)
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
            // 목표 방향 계산
            float direction = Mathf.Sign(targetX - transform.position.x);

            // 스프라이트 좌우 반전 (필요시 SpriteRenderer.flipX로 대체 가능)
            if (direction != 0)
            {
                transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * direction, transform.localScale.y, transform.localScale.z);
            }

            // Rigidbody2D를 이용한 물리 이동
            _rb.linearVelocity = new Vector2(direction * speed, _rb.linearVelocity.y);
        }

#if UNITY_EDITOR
        // 인스펙터 창에서 감지 범위를 시각적으로 확인하기 위한 기즈모
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
#endif
    }
}