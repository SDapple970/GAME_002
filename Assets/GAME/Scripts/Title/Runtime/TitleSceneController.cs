using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GAME.Title
{
    public sealed class TitleSceneController : MonoBehaviour
    {
        private enum TitleRequestStep
        {
            None,
            NpcPaper,
            MonsterPaper,
            Accepted
        }

        [SerializeField] private Button startButton;
        [SerializeField] private Button paperClickButton;
        [SerializeField] private GameObject titleGroup;
        [SerializeField] private RectTransform requestPaperRoot;
        [SerializeField] private CanvasGroup requestPaperCanvasGroup;
        [SerializeField] private CanvasGroup npcPaperCanvasGroup;
        [SerializeField] private CanvasGroup monsterPaperCanvasGroup;
        [SerializeField] private CanvasGroup stampCanvasGroup;
        [SerializeField] private CanvasGroup fadeCanvasGroup;
        [SerializeField] private TitleSceneAnimator titleSceneAnimator;
        [SerializeField] private string dungeonSceneName;

        private TitleRequestStep _currentStep = TitleRequestStep.None;
        private bool _startButtonBound;
        private bool _paperButtonBound;
        private bool _transitioning;

        private void Awake()
        {
            if (titleSceneAnimator == null)
                titleSceneAnimator = GetComponent<TitleSceneAnimator>();
        }

        private void Start()
        {
            InitializeState();
        }

        private void OnEnable()
        {
            BindButtons();
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

        private void InitializeState()
        {
            if (titleGroup != null)
                titleGroup.SetActive(true);

            SetGroup(requestPaperCanvasGroup, 0f, false, false);
            SetGroup(npcPaperCanvasGroup, 1f, false, false);
            SetGroup(monsterPaperCanvasGroup, 0f, false, false);
            SetGroup(stampCanvasGroup, 0f, false, false);
            SetGroup(fadeCanvasGroup, 0f, false, false);

            if (paperClickButton != null)
                paperClickButton.interactable = false;

            _currentStep = TitleRequestStep.None;
            _transitioning = false;
        }

        private void HandleStartClicked()
        {
            if (_transitioning || _currentStep != TitleRequestStep.None)
                return;

            if (!ValidateStartReferences())
                return;

            StartCoroutine(Co_OpenRequestPaper());
        }

        private IEnumerator Co_OpenRequestPaper()
        {
            _transitioning = true;

            if (startButton != null)
                startButton.interactable = false;

            if (titleGroup != null)
                titleGroup.SetActive(false);

            _currentStep = TitleRequestStep.NpcPaper;
            SetGroup(npcPaperCanvasGroup, 1f, false, false);
            SetGroup(monsterPaperCanvasGroup, 0f, false, false);
            SetGroup(stampCanvasGroup, 0f, false, false);

            yield return titleSceneAnimator.PlayPaperIn(requestPaperRoot, requestPaperCanvasGroup);

            if (paperClickButton != null)
                paperClickButton.interactable = true;

            _transitioning = false;
        }

        private void HandlePaperClicked()
        {
            if (_transitioning || _currentStep == TitleRequestStep.Accepted)
                return;

            if (!ValidatePaperReferences())
                return;

            if (_currentStep == TitleRequestStep.NpcPaper)
                StartCoroutine(Co_ShowMonsterPaper());
            else if (_currentStep == TitleRequestStep.MonsterPaper)
                StartCoroutine(Co_AcceptAndLoadDungeon());
        }

        private IEnumerator Co_ShowMonsterPaper()
        {
            _transitioning = true;

            if (paperClickButton != null)
                paperClickButton.interactable = false;

            yield return titleSceneAnimator.SwitchPaper(npcPaperCanvasGroup, monsterPaperCanvasGroup);

            _currentStep = TitleRequestStep.MonsterPaper;

            if (paperClickButton != null)
                paperClickButton.interactable = true;

            _transitioning = false;
        }

        private IEnumerator Co_AcceptAndLoadDungeon()
        {
            _transitioning = true;
            _currentStep = TitleRequestStep.Accepted;

            if (paperClickButton != null)
                paperClickButton.interactable = false;

            if (string.IsNullOrWhiteSpace(dungeonSceneName))
            {
                Debug.LogError("[TitleSceneController] Dungeon scene name is empty.", this);
                _transitioning = false;
                yield break;
            }

            RectTransform stampRect = stampCanvasGroup != null
                ? stampCanvasGroup.GetComponent<RectTransform>()
                : null;

            yield return titleSceneAnimator.PlayStamp(stampCanvasGroup, stampRect);
            yield return titleSceneAnimator.PlayFade(fadeCanvasGroup);

            float delay = titleSceneAnimator != null ? titleSceneAnimator.SceneLoadDelay : 0f;
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            SceneManager.LoadScene(dungeonSceneName);
        }

        private bool ValidateStartReferences()
        {
            bool valid = true;

            if (requestPaperRoot == null)
            {
                Debug.LogError("[TitleSceneController] Request Paper Root is not assigned.", this);
                valid = false;
            }

            if (requestPaperCanvasGroup == null)
            {
                Debug.LogError("[TitleSceneController] Request Paper CanvasGroup is not assigned.", this);
                valid = false;
            }

            if (titleSceneAnimator == null)
            {
                Debug.LogError("[TitleSceneController] TitleSceneAnimator is not assigned.", this);
                valid = false;
            }

            if (paperClickButton == null)
            {
                Debug.LogError("[TitleSceneController] Paper Click Button is not assigned.", this);
                valid = false;
            }

            return valid;
        }

        private bool ValidatePaperReferences()
        {
            bool valid = true;

            if (titleSceneAnimator == null)
            {
                Debug.LogError("[TitleSceneController] TitleSceneAnimator is not assigned.", this);
                valid = false;
            }

            if (npcPaperCanvasGroup == null)
            {
                Debug.LogError("[TitleSceneController] NPC Paper CanvasGroup is not assigned.", this);
                valid = false;
            }

            if (monsterPaperCanvasGroup == null)
            {
                Debug.LogError("[TitleSceneController] Monster Paper CanvasGroup is not assigned.", this);
                valid = false;
            }

            if (stampCanvasGroup == null)
            {
                Debug.LogError("[TitleSceneController] Stamp CanvasGroup is not assigned.", this);
                valid = false;
            }

            if (fadeCanvasGroup == null)
            {
                Debug.LogError("[TitleSceneController] Fade CanvasGroup is not assigned.", this);
                valid = false;
            }

            return valid;
        }

        private void BindButtons()
        {
            if (!_startButtonBound)
            {
                if (startButton != null)
                    startButton.onClick.AddListener(HandleStartClicked);
                else
                    Debug.LogError("[TitleSceneController] Start Button is not assigned.", this);

                _startButtonBound = true;
            }

            if (!_paperButtonBound)
            {
                if (paperClickButton != null)
                    paperClickButton.onClick.AddListener(HandlePaperClicked);

                _paperButtonBound = true;
            }
        }

        private void UnbindButtons()
        {
            if (_startButtonBound)
            {
                if (startButton != null)
                    startButton.onClick.RemoveListener(HandleStartClicked);

                _startButtonBound = false;
            }

            if (_paperButtonBound)
            {
                if (paperClickButton != null)
                    paperClickButton.onClick.RemoveListener(HandlePaperClicked);

                _paperButtonBound = false;
            }
        }

        private static void SetGroup(CanvasGroup group, float alpha, bool interactable, bool blocksRaycasts)
        {
            if (group == null)
                return;

            group.alpha = alpha;
            group.interactable = interactable;
            group.blocksRaycasts = blocksRaycasts;
        }
    }
}
