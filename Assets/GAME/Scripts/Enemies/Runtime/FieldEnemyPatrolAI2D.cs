using Game.Core;
using UnityEngine;

namespace Game.Enemies
{
    public sealed class FieldEnemyPatrolAI2D : MonoBehaviour
    {
        [SerializeField] private FieldEnemyMotor2D motor;
        [SerializeField] private Transform player;
        [SerializeField] private Transform leftPoint;
        [SerializeField] private Transform rightPoint;
        [SerializeField] private float detectionRange = 4f;
        [SerializeField] private float chaseStopDistance = 0.8f;
        [SerializeField] private float patrolWaitTime = 0.5f;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool chasePlayer = true;

        private bool movingRight = true;
        private float waitTimer;
        private bool _missingMotorWarned;
        private bool _missingPatrolPointsWarned;
        private bool _gameStateSubscribed;
        private bool _playerTagWarningLogged;

        private void Awake()
        {
            if (motor == null)
                motor = GetComponent<FieldEnemyMotor2D>();

            WarnIfMissingMotor();
        }

        private void OnEnable()
        {
            TrySubscribeGameState();
        }

        private void Start()
        {
            if (motor == null)
                motor = GetComponent<FieldEnemyMotor2D>();

            TrySubscribeGameState();
            ResolvePlayer();
        }

        private void OnDisable()
        {
            if (_gameStateSubscribed && GameStateMachine.Instance != null)
                GameStateMachine.Instance.OnStateChanged -= HandleGameStateChanged;

            _gameStateSubscribed = false;

            if (motor != null)
                motor.Stop();
        }

        private void Update()
        {
            TrySubscribeGameState();

            if (!CanMoveInCurrentState())
            {
                StopMotor();
                return;
            }

            if (motor == null)
            {
                WarnIfMissingMotor();
                return;
            }

            if (player == null)
                ResolvePlayer();

            if (chasePlayer && IsPlayerInDetectionRange())
            {
                ChasePlayer();
                return;
            }

            Patrol();
        }

        private void HandleGameStateChanged(GameState previous, GameState next)
        {
            if (next != GameState.Exploration)
                StopMotor();
        }

        private void TrySubscribeGameState()
        {
            if (_gameStateSubscribed || GameStateMachine.Instance == null)
                return;

            GameStateMachine.Instance.OnStateChanged += HandleGameStateChanged;
            _gameStateSubscribed = true;
        }

        private void ResolvePlayer()
        {
            if (player != null || string.IsNullOrEmpty(playerTag))
                return;

            try
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
                if (playerObject != null)
                    player = playerObject.transform;
            }
            catch (UnityException)
            {
                if (_playerTagWarningLogged)
                    return;

                _playerTagWarningLogged = true;
                Debug.LogWarning($"[FieldEnemyPatrolAI2D] Player tag '{playerTag}' is not defined. Player auto-detection is disabled.", this);
            }
        }

        private bool CanMoveInCurrentState()
        {
            if (GameStateMachine.Instance == null)
                return true;

            return GameStateMachine.Instance.Is(GameState.Exploration);
        }

        private bool IsPlayerInDetectionRange()
        {
            if (player == null)
                return false;

            float sqrRange = detectionRange * detectionRange;
            return ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude <= sqrRange;
        }

        private void ChasePlayer()
        {
            if (player == null)
            {
                StopMotor();
                return;
            }

            float distance = Vector2.Distance(transform.position, player.position);
            if (distance <= chaseStopDistance)
            {
                StopMotor();
                return;
            }

            motor.MoveToward(player.position);
            waitTimer = 0f;
        }

        private void Patrol()
        {
            if (leftPoint == null || rightPoint == null)
            {
                WarnIfMissingPatrolPoints();
                StopMotor();
                return;
            }

            if (waitTimer > 0f)
            {
                waitTimer -= Time.deltaTime;
                StopMotor();
                return;
            }

            Transform targetPoint = movingRight ? rightPoint : leftPoint;
            float deltaX = targetPoint.position.x - transform.position.x;

            if (Mathf.Abs(deltaX) <= 0.05f)
            {
                movingRight = !movingRight;
                waitTimer = patrolWaitTime;
                StopMotor();
                return;
            }

            motor.Move(Mathf.Sign(deltaX));
        }

        private void StopMotor()
        {
            if (motor != null)
                motor.Stop();
        }

        private void WarnIfMissingMotor()
        {
            if (motor != null || _missingMotorWarned)
                return;

            _missingMotorWarned = true;
            Debug.LogWarning("[FieldEnemyPatrolAI2D] FieldEnemyMotor2D is missing. Patrol AI cannot move this enemy.", this);
        }

        private void WarnIfMissingPatrolPoints()
        {
            if (_missingPatrolPointsWarned)
                return;

            _missingPatrolPointsWarned = true;
            Debug.LogWarning("[FieldEnemyPatrolAI2D] Left Point or Right Point is missing. Patrol movement will stop until both are assigned.", this);
        }
    }
}
