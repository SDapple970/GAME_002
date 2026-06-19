using Game.Core;
using Game.NonCombat.Choice;
using UnityEngine;

namespace Game.NonCombat.Dialogue
{
    public sealed class DialogueController : MonoBehaviour
    {
        public static DialogueController Instance { get; private set; }

        [SerializeField] private NonCombatDialogueUIPanel uiPanel;
        [SerializeField] private ChoiceRunner choiceRunner;

        private DialogueNodeSO _currentNode;
        private GameState _stateBeforeDialogue;
        private bool _ownsUiOnlyState;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            if (uiPanel != null)
                uiPanel.ChoiceSelected += HandleChoiceSelected;
        }

        private void OnDisable()
        {
            if (uiPanel != null)
                uiPanel.ChoiceSelected -= HandleChoiceSelected;
        }

        public void StartDialogue(DialogueNodeSO node)
        {
            if (node == null) return;
            if (GameStateMachine.Instance != null && GameStateMachine.Instance.IsCombatState())
                return;

            _stateBeforeDialogue = GameStateMachine.Instance != null ? GameStateMachine.Instance.Current : GameState.Exploration;
            _ownsUiOnlyState = _stateBeforeDialogue == GameState.Exploration;

            if (_ownsUiOnlyState && GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.UIOnly);

            SetNode(node);
        }

        public void EndDialogue()
        {
            _currentNode = null;
            if (uiPanel != null)
                uiPanel.Hide();

            if (_ownsUiOnlyState && GameStateMachine.Instance != null && GameStateMachine.Instance.Is(GameState.UIOnly))
                GameStateMachine.Instance.SetState(GameState.Exploration);

            _ownsUiOnlyState = false;
        }

        private void SetNode(DialogueNodeSO node)
        {
            _currentNode = node;
            if (_currentNode == null)
            {
                EndDialogue();
                return;
            }

            if (uiPanel == null)
            {
                Debug.LogWarning("[DialogueController] NonCombatDialogueUIPanel is missing.", this);
                EndDialogue();
                return;
            }

            ChoiceRunner runner = choiceRunner != null ? choiceRunner : ChoiceRunner.Instance;
            uiPanel.Show(_currentNode, runner);
        }

        private void HandleChoiceSelected(DialogueChoice choice)
        {
            if (choice == null)
            {
                AdvanceWithoutChoice();
                return;
            }

            ChoiceRunner runner = choiceRunner != null ? choiceRunner : ChoiceRunner.Instance;
            if (runner != null)
                runner.ApplyOutcomes(choice.Outcomes);

            if (choice.NextNode != null)
                SetNode(choice.NextNode);
            else
                EndDialogue();
        }

        public void AdvanceWithoutChoice()
        {
            if (_currentNode != null && _currentNode.NextNodeWhenNoChoice != null)
                SetNode(_currentNode.NextNodeWhenNoChoice);
            else
                EndDialogue();
        }
    }
}
