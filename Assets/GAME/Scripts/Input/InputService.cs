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
        public event Action Interact;
        public event Action Pause;

        internal void EmitMove(Vector2 value)
        {
            CurrentMove = value;
            Move?.Invoke(value);
        }

        internal void EmitJump() => Jump?.Invoke();
        internal void EmitAttack() => Attack?.Invoke();
        internal void EmitParry() => Parry?.Invoke();
        internal void EmitInteract() => Interact?.Invoke();
        internal void EmitPause() => Pause?.Invoke();
    }
}
