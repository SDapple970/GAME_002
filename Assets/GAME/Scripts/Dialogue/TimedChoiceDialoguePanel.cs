using System.Collections;
using Game.Core;
using Game.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Dialogue
{
    public sealed class TimedChoiceDialoguePanel : MonoBehaviour
    {
        public static TimedChoiceDialoguePanel Instance { get; private set; }

        [Header("Root")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private bool autoBuildIfMissing = true;

        [Header("TextMesh Pro")]
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text optionAText;
        [SerializeField] private TMP_Text optionBText;
        [SerializeField] private TMP_Text timerText;

        [Header("Legacy Text Fallback")]
        [SerializeField] private Text legacySpeakerText;
        [SerializeField] private Text legacyBodyText;
        [SerializeField] private Text legacyOptionAText;
        [SerializeField] private Text legacyOptionBText;
        [SerializeField] private Text legacyTimerText;

        [Header("Choices")]
        [SerializeField] private Button optionAButton;
        [SerializeField] private Button optionBButton;

        [Header("Timer")]
        [SerializeField] private Slider timerSlider;
        [SerializeField] private Image timerFill;
        [SerializeField] private bool pauseTimeScale;

        private TimedChoiceDialogueEventSO _currentData;
        private InteractionContext _currentContext;
        private Coroutine _timerRoutine;
        private bool _selectionLocked;
        private bool _ownsUiOnlyState;
        private bool _pausedTimeScale;
        private float _previousTimeScale = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[TimedChoiceDialoguePanel] Multiple panels exist. Replacing Instance.", this);
            }

            Instance = this;
            EnsureUiReferences();
            Hide();
        }

        private void OnDisable()
        {
            StopTimer();
            ClearButtonListeners();
            RestoreTimeScale();
            RestoreGameState();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Show(TimedChoiceDialogueEventSO data, InteractionContext context)
        {
            if (data == null)
                return;

            if (IsCombatBlockingState())
                return;

            Hide();

            _currentData = data;
            _currentContext = context;
            _selectionLocked = false;

            if (panelRoot != null)
                panelRoot.SetActive(true);

            SetText(speakerText, legacySpeakerText, data.speakerName);
            SetText(bodyText, legacyBodyText, data.bodyText);
            SetText(optionAText, legacyOptionAText, TimedChoiceDialogueEventSO.GetOptionLabel(data.optionA));
            SetText(optionBText, legacyOptionBText, TimedChoiceDialogueEventSO.GetOptionLabel(data.optionB));

            BindButton(optionAButton, 0);
            BindButton(optionBButton, 1);

            PrepareTimer(data.timeLimitSeconds);
            EnterUiOnlyState();
            ApplyTimeScalePause();

            _timerRoutine = StartCoroutine(Co_RunTimer(data.timeLimitSeconds));
        }

        public void Hide()
        {
            StopTimer();
            ClearButtonListeners();
            ResetTimerUi();
            RestoreTimeScale();
            RestoreGameState();

            _currentData = null;
            _selectionLocked = true;

            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private IEnumerator Co_RunTimer(float timeLimitSeconds)
        {
            float duration = Mathf.Max(0f, timeLimitSeconds);
            float remaining = duration;

            UpdateTimerUi(remaining, duration);

            while (remaining > 0f)
            {
                yield return null;
                remaining -= pauseTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                UpdateTimerUi(remaining, duration);
            }

            _timerRoutine = null;
            HandleTimeout();
        }

        private void SelectOption(int optionIndex)
        {
            if (_selectionLocked)
                return;

            _selectionLocked = true;
            StopTimer();

            TimedChoiceDialogueEventSO data = _currentData;
            InteractionContext context = _currentContext;

            if (data != null)
                data.ExecuteOption(optionIndex, context);

            Hide();
        }

        private void HandleTimeout()
        {
            if (_selectionLocked)
                return;

            _selectionLocked = true;
            TimedChoiceDialogueEventSO data = _currentData;
            InteractionContext context = _currentContext;

            if (data != null && data.autoSelectOnTimeout)
                data.ExecuteOption(data.GetClampedTimeoutDefaultOptionIndex(), context);

            Hide();
        }

        private void BindButton(Button button, int optionIndex)
        {
            if (button == null)
            {
                Debug.LogWarning($"[TimedChoiceDialoguePanel] Option button {optionIndex} is missing.", this);
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectOption(optionIndex));
        }

        private void ClearButtonListeners()
        {
            if (optionAButton != null)
                optionAButton.onClick.RemoveAllListeners();

            if (optionBButton != null)
                optionBButton.onClick.RemoveAllListeners();
        }

        private void StopTimer()
        {
            if (_timerRoutine == null)
                return;

            StopCoroutine(_timerRoutine);
            _timerRoutine = null;
        }

        private void PrepareTimer(float duration)
        {
            float clampedDuration = Mathf.Max(0f, duration);

            if (timerSlider != null)
            {
                timerSlider.minValue = 0f;
                timerSlider.maxValue = clampedDuration > 0f ? clampedDuration : 1f;
                timerSlider.value = clampedDuration;
            }

            if (timerFill != null)
                timerFill.fillAmount = clampedDuration > 0f ? 1f : 0f;

            UpdateTimerUi(clampedDuration, clampedDuration);
        }

        private void UpdateTimerUi(float remaining, float duration)
        {
            float clampedRemaining = Mathf.Max(0f, remaining);

            if (timerSlider != null)
                timerSlider.value = clampedRemaining;

            if (timerFill != null)
                timerFill.fillAmount = duration > 0f ? Mathf.Clamp01(clampedRemaining / duration) : 0f;

            SetText(timerText, legacyTimerText, Mathf.CeilToInt(clampedRemaining).ToString());
        }

        private void ResetTimerUi()
        {
            if (timerSlider != null)
                timerSlider.value = 0f;

            if (timerFill != null)
                timerFill.fillAmount = 0f;

            SetText(timerText, legacyTimerText, string.Empty);
        }

        private void EnterUiOnlyState()
        {
            _ownsUiOnlyState = false;

            if (GameStateMachine.Instance == null || !GameStateMachine.Instance.Is(GameState.Exploration))
                return;

            if (GameFlowController.Instance != null)
                GameFlowController.Instance.EnterUIOnly();
            else
                GameStateMachine.Instance.TrySetState(GameState.UIOnly, nameof(TimedChoiceDialoguePanel));
            _ownsUiOnlyState = true;
        }

        private void RestoreGameState()
        {
            if (!_ownsUiOnlyState)
                return;

            if (GameStateMachine.Instance != null && GameStateMachine.Instance.Is(GameState.UIOnly))
            {
                if (GameFlowController.Instance != null)
                    GameFlowController.Instance.EnterExploration();
                else
                    GameStateMachine.Instance.TrySetState(GameState.Exploration, nameof(TimedChoiceDialoguePanel));
            }

            _ownsUiOnlyState = false;
        }

        private void ApplyTimeScalePause()
        {
            if (!pauseTimeScale || _pausedTimeScale)
                return;

            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _pausedTimeScale = true;
        }

        private void RestoreTimeScale()
        {
            if (!_pausedTimeScale)
                return;

            Time.timeScale = _previousTimeScale;
            _pausedTimeScale = false;
        }

        private static void SetText(TMP_Text tmpText, Text legacyText, string value)
        {
            string safeValue = value ?? string.Empty;

            if (tmpText != null)
                tmpText.text = safeValue;

            if (legacyText != null)
                legacyText.text = safeValue;
        }

        private static bool IsCombatBlockingState()
        {
            return GameStateMachine.Instance != null &&
                   GameStateMachine.Instance.IsCombatState();
        }

        private void EnsureUiReferences()
        {
            if (!autoBuildIfMissing || panelRoot != null)
                return;

            panelRoot = CreateRect("PanelRoot", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(760f, 220f), new Vector2(0f, 140f)).gameObject;
            Image background = panelRoot.AddComponent<Image>();
            background.color = new Color(0.04f, 0.04f, 0.05f, 0.92f);

            speakerText = CreateTmpText("SpeakerText", panelRoot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(700f, 28f), new Vector2(24f, -22f), 20, TextAlignmentOptions.Left);
            bodyText = CreateTmpText("BodyText", panelRoot.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-48f, 72f), new Vector2(0f, -72f), 18, TextAlignmentOptions.TopLeft);
            timerText = CreateTmpText("TimerText", panelRoot.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(64f, 28f), new Vector2(-24f, -22f), 18, TextAlignmentOptions.Right);

            RectTransform timerBack = CreateRect("TimerFillBack", panelRoot.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-48f, 8f), new Vector2(0f, 82f));
            Image timerBackImage = timerBack.gameObject.AddComponent<Image>();
            timerBackImage.color = new Color(1f, 1f, 1f, 0.18f);

            RectTransform fillRect = CreateRect("TimerFill", timerBack, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            timerFill = fillRect.gameObject.AddComponent<Image>();
            timerFill.color = new Color(0.92f, 0.72f, 0.24f, 1f);
            timerFill.type = Image.Type.Filled;
            timerFill.fillMethod = Image.FillMethod.Horizontal;

            optionAButton = CreateButton("OptionAButton", panelRoot.transform, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-36f, 46f), new Vector2(18f, 30f), out optionAText);
            optionBButton = CreateButton("OptionBButton", panelRoot.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-36f, 46f), new Vector2(-18f, 30f), out optionBText);
        }

        private static Button CreateButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPosition, out TMP_Text label)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, pivot, sizeDelta, anchoredPosition);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.16f, 0.18f, 0.22f, 1f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            label = CreateTmpText("Text", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-24f, -12f), Vector2.zero, 17, TextAlignmentOptions.Center);
            return button;
        }

        private static TMP_Text CreateTmpText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPosition, int fontSize, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, pivot, sizeDelta, anchoredPosition);
            TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPosition)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)child.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPosition;
            return rect;
        }
    }
}
