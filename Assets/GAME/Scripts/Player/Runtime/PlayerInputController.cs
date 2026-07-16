using Game.Core;
using Game.Input;
using UnityEngine;

namespace Game.Player
{
    public sealed class PlayerInputController : MonoBehaviour
    {
        [SerializeField] private PlayerMotor2D_New motor;
        [SerializeField] private PlayerFieldAttackController fieldAttackController;

        private GameInputInstaller _inputInstaller;
        private InputService _inputService;
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
            EnsureInputSubscription();

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
            EnsureInputSubscription();
        }

        private void EnsureInputSubscription()
        {
            GameInputInstaller installer = GameInputInstaller.Instance;
            InputService service = installer != null ? installer.Service : null;

            if (_subscribed && _inputInstaller == installer && _inputService == service)
                return;

            Unsubscribe();

            if (installer == null || service == null)
                return;

            _inputInstaller = installer;
            _inputService = service;
            _inputService.Move += HandleMove;
            _inputService.Jump += HandleJump;
            _inputService.Attack += HandleAttack;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _inputService == null)
            {
                _subscribed = false;
                _inputInstaller = null;
                _inputService = null;
                return;
            }

            _inputService.Move -= HandleMove;
            _inputService.Jump -= HandleJump;
            _inputService.Attack -= HandleAttack;
            _subscribed = false;
            _inputInstaller = null;
            _inputService = null;
        }

        private static bool CanControl()
        {
            return GameStateMachine.Instance == null ||
                   GameStateMachine.Instance.AllowsExplorationInput();
        }
    }
}
