using System;
using System.Collections.Generic;
using Game.Story.Data;
using UnityEngine;

namespace Game.Search.Data
{
    [Serializable]
    public sealed class SearchOutcome
    {
        [SerializeField] private int weight = 1;
        [SerializeField] private string resultMessage;
        [SerializeField] private List<StoryCondition> conditions = new();
        [SerializeField] private bool forceWhenConditionsMet = false;
        [SerializeField] private List<SearchEffect> effects = new();

        public int Weight => weight;
        public string ResultMessage => resultMessage;
        public bool ForceWhenConditionsMet => forceWhenConditionsMet;
        public IReadOnlyList<SearchEffect> Effects => effects;

        public bool AreConditionsMet()
        {
            if (conditions == null || conditions.Count == 0) return true;

            foreach (StoryCondition condition in conditions)
            {
                if (condition != null && !condition.IsMet())
                {
                    return false;
                }
            }

            return true;
        }

        public void ApplyEffects()
        {
            if (effects == null) return;

            foreach (SearchEffect effect in effects)
            {
                effect?.Apply();
            }
        }
    }
}
