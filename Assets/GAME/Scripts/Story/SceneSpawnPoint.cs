using UnityEngine;

namespace Game.Story
{
    public sealed class SceneSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string spawnPointId;

        public string SpawnPointId => spawnPointId;
    }
}
