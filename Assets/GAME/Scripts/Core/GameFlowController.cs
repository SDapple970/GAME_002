using Game.Combat.Model;
using UnityEngine;

namespace Game.Core
{
    public sealed class GameFlowController : MonoBehaviour
    {
        public static GameFlowController Instance { get; private set; }

        private GameState _stateBeforePause = GameState.Exploration;
        private bool _hasPausedState;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void EnterExploration()
        {
            RequestState(GameState.Exploration, nameof(EnterExploration));
        }

        public void EnterReward()
        {
            RequestState(GameState.Reward, nameof(EnterReward));
        }

        public void BeginLoading() => RequestState(GameState.Loading, nameof(BeginLoading));
        public void BeginDialogue() => RequestState(GameState.Dialogue, nameof(BeginDialogue));
        public void BeginChoice() => RequestState(GameState.Choice, nameof(BeginChoice));
        public void BeginCombatTransition() => RequestState(GameState.CombatTransition, nameof(BeginCombatTransition));
        public void EnterCombatPlanning() => RequestState(GameState.CombatPlanning, nameof(EnterCombatPlanning));
        public void EnterCombatResolving() => RequestState(GameState.CombatResolving, nameof(EnterCombatResolving));
        public void EnterUIOnly() => RequestState(GameState.UIOnly, nameof(EnterUIOnly));
        public void EnterCutscene() => RequestState(GameState.Cutscene, nameof(EnterCutscene));

        public void Pause()
        {
            GameStateMachine stateMachine = GameStateMachine.Instance;
            if (stateMachine == null || stateMachine.Is(GameState.Paused))
                return;

            GameState previous = stateMachine.Current;
            if (stateMachine.TrySetState(GameState.Paused, nameof(Pause)))
            {
                _stateBeforePause = previous;
                _hasPausedState = true;
            }
        }

        public void ResumePreviousState()
        {
            GameStateMachine stateMachine = GameStateMachine.Instance;
            if (!_hasPausedState || stateMachine == null || !stateMachine.Is(GameState.Paused))
                return;

            if (stateMachine.TrySetState(_stateBeforePause, nameof(ResumePreviousState)))
                _hasPausedState = false;
        }

        public bool RequestState(GameState state, string reason)
        {
            GameStateMachine stateMachine = GameStateMachine.Instance;
            return stateMachine != null && stateMachine.TrySetState(state, $"GameFlowController.{reason}");
        }

        public void HandleCombatResult(CombatResult result)
        {
            if (result != null)
                EnterReward();
        }

        public void HandleRewardClosed()
        {
            EnterExploration();
        }
    }
}
