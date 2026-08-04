// Assets/GAME/Scripts/Story/Runtime/UI/TimedChoicePanel.cs
using System;
using System.Collections.Generic;
using Game.Story.Data;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
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

        private readonly List<ResolvedStoryChoice> _visibleChoices = new();
        private UnityAction[] _ownedButtonListeners;
        private Action<ResolvedStoryChoice> _onChoiceSelected;
        private Action _onTimeout;
        private float _remainingTime;
        private float _duration;
        private bool _runningTimer;
        private bool _selectionLocked;
        private int _generation;

        private void Awake()
        {
            // Retained only so existing serialized two-key configurations remain loadable.
            _ = firstChoiceKey;
            _ = secondChoiceKey;
            Hide();
        }

        private void OnDisable()
        {
            Clear();
        }

        private void Update()
        {
            if (_selectionLocked) return;

            int generation = _generation;

            if (!_runningTimer) return;

            _remainingTime -= Time.unscaledDeltaTime;
            UpdateTimerFill();

            if (_remainingTime > 0f) return;

            if (generation != _generation) return;
            _selectionLocked = true;
            Action callback = _onTimeout;
            Hide();
            callback?.Invoke();
        }

        public void ShowChoices(IReadOnlyList<StoryChoice> choices, float timeLimitSeconds, Action<StoryChoice> onChoiceSelected, Action onTimeout)
        {
            ShowChoices(
                StoryChoiceResolver.Resolve(choices, this),
                timeLimitSeconds,
                resolved => onChoiceSelected?.Invoke(resolved.Choice),
                onTimeout);
        }

        public void ShowChoices(IReadOnlyList<ResolvedStoryChoice> choices, float timeLimitSeconds, Action<ResolvedStoryChoice> onChoiceSelected, Action onTimeout)
        {
            Clear();

            int generation = ++_generation;

            _onChoiceSelected = onChoiceSelected;
            _onTimeout = onTimeout;
            _duration = Mathf.Max(0f, timeLimitSeconds);
            _remainingTime = _duration;
            _runningTimer = _duration > 0f;
            _selectionLocked = false;

            if (choices != null)
            {
                for (int i = 0; i < choices.Count && i < StoryChoiceResolver.MaxProductionChoices; i++)
                    _visibleChoices.Add(choices[i]);
            }
            BindButtons(generation);
            UpdateTimerFill();
            SetVisible(_visibleChoices.Count > 0 || _runningTimer);
        }

        public void Hide()
        {
            _selectionLocked = true;
            SetVisible(false);
            _runningTimer = false;
        }

        public void Clear()
        {
            _generation++;
            _visibleChoices.Clear();
            _onChoiceSelected = null;
            _onTimeout = null;
            _remainingTime = 0f;
            _duration = 0f;
            _runningTimer = false;
            _selectionLocked = false;

            if (choiceButtons != null)
            {
                for (int i = 0; i < choiceButtons.Length; i++)
                {
                    Button button = choiceButtons[i];
                    if (button == null) continue;
                    if (_ownedButtonListeners != null && i < _ownedButtonListeners.Length && _ownedButtonListeners[i] != null)
                        button.onClick.RemoveListener(_ownedButtonListeners[i]);
                    button.gameObject.SetActive(false);
                }
            }

            _ownedButtonListeners = null;

            if (timerFillImage != null)
            {
                timerFillImage.fillAmount = 0f;
                timerFillImage.gameObject.SetActive(false);
            }
        }

        private void BindButtons(int generation)
        {
            int buttonCount = choiceButtons != null ? choiceButtons.Length : 0;
            int textCount = choiceTexts != null ? choiceTexts.Length : 0;
            int count = Mathf.Min(StoryChoiceResolver.MaxProductionChoices, _visibleChoices.Count, buttonCount);
            _ownedButtonListeners = new UnityAction[buttonCount];

            for (int i = 0; i < count; i++)
            {
                ResolvedStoryChoice resolved = _visibleChoices[i];
                Button button = choiceButtons[i];
                if (button == null) continue;

                button.gameObject.SetActive(true);
                button.interactable = resolved.IsEnabled;

                if (i < textCount && choiceTexts[i] != null)
                {
                    choiceTexts[i].text = GetChoiceLabel(resolved);
                }

                if (!resolved.IsEnabled) continue;

                int capturedIndex = i;
                UnityAction listener = () => SelectVisibleChoice(capturedIndex, generation);
                _ownedButtonListeners[i] = listener;
                button.onClick.AddListener(listener);
            }
        }

        private void SelectVisibleChoice(int index, int generation)
        {
            if (generation != _generation) return;
            if (_selectionLocked) return;
            if (index < 0 || index >= _visibleChoices.Count) return;

            ResolvedStoryChoice resolved = _visibleChoices[index];
            if (!resolved.IsEnabled || resolved.Choice == null || !resolved.Choice.AreConditionsMet()) return;

            _selectionLocked = true;
            Action<ResolvedStoryChoice> callback = _onChoiceSelected;
            Hide();
            callback?.Invoke(resolved);
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

        private static string GetChoiceLabel(ResolvedStoryChoice resolved)
        {
            StoryChoice choice = resolved.Choice;
            string text = choice.Text ?? string.Empty;
            if (resolved.IsEnabled || string.IsNullOrEmpty(resolved.DisabledReason)) return text;
            return $"{text} ({resolved.DisabledReason})";
        }
    }
}
