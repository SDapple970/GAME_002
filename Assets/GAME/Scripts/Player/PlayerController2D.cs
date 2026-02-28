// Scripts/Player/PlayerController2D.cs
using UnityEngine;
using Game.Core;

namespace Game.Player
{
    [RequireComponent(typeof(PlayerMotor2D))]
    public sealed class PlayerController2D : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource; // IPlayerInput을 붙여 넣기
        private IPlayerInput _input;
        private PlayerMotor2D _motor;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor2D>();
            _input = inputSource as IPlayerInput;

            if (_input == null)
                Debug.LogError("[PlayerController2D] inputSource가 IPlayerInput이 아님.");
        }

        private void Update()
        {
            // 상태 머신으로 플레이어 조작 잠금 (전투진입/컷신 등)
            if (GameStateMachine.Instance != null)
            {
                var s = GameStateMachine.Instance.Current;
                if (s != GameState.Exploration)
                    return;
            }

            if (_input == null) return;

            if (_input.JumpPressed)
            {
                _motor.Jump();
                _input.ConsumeJumpPressed();
            }
        }

        private void FixedUpdate()
        {
            if (GameStateMachine.Instance != null)
            {
                var s = GameStateMachine.Instance.Current;
                if (s != GameState.Exploration)
                {
                    _motor.Move(0f);
                    return;
                }
            }

            if (_input == null) return;
            _motor.Move(_input.MoveX);
        }
    }
}
