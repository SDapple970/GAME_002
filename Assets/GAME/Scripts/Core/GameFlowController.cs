using Game.Combat.Model;
using UnityEngine;

namespace Game.Core
{
    public sealed class GameFlowController : MonoBehaviour
    {
        public static GameFlowController Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void EnterExploration()
        {
            GameStateMachine.Instance?.SetState(GameState.Exploration);
        }

        public void EnterReward()
        {
            GameStateMachine.Instance?.SetState(GameState.Reward);
        }

        public void HandleCombatResult(CombatResult result)
        {
            if (result != null)
                EnterReward();
            else
                EnterExploration();
        }

        public void HandleRewardClosed()
        {
            EnterExploration();
        }
    }
}
