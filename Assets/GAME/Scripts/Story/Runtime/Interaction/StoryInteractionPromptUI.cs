// Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionPromptUI.cs
using TMPro;
using UnityEngine;

namespace Game.Story.Interaction
{
    public sealed class StoryInteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text promptText;

        private void Awake()
        {
            Hide();
        }

        public void Configure(GameObject rootObject, TMP_Text text)
        {
            root = rootObject;
            promptText = text;
            Hide();
        }

        public void Configure(CanvasGroup group, GameObject rootObject, TMP_Text text)
        {
            Configure(rootObject, text);
        }

        public void Show(string text)
        {
            if (promptText != null)
            {
                promptText.text = text;
            }

            if (root != null)
            {
                root.SetActive(true);
            }
        }

        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }
    }
}
