using UnityEngine;
using UnityEngine.InputSystem;

public class OverworldInputBridge : MonoBehaviour
{
    [SerializeField] private PlayerMotor2D motor;
    [SerializeField] private OverworldAttack2D attack;
    [SerializeField] private PlayerInput playerInput;

    private void Awake()
    {
        if (!motor) motor = GetComponent<PlayerMotor2D>();
        if (!attack) attack = GetComponent<OverworldAttack2D>();
        if (!playerInput) playerInput = GetComponent<PlayerInput>();
    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        Vector2 value = ctx.ReadValue<Vector2>();
        motor.Move(value.x);
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
            motor.Jump();
    }

    public void OnAttack(InputAction.CallbackContext ctx)
    {
        if (ctx.started && attack) attack.RequestAttack();
    }

    private void OnEnable()
    {
        // ±âº» ¸Ê °­Á¦
        if (playerInput) playerInput.SwitchCurrentActionMap("Gameplay");
    }
}
