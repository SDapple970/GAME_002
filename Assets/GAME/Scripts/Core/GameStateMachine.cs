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

        public bool IsCombatState()
        {
            return IsCombatState(Current);
        }

        public bool AllowsExplorationInput()
        {
            return AllowsExplorationInput(Current);
        }

        public bool AllowsUIInput()
        {
            return AllowsUIInput(Current);
        }

        public static bool IsCombatState(GameState state)
        {
            return state == GameState.CombatTransition ||
                   state == GameState.CombatPlanning ||
                   state == GameState.CombatResolving;
        }

        public static bool AllowsExplorationInput(GameState state)
        {
            return state == GameState.Exploration;
        }

        public static bool AllowsUIInput(GameState state)
        {
            return state == GameState.Title ||
                   state == GameState.Dialogue ||
                   state == GameState.Choice ||
                   state == GameState.Reward ||
                   state == GameState.UIOnly ||
                   state == GameState.Paused;
        }
    }
}
