using System;
using Game.Search;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Search.UI
{
    public sealed class SearchDecisionHUD : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private RectTransform bubbleRect;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button questionButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmButtonText;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text cancelButtonText;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Vector2 screenOffset = new(0f, 32f);

        private SearchObjectAnchor _currentAnchor;
        private Action _onQuestionClicked;
        private Action _onConfirm;
        private Action _onCancel;
        private bool _visible;

        private void Awake()
        {
            ResolveFallbackReferences();
            Hide();
        }

        private void LateUpdate()
        {
            if (!_visible || _currentAnchor == null || bubbleRect == null) return;

            Camera cameraToUse = ResolveWorldCamera();
            Vector3 screenPosition = cameraToUse != null
                ? cameraToUse.WorldToScreenPoint(_currentAnchor.GetWorldPosition())
                : _currentAnchor.GetWorldPosition();

            bubbleRect.position = new Vector3(
                screenPosition.x + screenOffset.x,
                screenPosition.y + screenOffset.y,
                screenPosition.z);
        }

        public void ShowQuestionOnly(SearchObjectAnchor anchor, string message, Action onQuestionClicked)
        {
            ResolveFallbackReferences();
            ClearListeners();

            _currentAnchor = anchor;
            _onQuestionClicked = onQuestionClicked;
            _onConfirm = null;
            _onCancel = null;
            _visible = true;

            if (messageText != null)
            {
                messageText.text = string.IsNullOrEmpty(message) ? "조사해볼까?" : message;
            }

            SetQuestionButtonState(true, true);
            SetButtonVisible(confirmButton, false);
            SetButtonVisible(cancelButton, false);

            if (questionButton != null)
            {
                questionButton.onClick.AddListener(HandleQuestionClicked);
            }

            if (root != null)
            {
                root.SetActive(true);
            }

            if (rootGroup != null)
            {
                rootGroup.alpha = 1f;
                rootGroup.interactable = true;
                rootGroup.blocksRaycasts = true;
            }

            LateUpdate();
        }

        public void Show(
            SearchObjectAnchor anchor,
            string message,
            string confirmText,
            string cancelText,
            Action onConfirm,
            Action onCancel)
        {
            ResolveFallbackReferences();
            ClearListeners();

            _currentAnchor = anchor;
            _onQuestionClicked = null;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _visible = true;

            if (messageText != null)
            {
                messageText.text = string.IsNullOrEmpty(message) ? "어떻게 할까?" : message;
            }

            if (confirmButtonText != null)
            {
                confirmButtonText.text = string.IsNullOrEmpty(confirmText) ? "조사한다" : confirmText;
            }

            if (cancelButtonText != null)
            {
                cancelButtonText.text = string.IsNullOrEmpty(cancelText) ? "그만둔다" : cancelText;
            }

            SetQuestionButtonState(true, false);
            SetButtonVisible(confirmButton, true);
            SetButtonVisible(cancelButton, true);

            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(HandleConfirmClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(HandleCancelClicked);
            }

            if (root != null)
            {
                root.SetActive(true);
            }

            if (rootGroup != null)
            {
                rootGroup.alpha = 1f;
                rootGroup.interactable = true;
                rootGroup.blocksRaycasts = true;
            }

            LateUpdate();
        }

        public void Hide()
        {
            ClearListeners();
            _currentAnchor = null;
            _onQuestionClicked = null;
            _onConfirm = null;
            _onCancel = null;
            _visible = false;
            SetQuestionButtonState(false, false);
            SetButtonVisible(confirmButton, false);
            SetButtonVisible(cancelButton, false);

            if (rootGroup != null)
            {
                rootGroup.alpha = 0f;
                rootGroup.interactable = false;
                rootGroup.blocksRaycasts = false;
            }

            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private void HandleConfirmClicked()
        {
            Action callback = _onConfirm;
            Hide();
            callback?.Invoke();
        }

        private void HandleQuestionClicked()
        {
            Action callback = _onQuestionClicked;
            callback?.Invoke();
        }

        private void HandleCancelClicked()
        {
            Action callback = _onCancel;
            Hide();
            callback?.Invoke();
        }

        private void ClearListeners()
        {
            questionButton?.onClick.RemoveAllListeners();
            confirmButton?.onClick.RemoveAllListeners();
            cancelButton?.onClick.RemoveAllListeners();
        }

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button != null)
            {
                button.gameObject.SetActive(visible);
            }
        }

        private void SetQuestionButtonState(bool active, bool interactable)
        {
            if (questionButton == null) return;

            questionButton.interactable = interactable;
            if (questionButton.gameObject != root)
            {
                questionButton.gameObject.SetActive(active);
            }
        }

        private Camera ResolveWorldCamera()
        {
            if (worldCamera != null) return worldCamera;

            worldCamera = Camera.main;
            return worldCamera;
        }

        private void ResolveFallbackReferences()
        {
            if (root == null)
            {
                root = gameObject;
            }

            if (rootGroup == null)
            {
                rootGroup = GetComponent<CanvasGroup>();
            }

            if (bubbleRect == null)
            {
                bubbleRect = GetComponent<RectTransform>();
            }
        }
    }
}
