using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// World-interaction boundary for a Dungeon exit. Placement and destination are
    /// authored separately; this component never substitutes a fallback scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonExitGate : MonoBehaviour
    {
        [SerializeField] private DungeonCompletionFlow dungeonCompletionFlow;

        private bool _missingCompletionFlowWarned;

        public bool IsUnlocked => dungeonCompletionFlow != null && dungeonCompletionFlow.IsCompletionReady;

        /// <summary>
        /// Returns false while locked, or when destination authoring is pending. Scene
        /// travel remains owned by DungeonCompletionFlow and SceneFlowController.
        /// </summary>
        public bool TryUseExit()
        {
            ResolveCompletionFlow();
            if (dungeonCompletionFlow == null)
            {
                if (!_missingCompletionFlowWarned)
                {
                    _missingCompletionFlowWarned = true;
                    Debug.LogWarning("[DungeonExitGate] DungeonCompletionFlow is missing. Exit remains unavailable.", this);
                }

                return false;
            }

            return IsUnlocked && dungeonCompletionFlow.TryTravelToAuthoredDestination();
        }

        private void Awake()
        {
            ResolveCompletionFlow();
        }

        private void ResolveCompletionFlow()
        {
            if (dungeonCompletionFlow == null)
                dungeonCompletionFlow = FindFirstObjectByType<DungeonCompletionFlow>();
        }
    }
}
