// Scripts/Player/PlayerInputUnity.cs
using UnityEngine;

namespace Game.Player
{
    // "진짜 최소" 버전: Unity 구 Input(키보드) 기반
    // 나중에 Input System으로 바꾸려면 이 클래스만 교체하면 됨.
    public sealed class PlayerInputUnity : MonoBehaviour, IPlayerInput
    {
        public float MoveX => Input.GetAxisRaw("Horizontal");

        public bool JumpPressed { get; private set; }
        public bool JumpHeld => Input.GetButton("Jump");

        private void Update()
        {
            if (Input.GetButtonDown("Jump"))
                JumpPressed = true;
        }

        public void ConsumeJumpPressed()
        {
            JumpPressed = false;
        }
    }
}
