using System;
using UnityEngine;

namespace Game.Input
{
    public sealed class InputService
    {
        public Vector2 CurrentMove { get; private set; }

        public event Action<Vector2> Move;
        public event Action Jump;
        public event Action Attack;
        public event Action Parry;
        public event Action ExplorationInteract;
        public event Action DialogueAdvance;
        public event Action PauseRequested;

        internal void EmitMove(Vector2 value)
        {
            CurrentMove = value;
            Move?.Invoke(value);
        }

        internal void ClearMove(bool emitEvenIfAlreadyZero)
        {
            if (!emitEvenIfAlreadyZero && CurrentMove == Vector2.zero)
                return;

            EmitMove(Vector2.zero);
        }

        internal void EmitJump() => Jump?.Invoke();
        internal void EmitAttack() => Attack?.Invoke();
        internal void EmitParry() => Parry?.Invoke();
        internal void EmitExplorationInteract() => ExplorationInteract?.Invoke();
        internal void EmitDialogueAdvance() => DialogueAdvance?.Invoke();
        internal void EmitPauseRequested() => PauseRequested?.Invoke();
    }
}
