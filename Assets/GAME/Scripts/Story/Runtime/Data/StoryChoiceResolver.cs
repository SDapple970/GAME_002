using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Story.Data
{
    public static class StoryChoiceResolver
    {
        public const int MaxProductionChoices = 3;

        public static List<ResolvedStoryChoice> Resolve(
            IReadOnlyList<StoryChoice> choices,
            UnityEngine.Object warningContext = null)
        {
            List<ResolvedStoryChoice> resolved = new();
            if (choices == null)
                return resolved;

            HashSet<string> authoredIds = new(StringComparer.Ordinal);
            bool truncated = false;
            for (int authoredIndex = 0; authoredIndex < choices.Count; authoredIndex++)
            {
                StoryChoice choice = choices[authoredIndex];
                if (choice == null)
                    continue;

                bool conditionsMet = choice.AreConditionsMet();
                if (!conditionsMet && choice.HideIfConditionNotMet)
                    continue;

                string effectiveChoiceId = choice.ChoiceId;
                if (!string.IsNullOrEmpty(effectiveChoiceId) && !authoredIds.Add(effectiveChoiceId))
                {
                    Debug.LogWarning(
                        $"[StoryChoiceResolver] Duplicate displayable choiceId='{effectiveChoiceId}' at authored index {authoredIndex}. This choice will use its authored-index compatibility identity instead.",
                        warningContext);
                    effectiveChoiceId = null;
                }

                if (resolved.Count >= MaxProductionChoices)
                {
                    truncated = true;
                    continue;
                }

                resolved.Add(new ResolvedStoryChoice(
                    choice,
                    authoredIndex,
                    resolved.Count,
                    conditionsMet,
                    effectiveChoiceId));
            }

            if (truncated)
            {
                Debug.LogWarning(
                    $"[StoryChoiceResolver] More than {MaxProductionChoices} displayable choices were authored. Only the first {MaxProductionChoices} are presented in authored order.",
                    warningContext);
            }

            return resolved;
        }
    }
}
