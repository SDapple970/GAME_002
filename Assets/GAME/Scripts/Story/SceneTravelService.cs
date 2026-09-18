using System.Linq;
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
        private static string _pendingPlayerTag;
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
            _pendingPlayerTag = playerTag;
            _hasPendingSpawnPoint = !string.IsNullOrEmpty(spawnPointId);

            SceneFlowController sceneFlow = SceneFlowController.Instance;
            if (sceneFlow == null)
                sceneFlow = Object.FindFirstObjectByType<SceneFlowController>();
            if (sceneFlow == null)
            {
                ClearPendingSpawn();
                Debug.LogError(
                    $"[SceneTravelService] SceneFlowController is required to travel to '{sceneName}'.",
                    this);
                return;
            }

            sceneFlow.LoadScene(sceneName, HandleSceneLoadCompleted);
        }

        private static void HandleSceneLoadCompleted(bool succeeded)
        {
            if (succeeded && _hasPendingSpawnPoint)
                MovePlayerToSpawnPoint(_pendingSpawnPointId, _pendingPlayerTag);

            ClearPendingSpawn();
        }

        private static void MovePlayerToSpawnPoint(string spawnPointId, string targetPlayerTag)
        {
            SceneSpawnPoint[] matches = FindSpawnPoints(spawnPointId);
            if (matches.Length == 0)
            {
                Debug.LogWarning(
                    $"[SceneTravelService] Spawn point '{spawnPointId}' was not found in " +
                    $"scene '{SceneManager.GetActiveScene().name}'. Keeping the authored Player position.");
                return;
            }

            if (matches.Length > 1)
            {
                Debug.LogWarning(
                    $"[SceneTravelService] Spawn point '{spawnPointId}' is ambiguous in " +
                    $"scene '{SceneManager.GetActiveScene().name}' ({matches.Length} matches). " +
                    "Keeping the authored Player position.");
                return;
            }

            SceneSpawnPoint spawnPoint = matches[0];
            string resolvedPlayerTag = string.IsNullOrWhiteSpace(targetPlayerTag) ? "Player" : targetPlayerTag;
            GameObject player = GameObject.FindGameObjectWithTag(resolvedPlayerTag);
            if (player == null)
            {
                Debug.LogWarning(
                    $"[SceneTravelService] Player with tag '{resolvedPlayerTag}' was not found in " +
                    $"scene '{SceneManager.GetActiveScene().name}'.");
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

        private static SceneSpawnPoint[] FindSpawnPoints(string spawnPointId)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            return Object.FindObjectsByType<SceneSpawnPoint>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(spawnPoint =>
                    spawnPoint != null &&
                    spawnPoint.gameObject.scene == activeScene &&
                    spawnPoint.SpawnPointId == spawnPointId)
                .ToArray();
        }

        private static void ClearPendingSpawn()
        {
            _pendingSpawnPointId = null;
            _pendingPlayerTag = null;
            _hasPendingSpawnPoint = false;
        }
    }
}
