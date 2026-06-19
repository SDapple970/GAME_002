using Game.Input;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GameInputInstaller : MonoBehaviour
{
    public static GameInputInstaller Instance { get; private set; }
    public GameInput Actions { get; private set; }
    public InputService Service { get; private set; }
    public InputRouter Router { get; private set; }

    public event System.Action<Vector2> Move;
    public event System.Action Jump;
    public event System.Action Attack;
    public event System.Action Parry;
    public event System.Action Interact;
    public event System.Action Pause;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Actions = new GameInput();
        Service = new InputService();
        Router = new InputRouter();
    }

    private void OnEnable()
    {
        Actions.Enable();

        Actions.Gameplay.Move.performed += OnMovePerformed;
        Actions.Gameplay.Move.canceled += OnMoveCanceled;
        Actions.Gameplay.Jump.performed += OnJumpPerformed;
        Actions.Gameplay.Attack.performed += OnAttackPerformed;
        Actions.Gameplay.Parry.performed += OnParryPerformed;
        Actions.Gameplay.Interact.performed += OnInteractPerformed;
        Actions.Gameplay.Pause.performed += OnPausePerformed;
    }

    private void OnDisable()
    {
        Actions.Gameplay.Move.performed -= OnMovePerformed;
        Actions.Gameplay.Move.canceled -= OnMoveCanceled;
        Actions.Gameplay.Jump.performed -= OnJumpPerformed;
        Actions.Gameplay.Attack.performed -= OnAttackPerformed;
        Actions.Gameplay.Parry.performed -= OnParryPerformed;
        Actions.Gameplay.Interact.performed -= OnInteractPerformed;
        Actions.Gameplay.Pause.performed -= OnPausePerformed;

        Actions.Disable();
    }

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        EmitMove(Router == null || Router.AllowsExplorationInput() ? ctx.ReadValue<Vector2>() : Vector2.zero);
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        EmitMove(Vector2.zero);
    }

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (Router != null && !Router.AllowsExplorationInput())
            return;

        Service?.EmitJump();
        Jump?.Invoke();
    }

    private void OnAttackPerformed(InputAction.CallbackContext ctx)
    {
        if (Router != null && !Router.AllowsExplorationInput())
            return;

        Service?.EmitAttack();
        Attack?.Invoke();
    }

    private void OnParryPerformed(InputAction.CallbackContext ctx)
    {
        if (Router != null && !Router.AllowsExplorationInput())
            return;

        Service?.EmitParry();
        Parry?.Invoke();
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (Router != null && !Router.AllowsExplorationInput() && !Router.AllowsDialogueAdvanceInput() && !Router.AllowsUIInput())
            return;

        Service?.EmitInteract();
        Interact?.Invoke();
    }

    private void OnPausePerformed(InputAction.CallbackContext ctx)
    {
        if (Router != null && !Router.AllowsPauseInput())
            return;

        Service?.EmitPause();
        Pause?.Invoke();
    }

    private void EmitMove(Vector2 value)
    {
        Service?.EmitMove(value);
        Move?.Invoke(value);
    }
}