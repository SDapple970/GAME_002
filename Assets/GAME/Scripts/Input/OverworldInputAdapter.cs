using UnityEngine;
using UnityEngine.InputSystem;
using Game.Player; // IPlayerInput 있는 네임스페이스 (프로젝트에 맞게 유지)

public sealed class OverworldInputAdapter : MonoBehaviour
{
    [Header("Input Actions (InputActionReference)")]
    [SerializeField] private InputActionReference move;
    [SerializeField] private InputActionReference jump;
    [SerializeField] private InputActionReference attack; // 필요 없으면 빼도 됨

    public float MoveX { get; private set; }
    public bool JumpPressed { get; private set; }

    // (선택) 공격도 어딘가에서 쓰면 유지
    public bool AttackPressed { get; private set; }

    private void OnEnable()
    {
        if (move != null)
        {
            move.action.Enable();
            move.action.performed += OnMove;
            move.action.canceled += OnMove;
        }

        if (jump != null)
        {
            jump.action.Enable();
            jump.action.performed += OnJump;
        }

        if (attack != null)
        {
            attack.action.Enable();
            attack.action.performed += OnAttack;
        }
    }

    private void OnDisable()
    {
        if (move != null)
        {
            move.action.performed -= OnMove;
            move.action.canceled -= OnMove;
            move.action.Disable();
        }

        if (jump != null)
        {
            jump.action.performed -= OnJump;
            jump.action.Disable();
        }

        if (attack != null)
        {
            attack.action.performed -= OnAttack;
            attack.action.Disable();
        }
    }

    private void OnMove(InputAction.CallbackContext ctx)
    {
        // 보통 2D 플랫폼이면 x만 필요
        var v = ctx.ReadValue<Vector2>();
        MoveX = v.x;
    }

    private void OnJump(InputAction.CallbackContext ctx)
    {
        // PlayerController2D가 ConsumeJumpPressed()로 소모함
        JumpPressed = true;
    }

    private void OnAttack(InputAction.CallbackContext ctx)
    {
        AttackPressed = true;
    }

    public void ConsumeJumpPressed() => JumpPressed = false;

    // (선택) 공격 소모가 필요하면
    public void ConsumeAttackPressed() => AttackPressed = false;
}
