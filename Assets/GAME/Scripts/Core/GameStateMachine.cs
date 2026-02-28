// Scripts/Core/GameStateMachine.cs
using System;
using UnityEngine;

namespace Game.Core
{
    public sealed class GameStateMachine : MonoBehaviour
    {
        public static GameStateMachine Instance { get; private set; }

        public GameState Current { get; private set; } = GameState.Exploration;
        public event Action<GameState, GameState> OnStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetState(GameState next)
        {
            if (next == Current) return;

            var prev = Current;
            Current = next;
            OnStateChanged?.Invoke(prev, next);
        }

        public bool Is(GameState s) => Current == s;
    }
}
