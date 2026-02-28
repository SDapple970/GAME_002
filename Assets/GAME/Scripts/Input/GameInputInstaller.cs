using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GameInputInstaller : MonoBehaviour
{
    public static GameInputInstaller Instance { get; private set; }

    public GameInput Actions { get; private set; }

    // “기존 코드 최소 수정”용 이벤트
    public event System.Action<Vector2> Move;
    public event System.Action Jump;
    public event System.Action Attack;
    public event System.Action Parry;
    public event System.Action Interact;
    public event System.Action Pause;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Actions = new GameInput(); // <- Generate C# Class로 생긴 클래스명(예: GameInput)
    }

    private void OnEnable()
    {
        Actions.Enable();

        // Value
        Actions.Gameplay.Move.performed += ctx => Move?.Invoke(ctx.ReadValue<Vector2>());
        Actions.Gameplay.Move.canceled += ctx => Move?.Invoke(Vector2.zero);

        // Button
        Actions.Gameplay.Jump.performed += _ => Jump?.Invoke();
        Actions.Gameplay.Attack.performed += _ => Attack?.Invoke();
        Actions.Gameplay.Parry.performed += _ => Parry?.Invoke();
        Actions.Gameplay.Interact.performed += _ => Interact?.Invoke();
        Actions.Gameplay.Pause.performed += _ => Pause?.Invoke();
    }

    private void OnDisable()
    {
        Actions.Disable();
    }
}
