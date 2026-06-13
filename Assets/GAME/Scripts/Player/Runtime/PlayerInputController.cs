using Game.Core;
using UnityEngine;

namespace Game.Player
{
    public sealed class PlayerInputController : MonoBehaviour
    {
        [SerializeField] private PlayerMotor2D_New motor;
        [SerializeField] private PlayerFieldAttackController fieldAttackController;

        private GameInputInstaller _input;
        private bool _subscribed;

        private void Awake()
        {
            AutoBindReferences();
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void Start()
        {
            AutoBindReferences();
            TrySubscribe();
        }

        private void Update()
        {
            if (!_subscribed)
                TrySubscribe();

            if (!CanControl())
                motor?.ForceStopHorizontal();
        }

        private void OnDisable()
        {
            Unsubscribe();
            motor?.ForceStopHorizontal();
        }

        private void HandleMove(Vector2 move)
        {
            if (!CanControl())
            {
                motor?.ForceStopHorizontal();
                return;
            }

            motor?.SetMoveInput(move.x);
        }

        private void HandleJump()
        {
            if (!CanControl())
                return;

            motor?.RequestJump();
        }

        private void HandleAttack()
        {
            if (!CanControl())
                return;

            fieldAttackController?.RequestPrimaryAttack();
        }

        private void AutoBindReferences()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor2D_New>();

            if (fieldAttackController == null)
                fieldAttackController = GetComponent<PlayerFieldAttackController>();
        }

        private void TrySubscribe()
        {
            if (_subscribed)
                return;

            _input = GameInputInstaller.Instance;
            if (_input == null)
                return;

            _input.Move += HandleMove;
            _input.Jump += HandleJump;
            _input.Attack += HandleAttack;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _input == null)
            {
                _subscribed = false;
                _input = null;
                return;
            }

            _input.Move -= HandleMove;
            _input.Jump -= HandleJump;
            _input.Attack -= HandleAttack;
            _subscribed = false;
            _input = null;
        }

        private static bool CanControl()
        {
            return GameStateMachine.Instance == null ||
                   GameStateMachine.Instance.Is(GameState.Exploration);
        }
    }
}
