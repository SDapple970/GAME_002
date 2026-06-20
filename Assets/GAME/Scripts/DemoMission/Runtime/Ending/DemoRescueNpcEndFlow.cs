using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Core;
using Game.DemoMission.Runtime;

namespace Game.DemoMission
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class DemoRescueNpcEndFlow : MonoBehaviour
    {
        [Header("Mission")]
        [SerializeField] private DemoMissionRuntime missionRuntime;
        [SerializeField] private int requiredEnemyKills = 1;

        [Header("Player")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private bool requireExplorationState = true;

        [Header("Prompt")]
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private string promptMessage = "E: \uB300\uD654";

        [Header("Dialogue")]
        [SerializeField] private GameObject dialogueRoot;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button nextButton;
        [SerializeField] private string speakerName = "NPC";
        [SerializeField] private string[] dialogueLines =
        {
            "\uC640\uC918\uC11C \uACE0\uB9C8\uC6CC.",
            "\uC774\uC81C \uC5EC\uAE30\uC11C \uB098\uAC00\uC790.",
            "\uC900\uBE44\uB410\uC5B4?"
        };

        [Header("Choices")]
        [SerializeField] private GameObject choiceRoot;
        [SerializeField] private Button choiceButtonA;
        [SerializeField] private TMP_Text choiceTextA;
        [SerializeField] private string choiceLabelA = "\uC88B\uC544";
        [SerializeField] private Button choiceButtonB;
        [SerializeField] private TMP_Text choiceTextB;
        [SerializeField] private string choiceLabelB = "\uADF8\uB798";

        [Header("End")]
        [SerializeField] private DemoEndPanelController endPanel;
        [SerializeField] private bool returnToTitleAfterDialogue = true;
        [SerializeField] private string titleSceneName = "Title";

        [Header("Debug")]
        [SerializeField] private bool debugLogs = true;

        private bool playerInRange;
        private bool dialogueActive;
        private bool choiceActive;
        private bool completed;
        private int dialogueIndex;
        private bool _buttonsBound;
        private bool _playerTagWarningLogged;

        private void Awake()
        {
            ResolveReferences();
            HidePrompt();
            HideDialogue();
            HideChoices();
            ApplyStaticText();
            BindButtons();
        }

        private void OnEnable()
        {
            ResolveReferences();
            BindButtons();
            RefreshPrompt();
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

        private void Update()
        {
            if (completed)
                return;

            if (dialogueActive)
            {
                if (UnityEngine.Input.GetKeyDown(interactKey))
                    AdvanceDialogue();

                return;
            }

            if (choiceActive)
                return;

            RefreshPrompt();

            if (!CanStartInteraction())
                return;

            if (UnityEngine.Input.GetKeyDown(interactKey))
                StartDialogue();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other))
                return;

            playerInRange = true;
            RefreshPrompt();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsPlayer(other))
                return;

            playerInRange = false;
            HidePrompt();
        }

        private bool HasRequiredKills()
        {
            if (missionRuntime == null)
                ResolveReferences();

            if (missionRuntime == null)
                return false;

            return missionRuntime.CurrentEnemyKills >= Mathf.Max(0, requiredEnemyKills);
        }

        private void StartDialogue()
        {
            dialogueActive = true;
            choiceActive = false;
            dialogueIndex = 0;

            HidePrompt();
            ShowDialogue();
            HideChoices();

            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.UIOnly);

            if (dialogueLines == null || dialogueLines.Length == 0)
            {
                EndDialogue();
                return;
            }

            ShowCurrentDialogueLine();
            Log("Dialogue started.");
        }

        private void AdvanceDialogue()
        {
            if (!dialogueActive)
                return;

            dialogueIndex++;
            if (dialogueLines == null || dialogueIndex >= dialogueLines.Length)
            {
                EndDialogue();
                return;
            }

            ShowCurrentDialogueLine();
        }

        private void EndDialogue()
        {
            if (returnToTitleAfterDialogue)
            {
                EndDialogueAndReturnToTitle();
                return;
            }

            EndDialogueAndShowChoices();
        }

        private void EndDialogueAndReturnToTitle()
        {
            HideDialogue();
            HideChoices();
            HidePrompt();

            dialogueActive = false;
            choiceActive = false;
            completed = true;

            if (missionRuntime == null)
                ResolveReferences();

            if (missionRuntime != null)
                missionRuntime.RegisterNpcRescued();
            else
                Debug.LogWarning("[DemoRescueNpcEndFlow] DemoMissionRuntime is missing. NPC rescue was not registered.", this);

            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.UIOnly);

            if (string.IsNullOrWhiteSpace(titleSceneName))
            {
                Debug.LogWarning("[DemoRescueNpcEndFlow] Title scene name is empty. Cannot return to title.", this);
                return;
            }

            Log($"Dialogue ended. Loading title scene '{titleSceneName}'.");
            SceneManager.LoadScene(titleSceneName);
        }

        private void EndDialogueAndShowChoices()
        {
            dialogueActive = false;
            choiceActive = true;

            HideDialogue();
            ShowChoices();
            Log("Choices shown.");
        }

        private void HandleChoice()
        {
            if (completed)
                return;

            HideChoices();
            HideDialogue();
            HidePrompt();

            dialogueActive = false;
            choiceActive = false;
            completed = true;

            if (missionRuntime == null)
                ResolveReferences();

            if (missionRuntime != null)
                missionRuntime.RegisterNpcRescued();
            else
                Debug.LogWarning("[DemoRescueNpcEndFlow] DemoMissionRuntime is missing. NPC rescue was not registered.", this);

            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.UIOnly);

            if (endPanel != null)
                endPanel.Show();

            Log("Choice selected. Demo end flow completed.");
        }

        private bool CanStartInteraction()
        {
            if (!playerInRange)
            {
                HidePrompt();
                return false;
            }

            if (!HasRequiredKills())
            {
                HidePrompt();
                return false;
            }

            if (requireExplorationState && GameStateMachine.Instance != null && !GameStateMachine.Instance.Is(GameState.Exploration))
            {
                HidePrompt();
                return false;
            }

            return true;
        }

        private void RefreshPrompt()
        {
            if (completed || dialogueActive || choiceActive)
            {
                HidePrompt();
                return;
            }

            if (CanShowPrompt())
                ShowPrompt();
            else
                HidePrompt();
        }

        private bool CanShowPrompt()
        {
            if (!playerInRange)
                return false;

            if (!HasRequiredKills())
                return false;

            return !requireExplorationState ||
                   GameStateMachine.Instance == null ||
                   GameStateMachine.Instance.Is(GameState.Exploration);
        }

        private void ShowCurrentDialogueLine()
        {
            if (speakerText != null)
                speakerText.text = speakerName;

            if (bodyText != null)
                bodyText.text = dialogueLines != null && dialogueIndex >= 0 && dialogueIndex < dialogueLines.Length
                    ? dialogueLines[dialogueIndex]
                    : string.Empty;
        }

        private void ShowPrompt()
        {
            if (promptText != null)
                promptText.text = promptMessage;

            if (promptRoot != null)
                promptRoot.SetActive(true);
        }

        private void HidePrompt()
        {
            if (promptRoot != null)
                promptRoot.SetActive(false);
        }

        private void ShowDialogue()
        {
            if (dialogueRoot != null)
                dialogueRoot.SetActive(true);
        }

        private void HideDialogue()
        {
            if (dialogueRoot != null)
                dialogueRoot.SetActive(false);
        }

        private void ShowChoices()
        {
            ApplyStaticText();

            if (choiceRoot != null)
                choiceRoot.SetActive(true);
        }

        private void HideChoices()
        {
            if (choiceRoot != null)
                choiceRoot.SetActive(false);
        }

        private void ResolveReferences()
        {
            if (missionRuntime == null)
                missionRuntime = FindFirstObjectByType<DemoMissionRuntime>();

            if (endPanel == null)
                endPanel = FindFirstObjectByType<DemoEndPanelController>(FindObjectsInactive.Include);
        }

        private void ApplyStaticText()
        {
            if (promptText != null)
                promptText.text = promptMessage;

            if (choiceTextA != null)
                choiceTextA.text = choiceLabelA;

            if (choiceTextB != null)
                choiceTextB.text = choiceLabelB;
        }

        private void BindButtons()
        {
            if (_buttonsBound)
                return;

            if (nextButton != null)
                nextButton.onClick.AddListener(AdvanceDialogue);

            if (choiceButtonA != null)
                choiceButtonA.onClick.AddListener(HandleChoice);

            if (choiceButtonB != null)
                choiceButtonB.onClick.AddListener(HandleChoice);

            _buttonsBound = true;
        }

        private void UnbindButtons()
        {
            if (!_buttonsBound)
                return;

            if (nextButton != null)
                nextButton.onClick.RemoveListener(AdvanceDialogue);

            if (choiceButtonA != null)
                choiceButtonA.onClick.RemoveListener(HandleChoice);

            if (choiceButtonB != null)
                choiceButtonB.onClick.RemoveListener(HandleChoice);

            _buttonsBound = false;
        }

        private bool IsPlayer(Collider2D other)
        {
            if (other == null || string.IsNullOrEmpty(playerTag))
                return false;

            try
            {
                return other.CompareTag(playerTag);
            }
            catch (UnityException exception)
            {
                if (!_playerTagWarningLogged)
                {
                    _playerTagWarningLogged = true;
                    Debug.LogWarning($"[DemoRescueNpcEndFlow] Player tag '{playerTag}' is not defined. {exception.Message}", this);
                }

                return false;
            }
        }

        private void Log(string message)
        {
            if (!debugLogs)
                return;

            Debug.Log($"[DemoRescueNpcEndFlow] {message}", this);
        }
    }
}
