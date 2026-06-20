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

        private void Awake()
        {
            AutoBindMissingReferences();
        }

        public void SetTitleVisible(bool visible) => SetVisible(titleRoot, visible);
        public void SetFieldVisible(bool visible) => SetVisible(fieldRoot, visible);
        public void SetDialogueVisible(bool visible) => SetVisible(dialogueRoot, visible);
        public void SetChoiceVisible(bool visible) => SetVisible(choiceRoot, visible);
        public void SetCombatVisible(bool visible) => SetVisible(combatRoot, visible);
        public void SetRewardVisible(bool visible) => SetVisible(rewardRoot, visible);
        public void SetPauseVisible(bool visible) => SetVisible(pauseRoot, visible);
        public void SetLoadingVisible(bool visible) => SetVisible(loadingRoot, visible);

        public void AutoBindMissingReferences()
        {
            titleRoot ??= FindByName("TitleRoot");
            titleRoot ??= FindByName("TitleGroup");
            fieldRoot ??= FindObjectRoot<OverworldHUDRoot>();
            dialogueRoot ??= FindObjectRoot<Game.Story.UI.DialoguePanel>();
            dialogueRoot ??= FindObjectRoot<Game.Story.UI.DialogueUIPanel>();
            dialogueRoot ??= FindObjectRoot<Game.Story.UI.StoryDialogueHUD>();
            choiceRoot ??= FindObjectRoot<Game.Story.UI.ChoiceUIPanel>();
            choiceRoot ??= FindObjectRoot<Game.Story.UI.TimedChoicePanel>();
            combatRoot ??= FindObjectRoot<Game.Combat.UI.CombatUIRootController>();
            combatRoot ??= FindObjectRoot<Game.Combat.UI.CombatPlanningHUD>();
            rewardRoot ??= FindObjectRoot<RewardUIPanel>();
            pauseRoot ??= FindByName("PauseMenu");
            loadingRoot ??= FindByName("LoadingPanel");
        }

        private static void SetVisible(GameObject root, bool visible)
        {
            if (root != null && root.activeSelf != visible)
                root.SetActive(visible);
        }

        private static GameObject FindObjectRoot<T>() where T : Component
        {
            T component = FindFirstObjectByType<T>(FindObjectsInactive.Include);
            return component != null ? component.gameObject : null;
        }

        private static GameObject FindByName(string objectName)
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && candidate.name == objectName)
                    return candidate.gameObject;
            }

            return null;
        }
    }
}
