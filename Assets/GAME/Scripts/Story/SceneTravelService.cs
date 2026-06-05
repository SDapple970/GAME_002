using System.Collections;
using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Story
{
    public sealed class SceneTravelService : MonoBehaviour
    {
        public static SceneTravelService Instance { get; private set; }

        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private string playerTag = "Player";

        private static string _pendingSpawnPointId;
        private static bool _hasPendingSpawnPoint;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        public static void TravelTo(string sceneName, string spawnPointId)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("[SceneTravelService] Target scene name is empty.");
                return;
            }

            SceneTravelService service = Instance;
            if (service == null)
                service = Object.FindFirstObjectByType<SceneTravelService>();

            if (service == null)
            {
                GameObject serviceObject = new GameObject("SceneTravelService");
                service = serviceObject.AddComponent<SceneTravelService>();
            }

            service.Travel(sceneName, spawnPointId);
        }

        public void Travel(string sceneName, string spawnPointId)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("[SceneTravelService] Target scene name is empty.", this);
                return;
            }

            _pendingSpawnPointId = spawnPointId;
            _hasPendingSpawnPoint = !string.IsNullOrEmpty(spawnPointId);

            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.Cutscene);

            StartCoroutine(Co_LoadScene(sceneName));
        }

        private IEnumerator Co_LoadScene(string sceneName)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError($"[SceneTravelService] Failed to load scene: {sceneName}", this);
                RestoreExplorationState();
                yield break;
            }

            while (!operation.isDone)
                yield return null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_hasPendingSpawnPoint)
            {
                MovePlayerToSpawnPoint(_pendingSpawnPointId);
                _pendingSpawnPointId = null;
                _hasPendingSpawnPoint = false;
            }

            RestoreExplorationState();
        }

        private void MovePlayerToSpawnPoint(string spawnPointId)
        {
            SceneSpawnPoint spawnPoint = FindSpawnPoint(spawnPointId);
            if (spawnPoint == null)
            {
                Debug.LogWarning($"[SceneTravelService] Spawn point not found: {spawnPointId}", this);
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player == null)
            {
                Debug.LogWarning("[SceneTravelService] Player with tag 'Player' was not found.", this);
                return;
            }

            player.transform.SetPositionAndRotation(spawnPoint.transform.position, spawnPoint.transform.rotation);

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private static SceneSpawnPoint FindSpawnPoint(string spawnPointId)
        {
            SceneSpawnPoint[] spawnPoints = Object.FindObjectsByType<SceneSpawnPoint>(FindObjectsSortMode.None);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                SceneSpawnPoint spawnPoint = spawnPoints[i];
                if (spawnPoint != null && spawnPoint.SpawnPointId == spawnPointId)
                    return spawnPoint;
            }

            return null;
        }

        private static void RestoreExplorationState()
        {
            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.Exploration);
        }
    }
}
