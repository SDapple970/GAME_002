using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Core;

namespace Game.DemoMission.UI
{
    public sealed class MissionCompletePanel : MonoBehaviour
    {
        [SerializeField] private string titleSceneName = "TitleScene";
        [SerializeField] private Button returnButton;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private bool autoReturnToTitle;
        [SerializeField] private float autoReturnDelay = 2f;

        private bool _buttonBound;
        private bool _returning;
        private Coroutine _autoReturnRoutine;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            Hide();
        }

        private void OnEnable()
        {
            BindButton();
        }

        private void OnDisable()
        {
            UnbindButton();

            if (_autoReturnRoutine != null)
            {
                StopCoroutine(_autoReturnRoutine);
                _autoReturnRoutine = null;
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            if (autoReturnToTitle && _autoReturnRoutine == null)
                _autoReturnRoutine = StartCoroutine(Co_AutoReturn());
        }

        public void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }

        public void ReturnToTitle()
        {
            if (_returning)
                return;

            if (string.IsNullOrWhiteSpace(titleSceneName))
            {
                Debug.LogError("[MissionCompletePanel] Title scene name is empty.", this);
                return;
            }

            StartCoroutine(Co_ReturnToTitle());
        }

        private IEnumerator Co_AutoReturn()
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, autoReturnDelay));
            _autoReturnRoutine = null;
            ReturnToTitle();
        }

        private IEnumerator Co_ReturnToTitle()
        {
            _returning = true;

            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.Cutscene);

            AsyncOperation operation = SceneManager.LoadSceneAsync(titleSceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError($"[MissionCompletePanel] Failed to load title scene: {titleSceneName}", this);
                _returning = false;
                yield break;
            }

            while (!operation.isDone)
                yield return null;

            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.Exploration);
        }

        private void BindButton()
        {
            if (_buttonBound)
                return;

            if (returnButton != null)
                returnButton.onClick.AddListener(ReturnToTitle);
            else
                Debug.LogWarning("[MissionCompletePanel] Return button is not assigned.", this);

            _buttonBound = true;
        }

        private void UnbindButton()
        {
            if (!_buttonBound)
                return;

            if (returnButton != null)
                returnButton.onClick.RemoveListener(ReturnToTitle);

            _buttonBound = false;
        }
    }
}
