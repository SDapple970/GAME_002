using UnityEngine;
using UnityEngine.UI;

namespace Game.Interaction
{
    public sealed class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text messageText;

        private void Awake()
        {
            AutoBindReferences();
            Hide();
        }

        public void Show(string message)
        {
            AutoBindReferences();

            if (messageText != null)
                messageText.text = message;

            if (root != null)
                root.SetActive(true);
            else
                gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (root != null)
                root.SetActive(false);
            else if (messageText != null)
                messageText.text = string.Empty;
        }

        private void AutoBindReferences()
        {
            if (root == null)
                root = gameObject;

            if (messageText == null)
                messageText = GetComponentInChildren<Text>(true);
        }
    }
}
