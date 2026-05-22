// Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionPromptUI.cs
using TMPro;
using UnityEngine;

namespace Game.Story.Interaction
{
    public sealed class StoryInteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text promptText;

        private void Awake()
        {
            if (rootGroup != null || root != null)
            {
                Hide();
            }
        }

        public void Configure(CanvasGroup group, GameObject rootObject, TMP_Text text)
        {
            rootGroup = group;
            root = rootObject;
            promptText = text;
            Hide();
        }

        public void Show(string text)
        {
            if (promptText != null)
            {
                promptText.text = text ?? string.Empty;
            }
            else
            {
                Debug.LogWarning("[StoryInteractionPromptUI] Prompt text is not assigned.");
            }

            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (rootGroup != null)
            {
                rootGroup.alpha = visible ? 1f : 0f;
                rootGroup.interactable = visible;
                rootGroup.blocksRaycasts = visible;
                return;
            }

            if (root != null)
            {
                root.SetActive(visible);
                return;
            }

            Debug.LogWarning("[StoryInteractionPromptUI] Root group or root GameObject is not assigned.");
        }
    }
}
