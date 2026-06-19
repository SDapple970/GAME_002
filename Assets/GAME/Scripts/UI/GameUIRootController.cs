using UnityEngine;

namespace Game.UI
{
    public sealed class GameUIRootController : MonoBehaviour
    {
        [SerializeField] private GameObject fieldRoot;
        [SerializeField] private GameObject dialogueRoot;
        [SerializeField] private GameObject choiceRoot;
        [SerializeField] private GameObject combatRoot;
        [SerializeField] private GameObject rewardRoot;
        [SerializeField] private GameObject pauseRoot;
        [SerializeField] private GameObject loadingRoot;

        public void SetFieldVisible(bool visible) => SetVisible(fieldRoot, visible);
        public void SetDialogueVisible(bool visible) => SetVisible(dialogueRoot, visible);
        public void SetChoiceVisible(bool visible) => SetVisible(choiceRoot, visible);
        public void SetCombatVisible(bool visible) => SetVisible(combatRoot, visible);
        public void SetRewardVisible(bool visible) => SetVisible(rewardRoot, visible);
        public void SetPauseVisible(bool visible) => SetVisible(pauseRoot, visible);
        public void SetLoadingVisible(bool visible) => SetVisible(loadingRoot, visible);

        private static void SetVisible(GameObject root, bool visible)
        {
            if (root != null && root.activeSelf != visible)
                root.SetActive(visible);
        }
    }
}
