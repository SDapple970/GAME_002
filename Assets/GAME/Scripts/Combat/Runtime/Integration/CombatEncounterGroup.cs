using System.Collections.Generic;
using Game.Combat.Adapters;
using UnityEngine;

namespace Game.Combat.Integration
{
    public sealed class CombatEncounterGroup : MonoBehaviour
    {
        [SerializeField] private bool autoCollectChildren = true;
        [SerializeField] private List<GameObject> enemies = new();

        private readonly HashSet<int> _warnedInvalidAutoChildren = new();

        public List<GameObject> GetActiveEnemies()
        {
            List<GameObject> result = new List<GameObject>();
            HashSet<GameObject> seen = new HashSet<GameObject>();

            if (!autoCollectChildren)
            {
                AddActiveUnique(enemies, result, seen);
                return result;
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                GameObject candidate = child != null ? child.gameObject : null;
                if (candidate == null || !candidate.activeInHierarchy || !seen.Add(candidate))
                    continue;

                HpAccessor accessor = HpAccessor.TryCreate(candidate);
                if (accessor != null && accessor.IsValid)
                {
                    result.Add(candidate);
                    continue;
                }

                int instanceId = candidate.GetInstanceID();
                if (_warnedInvalidAutoChildren.Add(instanceId))
                {
                    Debug.LogWarning(
                        $"[CombatEncounterGroup] Auto-collected child '{candidate.name}' is not a field combatant and was excluded. " +
                        "Add a valid HP source or keep helper objects outside the combatant roster.",
                        this);
                }
            }

            return result;
        }

        private static void AddActiveUnique(
            List<GameObject> source,
            List<GameObject> destination,
            HashSet<GameObject> seen)
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                GameObject candidate = source[i];
                if (candidate != null && candidate.activeInHierarchy && seen.Add(candidate))
                    destination.Add(candidate);
            }
        }
    }
}
