using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Reward;
using Game.UI;

namespace Game.Core
{
    public sealed class RuntimeBootstrapper : MonoBehaviour
    {
        private enum InitialStateMode
        {
            InferFromSceneName,
            Explicit
        }

        [SerializeField] private InitialStateMode initialStateMode = InitialStateMode.InferFromSceneName;
        [SerializeField] private GameState initialState = GameState.Exploration;
        [SerializeField] private bool applyInitialStateOnStart = true;
        [SerializeField] private bool createMissingCoreServices = true;
        [SerializeField] private bool logWarnings = true;

        private void Awake()
        {
            BootstrapCoreServices(createMissingCoreServices, logWarnings);
        }

        private void Start()
        {
            if (applyInitialStateOnStart && GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(ResolveInitialState());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapLoadedScene()
        {
            RuntimeBootstrapper existing = FindFirstObjectByType<RuntimeBootstrapper>();
            if (existing != null)
            {
                existing.BootstrapCoreServices(existing.createMissingCoreServices, existing.logWarnings);
                if (existing.applyInitialStateOnStart && GameStateMachine.Instance != null)
                    GameStateMachine.Instance.SetState(existing.ResolveInitialState());
                return;
            }

            GameObject go = new GameObject("RuntimeBootstrapper");
            RuntimeBootstrapper bootstrapper = go.AddComponent<RuntimeBootstrapper>();
            bootstrapper.BootstrapCoreServices(true, true);

            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(ResolveInitialStateForScene(SceneManager.GetActiveScene().name));
        }

        private void BootstrapCoreServices(bool createMissing, bool warn)
        {
            FindOrCreate<GameStateMachine>("GameStateMachine", createMissing, warn);
            FindOrCreate<global::GameInputInstaller>("GameInputInstaller", createMissing, warn);
            FindOrCreate<GameFlowController>("GameFlowController", createMissing, warn);
            FindOrCreate<SceneFlowController>("SceneFlowController", createMissing, warn);
            FindOrCreate<SaveLoadService>("SaveLoadService", createMissing, warn);
            FindOrCreate<RewardService>("RewardService", createMissing, warn);
            FindOrCreate<GameUIRootController>("GameUIRootController", createMissing, warn);
            FindOrCreate<UIScreenRouter>("UIScreenRouter", createMissing, warn);
        }

        private GameState ResolveInitialState()
        {
            return initialStateMode == InitialStateMode.Explicit
                ? initialState
                : ResolveInitialStateForScene(SceneManager.GetActiveScene().name);
        }

        private static GameState ResolveInitialStateForScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return GameState.Exploration;

            string normalized = sceneName.ToLowerInvariant();
            if (normalized.Contains("title"))
                return GameState.Title;

            if (normalized.Contains("loading"))
                return GameState.Loading;

            if (normalized.Contains("cutscene"))
                return GameState.Cutscene;

            return GameState.Exploration;
        }

        private static T FindOrCreate<T>(string objectName, bool createMissing, bool warn) where T : Component
        {
            T[] existing = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existing.Length > 0)
            {
                if (warn && existing.Length > 1)
                    Debug.LogWarning($"[RuntimeBootstrapper] Multiple {typeof(T).Name} instances found. Existing singleton code should keep one active.");

                return existing[0];
            }

            if (!createMissing)
            {
                if (warn)
                    Debug.LogWarning($"[RuntimeBootstrapper] Missing {typeof(T).Name}. Assign or add {objectName} in the scene.");

                return null;
            }

            GameObject go = new GameObject(objectName);
            return go.AddComponent<T>();
        }
    }
}
