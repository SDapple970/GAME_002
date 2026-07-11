// Scripts/Core/GameStateMachine.cs
using System;
using UnityEngine;

namespace Game.Core
{
    public sealed class GameStateMachine : MonoBehaviour
    {
        public static GameStateMachine Instance { get; private set; }

        public GameState Current { get; private set; } = GameState.Exploration;
        public GameState Previous { get; private set; } = GameState.Exploration;
        public bool IsTransitioning => _isTransitioning;
        public event Action<GameState, GameState> OnStateChanged;

        private bool _isTransitioning;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetState(GameState next)
        {
            TrySetState(next, nameof(SetState));
        }

        public bool TrySetState(GameState next, string reason = null)
        {
            GameState current = Current;
            if (next == current)
                return false;

            if (_isTransitioning)
            {
                LogRejectedTransition(current, next, reason, "a state-change callback is already running");
                return false;
            }

            if (!IsTransitionAllowed(current, next))
            {
                LogRejectedTransition(current, next, reason, "the transition is not allowed by policy");
                return false;
            }

            _isTransitioning = true;
            try
            {
                Previous = current;
                Current = next;
                OnStateChanged?.Invoke(current, next);
                return true;
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        public static bool IsTransitionAllowed(GameState from, GameState to)
        {
            if (from == to)
                return true;

            if (to == GameState.Paused)
                return CanPause(from);

            switch (from)
            {
                case GameState.Boot:
                    return to == GameState.Title || to == GameState.Loading || to == GameState.Exploration;
                case GameState.Title:
                    return to == GameState.Loading || to == GameState.Exploration || to == GameState.UIOnly;
                case GameState.Loading:
                    return to == GameState.Title || to == GameState.Exploration || to == GameState.Cutscene;
                case GameState.Exploration:
                    return to == GameState.Title ||
                           to == GameState.Loading ||
                           to == GameState.Dialogue ||
                           to == GameState.CombatTransition ||
                           to == GameState.CombatPlanning ||
                           to == GameState.Cutscene ||
                           to == GameState.UIOnly ||
                           to == GameState.Reward;
                case GameState.Dialogue:
                    return to == GameState.Choice ||
                           to == GameState.Exploration ||
                           to == GameState.Cutscene ||
                           to == GameState.UIOnly ||
                           to == GameState.Loading;
                case GameState.Choice:
                    return to == GameState.Dialogue ||
                           to == GameState.Exploration ||
                           to == GameState.Cutscene ||
                           to == GameState.UIOnly ||
                           to == GameState.Loading;
                case GameState.CombatTransition:
                    return to == GameState.CombatPlanning || to == GameState.Reward || to == GameState.Loading;
                case GameState.CombatPlanning:
                    return to == GameState.CombatResolving || to == GameState.Reward || to == GameState.UIOnly;
                case GameState.CombatResolving:
                    return to == GameState.CombatPlanning || to == GameState.Reward || to == GameState.UIOnly;
                case GameState.Reward:
                    return to == GameState.Exploration || to == GameState.Loading || to == GameState.UIOnly;
                case GameState.Cutscene:
                    return to == GameState.Exploration ||
                           to == GameState.Dialogue ||
                           to == GameState.Choice ||
                           to == GameState.UIOnly ||
                           to == GameState.Reward ||
                           to == GameState.Loading;
                case GameState.UIOnly:
                    return to == GameState.Exploration ||
                           to == GameState.Dialogue ||
                           to == GameState.Choice ||
                           to == GameState.Reward ||
                           to == GameState.Cutscene ||
                           to == GameState.Loading;
                case GameState.Paused:
                    return to != GameState.Boot && to != GameState.Paused;
                default:
                    return false;
            }
        }

        private static bool CanPause(GameState state)
        {
            return state == GameState.Title ||
                   state == GameState.Exploration ||
                   state == GameState.Dialogue ||
                   state == GameState.Choice ||
                   state == GameState.CombatTransition ||
                   state == GameState.CombatPlanning ||
                   state == GameState.CombatResolving ||
                   state == GameState.Reward ||
                   state == GameState.Cutscene ||
                   state == GameState.UIOnly;
        }

        private void LogRejectedTransition(GameState previous, GameState requested, string reason, string rejection)
        {
            string context = string.IsNullOrWhiteSpace(reason) ? "unspecified caller" : reason;
            Debug.LogWarning(
                $"[GameStateMachine] Rejected transition {previous} -> {requested}; reason={context}; rejection={rejection}.",
                this);
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
