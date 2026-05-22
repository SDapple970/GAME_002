// Assets/GAME/Scripts/Story/Runtime/UI/DialoguePanel.cs
using System;
using System.Collections.Generic;
using Game.Story.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Story.UI
{
    public sealed class DialoguePanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Image portraitImage;
        [SerializeField] private Button nextButton;
        [SerializeField] private Transform choiceContainer;
        [SerializeField] private Button choiceButtonPrefab;

        private readonly List<Button> _choiceButtons = new();

        public int VisibleChoiceCount => _choiceButtons.Count;
        public int InteractableChoiceCount
        {
            get
            {
                int count = 0;
                foreach (Button button in _choiceButtons)
                {
                    if (button != null && button.interactable)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public event Action OnNextRequested;

        private void Awake()
        {
            Hide();
        }

        private void OnEnable()
        {
            if (nextButton == null)
            {
                Debug.LogWarning("[DialoguePanel] Next button is not assigned.");
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
            SetRootVisible(true);
        }

        public void Hide()
        {
            ClearChoices();
            SetRootVisible(false);
        }

        public void SetLine(string speaker, string body, Sprite portrait)
        {
            if (speakerText != null)
            {
                speakerText.text = speaker ?? string.Empty;
            }
            else
            {
                Debug.LogWarning("[DialoguePanel] Speaker text is not assigned.");
            }

            if (bodyText != null)
            {
                bodyText.text = body ?? string.Empty;
            }
            else
            {
                Debug.LogWarning("[DialoguePanel] Body text is not assigned.");
            }

            if (portraitImage != null)
            {
                portraitImage.sprite = portrait;
                portraitImage.enabled = portrait != null;
            }
            else if (portrait != null)
            {
                Debug.LogWarning("[DialoguePanel] Portrait image is not assigned.");
            }
        }

        public void SetNextVisible(bool visible)
        {
            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(visible);
            }
            else
            {
                Debug.LogWarning("[DialoguePanel] Next button is not assigned.");
            }
        }

        public void BuildChoices(IReadOnlyList<StoryChoice> choices, Action<StoryChoice> onChoiceSelected)
        {
            ClearChoices();

            if (choices == null || choices.Count == 0) return;

            if (choiceContainer == null)
            {
                Debug.LogWarning("[DialoguePanel] Choice container is not assigned.");
                return;
            }

            if (choiceButtonPrefab == null)
            {
                Debug.LogWarning("[DialoguePanel] Choice button prefab is not assigned.");
                return;
            }

            foreach (StoryChoice choice in choices)
            {
                if (choice == null) continue;

                bool isMet = choice.AreConditionsMet();
                if (!isMet && choice.HideIfConditionNotMet)
                {
                    continue;
                }

                Button button = Instantiate(choiceButtonPrefab, choiceContainer);
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = GetChoiceLabel(choice, isMet);
                }
                else
                {
                    Debug.LogWarning("[DialoguePanel] Choice button prefab has no TMP_Text child.");
                }

                button.interactable = isMet;
                if (isMet)
                {
                    StoryChoice capturedChoice = choice;
                    button.onClick.AddListener(() => onChoiceSelected?.Invoke(capturedChoice));
                }

                _choiceButtons.Add(button);
            }
        }

        public void ClearChoices()
        {
            foreach (Button button in _choiceButtons)
            {
                if (button != null)
                {
                    Destroy(button.gameObject);
                }
            }

            _choiceButtons.Clear();
        }

        private void SetRootVisible(bool visible)
        {
            if (rootGroup != null)
            {
                rootGroup.alpha = visible ? 1f : 0f;
                rootGroup.interactable = visible;
                rootGroup.blocksRaycasts = visible;
            }
            else if (root != null)
            {
                root.SetActive(visible);
            }
            else
            {
                Debug.LogWarning("[DialoguePanel] Root group or root GameObject is not assigned.");
            }
        }

        private static string GetChoiceLabel(StoryChoice choice, bool isMet)
        {
            string text = choice.Text ?? string.Empty;
            if (isMet) return text;

            if (!string.IsNullOrEmpty(choice.DisabledReason))
            {
                return $"{text} ({choice.DisabledReason})";
            }

            return text;
        }

        private void HandleNextClicked()
        {
            OnNextRequested?.Invoke();
        }
    }
}
