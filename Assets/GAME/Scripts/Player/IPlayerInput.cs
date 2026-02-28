// Scripts/Player/IPlayerInput.cs
namespace Game.Player
{
    public interface IPlayerInput
    {
        float MoveX { get; }
        bool JumpPressed { get; }
        bool JumpHeld { get; }
        void ConsumeJumpPressed();
    }
}
