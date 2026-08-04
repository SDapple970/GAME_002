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

                string choiceId = choice.ChoiceId;
                if (!string.IsNullOrEmpty(choiceId) && !authoredIds.Add(choiceId))
                {
                    Debug.LogWarning(
                        $"[StoryChoiceResolver] Duplicate choiceId='{choiceId}' at authored index {authoredIndex}. Choice IDs must be unique within one node.",
                        warningContext);
                }

                bool conditionsMet = choice.AreConditionsMet();
                if (!conditionsMet && choice.HideIfConditionNotMet)
                    continue;

                if (resolved.Count >= MaxProductionChoices)
                {
                    truncated = true;
                    continue;
                }

                resolved.Add(new ResolvedStoryChoice(
                    choice,
                    authoredIndex,
                    resolved.Count,
                    conditionsMet));
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
