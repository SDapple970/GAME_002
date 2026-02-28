using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// 플레이어의 이동, 점프 상태를 Animator에 전달하고, 캐릭터의 좌우 반전을 처리하는 클래스입니다.
    /// Update의 부담을 줄이기 위해 파라미터 해싱을 사용합니다.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerAnimator2D : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("애니메이터 컴포넌트. 비워두면 자식에서 자동으로 찾습니다.")]
        [SerializeField] private Animator _animator;

        [Tooltip("스프라이트와 공격 위치(hitOrigin)를 모두 포함하는 부모 트랜스폼. Scale 반전에 사용됩니다.")]
        [SerializeField] private Transform _visualRoot;

        private PlayerMotor2D _motor;
        private Rigidbody2D _rb;

        // 최적화: 문자열(String) 대신 정수형 해시(Hash)값으로 애니메이터 파라미터를 캐싱합니다.
        private readonly int _hashSpeed = Animator.StringToHash("Speed");
        private readonly int _hashIsGrounded = Animator.StringToHash("IsGrounded");
        private readonly int _hashAirSpeedY = Animator.StringToHash("AirSpeedY");

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor2D>();
            _rb = GetComponent<Rigidbody2D>();

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (_animator == null) return;

            // 1. 상태를 애니메이터 파라미터에 전달
            // 좌우 이동 속도의 절댓값을 전달 (0이면 Idle, 크면 Run)
            _animator.SetFloat(_hashSpeed, Mathf.Abs(_rb.linearVelocity.x));

            // 바닥에 닿아있는지 여부
            _animator.SetBool(_hashIsGrounded, _motor.IsGrounded);

            // Y축 속도 (양수면 상승(Jump), 음수면 하강(Fall) 애니메이션 재생용)
            _animator.SetFloat(_hashAirSpeedY, _rb.linearVelocity.y);

            // 2. 캐릭터 좌우 방향 반전
            UpdateFacingDirection();
        }

        private void UpdateFacingDirection()
        {
            // 움직임이 있을 때만 방향을 갱신합니다 (0.1f는 데드존/임계값 역할)
            if (_rb.linearVelocity.x > 0.1f)
            {
                // 오른쪽 바라보기
                if (_visualRoot != null)
                    _visualRoot.localScale = new Vector3(1f, 1f, 1f);
            }
            else if (_rb.linearVelocity.x < -0.1f)
            {
                // 왼쪽 바라보기 (-1로 스케일 반전)
                if (_visualRoot != null)
                    _visualRoot.localScale = new Vector3(-1f, 1f, 1f);
            }
        }
    }
}