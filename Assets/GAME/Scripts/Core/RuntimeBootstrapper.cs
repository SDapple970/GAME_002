using UnityEngine;
using Game.Reward;

namespace Game.Core
{
    public sealed class RuntimeBootstrapper : MonoBehaviour
    {
        [SerializeField] private GameState initialState = GameState.Exploration;
        [SerializeField] private bool applyInitialStateOnStart = true;

        private void Awake()
        {
            Ensure<GameStateMachine>("GameStateMachine");
            Ensure<GameFlowController>("GameFlowController");
            Ensure<SceneFlowController>("SceneFlowController");
            Ensure<SaveLoadService>("SaveLoadService");
            Ensure<RewardService>("RewardService");
        }

        private void Start()
        {
            if (applyInitialStateOnStart && GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(initialState);
        }

        private static T Ensure<T>(string objectName) where T : Component
        {
            T existing = FindFirstObjectByType<T>();
            if (existing != null)
                return existing;

            GameObject go = new GameObject(objectName);
            return go.AddComponent<T>();
        }
    }
}
