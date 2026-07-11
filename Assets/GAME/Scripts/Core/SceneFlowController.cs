using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    public sealed class SceneFlowController : MonoBehaviour
    {
        public static SceneFlowController Instance { get; private set; }

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

        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("[SceneFlowController] Scene name is empty.", this);
                return;
            }

            StartCoroutine(Co_LoadScene(sceneName));
        }

        private IEnumerator Co_LoadScene(string sceneName)
        {
            if (GameFlowController.Instance != null)
                GameFlowController.Instance.BeginLoading();
            else
                GameStateMachine.Instance?.TrySetState(GameState.Loading, nameof(SceneFlowController));

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError($"[SceneFlowController] Failed to load scene: {sceneName}", this);
                if (GameFlowController.Instance != null)
                    GameFlowController.Instance.EnterExploration();
                else
                    GameStateMachine.Instance?.TrySetState(GameState.Exploration, nameof(SceneFlowController));
                yield break;
            }

            while (!operation.isDone)
                yield return null;

            if (GameFlowController.Instance != null)
                GameFlowController.Instance.EnterExploration();
            else
                GameStateMachine.Instance?.TrySetState(GameState.Exploration, nameof(SceneFlowController));
        }
    }
}
