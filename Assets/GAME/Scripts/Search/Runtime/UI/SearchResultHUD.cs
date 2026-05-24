using System.Collections;
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

        private Coroutine _hideRoutine;

        private void Awake()
        {
            Hide();
        }

        public void ShowMessage(string message)
        {
            if (messageText != null)
            {
                messageText.text = string.IsNullOrEmpty(message) ? string.Empty : message;
            }

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

            if (autoHideSeconds > 0f && isActiveAndEnabled)
            {
                _hideRoutine = StartCoroutine(HideAfterDelay());
            }
        }

        public void Hide()
        {
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

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

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(autoHideSeconds);
            _hideRoutine = null;
            Hide();
        }
    }
}
