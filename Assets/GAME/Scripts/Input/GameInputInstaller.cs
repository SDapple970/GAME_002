using System;
using Game.Core;
using Game.Input;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GameInputInstaller : MonoBehaviour
{
    public static GameInputInstaller Instance { get; private set; }
    public GameInput Actions { get; private set; }
    public InputService Service { get; private set; }
    public InputRouter Router { get; private set; }

    // Compatibility-only surfaces. New production consumers subscribe to InputService.
    public event Action<Vector2> Move
    {
        add { if (Service != null) Service.Move += value; }
        remove { if (Service != null) Service.Move -= value; }
    }

    public event Action Jump
    {
        add { if (Service != null) Service.Jump += value; }
        remove { if (Service != null) Service.Jump -= value; }
    }

    public event Action Attack
    {
        add { if (Service != null) Service.Attack += value; }
        remove { if (Service != null) Service.Attack -= value; }
    }

    public event Action Parry
    {
        add { if (Service != null) Service.Parry += value; }
        remove { if (Service != null) Service.Parry -= value; }
    }

    public event Action Interact
    {
        add { if (Service != null) Service.ExplorationInteract += value; }
        remove { if (Service != null) Service.ExplorationInteract -= value; }
    }

    public event Action Pause
    {
        add { if (Service != null) Service.PauseRequested += value; }
        remove { if (Service != null) Service.PauseRequested -= value; }
    }

    private bool _callbacksRegistered;
    private int _callbackRegistrationCount;
    private bool _actionsEnabled;
    private GameStateMachine _stateMachine;
    private bool _stateMachineSubscribed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);

        Actions = new GameInput();
        Service = new InputService();
        Router = new InputRouter();
        EnsureStateMachineSubscription();
    }

    private void OnEnable()
    {
        if (Instance != this || Actions == null)
            return;

        RegisterCallbacks();
        EnableActions();
        EnsureStateMachineSubscription();
    }

    private void Update()
    {
        if (Instance == this)
            EnsureStateMachineSubscription();
    }

    private void OnDisable()
    {
        if (Instance != this)
            return;

        UnsubscribeStateMachine();
        DisableActions();
        UnregisterCallbacks();
        Service?.ClearMove(false);
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        UnsubscribeStateMachine();
        DisableActions();
        UnregisterCallbacks();
        Actions?.Dispose();
        Actions = null;
        Service = null;
        Router = null;
        Instance = null;
    }

    private void RegisterCallbacks()
    {
        if (_callbacksRegistered || Actions == null)
            return;

        Actions.Gameplay.Move.performed += OnMovePerformed;
        Actions.Gameplay.Move.canceled += OnMoveCanceled;
        Actions.Gameplay.Jump.performed += OnJumpPerformed;
        Actions.Gameplay.Attack.performed += OnAttackPerformed;
        Actions.Gameplay.Parry.performed += OnParryPerformed;
        Actions.Gameplay.Interact.performed += OnInteractPerformed;
        Actions.Gameplay.Pause.performed += OnPausePerformed;
        _callbacksRegistered = true;
        _callbackRegistrationCount++;
    }

    private void UnregisterCallbacks()
    {
        if (!_callbacksRegistered || Actions == null)
            return;

        Actions.Gameplay.Move.performed -= OnMovePerformed;
        Actions.Gameplay.Move.canceled -= OnMoveCanceled;
        Actions.Gameplay.Jump.performed -= OnJumpPerformed;
        Actions.Gameplay.Attack.performed -= OnAttackPerformed;
        Actions.Gameplay.Parry.performed -= OnParryPerformed;
        Actions.Gameplay.Interact.performed -= OnInteractPerformed;
        Actions.Gameplay.Pause.performed -= OnPausePerformed;
        _callbacksRegistered = false;
        _callbackRegistrationCount = Math.Max(0, _callbackRegistrationCount - 1);
    }

    private void EnableActions()
    {
        if (_actionsEnabled || Actions == null)
            return;

        Actions.Enable();
        _actionsEnabled = true;
    }

    private void DisableActions()
    {
        if (!_actionsEnabled || Actions == null)
            return;

        Actions.Disable();
        _actionsEnabled = false;
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        ProcessMove(context.ReadValue<Vector2>());
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        ProcessMoveCanceled();
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        ProcessJump();
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        ProcessAttack();
    }

    private void OnParryPerformed(InputAction.CallbackContext context)
    {
        ProcessParry();
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        ProcessInteract();
    }

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        ProcessPause();
    }

    private void ProcessMove(Vector2 value)
    {
        Vector2 routedValue = Router != null && Router.AllowsExplorationInput()
            ? value
            : Vector2.zero;
        Service?.EmitMove(routedValue);
    }

    private void ProcessMoveCanceled()
    {
        Service?.ClearMove(false);
    }

    private void ProcessJump()
    {
        if (Router != null && Router.AllowsExplorationInput())
            Service?.EmitJump();
    }

    private void ProcessAttack()
    {
        if (Router != null && Router.AllowsExplorationInput())
            Service?.EmitAttack();
    }

    private void ProcessParry()
    {
        if (Router != null && Router.AllowsExplorationInput())
            Service?.EmitParry();
    }

    private void ProcessInteract()
    {
        if (Router == null)
            return;

        switch (Router.ResolveInteractionDestination())
        {
            case InputRouter.InteractionDestination.Exploration:
                Service?.EmitExplorationInteract();
                break;
            case InputRouter.InteractionDestination.DialogueAdvance:
                Service?.EmitDialogueAdvance();
                break;
        }
    }

    private void ProcessPause()
    {
        if (Router != null && !Router.AllowsPauseInput())
            return;

        Service?.EmitPauseRequested();

        GameFlowController flow = GameFlowController.Instance;
        if (flow == null)
            return;

        if (GameStateMachine.Instance != null && GameStateMachine.Instance.Is(GameState.Paused))
            flow.ResumePreviousState();
        else
            flow.Pause();
    }

    private void EnsureStateMachineSubscription()
    {
        GameStateMachine candidate = GameStateMachine.Instance;
        if (_stateMachineSubscribed && _stateMachine == candidate)
            return;

        UnsubscribeStateMachine();
        _stateMachine = candidate;
        if (_stateMachine == null)
            return;

        _stateMachine.OnStateChanged += HandleGameStateChanged;
        _stateMachineSubscribed = true;

        if (!_stateMachine.Is(GameState.Exploration))
            Service?.ClearMove(false);
    }

    private void UnsubscribeStateMachine()
    {
        if (_stateMachineSubscribed && _stateMachine != null)
            _stateMachine.OnStateChanged -= HandleGameStateChanged;

        _stateMachineSubscribed = false;
        _stateMachine = null;
    }

    private void HandleGameStateChanged(GameState previous, GameState next)
    {
        if (previous == GameState.Exploration && next != GameState.Exploration)
            Service?.ClearMove(true);
    }
}
