using System.Collections.Generic;
using Game.Search.Data;
using Game.Search.UI;
using UnityEngine;

namespace Game.Search
{
    public sealed class SearchResultRunner : MonoBehaviour
    {
        [SerializeField] private SearchResultHUD resultHUD;

        public SearchOutcome Roll(SearchableObjectDefinitionSO definition)
        {
            if (definition == null) return null;

            IReadOnlyList<SearchOutcome> outcomes = definition.Outcomes;
            if (outcomes == null || outcomes.Count == 0)
            {
                Debug.LogWarning($"[SearchResultRunner] No outcomes configured for searchable object='{definition.ObjectId}'.", definition);
                return null;
            }

            foreach (SearchOutcome outcome in outcomes)
            {
                if (outcome != null && outcome.ForceWhenConditionsMet && outcome.AreConditionsMet())
                {
                    return outcome;
                }
            }

            List<SearchOutcome> candidates = new();
            int totalWeight = 0;

            foreach (SearchOutcome outcome in outcomes)
            {
                if (outcome == null || outcome.ForceWhenConditionsMet || outcome.Weight <= 0 || !outcome.AreConditionsMet())
                {
                    continue;
                }

                candidates.Add(outcome);
                totalWeight += outcome.Weight;
            }

            if (candidates.Count == 0 || totalWeight <= 0)
            {
                Debug.LogWarning($"[SearchResultRunner] No valid outcomes for searchable object='{definition.ObjectId}'.", definition);
                return null;
            }

            int roll = Random.Range(0, totalWeight);
            int cursor = 0;

            foreach (SearchOutcome outcome in candidates)
            {
                cursor += outcome.Weight;
                if (roll < cursor)
                {
                    return outcome;
                }
            }

            return candidates[candidates.Count - 1];
        }

        public bool Execute(SearchableObjectDefinitionSO definition)
        {
            return Execute(definition, null);
        }

        public bool Execute(SearchableObjectDefinitionSO definition, SearchObjectAnchor anchor)
        {
            if (definition == null) return false;

            SearchOutcome selected = Roll(definition);
            if (selected == null) return false;

            selected.ApplyEffects();
            ResolveHUD();
            resultHUD?.ShowMessage(selected.ResultMessage, anchor, definition.ResultMessageSeconds);
            return true;
        }

        private void ResolveHUD()
        {
            if (resultHUD != null) return;

#if UNITY_2023_1_OR_NEWER
            resultHUD = FindFirstObjectByType<SearchResultHUD>();
#else
            resultHUD = FindObjectOfType<SearchResultHUD>();
#endif
        }
    }
}
