// Assets/GAME/Scripts/Story/Runtime/UI/ChoiceButtonUI.cs
using Game.Story.Core;
using Game.Story.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Story.UI
{
    public sealed class ChoiceButtonUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;

        private ChoiceDefinition _choice;
        private DialogueRunner _runner;

        private void OnEnable()
        {
            if (button == null)
            {
                Debug.LogWarning("[ChoiceButtonUI] Button is not assigned.");
                return;
            }

            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        public void Setup(ChoiceDefinition choice, DialogueRunner runner, bool interactable)
        {
            _choice = choice;
            _runner = runner;

            if (label != null)
            {
                string text = choice != null ? choice.ChoiceText : "(null choice)";
                label.text = interactable ? text : "[조건 부족] " + text;
            }
            else
            {
                Debug.LogWarning("[ChoiceButtonUI] Label is not assigned.");
            }

            if (button != null)
            {
                button.interactable = interactable && runner != null;
            }
            else
            {
                Debug.LogWarning("[ChoiceButtonUI] Button is not assigned.");
            }
        }

        private void HandleClicked()
        {
            if (_runner != null)
            {
                _runner.SelectChoice(_choice);
            }
        }
    }
}
