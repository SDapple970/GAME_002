using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    public sealed class GameUIRootController : MonoBehaviour
    {
        [SerializeField] private GameObject titleRoot;
        [SerializeField] private GameObject fieldRoot;
        [SerializeField] private GameObject dialogueRoot;
        [SerializeField] private GameObject choiceRoot;
        [SerializeField] private GameObject combatRoot;
        [SerializeField] private GameObject rewardRoot;
        [SerializeField] private GameObject pauseRoot;
        [SerializeField] private GameObject loadingRoot;

        private readonly HashSet<string> _warnings = new();

        internal bool TitleVisible => IsVisible(titleRoot);
        internal bool FieldVisible => IsVisible(fieldRoot);
        internal bool DialogueVisible => IsVisible(dialogueRoot);
        internal bool ChoiceVisible => IsVisible(choiceRoot);
        internal bool CombatVisible => IsVisible(combatRoot);
        internal bool RewardVisible => IsVisible(rewardRoot);
        internal bool PauseVisible => IsVisible(pauseRoot);
        internal bool LoadingVisible => IsVisible(loadingRoot);

        private void Awake()
        {
            AutoBindMissingReferences();
        }

        public void SetTitleVisible(bool visible) => SetVisible(titleRoot, visible, nameof(titleRoot));
        public void SetFieldVisible(bool visible) => SetVisible(fieldRoot, visible, nameof(fieldRoot));
        public void SetDialogueVisible(bool visible) => SetVisible(dialogueRoot, visible, nameof(dialogueRoot));
        public void SetChoiceVisible(bool visible) => SetVisible(choiceRoot, visible, nameof(choiceRoot));
        public void SetCombatVisible(bool visible) => SetVisible(combatRoot, visible, nameof(combatRoot));
        public void SetRewardVisible(bool visible) => SetVisible(rewardRoot, visible, nameof(rewardRoot));
        public void SetPauseVisible(bool visible) => SetVisible(pauseRoot, visible, nameof(pauseRoot));
        public void SetLoadingVisible(bool visible) => SetVisible(loadingRoot, visible, nameof(loadingRoot));

        public void AutoBindMissingReferences()
        {
            titleRoot ??= FindUniqueByName("TitleRoot", "TitleGroup");
            fieldRoot ??= FindUniqueObjectRoot<OverworldHUDRoot>();
            dialogueRoot ??= FindUniqueObjectRoot<Game.Story.UI.DialoguePanel>();
            dialogueRoot ??= FindUniqueObjectRoot<Game.Story.UI.DialogueUIPanel>();
            dialogueRoot ??= FindUniqueObjectRoot<Game.Story.UI.StoryDialogueHUD>();
            choiceRoot ??= FindUniqueObjectRoot<Game.Story.UI.ChoiceUIPanel>();
            choiceRoot ??= FindUniqueObjectRoot<Game.Story.UI.TimedChoicePanel>();
            combatRoot ??= FindUniqueObjectRoot<Game.Combat.UI.CombatUIRootController>();
            combatRoot ??= FindUniqueObjectRoot<Game.Combat.UI.CombatPlanningHUD>();
            rewardRoot ??= FindUniqueObjectRoot<RewardUIPanel>();
            pauseRoot ??= FindUniqueByName("PauseMenu");
            loadingRoot ??= FindUniqueByName("LoadingPanel");
        }

        private void SetVisible(GameObject root, bool visible, string rootName)
        {
            if (root == null)
            {
                if (visible)
                    WarnOnce(rootName, $"[GameUIRootController] {rootName} is missing. Assign the global UI root in the Inspector.");
                return;
            }

            if (transform.IsChildOf(root.transform))
            {
                WarnOnce(rootName + ":owner", $"[GameUIRootController] {rootName} contains the routing owner and cannot be toggled safely. Assign a child content root instead.");
                return;
            }

            if (root.activeSelf != visible)
                root.SetActive(visible);
        }

        private GameObject FindUniqueObjectRoot<T>() where T : Component
        {
            T[] candidates = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (candidates.Length == 1)
                return candidates[0] != null ? candidates[0].gameObject : null;

            if (candidates.Length > 1)
                WarnOnce(typeof(T).FullName, $"[GameUIRootController] Multiple {typeof(T).Name} candidates were found. Assign the intended root in the Inspector.");

            return null;
        }

        private GameObject FindUniqueByName(params string[] objectNames)
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject match = null;
            int matchCount = 0;
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || !MatchesAny(candidate.name, objectNames))
                    continue;

                match = candidate.gameObject;
                matchCount++;
            }

            if (matchCount == 1)
                return match;

            if (matchCount > 1)
                WarnOnce(string.Join("|", objectNames), $"[GameUIRootController] Multiple named root candidates ({string.Join(", ", objectNames)}) were found. Assign the intended root in the Inspector.");

            return null;
        }

        private void WarnOnce(string key, string message)
        {
            if (_warnings.Add(key))
                Debug.LogWarning(message, this);
        }

        private static bool MatchesAny(string value, string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                if (value == candidates[i])
                    return true;
            }

            return false;
        }

        private static bool IsVisible(GameObject root)
        {
            return root != null && root.activeSelf;
        }
    }
}
