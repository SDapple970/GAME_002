using UnityEngine;

namespace Game.Demo
{
    public sealed class ObjectiveEnemyMarker : MonoBehaviour
    {
        [SerializeField] private DungeonObjectiveManager objectiveManager;
        [SerializeField] private bool countOnDisable = true;
        [SerializeField] private bool countOnlyOnce = true;

        private bool _counted;
        private bool _applicationQuitting;

        private void Awake()
        {
            if (objectiveManager == null)
                objectiveManager = FindFirstObjectByType<DungeonObjectiveManager>();
        }

        private void OnDisable()
        {
            if (!countOnDisable || !Application.isPlaying || _applicationQuitting)
                return;

            if (gameObject.activeSelf)
                return;

            MarkKilled();
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }

        public void MarkKilled()
        {
            if (countOnlyOnce && _counted)
                return;

            if (objectiveManager == null)
                objectiveManager = FindFirstObjectByType<DungeonObjectiveManager>();

            if (objectiveManager == null)
            {
                Debug.LogWarning("[ObjectiveEnemyMarker] DungeonObjectiveManager is missing.", this);
                return;
            }

            _counted = true;
            objectiveManager.RegisterMonsterKilled();
        }
    }
}
