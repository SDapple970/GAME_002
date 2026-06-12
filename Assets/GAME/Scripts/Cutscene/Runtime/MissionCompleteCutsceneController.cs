using Game.Core;
using Game.Mission;
using Game.Mission.Data;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.UI;
using UnityEngine.Video;

namespace Game.Cutscene
{
    public sealed class MissionCompleteCutsceneController : MonoBehaviour
    {
        [Header("Video UI")]
        [SerializeField] private GameObject videoRoot;
        [SerializeField] private RawImage videoScreen;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private AudioSource audioSource;

        [Header("State")]
        [SerializeField] private GameStateMachine gameStateMachine;
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private bool playMissionCompletionCutscenes = true;

        [Header("Completion")]
        [SerializeField] private GameObject rewardPanel;
        [SerializeField] private UnityEvent onFinished = new UnityEvent();

        [Header("Skip")]
        [SerializeField] private bool allowSkip;
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private Key skipKey = Key.Escape;
        [SerializeField] private InputActionReference skipAction;
#else
        [SerializeField] private KeyCode skipKey = KeyCode.Escape;
#endif

        [Header("Debug")]
        [SerializeField] private CutscenePlaybackRequest debugRequest;

        private CutscenePlaybackRequest _activeRequest;
        private bool _isPlaying;
        private bool _isPrepared;
        private bool _skipActionEnabledByController;

        public bool IsPlaying => _isPlaying;

        private void Awake()
        {
            AutoBindReferences();
            ConfigureVideoPlayerDefaults();

            if (videoRoot != null)
                videoRoot.SetActive(false);
        }

        private void OnEnable()
        {
            AutoBindReferences();

            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted += HandlePrepareCompleted;
                videoPlayer.loopPointReached += HandleLoopPointReached;
                videoPlayer.errorReceived += HandleVideoErrorReceived;
            }

            SubscribeMissionManager();

#if ENABLE_INPUT_SYSTEM
            if (skipAction != null && skipAction.action != null)
            {
                skipAction.action.performed += HandleSkipActionPerformed;
                if (!skipAction.action.enabled)
                {
                    skipAction.action.Enable();
                    _skipActionEnabledByController = true;
                }
            }
#endif
        }

        private void OnDisable()
        {
            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= HandlePrepareCompleted;
                videoPlayer.loopPointReached -= HandleLoopPointReached;
                videoPlayer.errorReceived -= HandleVideoErrorReceived;
            }

#if ENABLE_INPUT_SYSTEM
            if (skipAction != null && skipAction.action != null)
            {
                skipAction.action.performed -= HandleSkipActionPerformed;
                if (_skipActionEnabledByController)
                    skipAction.action.Disable();
            }

            _skipActionEnabledByController = false;
#endif

            UnsubscribeMissionManager();
            StopPlayback(invokeFinished: false);
        }

        private void Update()
        {
            if (!_isPlaying || !allowSkip)
                return;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[skipKey].wasPressedThisFrame)
                FinishPlayback();
#else
            if (Input.GetKeyDown(skipKey))
                FinishPlayback();
#endif
        }

        public void Play(CutscenePlaybackRequest request)
        {
            if (_isPlaying)
            {
                Debug.LogWarning("[MissionCompleteCutsceneController] Play ignored because a cutscene is already playing.", this);
                return;
            }

            if (request == null)
            {
                Debug.LogError("[MissionCompleteCutsceneController] Play failed. Request is null.", this);
                return;
            }

            if (!ValidatePlayback(request))
                return;

            _activeRequest = request;
            _isPlaying = true;
            _isPrepared = false;

            if (rewardPanel != null)
                rewardPanel.SetActive(false);

            videoRoot.SetActive(true);
            gameStateMachine.SetState(GameState.Cutscene);
            ConfigureVideoPlayerDefaults();
            ApplyRequestToVideoPlayer(request);

            videoPlayer.Prepare();
        }

        public void PlayDebugRequest()
        {
            Play(debugRequest);
        }

        public void Skip()
        {
            if (_isPlaying && allowSkip)
                FinishPlayback();
        }

        private void HandlePrepareCompleted(VideoPlayer source)
        {
            if (!_isPlaying || source != videoPlayer)
                return;

            _isPrepared = true;
            videoPlayer.Play();
        }

        private void HandleLoopPointReached(VideoPlayer source)
        {
            if (!_isPlaying || source != videoPlayer)
                return;

            FinishPlayback();
        }

        private void HandleVideoErrorReceived(VideoPlayer source, string message)
        {
            Debug.LogError($"[MissionCompleteCutsceneController] VideoPlayer error: {message}", this);
            if (_isPlaying && source == videoPlayer)
                FinishPlayback();
        }

        private void HandleMissionCompleted(MissionDefinitionSO mission)
        {
            if (!playMissionCompletionCutscenes || mission == null)
                return;

            CutscenePlaybackRequest request = mission.CompletionCutscene;
            if (request == null || !request.HasPlayableSource)
                return;

            Play(request);
        }

