// Assets/GAME/Scripts/Story/Runtime/StoryEventRunner.cs
using Game.Core;
using Game.Story.Data;
using Game.Story.UI;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Story
{
    public sealed class StoryEventRunner : MonoBehaviour
    {
        [SerializeField] private DialoguePanel dialoguePanel;
        [SerializeField] private StoryDialogueHUD storyDialogueHUD;
        [SerializeField] private GameState stateWhileRunning = GameState.UIOnly;
        [SerializeField] private bool restoreExplorationOnEnd = true;
        [SerializeField] private bool markCurrentEventCompletedOnEnd = true;

        private StoryEventDefinitionSO _currentEvent;
        private StoryNode _currentNode;
        private StorySpeakerAnchor _currentSpeakerAnchor;
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

        private void Update()
        {
            if (!_running || _waitingForChoice || storyDialogueHUD == null) return;

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
            {
                Advance();
            }
        }

        public void StartEvent(StoryEventDefinitionSO eventDefinition)
        {
            StartEvent(eventDefinition, null);
        }

        public void StartEvent(StoryEventDefinitionSO eventDefinition, StorySpeakerAnchor speakerAnchor)
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
            _currentSpeakerAnchor = speakerAnchor;
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
                if (storyDialogueHUD == null)
                {
                    dialoguePanel.Show();
                }
                else
                {
                    dialoguePanel.Hide();
                }
            }
            else if (storyDialogueHUD == null)
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
            storyDialogueHUD?.HideChoices();
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
            storyDialogueHUD?.HideAll();
            _currentEvent = null;
            _currentNode = null;
            _currentSpeakerAnchor = null;
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

            if (dialoguePanel != null && storyDialogueHUD == null)
            {
                dialoguePanel.SetLine(node.SpeakerName, node.Body, node.Portrait);
                dialoguePanel.ClearChoices();
            }
            else if (storyDialogueHUD == null)
            {
                Debug.LogWarning($"[StoryEventRunner] DialoguePanel missing while showing event='{_currentEvent?.EventId}' node='{node.NodeId}'.");
            }

            if (storyDialogueHUD != null)
            {
                storyDialogueHUD.ShowLine(_currentSpeakerAnchor, node);
            }

            bool hasChoices = node.Choices != null && node.Choices.Count > 0;
            if (hasChoices)
            {
                if (storyDialogueHUD != null)
                {
                    ShowHudChoices(node);
                }
                else
                {
                    ShowDialoguePanelChoices(node);
                }
            }
            else
            {
                storyDialogueHUD?.HideChoices();
                dialoguePanel?.SetNextVisible(true);
            }
        }

        private void ShowDialoguePanelChoices(StoryNode node)
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

        private void ShowHudChoices(StoryNode node)
        {
            List<StoryChoice> availableChoices = GetAvailableChoices(node.Choices, 2);
            if (availableChoices.Count == 0)
            {
                Debug.LogWarning($"[StoryEventRunner] No selectable choices for HUD. Falling back to Next. event='{_currentEvent?.EventId}' node='{node.NodeId}'.");
                _waitingForChoice = false;
                storyDialogueHUD?.HideChoices();
                dialoguePanel?.SetNextVisible(true);
                return;
            }

            _waitingForChoice = true;
            dialoguePanel?.SetNextVisible(false);
            dialoguePanel?.ClearChoices();

            storyDialogueHUD.ShowTimedChoices(node, availableChoices, SelectChoice, HandleTimedChoiceTimeout);
        }

        private void HandleTimedChoiceTimeout()
        {
            if (!_running || _currentNode == null) return;

            StoryNode timeoutSource = _currentNode;
            _waitingForChoice = false;
            storyDialogueHUD?.HideChoices();

            if (!string.IsNullOrEmpty(timeoutSource.TimeoutNodeId))
            {
                StoryNode timeoutNode = _currentEvent?.GetNode(timeoutSource.TimeoutNodeId);
                if (timeoutNode != null)
                {
                    ShowNode(timeoutNode);
                    return;
                }

                Debug.LogWarning($"[StoryEventRunner] Timeout node not found. event='{_currentEvent?.EventId}' node='{timeoutSource.NodeId}' timeout='{timeoutSource.TimeoutNodeId}'.");
            }

            List<StoryChoice> availableChoices = GetAvailableChoices(timeoutSource.Choices, 2);
            int choiceIndex = timeoutSource.TimeoutChoiceIndex;
            if (choiceIndex >= 0 && choiceIndex < availableChoices.Count)
            {
                SelectChoice(availableChoices[choiceIndex]);
                return;
            }

            EndEvent();
        }

        private static List<StoryChoice> GetAvailableChoices(IReadOnlyList<StoryChoice> choices, int maxCount)
        {
            List<StoryChoice> availableChoices = new();
            if (choices == null) return availableChoices;

            for (int i = 0; i < choices.Count && availableChoices.Count < maxCount; i++)
            {
                StoryChoice choice = choices[i];
                if (choice != null && choice.AreConditionsMet())
                {
                    availableChoices.Add(choice);
                }
            }

            return availableChoices;
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
