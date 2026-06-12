using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Game.Core;
using Game.DemoMission.Runtime;

namespace Game.DemoMission.UI
{
    public sealed class MissionCompletePanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [FormerlySerializedAs("returnButton")]
        [SerializeField] private Button returnToTitleButton;
        [SerializeField] private string titleSceneName = "TitleScene";
        [SerializeField] private bool pauseWhenShown;
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
            BindButton();

            if (titleText != null)
                titleText.text = "임무 완료";

            if (descriptionText != null)
                descriptionText.text = "구출 대상 확보가 완료되었습니다.";

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            if (pauseWhenShown)
                Time.timeScale = 0f;

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

            if (pauseWhenShown)
                Time.timeScale = 1f;

            gameObject.SetActive(false);
        }

        public void ReturnToTitle()
        {
            if (_returning)
                return;

            string sceneName = ResolveTitleSceneName();
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[MissionCompletePanel] Title scene name is empty.", this);
                return;
            }

            StartCoroutine(Co_ReturnToTitle());
        }

        private IEnumerator Co_AutoReturn()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, autoReturnDelay));
            _autoReturnRoutine = null;
            ReturnToTitle();
        }

        private IEnumerator Co_ReturnToTitle()
        {
            _returning = true;
            Time.timeScale = 1f;

            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.Cutscene);

            string sceneName = ResolveTitleSceneName();
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError($"[MissionCompletePanel] Failed to load title scene: {sceneName}", this);
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

            if (returnToTitleButton != null)
                returnToTitleButton.onClick.AddListener(ReturnToTitle);
            else
                Debug.LogWarning("[MissionCompletePanel] Return button is not assigned.", this);

            _buttonBound = true;
        }

        private void UnbindButton()
        {
            if (!_buttonBound)
                return;

            if (returnToTitleButton != null)
                returnToTitleButton.onClick.RemoveListener(ReturnToTitle);

            _buttonBound = false;
        }

        private string ResolveTitleSceneName()
        {
            if (!string.IsNullOrWhiteSpace(titleSceneName))
                return titleSceneName;

            DemoMissionRuntime runtime = DemoMissionRuntime.Instance;
            if (runtime != null && runtime.CurrentMission != null)
                return runtime.CurrentMission.titleSceneName;

            return string.Empty;
        }
    }
}
