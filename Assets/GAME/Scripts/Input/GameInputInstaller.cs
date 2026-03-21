// À§Ä¡: Assets/GAME/Scripts/Input/GameInputInstaller.cs
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GameInputInstaller : MonoBehaviour
{
    public static GameInputInstaller Instance { get; private set; }
    public GameInput Actions { get; private set; }

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

    private void OnMovePerformed(InputAction.CallbackContext ctx) => Move?.Invoke(ctx.ReadValue<Vector2>());
    private void OnMoveCanceled(InputAction.CallbackContext ctx) => Move?.Invoke(Vector2.zero);
    private void OnJumpPerformed(InputAction.CallbackContext ctx) => Jump?.Invoke();
    private void OnAttackPerformed(InputAction.CallbackContext ctx) => Attack?.Invoke();
    private void OnParryPerformed(InputAction.CallbackContext ctx) => Parry?.Invoke();
    private void OnInteractPerformed(InputAction.CallbackContext ctx) => Interact?.Invoke();
    private void OnPausePerformed(InputAction.CallbackContext ctx) => Pause?.Invoke();
}