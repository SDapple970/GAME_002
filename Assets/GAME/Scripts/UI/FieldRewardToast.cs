using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Non-blocking Exploration acquisition message. New messages replace the current message.</summary>
    public sealed class FieldRewardToast : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Text legacyMessageText;
        [SerializeField] private float autoHideSeconds = 1.5f;

        private Coroutine _routine;
        public bool IsVisible => root != null && root.activeSelf;

        private void Awake() => Hide();
        private void OnDisable() => StopOwnedRoutine();

        public bool Show(string message)
        {
            if (string.IsNullOrEmpty(message) || (messageText == null && legacyMessageText == null)) return false;
            StopOwnedRoutine();
            SetText(message);
            if (root != null) root.SetActive(true);
            _routine = StartCoroutine(HideAfterDelay());
            return true;
        }

        public void Hide()
        {
            StopOwnedRoutine();
            SetText(string.Empty);
            if (root != null && root.activeSelf) root.SetActive(false);
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, autoHideSeconds));
            _routine = null;
            SetText(string.Empty);
            if (root != null) root.SetActive(false);
        }

        private void StopOwnedRoutine()
        {
            if (_routine == null) return;
            StopCoroutine(_routine);
            _routine = null;
        }

        private void SetText(string value)
        {
            if (messageText != null) messageText.text = value;
            if (legacyMessageText != null) legacyMessageText.text = value;
        }
    }
}
