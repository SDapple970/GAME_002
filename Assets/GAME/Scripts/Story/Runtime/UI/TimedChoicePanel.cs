// Assets/GAME/Scripts/Story/Runtime/UI/TimedChoicePanel.cs
using System;
using System.Collections.Generic;
using Game.Story.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Story.UI
{
    public sealed class TimedChoicePanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private Image timerFillImage;
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private TMP_Text[] choiceTexts;
        [SerializeField] private KeyCode firstChoiceKey = KeyCode.Alpha1;
        [SerializeField] private KeyCode secondChoiceKey = KeyCode.Alpha2;

        private readonly List<StoryChoice> _visibleChoices = new();
        private Action<StoryChoice> _onChoiceSelected;
        private Action _onTimeout;
        private float _remainingTime;
        private float _duration;
        private bool _runningTimer;
        private bool _selectionLocked;

        private void Awake()
        {
            Hide();
        }

        private void OnDisable()
        {
            Clear();
        }

        private void Update()
        {
            if (_selectionLocked || _visibleChoices.Count == 0) return;

            if (UnityEngine.Input.GetKeyDown(firstChoiceKey))
            {
                SelectVisibleChoice(0);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(secondChoiceKey))
            {
                SelectVisibleChoice(1);
                return;
            }

            if (!_runningTimer) return;

            _remainingTime -= Time.unscaledDeltaTime;
            UpdateTimerFill();

            if (_remainingTime > 0f) return;

            _selectionLocked = true;
            Action callback = _onTimeout;
            Hide();
            callback?.Invoke();
        }

        public void ShowChoices(IReadOnlyList<StoryChoice> choices, float timeLimitSeconds, Action<StoryChoice> onChoiceSelected, Action onTimeout)
        {
            Clear();

            _onChoiceSelected = onChoiceSelected;
            _onTimeout = onTimeout;
            _duration = Mathf.Max(0f, timeLimitSeconds);
            _remainingTime = _duration;
            _runningTimer = _duration > 0f;
            _selectionLocked = false;

            BuildVisibleChoices(choices);
            BindButtons();
            UpdateTimerFill();
            SetVisible(_visibleChoices.Count > 0);
        }

        public void Hide()
        {
            _selectionLocked = true;
            SetVisible(false);
            _runningTimer = false;
        }

        public void Clear()
        {
            _visibleChoices.Clear();
            _onChoiceSelected = null;
            _onTimeout = null;
            _remainingTime = 0f;
            _duration = 0f;
            _runningTimer = false;
            _selectionLocked = false;

            if (choiceButtons != null)
            {
                foreach (Button button in choiceButtons)
                {
                    if (button == null) continue;
                    button.onClick.RemoveAllListeners();
                    button.gameObject.SetActive(false);
                }
            }

            if (timerFillImage != null)
            {
                timerFillImage.fillAmount = 0f;
                timerFillImage.gameObject.SetActive(false);
            }
        }

        private void BuildVisibleChoices(IReadOnlyList<StoryChoice> choices)
        {
            if (choices == null) return;

            int maxCount = Mathf.Min(2, choices.Count);
            for (int i = 0; i < choices.Count && _visibleChoices.Count < maxCount; i++)
            {
                StoryChoice choice = choices[i];
                if (choice == null) continue;

                bool isMet = choice.AreConditionsMet();
                if (!isMet && choice.HideIfConditionNotMet) continue;

                _visibleChoices.Add(choice);
            }
        }

        private void BindButtons()
        {
            int buttonCount = choiceButtons != null ? choiceButtons.Length : 0;
            int textCount = choiceTexts != null ? choiceTexts.Length : 0;
            int count = Mathf.Min(2, _visibleChoices.Count, buttonCount);

            for (int i = 0; i < count; i++)
            {
                StoryChoice choice = _visibleChoices[i];
                Button button = choiceButtons[i];
                if (button == null) continue;

                bool isMet = choice.AreConditionsMet();
                button.gameObject.SetActive(true);
                button.interactable = isMet;

                if (i < textCount && choiceTexts[i] != null)
                {
                    choiceTexts[i].text = GetChoiceLabel(choice, isMet);
                }

                if (!isMet) continue;

                int capturedIndex = i;
                button.onClick.AddListener(() => SelectVisibleChoice(capturedIndex));
            }
        }

        private void SelectVisibleChoice(int index)
        {
            if (_selectionLocked) return;
            if (index < 0 || index >= _visibleChoices.Count) return;

            StoryChoice choice = _visibleChoices[index];
            if (choice == null || !choice.AreConditionsMet()) return;

            _selectionLocked = true;
            Action<StoryChoice> callback = _onChoiceSelected;
            Hide();
            callback?.Invoke(choice);
        }

        private void UpdateTimerFill()
        {
            if (timerFillImage == null) return;

            timerFillImage.gameObject.SetActive(_runningTimer);
            timerFillImage.fillAmount = _duration > 0f ? Mathf.Clamp01(_remainingTime / _duration) : 0f;
        }

        private void SetVisible(bool visible)
        {
            if (rootGroup != null)
            {
                rootGroup.alpha = visible ? 1f : 0f;
                rootGroup.interactable = visible;
                rootGroup.blocksRaycasts = visible;
            }

            if (root != null && root != gameObject)
            {
                root.SetActive(visible);
            }
        }

        private static string GetChoiceLabel(StoryChoice choice, bool isMet)
        {
            string text = choice.Text ?? string.Empty;
            if (isMet || string.IsNullOrEmpty(choice.DisabledReason)) return text;
            return $"{text} ({choice.DisabledReason})";
        }
    }
}
