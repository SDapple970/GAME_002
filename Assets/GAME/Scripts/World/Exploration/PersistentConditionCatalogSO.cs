using System.Collections.Generic;
using UnityEngine;

namespace Game.World.Exploration
{
    [CreateAssetMenu(menuName = "GAME/Exploration/Persistent Condition Catalog", fileName = "PersistentConditionCatalog")]
    public sealed class PersistentConditionCatalogSO : ScriptableObject
    {
        [SerializeField] private List<PersistentConditionDefinitionSO> definitions = new();

        public bool TryGet(string conditionId, out PersistentConditionDefinitionSO definition)
        {
            string id = PersistentConditionIdentity.Normalize(conditionId);
            definition = null;
            if (id == null || definitions == null) return false;
            foreach (PersistentConditionDefinitionSO candidate in definitions)
            {
                if (candidate == null || candidate.ConditionId != id) continue;
                if (definition != null)
                {
                    Debug.LogError($"[PersistentConditionCatalog] Duplicate condition ID '{id}'.", this);
                    definition = null;
                    return false;
                }
                definition = candidate;
            }
            return definition != null;
        }
    }
}
