// 위치: GAME/Scripts/Enemy/Overworld/EnemyAnimator2D.cs
using UnityEngine;

namespace Game.Enemy.Overworld
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyAnimator2D : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("enemy_angel 루트에 붙은 Animator를 연결. 비워두면 같은 오브젝트에서 찾습니다.")]
        [SerializeField] private Animator animator;

        [Tooltip("적 루트의 Rigidbody2D. 비워두면 같은 오브젝트에서 찾습니다.")]
        [SerializeField] private Rigidbody2D rb;

        [Tooltip("선택. AI 상태값을 IsChasing 파라미터에 반영할 때 사용합니다.")]
        [SerializeField] private OverworldEnemyAI enemyAI;

        [Tooltip("좌우 반전할 SpriteRenderer. 비워두면 자식에서 찾습니다.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Animator Parameters")]
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string isMovingParameter = "IsMoving";
        [SerializeField] private string isChasingParameter = "IsChasing";

        [Header("Flip Settings")]
        [SerializeField] private bool autoFlip = true;

        [Tooltip("스프라이트 원본이 왼쪽을 보고 있으면 체크")]
        [SerializeField] private bool invertFlipX = false;

        [Header("Thresholds")]
        [SerializeField] private float movingThreshold = 0.05f;

        private int _speedHash;
        private int _isMovingHash;
        private int _isChasingHash;

        private bool _hasSpeed;
        private bool _hasIsMoving;
        private bool _hasIsChasing;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            if (rb == null)
                rb = GetComponent<Rigidbody2D>();

            if (enemyAI == null)
                enemyAI = GetComponent<OverworldEnemyAI>();

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            CacheAnimatorParameters();
        }

        private void Update()
        {
            if (animator == null || rb == null) return;

            float horizontalSpeed = Mathf.Abs(rb.linearVelocity.x);
            bool isMoving = horizontalSpeed > movingThreshold;
            bool isChasing = enemyAI != null && enemyAI.CurrentState == OverworldEnemyAI.AIState.Chase;

            if (_hasSpeed)
                animator.SetFloat(_speedHash, horizontalSpeed);

            if (_hasIsMoving)
                animator.SetBool(_isMovingHash, isMoving);

            if (_hasIsChasing)
                animator.SetBool(_isChasingHash, isChasing);

            UpdateFlip();
        }

        private void UpdateFlip()
        {
            if (!autoFlip || spriteRenderer == null || rb == null) return;

            float vx = rb.linearVelocity.x;
            if (Mathf.Abs(vx) <= movingThreshold) return;

            bool shouldFaceLeft = vx < 0f;
            spriteRenderer.flipX = invertFlipX ? !shouldFaceLeft : shouldFaceLeft;
        }

        private void CacheAnimatorParameters()
        {
            _speedHash = Animator.StringToHash(speedParameter);
            _isMovingHash = Animator.StringToHash(isMovingParameter);
            _isChasingHash = Animator.StringToHash(isChasingParameter);

            _hasSpeed = HasParameter(speedParameter, AnimatorControllerParameterType.Float);
            _hasIsMoving = HasParameter(isMovingParameter, AnimatorControllerParameterType.Bool);
            _hasIsChasing = HasParameter(isChasingParameter, AnimatorControllerParameterType.Bool);
        }

        private bool HasParameter(string parameterName, AnimatorControllerParameterType expectedType)
        {
            if (animator == null) return false;
            if (string.IsNullOrWhiteSpace(parameterName)) return false;

            AnimatorControllerParameter[] parameters = animator.parameters;

            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];

                if (parameter.name == parameterName && parameter.type == expectedType)
                    return true;
            }

            return false;
        }
    }
}