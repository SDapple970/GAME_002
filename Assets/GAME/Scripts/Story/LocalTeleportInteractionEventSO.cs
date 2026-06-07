using Game.Interaction;
using UnityEngine;

namespace Game.Story
{
    [CreateAssetMenu(menuName = "GAME/Story/Local Teleport Interaction Event", fileName = "LocalTeleportInteractionEvent")]
    public sealed class LocalTeleportInteractionEventSO : InteractionEventSO
    {
        [SerializeField] private string targetSpawnPointId;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool resetRigidbodyVelocity = true;

        public override void Execute(InteractionContext context)
        {
            SceneSpawnPoint spawnPoint = FindSpawnPoint(targetSpawnPointId);
            if (spawnPoint == null)
            {
                Debug.LogWarning($"[LocalTeleportInteractionEventSO] Spawn point not found: {targetSpawnPointId}", context.Target);
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player == null)
            {
                Debug.LogWarning($"[LocalTeleportInteractionEventSO] Player not found. tag={playerTag}", context.Target);
                return;
            }

            player.transform.SetPositionAndRotation(spawnPoint.transform.position, spawnPoint.transform.rotation);

            if (!resetRigidbodyVelocity)
                return;

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private static SceneSpawnPoint FindSpawnPoint(string spawnPointId)
        {
            if (string.IsNullOrEmpty(spawnPointId))
                return null;

            SceneSpawnPoint[] spawnPoints = Object.FindObjectsByType<SceneSpawnPoint>(FindObjectsSortMode.None);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                SceneSpawnPoint spawnPoint = spawnPoints[i];
                if (spawnPoint != null && spawnPoint.SpawnPointId == spawnPointId)
                    return spawnPoint;
            }

            return null;
        }
    }
}
