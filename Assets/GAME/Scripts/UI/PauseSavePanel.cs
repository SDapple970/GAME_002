using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The production pause presentation. It lives under the router-owned PauseRoot and
    /// only delegates state and persistence requests to their canonical owners.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseSavePanel : MonoBehaviour
    {
        private const float ButtonHeight = 52f;

        private Button _saveButton;
        private Button _resumeButton;
        private TMP_Text _feedbackText;
        private bool _saveRequested;
        private bool _buttonsBound;

        public static void EnsureInstalled(GameObject pauseRoot)
        {
            if (pauseRoot != null && pauseRoot.GetComponent<PauseSavePanel>() == null)
                pauseRoot.AddComponent<PauseSavePanel>();
        }

        private void Awake()
        {
            BuildIfNeeded();
        }

        private void OnEnable()
        {
            BuildIfNeeded();
            BindButtons();
            RefreshAvailability();
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

        private void HandleSaveClicked()
        {
            if (_saveRequested)
                return;

            SaveLoadService service = SaveLoadService.Instance;
            if (service == null)
            {
                ShowFeedback("Save service unavailable.");
                RefreshAvailability();
                return;
            }

            _saveRequested = true;
            RefreshAvailability();
            bool succeeded = service.TrySave(out string message);
            _saveRequested = false;
            ShowFeedback(succeeded ? "Saved." : $"Save failed: {message}");
            RefreshAvailability();
        }

        private void HandleResumeClicked()
        {
            GameFlowController flow = GameFlowController.Instance;
            if (flow == null)
            {
                ShowFeedback("Game flow is unavailable.");
                return;
            }

            flow.ResumePreviousState();
        }

        private void RefreshAvailability()
        {
            SaveLoadService service = SaveLoadService.Instance;
            bool busy = _saveRequested || (service != null && service.CurrentOperationState != SaveLoadService.OperationState.Idle);
            if (_saveButton != null)
                _saveButton.interactable = !busy && service != null && service.CanSave;
            if (_resumeButton != null)
                _resumeButton.interactable = !busy && GameFlowController.Instance != null;
        }

        private void BuildIfNeeded()
        {
            if (_saveButton != null)
                return;

            RectTransform root = transform as RectTransform;
            if (root == null)
                return;

            Canvas canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            Image backdrop = gameObject.GetComponent<Image>();
            if (backdrop == null)
                backdrop = gameObject.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.78f);

            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            GameObject panel = new("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(transform, false);
            RectTransform panelTransform = panel.GetComponent<RectTransform>();
            panelTransform.anchorMin = new Vector2(0.5f, 0.5f);
            panelTransform.anchorMax = new Vector2(0.5f, 0.5f);
            panelTransform.sizeDelta = new Vector2(360f, 0f);
            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.11f, 0.13f, 0.18f, 0.98f);
            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 24);
            layout.spacing = 14;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateLabel("Title", panel.transform, "Paused", 34, FontStyles.Bold, out _);
            _saveButton = CreateButton("SaveButton", panel.transform, "Save");
            _resumeButton = CreateButton("ResumeButton", panel.transform, "Resume");
            CreateLabel("Feedback", panel.transform, string.Empty, 18, FontStyles.Normal, out _feedbackText);
        }

        private void BindButtons()
        {
            if (_buttonsBound)
                return;

            _saveButton?.onClick.AddListener(HandleSaveClicked);
            _resumeButton?.onClick.AddListener(HandleResumeClicked);
            _buttonsBound = true;
        }

        private void UnbindButtons()
        {
            if (!_buttonsBound)
                return;

            _saveButton?.onClick.RemoveListener(HandleSaveClicked);
            _resumeButton?.onClick.RemoveListener(HandleResumeClicked);
            _buttonsBound = false;
        }

        private void ShowFeedback(string message)
        {
            if (_feedbackText != null)
                _feedbackText.text = message;
        }

        private static Button CreateButton(string name, Transform parent, string label)
        {
            GameObject buttonObject = new(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.23f, 0.39f, 0.62f, 1f);
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = ButtonHeight;
            CreateLabel("Label", buttonObject.transform, label, 24, FontStyles.Bold, out _);
            return button;
        }

        private static void CreateLabel(string name, Transform parent, string text, float size, FontStyles style, out TMP_Text label)
        {
            GameObject labelObject = new(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            labelObject.transform.SetParent(parent, false);
            label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            LayoutElement layout = labelObject.GetComponent<LayoutElement>();
            layout.preferredHeight = size + 12f;
        }
    }
}
