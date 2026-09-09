using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Game.Core;
using Game.DemoMission.Runtime;

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
        [SerializeField] private Button continueButton;
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
        private bool _continueButtonBound;
        private bool _paperButtonBound;
        private bool _transitioning;
        private SaveLoadService _loadCompletionService;

        private void Awake()
        {
            if (titleSceneAnimator == null)
                titleSceneAnimator = GetComponent<TitleSceneAnimator>();

            EnsureContinueButton();
        }

        private void Start()
        {
            InitializeState();
            SubscribeToLoadCompletion();
        }

        private void OnEnable()
        {
            BindButtons();
            SubscribeToLoadCompletion();
        }

        private void OnDisable()
        {
            UnbindButtons();
            UnsubscribeFromLoadCompletion();
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
            RefreshContinueAvailability();
        }

        private void HandleStartClicked()
        {
            if (_transitioning || _currentStep != TitleRequestStep.None)
                return;

            if (!ValidateStartReferences())
                return;

            StartCoroutine(Co_OpenRequestPaper());
        }

        private void HandleContinueClicked()
        {
            if (_transitioning)
                return;

            SaveLoadService service = SaveLoadService.Instance;
            if (service == null || !service.CanLoad)
            {
                RefreshContinueAvailability();
                return;
            }

            _transitioning = true;
            RefreshContinueAvailability();
            if (!service.TryLoad(out string message))
            {
                Debug.LogWarning($"[TitleSceneController] Continue failed: {message}", this);
                _transitioning = false;
                RefreshContinueAvailability();
            }
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

            if (DemoMissionRuntime.Instance != null)
                DemoMissionRuntime.Instance.ResetMissionProgress();

            LoadDungeonScene(dungeonSceneName);
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
            EnsureContinueButton();
            if (!_startButtonBound)
            {
                if (startButton != null)
                    startButton.onClick.AddListener(HandleStartClicked);
                else
                    Debug.LogError("[TitleSceneController] Start Button is not assigned.", this);

                _startButtonBound = true;
            }

            if (!_continueButtonBound)
            {
                if (continueButton != null)
                    continueButton.onClick.AddListener(HandleContinueClicked);

                _continueButtonBound = true;
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

            if (_continueButtonBound)
            {
                if (continueButton != null)
                    continueButton.onClick.RemoveListener(HandleContinueClicked);

                _continueButtonBound = false;
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

        private void EnsureContinueButton()
        {
            if (continueButton != null || startButton == null)
                return;

            continueButton = Instantiate(startButton, startButton.transform.parent);
            continueButton.name = "ContinueButton";
            RectTransform source = startButton.transform as RectTransform;
            RectTransform created = continueButton.transform as RectTransform;
            if (source != null && created != null)
            {
                created.anchorMin = source.anchorMin;
                created.anchorMax = source.anchorMax;
                created.pivot = source.pivot;
                created.sizeDelta = source.sizeDelta;
                created.anchoredPosition = source.anchoredPosition + Vector2.down * (source.sizeDelta.y + 18f);
            }

            TMP_Text label = continueButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = "Continue";
        }

        private void RefreshContinueAvailability()
        {
            if (continueButton == null)
                return;

            SaveLoadService service = SaveLoadService.Instance;
            continueButton.interactable = !_transitioning && service != null && service.CanLoad;
        }

        private void SubscribeToLoadCompletion()
        {
            SaveLoadService service = SaveLoadService.Instance;
            if (_loadCompletionService == service)
                return;

            UnsubscribeFromLoadCompletion();
            _loadCompletionService = service;
            if (_loadCompletionService != null)
                _loadCompletionService.OnLoadCompleted += HandleLoadCompleted;
        }

        private void UnsubscribeFromLoadCompletion()
        {
            if (_loadCompletionService != null)
                _loadCompletionService.OnLoadCompleted -= HandleLoadCompleted;
            _loadCompletionService = null;
        }

        private void HandleLoadCompleted(bool succeeded, string message)
        {
            if (!_transitioning)
                return;

            _transitioning = false;
            if (!succeeded)
                Debug.LogWarning($"[TitleSceneController] Continue failed: {message}", this);
            RefreshContinueAvailability();
        }

        private static void LoadDungeonScene(string sceneName)
        {
            if (SceneFlowController.Instance != null)
            {
                SceneFlowController.Instance.LoadScene(sceneName);
                return;
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}
