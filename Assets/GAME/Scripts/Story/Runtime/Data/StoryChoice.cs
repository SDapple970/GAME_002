// Assets/GAME/Scripts/Story/Runtime/Data/StoryChoice.cs
using System.Collections.Generic;
using UnityEngine;

namespace Game.Story.Data
{
    [System.Serializable]
    public sealed class StoryChoice
    {
        [SerializeField] private string text;
        [SerializeField] private string nextNodeId;
        [SerializeField] private List<StoryCondition> conditions = new();
        [SerializeField] private List<StoryEffect> effects = new();
        [SerializeField] private bool hideIfConditionNotMet = true;
        [SerializeField] private string disabledReason;

        public string Text => text;
        public string NextNodeId => nextNodeId;
        public IReadOnlyList<StoryCondition> Conditions => conditions;
        public IReadOnlyList<StoryEffect> Effects => effects;
        public bool HideIfConditionNotMet => hideIfConditionNotMet;
        public string DisabledReason => disabledReason;

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
            ApplyEffects(default);
        }

        internal void ApplyEffects(StoryEffectContext context)
        {
            if (effects == null) return;

            for (int i = 0; i < effects.Count; i++)
            {
                StoryEffect effect = effects[i];
                effect?.Apply(new StoryEffectContext(
                    context.Source,
                    string.IsNullOrWhiteSpace(context.EventId) ? null : $"{context.EventId}:effect:{i}"));
            }
        }
    }
}
