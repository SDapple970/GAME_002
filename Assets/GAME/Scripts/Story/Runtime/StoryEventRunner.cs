// Assets/GAME/Scripts/Story/Runtime/StoryEventRunner.cs
using Game.Core;
using Game.Story.Data;
using Game.Story.UI;
using UnityEngine;

namespace Game.Story
{
    public sealed class StoryEventRunner : MonoBehaviour
    {
        [SerializeField] private DialoguePanel dialoguePanel;
        [SerializeField] private GameState stateWhileRunning = GameState.UIOnly;
        [SerializeField] private bool restoreExplorationOnEnd = true;
        [SerializeField] private bool markCurrentEventCompletedOnEnd = true;

        private StoryEventDefinitionSO _currentEvent;
        private StoryNode _currentNode;
        private bool _running;
        private bool _waitingForChoice;

        public bool IsRunning => _running;

        private void OnEnable()
        {
            if (dialoguePanel != null)
            {
                dialoguePanel.OnNextRequested -= Advance;
                dialoguePanel.OnNextRequested += Advance;
            }
        }

        private void OnDisable()
        {
            if (dialoguePanel != null)
            {
                dialoguePanel.OnNextRequested -= Advance;
            }
        }

        public void StartEvent(StoryEventDefinitionSO eventDefinition)
        {
            if (_running)
            {
                Debug.LogWarning($"[StoryEventRunner] Event already running. Ignored event='{eventDefinition?.EventId}'.");
                return;
            }

            if (eventDefinition == null)
            {
                Debug.LogWarning("[StoryEventRunner] Cannot start a null story event.");
                return;
            }

            _currentEvent = eventDefinition;
            _running = true;
            _waitingForChoice = false;

            if (GameStateMachine.Instance != null)
            {
                GameStateMachine.Instance.SetState(stateWhileRunning);
            }

            if (dialoguePanel != null)
            {
                dialoguePanel.OnNextRequested -= Advance;
                dialoguePanel.OnNextRequested += Advance;
                dialoguePanel.Show();
            }
            else
            {
                Debug.LogWarning($"[StoryEventRunner] DialoguePanel missing for event='{eventDefinition.EventId}'.");
            }

            StoryNode firstNode = eventDefinition.GetNode(eventDefinition.StartNodeId);
            if (firstNode == null)
            {
                Debug.LogWarning($"[StoryEventRunner] Start node not found. event='{eventDefinition.EventId}' node='{eventDefinition.StartNodeId}'.");
                EndEvent();
                return;
            }

            ShowNode(firstNode);
        }

        public void Advance()
        {
            if (!_running || _waitingForChoice) return;

            if (_currentNode == null)
            {
                EndEvent();
                return;
            }

            if (_currentNode.EndEvent)
            {
                EndEvent();
                return;
            }

            if (string.IsNullOrEmpty(_currentNode.NextNodeId))
            {
                EndEvent();
                return;
            }

            StoryNode nextNode = _currentEvent?.GetNode(_currentNode.NextNodeId);
            if (nextNode == null)
            {
                Debug.LogWarning($"[StoryEventRunner] Next node not found. event='{_currentEvent?.EventId}' from='{_currentNode.NodeId}' next='{_currentNode.NextNodeId}'.");
                EndEvent();
                return;
            }

            ShowNode(nextNode);
        }

        public void SelectChoice(StoryChoice choice)
        {
            if (!_running || choice == null) return;

            if (!choice.AreConditionsMet())
            {
                Debug.LogWarning($"[StoryEventRunner] Choice condition not met. event='{_currentEvent?.EventId}' node='{_currentNode?.NodeId}' choice='{choice.Text}'.");
                return;
            }

            choice.ApplyEffects();
            dialoguePanel?.ClearChoices();
            _waitingForChoice = false;

            if (string.IsNullOrEmpty(choice.NextNodeId))
            {
                EndEvent();
                return;
            }

            StoryNode nextNode = _currentEvent?.GetNode(choice.NextNodeId);
            if (nextNode == null)
            {
                Debug.LogWarning($"[StoryEventRunner] Choice next node not found. event='{_currentEvent?.EventId}' node='{_currentNode?.NodeId}' next='{choice.NextNodeId}'.");
                EndEvent();
                return;
            }

            ShowNode(nextNode);
        }

        public void EndEvent()
        {
            if (!_running) return;

            MarkCurrentEventCompletedIfNeeded();

            dialoguePanel?.Hide();
            _currentEvent = null;
            _currentNode = null;
            _waitingForChoice = false;
            _running = false;

            if (restoreExplorationOnEnd && GameStateMachine.Instance != null)
            {
                GameState current = GameStateMachine.Instance.Current;
                if (current == stateWhileRunning || current == GameState.UIOnly || current == GameState.Cutscene)
                {
                    GameStateMachine.Instance.SetState(GameState.Exploration);
                }
            }
        }

        private void MarkCurrentEventCompletedIfNeeded()
        {
            if (!markCurrentEventCompletedOnEnd || _currentEvent == null) return;

            string eventId = _currentEvent.EventId;
            if (string.IsNullOrEmpty(eventId))
            {
                Debug.LogWarning($"[StoryEventRunner] Event id is empty. Completion was not marked for event asset='{_currentEvent.name}'.");
                return;
            }

            if (StoryProgressManager.Instance == null)
            {
                Debug.LogWarning($"[StoryEventRunner] StoryProgressManager missing. Completion was not marked for event='{eventId}'.");
                return;
            }

            StoryProgressManager.Instance.MarkEventCompleted(eventId);
        }

        private void ShowNode(StoryNode node)
        {
            _currentNode = node;
            _waitingForChoice = false;

            ApplyNodeEffects(node);

            if (dialoguePanel != null)
            {
                dialoguePanel.SetLine(node.SpeakerName, node.Body, node.Portrait);
                dialoguePanel.ClearChoices();
            }
            else
            {
                Debug.LogWarning($"[StoryEventRunner] DialoguePanel missing while showing event='{_currentEvent?.EventId}' node='{node.NodeId}'.");
            }

            bool hasChoices = node.Choices != null && node.Choices.Count > 0;
            if (hasChoices)
            {
                _waitingForChoice = true;
                dialoguePanel?.SetNextVisible(false);
                dialoguePanel?.BuildChoices(node.Choices, SelectChoice);

                if (dialoguePanel != null && dialoguePanel.InteractableChoiceCount == 0)
                {
                    Debug.LogWarning($"[StoryEventRunner] No selectable choices. Falling back to Next. event='{_currentEvent?.EventId}' node='{node.NodeId}'.");
                    _waitingForChoice = false;
                    dialoguePanel.SetNextVisible(true);
                }
            }
            else
            {
                dialoguePanel?.SetNextVisible(true);
            }
        }

        private void ApplyNodeEffects(StoryNode node)
        {
            if (node.Effects == null) return;

            foreach (StoryEffect effect in node.Effects)
            {
                effect?.Apply();
            }
        }
    }
}
