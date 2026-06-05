// Scripts/Player/OverworldPlayerController.cs
using UnityEngine;
using Game.Core;

namespace Game.Player
{
    [RequireComponent(typeof(PlayerMotor2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class OverworldPlayerController : MonoBehaviour
    {
        private PlayerMotor2D _motor;
        private OverworldAttack2D _attack;
        private Rigidbody2D _rb;
        private GameInputInstaller _input;

        private float _currentMoveInput;
        private bool _subscribedToInput;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor2D>();
            _attack = GetComponent<OverworldAttack2D>();
            _rb = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            TrySubscribeInput();
        }

        private void Start()
        {
            TrySubscribeInput();
        }

        private void OnDisable()
        {
            UnsubscribeInput();
            _currentMoveInput = 0f;
        }

        private void Update()
        {
            if (!_subscribedToInput)
                TrySubscribeInput();

            if (!CanMove())
            {
                _currentMoveInput = 0f;
                if (_rb != null)
                    _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                return;
            }
        }

        private void FixedUpdate()
        {
            if (!CanMove())
            {
                if (_motor != null)
                    _motor.Move(0f);
                return;
            }

            if (_motor != null)
                _motor.Move(_currentMoveInput);
        }

        private void TrySubscribeInput()
        {
            if (_subscribedToInput)
                return;

            _input = GameInputInstaller.Instance;
            if (_input == null)
                return;

            _input.Move += OnMove;
            _input.Jump += OnJump;
            _input.Attack += OnAttack;
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

            _input.Move -= OnMove;
            _input.Jump -= OnJump;
            _input.Attack -= OnAttack;
            _subscribedToInput = false;
            _input = null;
        }

        private void OnMove(Vector2 move)
        {
            _currentMoveInput = CanMove() ? move.x : 0f;
        }

        private void OnJump()
        {
            if (!CanMove() || _motor == null)
                return;

            _motor.Jump();
        }

        private void OnAttack()
        {
            if (!CanMove() || _attack == null)
                return;

            _attack.RequestAttack();
        }

        private bool CanMove()
        {
            if (GameStateMachine.Instance == null) return true;
            return GameStateMachine.Instance.Is(GameState.Exploration);
        }

#if UNITY_EDITOR
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
#endif
    }
}