#if ENABLE_INPUT_SYSTEM
        private void HandleSkipActionPerformed(InputAction.CallbackContext context)
        {
            if (_isPlaying && allowSkip)
                FinishPlayback();
        }
#endif

        private void FinishPlayback()
        {
            StopPlayback(invokeFinished: true);
        }

        private void StopPlayback(bool invokeFinished)
        {
            if (!_isPlaying && !_isPrepared)
                return;

            CutscenePlaybackRequest completedRequest = _activeRequest;
            _activeRequest = null;
            _isPlaying = false;
            _isPrepared = false;

            if (videoPlayer != null)
                videoPlayer.Stop();

            if (videoRoot != null)
                videoRoot.SetActive(false);

            if (!invokeFinished)
                return;

            bool showRewardPanel = completedRequest == null || completedRequest.ShowRewardPanelOnFinished;
            if (showRewardPanel && rewardPanel != null)
            {
                if (gameStateMachine != null)
                    gameStateMachine.SetState(GameState.UIOnly);

                rewardPanel.SetActive(true);
            }
            else if (gameStateMachine != null)
            {
                gameStateMachine.SetState(GameState.Exploration);
            }

            completedRequest?.InvokeFinished();
            onFinished?.Invoke();
        }

        private void ApplyRequestToVideoPlayer(CutscenePlaybackRequest request)
        {
            if (request.UseUrl)
            {
                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = request.Url;
                videoPlayer.clip = null;
            }
            else
            {
                videoPlayer.source = VideoSource.VideoClip;
                videoPlayer.clip = request.Clip;
                videoPlayer.url = string.Empty;
            }

            if (audioSource != null)
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
                videoPlayer.SetTargetAudioSource(0, audioSource);
            }

            if (videoScreen != null && videoScreen.texture == null && videoPlayer.targetTexture != null)
                videoScreen.texture = videoPlayer.targetTexture;
        }

        private bool ValidatePlayback(CutscenePlaybackRequest request)
        {
            bool valid = true;

            if (videoRoot == null)
            {
                Debug.LogError("[MissionCompleteCutsceneController] Missing required field: videoRoot.", this);
                valid = false;
            }

            if (videoScreen == null)
            {
                Debug.LogError("[MissionCompleteCutsceneController] Missing required field: videoScreen.", this);
                valid = false;
            }

            if (videoPlayer == null)
            {
                Debug.LogError("[MissionCompleteCutsceneController] Missing required field: videoPlayer.", this);
                valid = false;
            }

            if (gameStateMachine == null)
            {
                Debug.LogError("[MissionCompleteCutsceneController] Missing required field: gameStateMachine.", this);
                valid = false;
            }

            if (!request.HasPlayableSource)
            {
                Debug.LogError("[MissionCompleteCutsceneController] Request has no playable video source. Assign a clip or enable useUrl with a non-empty url.", this);
                valid = false;
            }

            if (videoPlayer != null && videoPlayer.targetTexture == null)
            {
                Debug.LogError("[MissionCompleteCutsceneController] Missing video output. Assign a RenderTexture to videoPlayer.targetTexture and use it on the RawImage.", this);
                valid = false;
            }

            if (videoPlayer != null && videoPlayer.audioOutputMode == VideoAudioOutputMode.AudioSource && audioSource == null)
            {
                Debug.LogError("[MissionCompleteCutsceneController] Missing required field: audioSource for VideoPlayer AudioSource output mode.", this);
                valid = false;
            }

            return valid;
        }

        private void AutoBindReferences()
        {
            if (gameStateMachine == null)
                gameStateMachine = GameStateMachine.Instance != null ? GameStateMachine.Instance : FindFirstObjectByType<GameStateMachine>();

            if (missionManager == null)
                missionManager = MissionManager.Instance != null ? MissionManager.Instance : FindFirstObjectByType<MissionManager>();
        }

        private void ConfigureVideoPlayerDefaults()
        {
            if (videoPlayer == null)
                return;

            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.waitForFirstFrame = true;
        }

        private void SubscribeMissionManager()
        {
            if (missionManager != null)
                missionManager.OnMissionCompleted += HandleMissionCompleted;
        }

        private void UnsubscribeMissionManager()
        {
            if (missionManager != null)
                missionManager.OnMissionCompleted -= HandleMissionCompleted;
        }
    }
}
