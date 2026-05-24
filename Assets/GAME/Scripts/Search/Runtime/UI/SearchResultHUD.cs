using System.Collections;
using Game.Search;
using TMPro;
using UnityEngine;

namespace Game.Search.UI
{
    public sealed class SearchResultHUD : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private float autoHideSeconds = 2.5f;
        [SerializeField] private RectTransform messageRect;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Vector2 screenOffset = new(0f, 48f);

        private Coroutine _hideRoutine;
        private SearchObjectAnchor _currentAnchor;
        private bool _followAnchor;

        private void Awake()
        {
            ResolveFallbackReferences();
            Hide();
        }

        private void LateUpdate()
        {
            if (!_followAnchor || _currentAnchor == null || messageRect == null) return;

            Camera cameraToUse = ResolveWorldCamera();
            Vector3 screenPosition = cameraToUse != null
                ? cameraToUse.WorldToScreenPoint(_currentAnchor.GetWorldPosition())
                : _currentAnchor.GetWorldPosition();

            messageRect.position = new Vector3(
                screenPosition.x + screenOffset.x,
                screenPosition.y + screenOffset.y,
                screenPosition.z);
        }

        public void ShowMessage(string message)
        {
            ShowMessage(message, null, -1f);
        }

        public void ShowMessage(string message, SearchObjectAnchor anchor, float autoHideSecondsOverride = -1f)
        {
            ResolveFallbackReferences();

            if (messageText != null)
            {
                messageText.text = string.IsNullOrEmpty(message) ? string.Empty : message;
            }

            _currentAnchor = anchor;
            _followAnchor = anchor != null;

            if (root != null)
            {
                root.SetActive(true);
            }

            if (rootGroup != null)
            {
                rootGroup.alpha = 1f;
                rootGroup.interactable = false;
                rootGroup.blocksRaycasts = false;
            }

            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
            }

            LateUpdate();

            float hideDelay = autoHideSecondsOverride > 0f ? autoHideSecondsOverride : autoHideSeconds;
            if (hideDelay > 0f && isActiveAndEnabled)
            {
                _hideRoutine = StartCoroutine(HideAfterDelay(hideDelay));
            }
        }

        public void Hide()
        {
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            _currentAnchor = null;
            _followAnchor = false;

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

        private IEnumerator HideAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            _hideRoutine = null;
            Hide();
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

            if (messageRect == null && messageText != null)
            {
                messageRect = messageText.GetComponent<RectTransform>();
            }

            if (messageRect == null)
            {
                messageRect = GetComponent<RectTransform>();
            }
        }
    }
}
