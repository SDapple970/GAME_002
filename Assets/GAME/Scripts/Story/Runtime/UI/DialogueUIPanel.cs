// Assets/GAME/Scripts/Story/Runtime/UI/DialogueUIPanel.cs
using Game.Story.Core;
using Game.Story.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Story.UI
{
    public sealed class DialogueUIPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text speakerText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Button nextButton;
        [SerializeField] private DialogueRunner runner;

        private void Awake()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (nextButton == null)
            {
                Debug.LogWarning("[DialogueUIPanel] Next button is not assigned.");
                return;
            }

            nextButton.onClick.RemoveListener(HandleNextClicked);
            nextButton.onClick.AddListener(HandleNextClicked);
        }

        private void OnDisable()
        {
            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(HandleNextClicked);
            }
        }

        public void Show()
        {
            if (root == null)
            {
                Debug.LogWarning("[DialogueUIPanel] Root is not assigned.");
                return;
            }

            root.SetActive(true);
        }

        public void Hide()
        {
            if (root == null)
            {
                Debug.LogWarning("[DialogueUIPanel] Root is not assigned.");
                return;
            }

            root.SetActive(false);
        }

        public void ShowLine(DialogueLine line)
        {
            if (speakerText != null)
            {
                speakerText.text = line != null ? line.SpeakerName : string.Empty;
            }
            else
            {
                Debug.LogWarning("[DialogueUIPanel] Speaker text is not assigned.");
            }

            if (bodyText != null)
            {
                bodyText.text = line != null ? line.BodyText : string.Empty;
            }
            else
            {
                Debug.LogWarning("[DialogueUIPanel] Body text is not assigned.");
            }
        }

        public void Bind(DialogueRunner dialogueRunner)
        {
            runner = dialogueRunner;
        }

        private void HandleNextClicked()
        {
            if (runner != null)
            {
                runner.Advance();
            }
        }
    }
}
