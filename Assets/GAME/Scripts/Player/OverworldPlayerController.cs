// Scripts/Player/OverworldPlayerController.cs
using UnityEngine;
using Game.Core;

namespace Game.Player
{
    [RequireComponent(typeof(PlayerMotor2D))]
    [RequireComponent(typeof(Rigidbody2D))] // 💡 물리 제어를 위해 추가
    public sealed class OverworldPlayerController : MonoBehaviour
    {
        private PlayerMotor2D _motor;
        private OverworldAttack2D _attack;
        private Rigidbody2D _rb; // 💡 직접 제어용

        private float _currentMoveInput;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor2D>();
            _attack = GetComponent<OverworldAttack2D>();
            _rb = GetComponent<Rigidbody2D>();
        }

        private void Start()
        {
            if (GameInputInstaller.Instance != null)
            {
                GameInputInstaller.Instance.Jump += OnJump;
                GameInputInstaller.Instance.Attack += OnAttack;
            }
        }

        private void OnDestroy()
        {
            if (GameInputInstaller.Instance != null)
            {
                GameInputInstaller.Instance.Jump -= OnJump;
                GameInputInstaller.Instance.Attack -= OnAttack;
            }
        }

        private void Update()
        {
            if (!CanMove())
            {
                _currentMoveInput = 0f;
                // 💡 킬스위치: 탐험 상태가 아니면 관성이고 뭐고 즉시 물리력을 0으로 만듭니다!
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                return;
            }

            if (GameInputInstaller.Instance != null)
            {
                _currentMoveInput = GameInputInstaller.Instance.Actions.Gameplay.Move.ReadValue<Vector2>().x;
            }
        }

        private void FixedUpdate()
        {
            // 💡 [핵심 수정] 조작이 불가능할 때 그냥 return 해버리면 Motor가 마지막 방향을 기억하고 계속 미끄러집니다.
            // 확실하게 0을 전달해서 Motor의 잔류 입력을 싹 지워줍니다!
            if (!CanMove())
            {
                _motor.Move(0f);
                return;
            }

            _motor.Move(_currentMoveInput);
        }

        private void OnJump()
        {
            if (!CanMove()) return;
            _motor.Jump();
        }

        private void OnAttack()
        {
            if (!CanMove()) return;
            if (_attack != null) _attack.RequestAttack();
        }

        private bool CanMove()
        {
            if (GameStateMachine.Instance == null) return true;
            return GameStateMachine.Instance.Is(GameState.Exploration);
        }

        // 💡 화면 좌측 상단에 현재 상태를 실시간으로 그립니다.
        private void OnGUI()
        {
            if (GameStateMachine.Instance != null)
            {
                GUIStyle style = new GUIStyle();
                style.fontSize = 30;
                style.normal.textColor = Color.red;
                style.fontStyle = FontStyle.Bold;

                GUI.Label(new Rect(20, 20, 500, 100), $"[상태 확인] 현재 상태: {GameStateMachine.Instance.Current}", style);
            }
        }
    }
}